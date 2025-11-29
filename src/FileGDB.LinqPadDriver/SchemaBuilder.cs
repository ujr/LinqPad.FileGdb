using FileGDB.Core;
using LINQPad.Extensibility.DataContext;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using FieldInfo = FileGDB.Core.FieldInfo;

namespace FileGDB.LinqPadDriver;

internal class SchemaBuilder : IDisposable
{
	private readonly IConnectionInfo _cxInfo;
	private readonly bool _debugMode;
	private Core.FileGDB? _gdb;
	private bool _disposed;

	public SchemaBuilder(IConnectionInfo cxInfo)
	{
		_cxInfo = cxInfo ?? throw new ArgumentNullException(nameof(cxInfo));

		_debugMode = cxInfo.GetDebugMode();

		var gdbFolderPath = cxInfo.GetGdbFolderPath();
		_gdb = gdbFolderPath is null ? null : Core.FileGDB.Open(gdbFolderPath);
		_disposed = false;
	}

	public void Dispose()
	{
		_gdb?.Dispose();
		_gdb = null;
		_disposed = true;
	}

	public List<ExplorerItem> GetSchema()
	{
		if (_disposed)
			throw new ObjectDisposedException(GetType().Name);

		var catalogItem = new ExplorerItem("Catalog", ExplorerItemKind.Schema, ExplorerIcon.Schema)
		{
			IsEnumerable = true,
			DragText = nameof(DataContextBase.Catalog),
			ToolTipText = "Catalog (list of tables)"
		};

		var systemItem = new ExplorerItem("System", ExplorerItemKind.Category, ExplorerIcon.Table)
		{
			Children = GetSystemTableItems(_gdb),
			ToolTipText = "Geodatabase System Tables"
		};

		var tablesItem = new ExplorerItem("Tables", ExplorerItemKind.Category, ExplorerIcon.Table)
		{
			Children = GetUserTableItems(_gdb),
			ToolTipText = "User-defined tables"
		};

		var items = new List<ExplorerItem>
		{
			catalogItem,
			systemItem,
			tablesItem
		};

		if (_debugMode)
		{
			var debugItem = new ExplorerItem("Debug", ExplorerItemKind.Category, ExplorerIcon.Box);

			var children = debugItem.Children = new List<ExplorerItem>();

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

	public class BuildResult
	{
		public string SourceCode { get; }
		public bool HasErrors => Errors.Length > 0;
		public string[] Errors { get; }

		public BuildResult(string sourceCode, string[]? errors = null)
		{
			SourceCode = sourceCode;
			Errors = errors ?? Array.Empty<string>();
		}
	}

	public BuildResult BuildAssembly(
		AssemblyName assemblyToBuild, string nameSpace, string typeName)
	{
		if (_disposed)
			throw new ObjectDisposedException(GetType().Name);

		var generator = new DataContextSourceBuilder(nameSpace, typeName);
		var source = generator.Build(_gdb);
		var outputFile = assemblyToBuild.CodeBase ??
		                 throw new Exception($"No CodeBase on {nameof(assemblyToBuild)}");

		return Compile(source, outputFile, _cxInfo);
	}

	private static BuildResult Compile(string cSharpSourceCode, string outputFile, IConnectionInfo cxInfo)
	{
		var assembliesToReference =
#if NETCORE || true
			// GetCoreFxReferenceAssemblies is helper method that returns the full
			// set of .NET Core reference assemblies (there are more than 100 of them).
			DataContextDriver.GetCoreFxReferenceAssemblies(cxInfo).ToList();
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
		var compileResult = DataContextDriver.CompileSource(new CompilationInput
		{
			FilePathsToReference = assembliesToReference.ToArray(),
			OutputPath = outputFile,
			SourceCode = new[] { cSharpSourceCode }
		});

		return new BuildResult(cSharpSourceCode, compileResult.Errors);
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
}
