using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FileGDB.Core;
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
		yield return typeof(Core.FileGDB).Namespace!;
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
				var rows = tableBase.Table.RowCount;
				var label = rows == 1 ? "1 row" : $"{rows} rows";
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

	#region Schema explorer and typed context generation

	public override List<ExplorerItem> GetSchemaAndBuildAssembly(
		IConnectionInfo cxInfo, AssemblyName assemblyToBuild,
		ref string nameSpace, ref string typeName)
	{
		var debugMode = cxInfo.GetDebugMode();

		if (debugMode)
		{
			Debugger.Launch();
		}

		var gdbFolderPath = cxInfo.GetGdbFolderPath();
		using var gdb = gdbFolderPath is null ? null : Core.FileGDB.Open(gdbFolderPath);

		var catalogItem = new ExplorerItem("Catalog", ExplorerItemKind.Schema, ExplorerIcon.Schema)
		{
			IsEnumerable = true,
			DragText = nameof(DriverBase.Catalog),
			ToolTipText = "Catalog (list of tables)"
		};

		var systemItem = new ExplorerItem("System", ExplorerItemKind.Category, ExplorerIcon.Table)
		{
			Children = GetSystemTableItems(gdb),
			ToolTipText = "Geodatabase System Tables"
		};

		var tablesItem = new ExplorerItem("Tables", ExplorerItemKind.Category, ExplorerIcon.Table)
		{
			Children = GetUserTableItems(gdb),
			ToolTipText = "User-defined tables"
		};

		var source = MakeContextSourceCode(nameSpace, typeName, gdb);
		var outputFile = assemblyToBuild.CodeBase ?? throw new Exception($"No CodeBase on {nameof(assemblyToBuild)}");

		Compile(source, outputFile, cxInfo);

		var items = new List<ExplorerItem>
		{
			catalogItem,
			systemItem,
			tablesItem
		};

		if (debugMode)
		{
			var debugItem = new ExplorerItem("Debug", ExplorerItemKind.Category, ExplorerIcon.Box);

			var children = debugItem.Children = new List<ExplorerItem>();

			children.Add(new ExplorerItem($"{nameSpace}.{typeName}", ExplorerItemKind.Property, ExplorerIcon.Box)
			{
				IsEnumerable = false,
				DragText = source,
				ToolTipText = "generated source code for data context"
			});

			children.Add(new ExplorerItem("Debugger.Launch()", ExplorerItemKind.Property, ExplorerIcon.ScalarFunction)
			{
				IsEnumerable = false,
				DragText = "System.Diagnostics.Debugger.Launch()",
				ToolTipText = "launch and attach a debugger"
			});

			children.Add(new ExplorerItem("Item Kinds", ExplorerItemKind.Category, ExplorerIcon.Schema)
			{
				ToolTipText = "Dummy item for each ExplorerItemKind",
				Children = GetItemKindItems(debugItem)
			});

			children.Add(new ExplorerItem("Icons", ExplorerItemKind.Category, ExplorerIcon.Schema)
			{
				ToolTipText = "Dummy item for each ExplorerIcon",
				Children = GetIconItems()
			});

			items.Add(debugItem);
		}

		return items;
	}

	private static void Compile(string cSharpSourceCode, string outputFile, IConnectionInfo cxInfo)
	{
		var assembliesToReference =
#if NETCORE || true
			// GetCoreFxReferenceAssemblies is helper method that returns the full
			// set of .NET Core reference assemblies (there are more than 100 of them).
			GetCoreFxReferenceAssemblies(cxInfo).ToList();
#else
			// .NET Framework - here's how to get the basic Framework assemblies:
			new List<string>
			{
				typeof (int).Assembly.Location,            // mscorlib
				typeof (Uri).Assembly.Location,            // System
				typeof (XmlConvert).Assembly.Location,     // System.Xml
				typeof (Enumerable).Assembly.Location,     // System.Core
				typeof (DataSet).Assembly.Location         // System.Data
			};
#endif

		assembliesToReference.Add(typeof(Core.FileGDB).Assembly.Location);
		assembliesToReference.Add(typeof(FileGdbDriver).Assembly.Location);

		// CompileSource is a static helper method to compile C# source code using LINQPad's built-in Roslyn libraries.
		// If you prefer, you can add a NuGet reference to the Roslyn libraries and use them directly.
		var compileResult = CompileSource(new CompilationInput
		{
			FilePathsToReference = assembliesToReference.ToArray(),
			OutputPath = outputFile,
			SourceCode = new[] { cSharpSourceCode }
		});

		if (compileResult.Errors.Length > 0)
			throw new Exception("Cannot compile typed context: " + compileResult.Errors[0]);
	}

	private static string MakeContextSourceCode(string nameSpace, string typeName, Core.FileGDB? gdb)
	{
		if (string.IsNullOrEmpty(nameSpace))
			throw new ArgumentNullException(nameof(nameSpace));
		if (string.IsNullOrEmpty(typeName))
			throw new ArgumentNullException(nameof(typeName));

		const string mainSourceTemplate = @"using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace $$Namespace$$;

public class $$TypeName$$ : FileGDB.LinqPadDriver.DriverBase
{
  public $$TypeName$$(string gdbFolderPath, bool debugMode) : base(gdbFolderPath, debugMode)
  {
    Tables = new TableContainer(GDB, debugMode);
  }

  public TableContainer Tables { get; }

  public class TableContainer : FileGDB.LinqPadDriver.TableContainerBase
  {
    public TableContainer(FileGDB.Core.FileGDB gdb, bool debugMode) : base(gdb, debugMode) {}

    // public @FooTable @Foo => GetTable<@FooTable>(""Foo"");
    $$TableProperties$$
  }

  $$PerTableClasses$$

  public override void Dispose()
  {
    Tables.Dispose();
  }
}
";

		const string perTableTemplate = @"
public class $$TableName$$_Table : FileGDB.LinqPadDriver.TableBase, IEnumerable<$$TableName$$_Table.Row>
{
  public $$TableName$$_Table(FileGDB.Core.Table table, bool debugMode)
    : base(table, debugMode) { }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public IEnumerator<Row> GetEnumerator() => Search().GetEnumerator();

  public IEnumerable<Row> Search()
  {
    var cursor = ReadRows();
    var values = (FileGDB.Core.IRowValues) cursor;

    while (cursor.Step())
    {
      var row = new Row();
      //row.Bar = (BarType)values.GetValue(""Bar"");
      $$FieldAssignments$$
      yield return row;
    }
  }

  public Row GetRow(long oid)
  {
    var values = ReadRow(oid);
    if (values is null) return null;

    var row = new Row();
    //row.Bar = (BarType)values.GetValue(""Bar"");
    $$FieldAssignments$$
    return row;
  }

  public class Row : FileGDB.LinqPadDriver.RowBase
  {
    //public BarType Bar { get; set; }
    $$FieldProperties$$
  }
}
";

		if (gdb is null)
		{
			return mainSourceTemplate
				.Replace("$$Namespace$$", nameSpace)
				.Replace("$$TypeName$$", typeName)
				.Replace("$$TableProperties$$", string.Empty)
				.Replace("$$PerTableClasses$$", string.Empty);
		}

		var tableProps = new StringBuilder();
		var tableClasses = new StringBuilder();

		foreach (var entry in gdb.Catalog)
		{
			var tableName = entry.Name;
			var tableClassName = MakeIdentifier(tableName);

			tableProps.Append($"public @{tableClassName}_Table @{tableClassName} => ");
			tableProps.AppendLine($"GetTable<@{tableClassName}_Table>(\"{tableClassName}\");");

			try
			{
				using var table = gdb.OpenTable(tableName);

				var fieldAssignments = new StringBuilder();
				var fieldProperties = new StringBuilder();

				foreach (var field in table.Fields)
				{
					var fieldName = field.Name;
					var propName = MakeIdentifier(fieldName);
					var escaped = EscapeForString(fieldName);
					var fieldType = Table.GetDataType(field.Type);
					var fieldTypeName = GetPropertyTypeName(fieldType);

					fieldAssignments.AppendLine($"row.@{propName} = ({fieldTypeName}) values.GetValue(\"{escaped}\");");
					fieldProperties.AppendLine($"public {fieldTypeName} @{propName} {{ get; set; }}");
				}

				var perTableCode = perTableTemplate
					.Replace("$$TableName$$", tableClassName)
					.Replace("$$FieldAssignments$$", fieldAssignments.ToString())
					.Replace("$$FieldProperties$$", fieldProperties.ToString());
				tableClasses.AppendLine(perTableCode);
			}
			catch (IOException)
			{
				// Could not open table: assume it has no fields and generate
				// code accordingly; the error will pop up again when enumerated
				var perTableCode = perTableTemplate
					.Replace("$$TableName$$", tableClassName)
					.Replace("$$FieldAssignments$$", string.Empty)
					.Replace("$$FieldProperties$$", string.Empty);
				tableClasses.AppendLine(perTableCode);
			}
		}

		var sourceCode = mainSourceTemplate
			.Replace("$$Namespace$$", nameSpace)
			.Replace("$$TypeName$$", typeName)
			.Replace("$$TableProperties$$", tableProps.ToString())
			.Replace("$$PerTableClasses$$", tableClasses.ToString());

		return sourceCode;
	}

	private static string MakeIdentifier(string name)
	{
		// must start with '_' or letter
		// can contain '_' or letter or digit
		if (name.Length < 1) return "_";
		var sb = new StringBuilder();
		for (int i = 0; i < name.Length; i++)
		{
			char c = name[i];
			if (i == 0 && c != '_' && !char.IsLetter(c))
			{
				sb.Append('_'); // prepend to make a valid ident
			}

			if (c == '_' || char.IsLetterOrDigit(c))
			{
				sb.Append(c);
			}
			else
			{
				sb.Append('_');
			}
		}

		return sb.ToString();
	}

	private static string EscapeForString(string name)
	{
		var sb = new StringBuilder();

		foreach (char c in name)
		{
			switch (c)
			{
				case '\\': sb.Append("\\\\"); break;
				case '"': sb.Append("\\\""); break;
				case '\0': sb.Append("\\0"); break;
				case '\a': sb.Append("\\a"); break;
				case '\b': sb.Append("\\b"); break;
				case '\f': sb.Append("\\f"); break;
				case '\n': sb.Append("\\n"); break;
				case '\r': sb.Append("\\r"); break;
				case '\t': sb.Append("\\t"); break;
				case '\v': sb.Append("\\v"); break;
				default:
					sb.Append(c);
					break;
			}
		}

		return sb.ToString();
	}

	private static string GetPropertyTypeName(Type fieldType)
	{
		if (fieldType == typeof(short))
			return "short";
		if (fieldType == typeof(short?))
			return "short?";
		if (fieldType == typeof(int))
			return "int";
		if (fieldType == typeof(int?))
			return "int?";
		if (fieldType == typeof(long))
			return "long";
		if (fieldType == typeof(long?))
			return "long?";
		if (fieldType == typeof(float))
			return "float";
		if (fieldType == typeof(float?))
			return "float?";
		if (fieldType == typeof(double))
			return "double";
		if (fieldType == typeof(double?))
			return "double?";
		if (fieldType == typeof(string))
			return "string";
		if (fieldType == typeof(byte[]))
			return "byte[]";
		if (fieldType == typeof(object))
			return "object";

		if (IsNullable(fieldType, out var underlyingType))
		{
			return $"{underlyingType.FullName}?";
		}

		return fieldType.FullName!;
	}

	private static bool IsNullable(Type type, out Type underlyingType)
	{
		var baseType = Nullable.GetUnderlyingType(type);
		underlyingType = baseType!;
		return baseType != null;
	}

	private static List<ExplorerItem> GetSystemTableItems(Core.FileGDB? gdb)
	{
		if (gdb is null)
		{
			return new List<ExplorerItem>(0);
		}

		return gdb.Catalog
			.Where(entry => entry.IsSystemTable())
			.OrderBy(entry => entry.ID)
			.Select(entry => CreateTableItem(gdb, entry, "Tables"))
			.ToList();
	}

	private static List<ExplorerItem> GetUserTableItems(Core.FileGDB? gdb)
	{
		if (gdb is null)
		{
			return new List<ExplorerItem>(0);
		}

		return gdb.Catalog
			.Where(entry => entry.IsUserTable())
			.OrderBy(entry => entry.Name.ToLowerInvariant())
			.Select(entry => CreateTableItem(gdb, entry, "Tables"))
			.ToList();
	}

	private static ExplorerItem CreateTableItem(Core.FileGDB gdb, CatalogEntry entry, string driverProperty)
	{
		try
		{
			using var table = gdb.OpenTable(entry.ID);

			var itemName = GetTableExplorerName(entry.Name, table);

			return new ExplorerItem(itemName, ExplorerItemKind.QueryableObject, ExplorerIcon.Table)
			{
				IsEnumerable = true,
				DragText = $"{driverProperty}.{entry.Name}",
				ToolTipText = $"ID {entry.ID} ({table.BaseName}), v{table.Version}, #rows {table.RowCount}, max OID {table.MaxObjectID}",
				Children = table.Fields.Select(CreateColumnItem).ToList()
			};
		}
		catch (Exception ex)
		{
			return new ExplorerItem(entry.Name, ExplorerItemKind.Category, ExplorerIcon.Table)
			{
				IsEnumerable = true,
				DragText = $"{driverProperty}.{entry.Name}",
				ToolTipText = $"Error: {ex.Message}"
			};
		}
	}

	private static string GetTableExplorerName(string tableName, Table table)
	{
		if (tableName is null)
			throw new ArgumentNullException(nameof(tableName));
		if (table is null)
			throw new ArgumentNullException(nameof(table));

		var sb = new StringBuilder(tableName);

		if (table.GeometryType != GeometryType.Null)
		{
			sb.Append(" (");
			sb.Append(table.GeometryType);
			if (table.HasZ || table.HasM)
			{
				sb.Append(' ');
				if (table.HasZ) sb.Append('Z');
				if (table.HasM) sb.Append('M');
			}
			sb.Append(')');
		}

		return sb.ToString();
	}

	private static ExplorerItem CreateColumnItem(FieldInfo field)
	{
		var icon = field.Type switch
		{
			FieldType.ObjectID => ExplorerIcon.Key,
			FieldType.Geometry => ExplorerIcon.Box,
			_ => ExplorerIcon.Column
		};

		return new ExplorerItem($"{field.Name} ({field.Type})", ExplorerItemKind.Property, icon)
		{
			ToolTipText = field.Alias ?? field.Name
		};
	}

	private static List<ExplorerItem> GetItemKindItems(ExplorerItem sample)
	{
		return new List<ExplorerItem>
			{
				new("QueryableObject", ExplorerItemKind.QueryableObject, ExplorerIcon.Schema),
				new("Category", ExplorerItemKind.Category, ExplorerIcon.Schema),
				new("Schema", ExplorerItemKind.Schema, ExplorerIcon.Schema),
				new("Parameter", ExplorerItemKind.Parameter, ExplorerIcon.Schema),
				new("Property", ExplorerItemKind.Property, ExplorerIcon.Schema),
				new("ReferenceLink", ExplorerItemKind.ReferenceLink, ExplorerIcon.Schema) {HyperlinkTarget = sample},
				new("CollectionLink", ExplorerItemKind.CollectionLink, ExplorerIcon.Schema) {HyperlinkTarget = sample}
			};
	}

	private static List<ExplorerItem> GetIconItems()
	{
		return new List<ExplorerItem>
			{
				new("Schema", ExplorerItemKind.Category, ExplorerIcon.Schema),
				new("Table", ExplorerItemKind.Category, ExplorerIcon.Table),
				new("View", ExplorerItemKind.Category, ExplorerIcon.View),
				new("Column", ExplorerItemKind.Category, ExplorerIcon.Column),
				new("Key", ExplorerItemKind.Category, ExplorerIcon.Key),
				new("StoredProc", ExplorerItemKind.Category, ExplorerIcon.StoredProc),
				new("ScalarFunction", ExplorerItemKind.Category, ExplorerIcon.ScalarFunction),
				new("TableFunction", ExplorerItemKind.Category, ExplorerIcon.TableFunction),
				new("Parameter", ExplorerItemKind.Category, ExplorerIcon.Parameter),
				new("ManyToOne", ExplorerItemKind.Category, ExplorerIcon.ManyToOne),
				new("OneToMany", ExplorerItemKind.Category, ExplorerIcon.OneToMany),
				new("OneToOne", ExplorerItemKind.Category, ExplorerIcon.OneToOne),
				new("ManyToMany", ExplorerItemKind.Category, ExplorerIcon.ManyToMany),
				new("Inherited", ExplorerItemKind.Category, ExplorerIcon.Inherited),
				new("LinkedDatabase", ExplorerItemKind.Category, ExplorerIcon.LinkedDatabase),
				new("Box", ExplorerItemKind.Category, ExplorerIcon.Box),
				new("Blank", ExplorerItemKind.Category, ExplorerIcon.Blank)
			};
	}

	#endregion
}

