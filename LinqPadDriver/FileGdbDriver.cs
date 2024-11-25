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
using LinqPadDriver;
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

		var assembly = Assembly.GetExecutingAssembly();
		var dllFileName = Path.GetFileName(assembly.Location);
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
			objectToWrite = FormatGeometryBlob(blob)!;
		}
		//else if (objectToWrite is Shape shape)
		//{
		//	objectToWrite = ShapeProxy.Get(shape)!;
		//}
		//else if (objectToWrite is ShapeBuffer shapeBuffer)
		//{
		//	objectToWrite = new ShapeBufferProxy(shapeBuffer);
		//	objectToWrite = shapeBuffer.ToString();
		//	objectToWrite = Util.WithStyle(shapeBuffer.ToString(), "font-weight:bold");
		//}
		else if (objectToWrite is IEnumerable<byte> bytes)
		{
			var parent = info.ParentHierarchy.FirstOrDefault();
			// Show first few bytes (fewer if on ShapeBuffer, because there are many more properties)
			int maxBytes = parent is ShapeBuffer ? 12 : 24;
			var text = FormatBytes(bytes, maxBytes);
			objectToWrite = Util.WithStyle(text, "color:green");
		}
		base.PreprocessObjectToWrite(ref objectToWrite, info);
	}

	private static object FormatGeometryBlob(GeometryBlob? blob)
	{
		if (blob is null)
			throw new ArgumentNullException(nameof(blob));

		try
		{
			var head = Util.ToHtmlString(FormatBytes(blob.Bytes, 12));
			var type = Util.ToHtmlString(blob.ShapeBuffer.GeometryType.ToString());
			return Util.RawHtml($"<font color='green'>{head}</font> <b>{type}</b>");
		}
		catch (Exception ex)
		{
			return Util.WithStyle($"Error: {ex.Message}", "color:Crimson");
		}
	}

	private static string FormatBytes(IEnumerable<byte> bytes, int maxBytes)
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

		if (bytes is ICollection<byte> collection)
		{
			sb.AppendFormat(" ({0} bytes)", collection.Count);
		}

		return sb.ToString();
	}

	private class ShapeBufferProxy
	{
		private ShapeBuffer ShapeBuffer { get; }

		public ShapeBufferProxy(ShapeBuffer shapeBuffer)
		{
			ShapeBuffer = shapeBuffer ?? throw new ArgumentNullException(nameof(shapeBuffer));
		}

		public GeometryType Type => ShapeBuffer.GeometryType;
		public ShapeFlags Flags => GetFlags(ShapeBuffer);
		public bool? IsEmpty => ShapeBuffer.IsEmpty;

		private static ShapeFlags GetFlags(ShapeBuffer shapeBuffer)
		{
			var flags = ShapeFlags.None;

			if (shapeBuffer.HasZ) flags |= ShapeFlags.HasZ;
			if (shapeBuffer.HasM) flags |= ShapeFlags.HasM;
			if (shapeBuffer.HasID) flags |= ShapeFlags.HasID;

			return flags;
		}

		public override string ToString()
		{
			return ShapeBuffer.GeometryType.ToString();
		}
	}

	private class ShapeProxy
	{
		protected Shape Shape { get; }

		protected ShapeProxy(Shape shape)
		{
			Shape = shape ?? throw new ArgumentNullException(nameof(shape));
		}

		public GeometryType Type => Shape.Type;
		public ShapeFlags Flags => Shape.Flags;
		public bool IsEmpty => Shape.IsEmpty;
		public object Box => Util.OnDemand("Box", () => Shape.Box);

		public static ShapeProxy? Get(Shape? shape)
		{
			if (shape is null) return null;
			if (shape is PointShape point) return new PointShapeProxy(point);
			if (shape is BoxShape box) return new BoxShapeProxy(box);
			if (shape is MultipointShape multipoint) return new MultipointShapeProxy(multipoint);
			if (shape is PolylineShape polyline) return new PolylineShapeProxy(polyline);
			if (shape is PolygonShape polygon) return new PolygonShapeProxy(polygon);
			throw new NotSupportedException($"Unknown shape type {shape.GetType().Name}");
		}
	}

	private class PointShapeProxy : ShapeProxy
	{
		public PointShapeProxy(PointShape shape) : base(shape) { }

		public double X => ((PointShape)Shape).X;
		public double Y => ((PointShape)Shape).Y;
		public double Z => ((PointShape)Shape).Z;
		public double M => ((PointShape)Shape).M;
		public int ID => ((PointShape)Shape).ID;
	}

	private class BoxShapeProxy : ShapeProxy
	{
		public BoxShapeProxy(BoxShape shape) : base(shape) { }

		public double XMin => ((BoxShape)Shape).XMin;
		public double YMin => ((BoxShape)Shape).YMin;
		public double XMax => ((BoxShape)Shape).XMax;
		public double YMax => ((BoxShape)Shape).YMax;
		public double ZMin => ((BoxShape)Shape).ZMin;
		public double ZMax => ((BoxShape)Shape).ZMax;
		public double MMin => ((BoxShape)Shape).MMin;
		public double MMax => ((BoxShape)Shape).MMax;
	}

	private class PointListShapeProxy : ShapeProxy
	{
		protected PointListShapeProxy(PointListShape shape) : base(shape) { }

		public object Points => Util.OnDemand(PointsLabel, () => ((PointListShape)Shape).Points);

		private string PointsLabel => $"Points ({((PointListShape)Shape).NumPoints})";
	}

	private class MultipointShapeProxy : PointListShapeProxy
	{
		public MultipointShapeProxy(MultipointShape shape) : base(shape) { }
	}

	private class MultipartShapeProxy : PointListShapeProxy
	{
		protected MultipartShapeProxy(MultipartShape shape) : base(shape) { }

		protected string PartsLabel => $"Parts ({((MultipartShape)Shape).NumParts})";
	}

	private class PolylineShapeProxy : MultipartShapeProxy
	{
		public PolylineShapeProxy(PolylineShape shape) : base(shape) { }

		public object Parts => Util.OnDemand(PartsLabel, () => ((PolylineShape)Shape).Parts);
	}

	private class PolygonShapeProxy : MultipartShapeProxy
	{
		public PolygonShapeProxy(PolygonShape shape) : base(shape) { }

		public object Parts => Util.OnDemand(PartsLabel, () => ((PolygonShape)Shape).Parts);
	}

	//public override void DisplayObjectInGrid(object objectToDisplay, GridOptions options)
	//{
	//	if (objectToDisplay is FileGdbContext.RowProxy row)
	//	{
	//		Debugger.Launch();
	//		dynamic obj = new ExpandoObject();
	//		var dict = (IDictionary<string, object?>)obj;

	//		foreach (var foo in row.GetNames().Zip(row.GetValues()))
	//		{
	//			dict.Add(foo.First, foo.Second);
	//		}

	//		objectToDisplay = obj;
	//	}

	//	base.DisplayObjectInGrid(objectToDisplay, options);
	//}

	//public override List<ExplorerItem> GetSchema(IConnectionInfo cxInfo, Type customType)
	//{
	//	var gdbFolderPath = cxInfo.GetGdbFolderPath();
	//	var gdb = gdbFolderPath is null ? null : Core.FileGDB.Open(gdbFolderPath);

	//	var folderPathItem = new ExplorerItem("FolderPath", ExplorerItemKind.Property, ExplorerIcon.Parameter)
	//	{
	//		IsEnumerable = false,
	//		DragText = nameof(FileGdbContext.FolderPath),
	//		ToolTipText = gdb?.FolderPath ?? "Full path to the .gdb/ folder"
	//	};

	//	var systemItem = new ExplorerItem("System", ExplorerItemKind.Schema, ExplorerIcon.Schema)
	//	{
	//		Children = GetSystemTableItems(),
	//		ToolTipText = "Geodatabase System Tables"
	//	};

	//	var systemTables = systemItem.Children.Select(item => item.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);

	//	var tablesItem = new ExplorerItem("Tables", ExplorerItemKind.Schema, ExplorerIcon.Schema)
	//	{
	//		Children = GetTableItems(gdb, name => !systemTables.Contains(name)),
	//		ToolTipText = "User-defined tables (all but system tables)"
	//	};

	//	return new List<ExplorerItem>
	//	{
	//		folderPathItem, systemItem, tablesItem
	//	};
	//}

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
			DragText = nameof(FileGdbContext.FolderPath),
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

		Compile(source, assemblyToBuild.CodeBase, cxInfo);

		var items = new List<ExplorerItem>
		{
			folderPathItem,
			systemItem,
			tablesItem
		};

		if (debugMode)
		{
			items.Add(new($"NameSpace={nameSpace}", ExplorerItemKind.Schema, ExplorerIcon.Box));
			items.Add(new($"TypeName={typeName}", ExplorerItemKind.Schema, ExplorerIcon.Box));
			items.Add(new("SourceCode", ExplorerItemKind.Property, ExplorerIcon.Box)
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

public class $$TYPENAME$$
{
  private readonly FileGDB.Core.FileGDB _gdb;
  private readonly bool _debugMode;

  public $$TYPENAME$$(string gdbFolderPath, bool debugMode)
  {
    if (string.IsNullOrEmpty(gdbFolderPath))
      throw new ArgumentNullException(nameof(gdbFolderPath));
    _gdb = FileGDB.Core.FileGDB.Open(gdbFolderPath);
    _debugMode = debugMode;
    GDB = new SystemTables(_gdb, debugMode);
    Tables = new UserTables(_gdb, debugMode);
  }

  public string FolderPath => _gdb.FolderPath;
  //public IEnumerable<string> TableNames => _gdb.TableNames;
  public IEnumerable<FileGDB.Core.FileGDB.CatalogEntry> Catalog => _gdb.Catalog;
  public SystemTables GDB { get; }
  public UserTables Tables { get; }

  public FileGDB.Core.Table OpenTable(int id)
  {
    if (_debugMode) Debugger.Launch();
    return _gdb.OpenTable(id);
  }

  public FileGDB.Core.Table OpenTable(string name)
  {
    if (_debugMode) Debugger.Launch();
    return _gdb.OpenTable(name);
  }

  public class SystemTables : TableContainer
  {
    public SystemTables(FileGDB.Core.FileGDB gdb, bool debugMode) : base(gdb, debugMode) {}

    // public @FooTable @Foo => GetTable<@FooTable>(""Foo"");
    $$SYSTEMTABLEPROPS$$
  }

  public class UserTables : TableContainer
  {
    public UserTables(FileGDB.Core.FileGDB gdb, bool debugMode) : base(gdb, debugMode) {}

    // public @FooTable @Foo => GetTable<@FooTable>(""Foo"");
    $$USERTABLEPROPS$$
  }

  public abstract class TableContainer
  {
    private readonly FileGDB.Core.FileGDB _gdb;
    private readonly bool _debugMode;
    private readonly IDictionary<string, TableBase> _cache;

    protected TableContainer(FileGDB.Core.FileGDB gdb, bool debugMode)
    {
      _gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
      _debugMode = debugMode;
      _cache = new Dictionary<string, TableBase>();
    }

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

  public abstract class TableBase
  {
    private FileGDB.Core.Table? _table;
    protected bool DebugMode { get; private set; }

    public TableBase SetTable(FileGDB.Core.Table table, bool debugMode)
    {
      _table = table ?? throw new ArgumentNullException(nameof(table));
      DebugMode = debugMode;
      return this;
    }

    public int FieldCount => Table.FieldCount;
    public long RowCount => Table.RowCount;
    public FileGDB.Core.GeometryType GeometryType => Table.GeometryType;
    public bool HasZ => Table.HasZ;
    public bool HasM => Table.HasM;
    public int Version => Table.Version;
    public bool UseUtf8 => Table.UseUtf8;
    public long MaxOID => Table.MaxObjectID;
    public int MaxEntrySize => _table.MaxEntrySize;
    public IReadOnlyList<FileGDB.Core.FieldInfo> Fields => Table.Fields;

    protected FileGDB.Core.Table Table =>
      _table ?? throw new InvalidOperationException(""This table wrapper has not been initialized"");
  }

  $$PERTABLECLASSES$$
}
";

		const string perTableTemplate = @"
public class $$TABLENAME$$Table : TableBase, IEnumerable<$$TABLENAME$$Table.Row>
{
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  public IEnumerator<Row> GetEnumerator()
  {
    if (DebugMode) Debugger.Launch();

    var cursor = Table.Search(null, null, null);

    while (cursor.Step())
    {
      var row = new Row();
      //row.Bar = (BarType)cursor.GetValue(""Bar"");
      $$FIELDPROPERTYINIT$$
      yield return row;
    }
  }

  public class Row
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

					propertyInits.AppendLine($"row.@{propName} = ({fieldTypeName}) cursor.GetValue(\"{escaped}\");");
					fieldProperties.AppendLine($"public {fieldTypeName} @{propName} {{ get; set; }}");
				}

				var perTableCode = perTableTemplate
					.Replace("$$TABLENAME$$", tableClassName)
					.Replace("$$FIELDPROPERTYINIT$$", propertyInits.ToString())
					.Replace("$$PERFIELDPROPERTIES$$", fieldProperties.ToString());
				tableClasses.AppendLine(perTableCode);
			}
			catch (IOException ex)
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
				ToolTipText = $"{table.BaseName}, v{table.Version}, {table.RowCount} #rows, {table.MaxObjectID} max OID",
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
