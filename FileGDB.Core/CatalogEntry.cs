using System;

namespace FileGDB.Core;

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

	public bool IsSystemTable()
	{
		if (Name is null) return false;
		// Tables whose names begin with "GDB_" are considered system tables:
		return Name.StartsWith("GDB_", StringComparison.OrdinalIgnoreCase);
	}

	public bool IsUserTable()
	{
		if (Name is null) return false;
		// Tables whose names do not start with "GDB_" are considered user tables:
		return !Name.StartsWith("GDB_", StringComparison.OrdinalIgnoreCase);
	}

	public override string ToString()
	{
		return $"ID={ID} Name={Name} Format={Format}";
	}
}
