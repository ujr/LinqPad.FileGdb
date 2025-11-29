using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FileGDB.Core;
using FileGDB.Core.Geometry;
using FileGDB.Core.Shapes;
using FileGDB.Core.WKT;
using JetBrains.Annotations;
using LINQPad;
using LINQPad.Extensibility.DataContext;
using FieldInfo = FileGDB.Core.FieldInfo;

namespace FileGDB.LinqPadDriver;

[UsedImplicitly]
public class FileGdbDriver : DynamicDataContextDriver
{
	static FileGdbDriver()
	{
		// Attach to Visual Studio's debugger when an exception is thrown:
		AppDomain.CurrentDomain.FirstChanceException += (_, args) =>
		{
			if (args.Exception.StackTrace != null &&
			    args.Exception.StackTrace.Contains(nameof(FileGdbDriver)))
			{
				Debugger.Launch();
			}
		};
	}

	public override string Name => "FileGDB Driver";
	public override string Author => "ujr";

	public override string GetConnectionDescription(IConnectionInfo cxInfo)
	{
		var path = (string?)cxInfo.DriverData?.Element(Constants.DriverDataFolderPath);

		if (string.IsNullOrEmpty(path))
		{
			return "File GDB (not configured)";
		}

		try
		{
			return Directory.Exists(path) ? path : $"{path} (no such directory)";
		}
		catch (Exception ex)
		{
			return $"{path}: {ex.Message}";
		}
	}