[UsedImplicitly]
public abstract class RowBase
{
	// just tagging
}

[UsedImplicitly]
public abstract class TableBase
{
	private readonly Table? _table;
	private readonly bool _debugMode;
	private RowResult? _rowResult;

	protected TableBase(Table table, bool debugMode)
	{
		_table = table ?? throw new ArgumentNullException(nameof(table));
		_debugMode = debugMode;
	}

	[PublicAPI]
	protected RowsResult ReadRows()
	{
		if (_debugMode)
		{
			Debugger.Launch();
		}

		return Table.ReadRows(null, null);
	}

	[PublicAPI]
	protected RowResult? ReadRow(long oid)
	{
		if (_debugMode)
		{
			Debugger.Launch();
		}

		var values = Table.ReadRow(oid);
		if (values is null) return null;

		_rowResult ??= new RowResult(Table.Fields);
		_rowResult.SetValues(values);

		return _rowResult;
	}

	[PublicAPI] // just a convenient abbreviation
	public IReadOnlyList<FieldInfo> Fields => Table.Fields;

	[PublicAPI]
	public Table Table =>
		_table ?? throw new InvalidOperationException("This table wrapper has not been initialized");
}

public abstract class TableContainerBase : IDisposable
{
	private readonly Core.FileGDB _gdb;
	private readonly bool _debugMode;
	private readonly IDictionary<string, Table> _tableCache;

