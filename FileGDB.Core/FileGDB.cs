using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FileGDB.Core;

public sealed class FileGDB : IDisposable
{
	private readonly object _syncLock = new();
	private readonly IList<Table> _openTables;
	private IReadOnlyList<CatalogEntry>? _catalog;

	static FileGDB()
	{
		SystemTableDescriptions = GetSystemTableDescriptions();
	}

	private FileGDB(string gdbFolderPath)
	{
		FolderPath = gdbFolderPath ?? throw new ArgumentNullException(nameof(gdbFolderPath));

		_openTables = new List<Table>();
	}

	public string FolderPath { get; }

	public static FileGDB Open(string gdbFolderPath)
	{
		if (gdbFolderPath is null)
			throw new ArgumentNullException(nameof(gdbFolderPath));

		var gdb = new FileGDB(gdbFolderPath);
		gdb.LoadCatalog();
		return gdb;
	}

	public static IReadOnlyDictionary<string, string> SystemTableDescriptions { get; }

	public void Dispose()
	{
		Table[] copy;

		lock (_syncLock)
		{
			copy = _openTables.ToArray();
			_openTables.Clear();
		}

		foreach (var table in copy)
		{
			table.Dispose();
		}
	}

	public IReadOnlyList<CatalogEntry> Catalog => GetCatalog();

	public Table OpenTable(int tableID)
	{
		var baseName = GetTableBaseName(tableID);
		var table = Table.Open(baseName, FolderPath);

		lock (_syncLock)
		{
			_openTables.Add(table);
		}

		return table;
	}

	public Table OpenTable(string tableName)
	{
		var entry = GetCatalogEntry(tableName);
		if (entry.ID <= 0)
			throw Error($"No such table: {tableName}");
		return OpenTable(entry.ID);
	}

	#region Private methods

	private void LoadCatalog()
	{
		var list = new List<CatalogEntry>();

		var baseName = GetTableBaseName(1); // "a00000001"

		using (var table = Table.Open(baseName, FolderPath))
		{
			var limit = (int) Math.Min(int.MaxValue, table.MaxObjectID);
			for (int oid = 1; oid <= limit; oid++)
			{
				var row = table.ReadRow(oid);
				if (row is null) continue;
				var name = Convert.ToString(row[1]);
				if (name is null)
					throw Error("Catalog contains NULL name");
				var format = Convert.ToInt32(row[2] ?? 0);
				list.Add(new CatalogEntry(oid, name, format));
			}
		}

		SetCatalog(list);
	}

	private CatalogEntry GetCatalogEntry(string tableName)
	{
		var catalog = GetCatalog();

		var entry = catalog.FirstOrDefault(entry => entry.Name == tableName);

		if (entry.ID <= 0)
		{
			const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;
			entry = catalog.FirstOrDefault(e => string.Equals(e.Name, tableName, ignoreCase));
		}

		return entry;
	}

	private IReadOnlyList<CatalogEntry> GetCatalog()
	{
		lock (_syncLock)
		{
			return _catalog ?? Array.Empty<CatalogEntry>();
		}
	}

	private void SetCatalog(IList<CatalogEntry> list)
	{
		if (list is null)
			throw new ArgumentNullException(nameof(list));

		lock (_syncLock)
		{
			_catalog = new ReadOnlyCollection<CatalogEntry>(list);
		}
	}

	private static string GetTableBaseName(int tableID)
	{
		return string.Format("a{0:x8}", tableID);
	}

	private static Exception Error(string? message)
	{
		return new FileGDBException(message ?? "File GDB error");
	}

	private static IReadOnlyDictionary<string, string> GetSystemTableDescriptions()
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "GDB_SystemCatalog", "Catalog system table (list of all tables)" },
			{ "GDB_DBTune", "DBTune system table (config keyword parameters)" },
			{ "GDB_SpatialRefs", "Spatial references used by tables in this File GDB" },
			{ "GDB_Items", "The GDB_Items system table" },
			{ "GDB_ItemTypes", "The GDB_ItemTypes system table" },
			{ "GDB_ItemRelationships", "The GDB_ItemRelationships system table" },
			{ "GDB_ItemRelationshipTypes", "The GDB_ItemRelationshipTypes system table" },
			{ "GDB_ReplicaLog", "The ReplicaLog system table (may not exist)" },
			{ "GDB_EditingTemplates", "new with Pro 3.2" },
			{ "GDB_EditingTemplateRelationships", "new with Pro 3.2" },
			{ "GDB_ReplicaChanges", "Replica changes, only exists if this GDB is a replica" }
		};

		// TODO Version 9.2 File GDBs had many more system tables

		return new ReadOnlyDictionary<string, string>(result);
	}

	#endregion

	public readonly struct CatalogEntry
	{
		public int ID { get; }
		public string Name { get; }
		public int Format { get; }

		public CatalogEntry(int id, string name, int format = 0)
		{
			ID = id;
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Format = format;
		}

		//public bool Missing => ID <= 0 || Name == null;

		public override string ToString()
		{
			return $"ID={ID} Name={Name} Format={Format}";
		}
	}
}
