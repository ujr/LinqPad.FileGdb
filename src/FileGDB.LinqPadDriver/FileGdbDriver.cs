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

		var gdbFolderPath = cxInfo.GetGdbFolderPath() ??
		                    throw new InvalidOperationException("No GDB folder path in connection info");
		var debugMode = cxInfo.GetDebugMode();

		return new object[]
		{
			gdbFolderPath,
			debugMode
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
		var parent = info.ParentHierarchy.FirstOrDefault();

		if (objectToWrite is GeometryBlob blob)
		{
			// Collapse GeometryBlob if a field in a row:
			objectToWrite = parent is RowBase
				? OnDemand(blob, blob.ShapeType.ToString())
				: blob;
		}
		else if (objectToWrite is CatalogWrapper entry)
		{
			objectToWrite = parent is null ? entry : PreprocessCatalogEntry(entry);
		}
		else if (objectToWrite is FieldInfo fieldInfo)
		{
			objectToWrite = parent is null ? fieldInfo : PreprocessFieldInfo(fieldInfo);
		}
		else if (objectToWrite is IndexInfo indexInfo)
		{
			objectToWrite = parent is null ? indexInfo : PreprocessIndexInfo(indexInfo);
		}
		else if (parent is GeometryBlob)
		{
			if (objectToWrite is IReadOnlyList<byte> blobBytes)
			{
				var label = FormatBytes(blobBytes, 8, true);
				objectToWrite = OnDemand(blobBytes, label);
			}
			else if (objectToWrite is ShapeBuffer shapeBuffer)
			{
				var label = shapeBuffer.GeometryType.ToString();
				objectToWrite = OnDemand(shapeBuffer, label);
			}
			else if (objectToWrite is Shape shape)
			{
				var label = shape.GeometryType.ToString();
				objectToWrite = OnDemand(shape, label);
			}
		}
		else if (parent is ShapeBuffer)
		{
			if (objectToWrite is IReadOnlyList<byte> bytes)
			{
				var label = FormatBytes(bytes, 12);
				objectToWrite = OnDemand(bytes, label);
			}
		}
		else if (parent is Shape)
		{
			if (objectToWrite is BoxShape box)
			{
				const string label = "Box";
				objectToWrite = OnDemand(box, label);
			}
			else if (objectToWrite is IReadOnlyList<XY> coordsXY)
			{
				var label = FormatCount(coordsXY.Count, "pair");
				objectToWrite = OnDemand(coordsXY, label);
			}
			else if (objectToWrite is IReadOnlyList<double> ords)
			{
				var label = FormatCount(ords.Count, "double");
				objectToWrite = OnDemand(ords, label);
			}
			else if (objectToWrite is IReadOnlyList<int> ids)
			{
				var label = FormatCount(ids.Count, "ID", "IDs");
				objectToWrite = OnDemand(ids, label);
			}
			else if (objectToWrite is IReadOnlyList<PointShape> points)
			{
				var label = FormatCount(points.Count, "point");
				objectToWrite = OnDemand(points, label);
			}
			else if (objectToWrite is IReadOnlyList<Shape> parts)
			{
				var label = FormatCount(parts.Count, "part");
				objectToWrite = OnDemand(parts, label);
			}
			else if (objectToWrite is IReadOnlyList<SegmentModifier> curves)
			{
				var label = FormatCount(curves.Count, "curve");
				objectToWrite = OnDemand(curves, label);
			}
		}
		else if (parent is TableBase || parent is Table)
		{
			if (objectToWrite is IReadOnlyList<FieldInfo> fieldInfos)
			{
				var label = FormatCount(fieldInfos.Count, "field");
				objectToWrite = OnDemand(fieldInfos, label);
			}
			else if (objectToWrite is IReadOnlyList<IndexInfo> indexInfos)
			{
				var label = FormatCount(indexInfos.Count, "index", "indices");
				objectToWrite = OnDemand(indexInfos, label);
			}
			else if (objectToWrite is Table.InternalInfo internals)
			{
				objectToWrite = OnDemand(internals, "Internals");
			}
		}
		else if (parent is TableContainerBase)
		{
			if (objectToWrite is TableBase tableBase)
			{
				var label = tableBase.TableName;
				objectToWrite = OnDemand(tableBase, label); // TODO or a Link
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

	private static object PreprocessCatalogEntry(CatalogWrapper catalogEntry)
	{
		//if (catalogEntry is null) return null!;

		// Reorder properties, link to open the table

		return new
		{
			catalogEntry.ID,
			catalogEntry.Name,
			catalogEntry.Format,
			Table = Util.OnDemand<object>("Open", delegate
			{
				try
				{
					return catalogEntry.OpenTable();
				}
				catch (Exception ex)
				{
					return ex;
				}
			})
		};
	}

	private static object PreprocessFieldInfo(FieldInfo? fieldInfo)
	{
		if (fieldInfo is null) return null!;

		// Reorder properties and make GeometryDef and RasterDef lazy:

		return new
		{
			fieldInfo.Type,
			fieldInfo.Name,
			fieldInfo.Alias,
			fieldInfo.Nullable,
			fieldInfo.Required,
			fieldInfo.Editable,
			fieldInfo.Length,
			GeometryDef = OnDemand(fieldInfo.GeometryDef),
			RasterDef = OnDemand(fieldInfo.RasterDef),
			fieldInfo.Size,
			fieldInfo.Flags
		};
	}

	private static object PreprocessIndexInfo(IndexInfo? indexInfo)
	{
		if (indexInfo is null) return null!;

		// Reorder properties:

		return new
		{
			indexInfo.Name,
			indexInfo.FieldName,
			indexInfo.IndexType,
			FileName = Util.WithStyle(indexInfo.FileName, "color:gray;")
		};
	}

	private static string FormatCount(int count, string singular, string? plural = null)
	{
		if (string.IsNullOrEmpty(singular))
			return count.ToString();

		if (count == 1)
			return $"1 {singular}";

		plural ??= Pluralize(singular);

		return $"{count} {plural}";
	}

	private static string Pluralize(string word)
	{
		if (string.IsNullOrEmpty(word))
			return word;

		int n = word.Length;
		char e1 = n > 0 ? word[n - 1] : '$'; // last letter
		char e2 = n > 1 ? word[n - 2] : '$'; // second to last

		// Cases like "fifty" => "fifties" but not "joy" (vowel)
		const string vowels = "aeiou";
		if (e1 == 'y' && !vowels.Contains(e2))
			return string.Concat(word.AsSpan(0, n - 1), "ies");

		// Cases like "boss" and "buzz" and "bash"
		if (e1 == 's' || e1 == 'x' || e1 == 'z' || (e1 == 'h' && (e2 == 'c' || e2 == 's')))
			return word + "es";

		// All other cases:
		return word + "s";
	}

	private static DumpContainer OnDemand<T>(T value, string? label = null)
	{
		if (value is null) return null!;
		label ??= GetLabelText(value);
		return Util.OnDemand(label, () => value);
	}

	private static string? GetLabelText(object? obj)
	{
		if (obj is null) return null;
		if (obj is GeometryDef geometryDef)
			return GetLabelText(geometryDef);
		if (obj is RasterDef rasterDef)
			return GetLabelText(rasterDef);
		return Convert.ToString(obj);
	}

	private static string GetLabelText(GeometryDef? geometryDef)
	{
		if (geometryDef is null) return null!;

		var sb = new StringBuilder();
		sb.Append(geometryDef.GeometryType);

		if (geometryDef.HasZ || geometryDef.HasM)
		{
			sb.Append(' ');

			if (geometryDef.HasZ)
			{
				sb.Append('Z');
			}

			if (geometryDef.HasM)
			{
				sb.Append('M');
			}
		}

		return sb.ToString();
	}

	private static string GetLabelText(RasterDef? rasterDef)
	{
		if (rasterDef is null) return null!;

		return rasterDef.RasterColumn ?? string.Empty;
	}

	#endregion
}