	protected TableContainerBase(Core.FileGDB gdb, bool debugMode)
	{
		_gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
		_debugMode = debugMode;
		_tableCache = new Dictionary<string, Table>();
	}

	[PublicAPI]
	protected T GetTable<T>(string tableName) where T : TableBase
	{
		var table = GetRawTable(tableName);
		var args = new object[] { table, _debugMode };
		var wrapper = (T?) Activator.CreateInstance(typeof(T), args, null);
		return wrapper ?? throw new Exception("Got null from Activator");
	}

	private Table GetRawTable(string tableName)
	{
		if (! _tableCache.TryGetValue(tableName, out var table))
		{
			table = _gdb.OpenTable(tableName);
			_tableCache.Add(tableName, table);
		}

		return table;
	}

	public void Dispose()
	{
		List<Table> list;

		lock (_tableCache)
		{
			list = _tableCache.Values.ToList();
			_tableCache.Clear();
		}

		foreach (var table in list)
		{
			table.Dispose();
		}

		GC.SuppressFinalize(this);
	}
}

[PublicAPI]
public abstract class DriverBase : IDisposable
{
	private readonly bool _debugMode;

	protected DriverBase(string gdbFolderPath, bool debugMode)
	{
		if (string.IsNullOrEmpty(gdbFolderPath))
			throw new ArgumentNullException(nameof(gdbFolderPath));
		GDB = Core.FileGDB.Open(gdbFolderPath);
		_debugMode = debugMode;
	}

	public Core.FileGDB GDB { get; }

	public string FolderPath => GDB.FolderPath;

	public IEnumerable<CatalogEntry> Catalog => GDB.Catalog;

	public Table OpenTable(int id)
	{
		return GDB.OpenTable(id);
	}

	public Table OpenTable(string name)
	{
		return GDB.OpenTable(name);
	}

	public static bool LaunchDebugger()
	{
		return Debugger.Launch();
	}

	public abstract void Dispose();
}
