using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FileGDB.Core;
using JetBrains.Annotations;
using LINQPad.Extensibility.DataContext;
using LinqPadDriver;
using FieldInfo = FileGDB.Core.FieldInfo;

namespace FileGDB.LinqPadDriver;

[UsedImplicitly]
public class FileGdbDriver : StaticDataContextDriver
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
		cxInfo.CustomTypeInfo.CustomAssemblyPath = dllFileName;
		cxInfo.CustomTypeInfo.CustomTypeName = typeof(FileGdbContext).FullName;

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
			new ParameterDescriptor("folderPath", typeof(string).FullName)
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
			gdbFolderPath
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

	public override void DisplayObjectInGrid(object objectToDisplay, GridOptions options)
	{
		if (objectToDisplay is FileGdbContext.RowProxy row)
		{
			Debugger.Launch();
			dynamic obj = new ExpandoObject();
			var dict = (IDictionary<string, object?>)obj;

			foreach (var foo in row.GetNames().Zip(row.GetValues()))
			{
				dict.Add(foo.First, foo.Second);
			}

			objectToDisplay = obj;
		}

		base.DisplayObjectInGrid(objectToDisplay, options);
	}

	public override List<ExplorerItem> GetSchema(IConnectionInfo cxInfo, Type customType)
	{
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
			Children = GetSystemTableItems(),
			ToolTipText = "Geodatabase System Tables"
		};

		var systemTables = systemItem.Children.Select(item => item.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);

		var tablesItem = new ExplorerItem("Tables", ExplorerItemKind.Schema, ExplorerIcon.Schema)
		{
			Children = GetTableItems(gdb, name => !systemTables.Contains(name)),
			ToolTipText = "User-defined tables (all but system tables)"
		};

		return new List<ExplorerItem>
		{
			folderPathItem, systemItem, tablesItem
		};
	}

	private static List<ExplorerItem> GetSystemTableItems()
	{
		const string systemTables = nameof(FileGdbContext.GDB);

		var list = new List<ExplorerItem>
		{
			CreateSystemItem("GDB_SystemCatalog",
				$"{systemTables}.{nameof(FileGdbContext.SystemTables.SystemCatalog)}",
				"Catalog system table (list of all tables)"),
			CreateSystemItem("GDB_DBTune",
				$"{systemTables}.{nameof(FileGdbContext.SystemTables.DBTune)}",
				"DBTune system table (config keyword parameters)"),
			CreateSystemItem("GDB_SpatialRefs",
				$"{systemTables}.{nameof(FileGdbContext.SystemTables.SpatialRefs)}",
				"Spatial references used by tables in this File GDB"),
			CreateSystemItem("GDB_Items",
				$"{systemTables}.{nameof(FileGdbContext.SystemTables.Items)}",
				"The GDB_Items system table"),
			CreateSystemItem("GDB_ItemTypes",
				$"{systemTables}.{nameof(FileGdbContext.SystemTables.ItemTypes)}",
				"The GDB_ItemTypes system table"),
			CreateSystemItem("GDB_ItemRelationships",
				$"{systemTables}.{nameof(FileGdbContext.SystemTables.ItemRelationships)}",
				"The GDB_ItemRelationships system table"),
			CreateSystemItem("GDB_ItemRelationshipTypes",
				$"{systemTables}.{nameof(FileGdbContext.SystemTables.ItemRelationshipTypes)}",
				"The GDB_ItemRelationshipTypes system table"),
			CreateSystemItem("GDB_ReplicaLog",
				$"{systemTables}.{nameof(FileGdbContext.SystemTables.ReplicaLog)}",
				"The ReplicaLog system table (may not exist)")
		};

		return list;
	}

	private static ExplorerItem CreateSystemItem(string name, string dragText, string toolTip)
	{
		return new ExplorerItem(name, ExplorerItemKind.QueryableObject, ExplorerIcon.Table)
		{
			IsEnumerable = true,
			DragText = dragText,
			ToolTipText = toolTip
		};
	}

	private static List<ExplorerItem> GetTableItems(Core.FileGDB? gdb, Predicate<string> filter)
	{
		if (gdb is null)
		{
			return new List<ExplorerItem>(0);
		}

		return gdb.TableNames
			.Where(tableName => filter(tableName))
			.Select(tableName => CreateTableItem(gdb, tableName))
			.ToList();
	}

	private static ExplorerItem CreateTableItem(Core.FileGDB gdb, string tableName)
	{
		try
		{
			using var table = gdb.OpenTable(tableName);

			return new ExplorerItem(tableName, ExplorerItemKind.QueryableObject, ExplorerIcon.Table)
			{
				IsEnumerable = true,
				DragText = $"{nameof(FileGdbContext.Table)}[{Utils.FormatString(tableName)}]",
				ToolTipText = $"Table {tableName}, {table.RowCount} #rows, {table.MaxObjectID} max OID",
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
