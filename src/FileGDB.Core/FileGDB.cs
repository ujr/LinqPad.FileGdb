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
		if (entry is null || entry.ID <= 0)
			throw Error($"No such table: {tableName}");
		return OpenTable(entry.ID);
	}

	public static string GetTableBaseName(int tableID)
	{
		return string.Format("a{0:x8}", tableID);
	}

	#region Private methods

	private void LoadCatalog()
	{
		var list = new List<CatalogEntry>();

		const int catalogTableID = 1; // "a00000001"
		var baseName = GetTableBaseName(catalogTableID);

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
				list.Add(new CatalogEntry(this, oid, name, format));
			}
		}

		SetCatalog(list);
	}

	private CatalogEntry? GetCatalogEntry(string tableName)
	{
		var catalog = GetCatalog();

		var entry = catalog.FirstOrDefault(entry => entry.Name == tableName);

		if (entry is null || entry.ID <= 0)
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

	private static Exception Error(string? message)
	{
		return new FileGDBException(message ?? "File GDB error");
	}

	#endregion
}
