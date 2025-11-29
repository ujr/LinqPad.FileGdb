using System;

namespace FileGDB.Core;

public class CatalogEntry
{
	public int ID { get; }
	public string Name { get; }
	public int Format { get; }

	private readonly FileGDB _gdb;

	public CatalogEntry(FileGDB gdb, int id, string name, int format = 0)
	{
		ID = id;
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Format = format;
		_gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
	}

	//public bool Missing => ID <= 0 || Name == null;

	/// <remarks>
	/// Tables whose names begin with "GDB_" are considered system tables.
	/// </remarks>
	public bool IsSystemTable()
	{
		return Name.StartsWith("GDB_", StringComparison.OrdinalIgnoreCase);
	}

	/// <remarks>
	/// Tables whose names do not begin with "GDB_" are considered user tables.
	/// </remarks>
	public bool IsUserTable()
	{
		return !Name.StartsWith("GDB_", StringComparison.OrdinalIgnoreCase);
	}

	public Table OpenTable()
	{
		return _gdb.OpenTable(ID);
	}

	public bool TableExists()
	{
		return TableExists(out _);
	}

	public bool TableExists(out string reason)
	{
		var baseName = FileGDB.GetTableBaseName(ID);
		return Table.Exists(baseName, _gdb.FolderPath, out reason);
	}

	public override string ToString()
	{
		return $"ID={ID} Name={Name} Format={Format}";
	}
}
