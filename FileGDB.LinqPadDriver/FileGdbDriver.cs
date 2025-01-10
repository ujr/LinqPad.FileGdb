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

	//public override bool AreRepositoriesEquivalent(IConnectionInfo c1, IConnectionInfo c2)
	//{
	//	if (c1 is null)
	//		throw new ArgumentNullException(nameof(c1));
	//	if (c2 is null)
	//		throw new ArgumentNullException(nameof(c2));

	//	var folder1 = c1.GetGdbFolderPath();
	//	var folder2 = c2.GetGdbFolderPath();
	//	if (folder1 is null && folder2 is null) return true;
	//	if (folder1 is null || folder2 is null) return false;
	//	var path1 = Path.GetFullPath(folder1);
	//	var path2 = Path.GetFullPath(folder2);
	//	return string.Equals(path1, path2, StringComparison.Ordinal);
	//}

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
		else if (info.ParentHierarchy.FirstOrDefault() is FieldInfo)
		{
			if (objectToWrite is GeometryDef geometryDef)
			{
				var description = Utils.GetDisplayName(geometryDef);
				objectToWrite = Util.OnDemand(description, () => geometryDef);
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

	public override List<ExplorerItem> GetSchemaAndBuildAssembly(
		IConnectionInfo cxInfo, AssemblyName assemblyToBuild,
		ref string nameSpace, ref string typeName)
	{
		bool debugMode = cxInfo.GetDebugMode();

		if (debugMode)
		{
			Debugger.Launch();
		}

		var gdbFolderPath = cxInfo.GetGdbFolderPath();
		var gdb = gdbFolderPath is null ? null : Core.FileGDB.Open(gdbFolderPath);

		var folderPathItem = new ExplorerItem("FolderPath", ExplorerItemKind.Property, ExplorerIcon.Parameter)
		{
			IsEnumerable = false,
			DragText = nameof(DriverBase.FolderPath),
			ToolTipText = gdb?.FolderPath ?? "Full path to the .gdb/ folder"
		};

		var systemItem = new ExplorerItem("System", ExplorerItemKind.Schema, ExplorerIcon.Schema)
		{
			Children = GetSystemTableItems(gdb),
			ToolTipText = "Geodatabase System Tables"
		};

		var systemTables = systemItem.Children.Select(item => item.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);

		var tablesItem = new ExplorerItem("Tables", ExplorerItemKind.Schema, ExplorerIcon.Schema)
		{
			Children = GetTableItems(gdb, name => !systemTables.Contains(name)),
			ToolTipText = "User-defined tables (all but system tables)"
		};

		var source = MakeContextSourceCode(nameSpace, typeName, gdb);
		var outputFile = assemblyToBuild.CodeBase ?? throw new Exception($"No CodeBase on {nameof(assemblyToBuild)}");

		Compile(source, outputFile, cxInfo);

		var items = new List<ExplorerItem>
		{
			folderPathItem,
			systemItem,
			tablesItem
		};

		if (debugMode)
		{
			items.Add(new ExplorerItem($"{nameSpace}.{typeName}", ExplorerItemKind.Property, ExplorerIcon.Box)
			{
				IsEnumerable = false,
				DragText = source,
				ToolTipText = "generated source code for data context"
			});
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

		const string mainSourceTemplate = @"
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace $$NAMESPACE$$;

public class $$TYPENAME$$ : FileGDB.LinqPadDriver.DriverBase
{
  public $$TYPENAME$$(string gdbFolderPath, bool debugMode) : base(gdbFolderPath, debugMode)
  {
    var gdb = GetFileGDB();
    GDB = new SystemTables(gdb, debugMode);
    Tables = new UserTables(gdb, debugMode);
  }

  public SystemTables GDB { get; }
  public UserTables Tables { get; }

  public class SystemTables : FileGDB.LinqPadDriver.TableContainer
  {
    public SystemTables(FileGDB.Core.FileGDB gdb, bool debugMode) : base(gdb, debugMode) {}

    // public @FooTable @Foo => GetTable<@FooTable>(""Foo"");
    $$SYSTEMTABLEPROPS$$
  }

  public class UserTables : FileGDB.LinqPadDriver.TableContainer
  {
    public UserTables(FileGDB.Core.FileGDB gdb, bool debugMode) : base(gdb, debugMode) {}

    // public @FooTable @Foo => GetTable<@FooTable>(""Foo"");
    $$USERTABLEPROPS$$
  }

  $$PERTABLECLASSES$$
}
";

		const string perTableTemplate = @"
public class $$TABLENAME$$Table : FileGDB.LinqPadDriver.TableBase, IEnumerable<$$TABLENAME$$Table.Row>
{
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public IEnumerator<Row> GetEnumerator()
  {
    var cursor = SearchRows();
    var values = (FileGDB.Core.IRowValues) cursor;

    while (cursor.Step())
    {
      var row = new Row();
      //row.Bar = (BarType)values.GetValue(""Bar"");
      $$FIELDPROPERTYINIT$$
      yield return row;
    }
  }

  public Row GetRow(long oid)
  {
    var values = ReadRow(oid);
    if (values is null) return null;

    var row = new Row();
    //row.Bar = (BarType)values.GetValue(""Bar"");
    $$FIELDPROPERTYINIT$$
    return row;
  }

  public class Row : FileGDB.LinqPadDriver.RowBase
  {
    //public BarType Bar { get; set; }
    $$PERFIELDPROPERTIES$$
  }
}
";
		if (gdb is null)
		{
			return mainSourceTemplate
				.Replace("$$NAMESPACE$$", nameSpace)
				.Replace("$$TYPENAME$$", typeName)
				.Replace("$$SYSTEMTABLEPROPS$$", string.Empty)
				.Replace("$$USERTABLEPROPS$$", string.Empty)
				.Replace("$$PERTABLECLASSES$$", string.Empty);
		}

		var systemTableProps = new StringBuilder();
		var userTableProps = new StringBuilder();
		var tableClasses = new StringBuilder();

		foreach (var entry in gdb.Catalog)
		{
			var tableName = entry.Name;
			var tableClassName = MakeIdentifier(tableName);

			if (tableName.StartsWith("GDB_", StringComparison.OrdinalIgnoreCase))
			{
				systemTableProps.Append($"public @{tableClassName}Table @{tableClassName} => ");
				systemTableProps.AppendLine($"GetTable<@{tableClassName}Table>(\"{tableClassName}\");");
			}
			else
			{
				userTableProps.Append($"public @{tableClassName}Table @{tableClassName} => ");
				userTableProps.AppendLine($"GetTable<@{tableClassName}Table>(\"{tableClassName}\");");
			}

			try
			{
				using var table = gdb.OpenTable(tableName);

				var propertyInits = new StringBuilder();
				var fieldProperties = new StringBuilder();

				foreach (var field in table.Fields)
				{
					var fieldName = field.Name;
					var propName = MakeIdentifier(fieldName);
					var escaped = EscapeForString(fieldName);
					var fieldType = Table.GetDataType(field.Type);
					var fieldTypeName = GetPropertyTypeName(fieldType);

					propertyInits.AppendLine($"row.@{propName} = ({fieldTypeName}) values.GetValue(\"{escaped}\");");
					fieldProperties.AppendLine($"public {fieldTypeName} @{propName} {{ get; set; }}");
				}

				var perTableCode = perTableTemplate
					.Replace("$$TABLENAME$$", tableClassName)
					.Replace("$$FIELDPROPERTYINIT$$", propertyInits.ToString())
					.Replace("$$PERFIELDPROPERTIES$$", fieldProperties.ToString());
				tableClasses.AppendLine(perTableCode);
			}
			catch (IOException)
			{
				// Could not open table: assume it has no fields and generate
				// code accordingly; the error will pop up again when enumerated
				var perTableCode = perTableTemplate
					.Replace("$$TABLENAME$$", tableClassName)
					.Replace("$$FIELDPROPERTYINIT$$", string.Empty)
					.Replace("$$PERFIELDPROPERTIES$$", string.Empty);
				tableClasses.AppendLine(perTableCode);
			}
		}

		var sourceCode = mainSourceTemplate
			.Replace("$$NAMESPACE$$", nameSpace)
			.Replace("$$TYPENAME$$", typeName)
			.Replace("$$SYSTEMTABLEPROPS$$", systemTableProps.ToString())
			.Replace("$$USERTABLEPROPS$$", userTableProps.ToString())
			.Replace("$$PERTABLECLASSES$$", tableClasses.ToString());

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

		// Tables whose names begin with "GDB_" (ignoring case) are considered system tables:
		var systemEntries = gdb.Catalog
			.Where(e => e.Name.StartsWith("GDB_", StringComparison.OrdinalIgnoreCase))
			.OrderBy(e => e.ID);

		var list = systemEntries.Select(CreateSystemItem).ToList();

		return list;
	}

	private static ExplorerItem CreateSystemItem(Core.FileGDB.CatalogEntry entry)
	{
		var description = Core.FileGDB.SystemTableDescriptions.GetValueOrDefault(entry.Name);

		return new ExplorerItem(entry.Name, ExplorerItemKind.QueryableObject, ExplorerIcon.Table)
		{
			IsEnumerable = true,
			DragText = $"GDB.{entry.Name}",
			ToolTipText = description is null ? $"Table ID {entry.ID}" : $"Table ID {entry.ID}, {description}"
		};
	}

	private static List<ExplorerItem> GetTableItems(Core.FileGDB? gdb, Predicate<string> filter)
	{
		if (gdb is null)
		{
			return new List<ExplorerItem>(0);
		}

		return gdb.Catalog
			.Where(entry => filter(entry.Name))
			.Select(entry=> CreateTableItem(gdb, entry.Name))
			.ToList();
	}

	private static ExplorerItem CreateTableItem(Core.FileGDB gdb, string tableName)
	{
		try
		{
			using var table = gdb.OpenTable(tableName);

			var itemName = GetTableExplorerName(tableName, table);

			return new ExplorerItem(itemName, ExplorerItemKind.QueryableObject, ExplorerIcon.Table)
			{
				IsEnumerable = true,
				//DragText = $"{nameof(FileGdbContext.Table)}[{Utils.FormatString(tableName)}]",
				DragText = $"Tables.{tableName}",
				ToolTipText = $"{table.BaseName}, v{table.Version}, #rows {table.RowCount}, max OID {table.MaxObjectID} ",
				Children = CreateColumnItems(table)
			};
		}
		catch (Exception ex)
		{
			return new ExplorerItem(tableName, ExplorerItemKind.Category, ExplorerIcon.Table)
			{
				IsEnumerable = false,
				DragText = "Error",
				ToolTipText = $"Error: {ex.Message}",
				Children = new List<ExplorerItem>
				{
					new($"Error: {ex.Message}", ExplorerItemKind.Category, ExplorerIcon.Blank)
				}
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

	private static List<ExplorerItem> CreateColumnItems(Table table)
	{
		var list = new List<ExplorerItem>();

		if (table.GeometryType != GeometryType.Null)
		{
			var sb = new StringBuilder();
			sb.Append('(').Append(table.GeometryType);
			if (table.HasZ) sb.Append(" hasZ");
			if (table.HasM) sb.Append(" hasM");
			sb.Append(')');
			list.Add(new ExplorerItem(sb.ToString(), ExplorerItemKind.Property, ExplorerIcon.Box));
		}

		list.AddRange(table.Fields.Select(CreateColumnItem));

		return list;
	}

	private static ExplorerItem CreateColumnItem(FieldInfo field)
	{
		return new ExplorerItem($"{field.Name} ({field.Type})", ExplorerItemKind.Property, ExplorerIcon.Column)
		{
			ToolTipText = field.Alias ?? field.Name
		};
	}
}

[UsedImplicitly]
public abstract class RowBase
{
	// just tagging
}

[UsedImplicitly]
public abstract class TableBase
{
	private Table? _table;
	private RowResult? _rowResult;
	private bool _debugMode;

	public TableBase SetTable(Table table, bool debugMode)
	{
		_table = table ?? throw new ArgumentNullException(nameof(table));
		_debugMode = debugMode;
		return this;
	}

	[PublicAPI] public string DataFilePath => Table.GetDataFilePath();
	[PublicAPI] public string IndexFilePath => Table.GetIndexFilePath();
	[PublicAPI] public int Version => Table.Version;
	[PublicAPI] public long MaxObjectID => Table.MaxObjectID;
	[PublicAPI] public long RowCount => Table.RowCount;
	[PublicAPI] public GeometryType GeometryType => Table.GeometryType;
	[PublicAPI] public bool HasZ => Table.HasZ;
	[PublicAPI] public bool HasM => Table.HasM;
//	[PublicAPI] public bool UseUtf8 => Table.UseUtf8;
//	[PublicAPI] public int MaxEntrySize => Table.MaxEntrySize;
//	[PublicAPI] public int OffsetSize => Table.OffsetSize;
//	[PublicAPI] public long DataFileSize => Table.FileSizeBytes;
//	[PublicAPI] public int FieldCount => Table.FieldCount;
	[PublicAPI] public IReadOnlyList<FieldInfo> Fields => Table.Fields;
	[PublicAPI] public IReadOnlyList<IndexInfo> Indexes => Table.Indexes;
	[PublicAPI] public Table.InternalInfo Internals => Table.Internals;

	[PublicAPI]
	protected RowsResult SearchRows()
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

	[PublicAPI]
	public Table Table =>
		_table ?? throw new InvalidOperationException("This table wrapper has not been initialized");
}

[UsedImplicitly]
public abstract class TableContainer
{
	private readonly Core.FileGDB _gdb;
	private readonly bool _debugMode;
	private readonly IDictionary<string, TableBase> _cache;

	protected TableContainer(Core.FileGDB gdb, bool debugMode)
	{
		_gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
		_debugMode = debugMode;
		_cache = new Dictionary<string, TableBase>();
	}

	[PublicAPI]
	protected T GetTable<T>(string tableName) where T : TableBase, new()
	{
		if (!_cache.TryGetValue(tableName, out var table))
		{
			var inner = _gdb.OpenTable(tableName);
			table = new T().SetTable(inner, _debugMode);
			_cache.Add(tableName, table);
		}
		return (T)table;
	}
}

[PublicAPI]
public abstract class DriverBase
{
	private readonly Core.FileGDB _gdb;
	private readonly bool _debugMode;

	protected DriverBase(string gdbFolderPath, bool debugMode)
	{
		if (string.IsNullOrEmpty(gdbFolderPath))
			throw new ArgumentNullException(nameof(gdbFolderPath));
		_gdb = Core.FileGDB.Open(gdbFolderPath);
		_debugMode = debugMode;
	}

	protected Core.FileGDB GetFileGDB() => _gdb;

	public string FolderPath => _gdb.FolderPath;
	//public IEnumerable<string> TableNames => _gdb.TableNames;
	public IEnumerable<Core.FileGDB.CatalogEntry> Catalog => _gdb.Catalog;

	public Table OpenTable(int id)
	{
		if (_debugMode)
		{
			Debugger.Launch();
		}
		return _gdb.OpenTable(id);
	}

	public Table OpenTable(string name)
	{
		if (_debugMode)
		{
			Debugger.Launch();
		}
		return _gdb.OpenTable(name);
	}
}