	public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions options)
	{
		//Debugger.Launch();

		//var assembly = Assembly.GetExecutingAssembly();
		//var dllFileName = Path.GetFileName(assembly.Location);
		// Note: assembly.Location is in a temp folder; use file name only!
		//cxInfo.CustomTypeInfo.CustomAssemblyPath = dllFileName;
		//cxInfo.CustomTypeInfo.CustomTypeName = typeof(FileGdbContext).FullName;

		// TODO How to set dialog's owner?
		var dialog = new ConnectionDialog(cxInfo);
		return dialog.ShowDialog() == true;
	}

	public override bool AreRepositoriesEquivalent(IConnectionInfo c1, IConnectionInfo c2)
	{
		if (c1 is null)
			throw new ArgumentNullException(nameof(c1));
		if (c2 is null)
			throw new ArgumentNullException(nameof(c2));

		var folder1 = c1.GetGdbFolderPath();
		var folder2 = c2.GetGdbFolderPath();
		if (folder1 is null && folder2 is null) return true;
		if (folder1 is null || folder2 is null) return false;
		var path1 = Path.GetFullPath(folder1);
		var path2 = Path.GetFullPath(folder2);
		return string.Equals(path1, path2, StringComparison.Ordinal);
	}

	public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
	{
		return new[]
		{
			new ParameterDescriptor("folderPath", typeof(string).FullName),
			new ParameterDescriptor("debugMode", typeof(bool).FullName)
		};
	}

	public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
	{
		if (cxInfo is null)
			throw new ArgumentNullException(nameof(cxInfo));

		var gdbFolderPath = cxInfo.GetGdbFolderPath();
		if (gdbFolderPath is null)
			throw new InvalidOperationException("No GDB folder path in connection info");

		return new object[]
		{
			gdbFolderPath,
			cxInfo.GetDebugMode()
		};
	}

	public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo cxInfo)
	{
		//Debugger.Launch();
		var assembly = typeof(Core.FileGDB).Assembly;
		var dllFileName = Path.GetFileName(assembly.Location);
		yield return dllFileName;
	}

	public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo cxInfo)
	{
		//Debugger.Launch();
		return new[]
			{
				typeof(Core.FileGDB).Namespace,
				typeof(GeometryUtils).Namespace,
				typeof(Shape).Namespace,
				typeof(ShapeExtensions).Namespace,
				typeof(ShapeBufferExtensions).Namespace
			}
			// Namespace could be null:
			.Where(ns => ns is not null)
			// LINQPad probably handles duplicates, but better safe than sorry:
			.Distinct()!;
	}

	public override List<ExplorerItem> GetSchemaAndBuildAssembly(
		IConnectionInfo cxInfo, AssemblyName assemblyToBuild,
		ref string nameSpace, ref string typeName)
	{
		var debugMode = cxInfo.GetDebugMode();

		if (debugMode)
		{
			Debugger.Launch();
		}

		using var builder = new SchemaBuilder(cxInfo);

		var schema = builder.GetSchema();
		var result = builder.BuildAssembly(assemblyToBuild, nameSpace, typeName);

		if (result.HasErrors)
		{
			schema.Insert(0,
				new ExplorerItem("Data context compilation failed", ExplorerItemKind.ReferenceLink, ExplorerIcon.Box)
				{
					DragText = string.Join(Environment.NewLine, result.Errors),
					ToolTipText = "Drag to text window to see compilation errors"
				});
		}
		else if (debugMode)
		{
			schema.Add(new ExplorerItem("Data context source code", ExplorerItemKind.Schema, ExplorerIcon.Schema)
			{
				DragText = result.SourceCode,
				ToolTipText = "Drag to text window to see data context source code"
			});
		}

		return schema;
	}

	#region Output formatting

	public override void PreprocessObjectToWrite(ref object objectToWrite, ObjectGraphInfo info)
	{
		if (objectToWrite is GeometryBlob blob)
		{
			var parent = info.ParentHierarchy.FirstOrDefault();
			objectToWrite = parent is RowBase
				? Util.OnDemand(blob.ShapeType.ToString(), () => blob)
				: blob;
			//objectToWrite = FormatGeometryBlob(blob);
		}
		else if (info.ParentHierarchy.FirstOrDefault() is GeometryBlob)
		{
			if (objectToWrite is IReadOnlyList<byte> blobBytes)
			{
				var description = FormatBytes(blobBytes, 8, true);
				objectToWrite = Util.OnDemand(description, () => blobBytes);
			}
			else if (objectToWrite is ShapeBuffer shapeBuffer)
			{
				objectToWrite = Util.OnDemand(shapeBuffer.GeometryType.ToString(), () => shapeBuffer);
			}
			else if (objectToWrite is Shape shape)
			{
				objectToWrite = Util.OnDemand(shape.GeometryType.ToString(), () => shape);
			}
		}
		else if (info.ParentHierarchy.FirstOrDefault() is ShapeBuffer)
		{
			if (objectToWrite is IReadOnlyList<byte> bytes)
			{
				var text = FormatBytes(bytes, 12);
				objectToWrite = Util.OnDemand(text, () => bytes);
			}
		}
		else if (info.ParentHierarchy.FirstOrDefault() is Shape)
		{
			if (objectToWrite is BoxShape box)
			{
				objectToWrite = Util.OnDemand("Box", () => box);
			}
			else if (objectToWrite is IReadOnlyList<XY> coordsXY)
			{
				var description = coordsXY.Count == 1 ? "1 pair" : $"{coordsXY.Count} pairs";
				objectToWrite = Util.OnDemand(description, () => coordsXY);
			}
			else if (objectToWrite is IReadOnlyList<double> ords)
			{
				var description = ords.Count == 1 ? "1 double" : $"{ords.Count} doubles";
				objectToWrite = Util.OnDemand(description, () => ords);
			}
			else if (objectToWrite is IReadOnlyList<int> ids)
			{
				var description = ids.Count == 1 ? "1 ID" : $"{ids.Count} IDs";
				objectToWrite = Util.OnDemand(description, () => ids);
			}
			else if (objectToWrite is IReadOnlyList<PointShape> points)
			{
				var description = points.Count == 1 ? "1 point" : $"{points.Count} points";
				objectToWrite = Util.OnDemand(description, () => points);
			}
			else if (objectToWrite is IReadOnlyList<Shape> parts)
			{
				var description = parts.Count == 1 ? "1 part" : $"{parts.Count} parts";
				objectToWrite = Util.OnDemand(description, () => parts);
			}
			else if (objectToWrite is IReadOnlyList<SegmentModifier> curves)
			{
				var description = curves.Count == 1 ? "1 curve" : $"{curves.Count} curves";
				objectToWrite = Util.OnDemand(description, () => curves);
			}
		}
		else if (info.ParentHierarchy.FirstOrDefault() is TableBase)
		{
			if (objectToWrite is ICollection<FieldInfo> fieldInfos)
			{
				var description = fieldInfos.Count == 1 ? "1 field" : $"{fieldInfos.Count} fields";
				objectToWrite = Util.OnDemand(description, () => fieldInfos);
			}
			else if (objectToWrite is ICollection<IndexInfo> indexInfos)
			{
				var description = indexInfos.Count == 1 ? "1 index" : $"{indexInfos.Count} indices";
				objectToWrite = Util.OnDemand(description, () => indexInfos);
			}
			else if (objectToWrite is Table.InternalInfo internals)
			{
				objectToWrite = Util.OnDemand("Internals", () => internals);
			}
		}
		else if (info.ParentHierarchy.FirstOrDefault() is Table)
		{
			if (objectToWrite is ICollection<FieldInfo> fieldInfos)
			{
				var description = fieldInfos.Count == 1 ? "1 field" : $"{fieldInfos.Count} fields";
				objectToWrite = Util.OnDemand(description, () => fieldInfos);
			}
			else if (objectToWrite is ICollection<IndexInfo> indexInfos)
			{
				var description = indexInfos.Count == 1 ? "1 index" : $"{indexInfos.Count} indices";
				objectToWrite = Util.OnDemand(description, () => indexInfos);
			}
			else if (objectToWrite is Table.InternalInfo internals)
			{
				objectToWrite = Util.OnDemand("Internals", () => internals);
			}
		}
		else if (info.ParentHierarchy.FirstOrDefault() is FieldInfo)
		{
			if (objectToWrite is GeometryDef geometryDef)
			{
				var description = Utils.GetDisplayName(geometryDef);
				objectToWrite = Util.OnDemand(description, () => geometryDef);
			}
		}
		else if (info.ParentHierarchy.FirstOrDefault() is TableContainerBase)
		{
			if (objectToWrite is TableBase tableBase)
			{
				//var rows = tableBase.Table.RowCount;
				//var label = rows == 1 ? "1 row" : $"{rows} rows";
				var label = tableBase.TableName;
				objectToWrite = Util.OnDemand(label, () => tableBase);
			}
		}

		base.PreprocessObjectToWrite(ref objectToWrite, info);
	}

	[PublicAPI]
	public static string FormatBytes(IEnumerable<byte> bytes, int maxBytes, bool omitCount = false)
	{
		if (bytes is null)
			throw new ArgumentNullException(nameof(bytes));

		var sb = new StringBuilder();
		sb.Append('<');

		using var enumerator = bytes.GetEnumerator();

		for (int i = 0; i < maxBytes; i++)
		{
			if (!enumerator.MoveNext()) break;
			if (i > 0) sb.Append(' ');
			sb.AppendFormat("{0:X2}", enumerator.Current);
		}

		if (enumerator.MoveNext())
		{
			sb.Append(" ...");
		}

		sb.Append('>');

		if (bytes is ICollection<byte> collection && !omitCount)
		{
			sb.AppendFormat(" ({0} bytes)", collection.Count);
		}

		return sb.ToString();
	}

	#endregion
}
