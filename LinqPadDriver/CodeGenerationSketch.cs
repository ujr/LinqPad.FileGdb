// Sketch of code to generate for the data context:

using System;
using System.Collections;
using System.Collections.Generic;
using FileGDB.LinqPadDriver;

namespace NAMESPACE;

public class TYPENAME
{
	private readonly FileGDB.Core.FileGDB _gdb;

	public TYPENAME(string gdbFolderPath)
	{
		if (string.IsNullOrEmpty(gdbFolderPath))
			throw new ArgumentNullException(nameof(gdbFolderPath));

		_gdb = FileGDB.Core.FileGDB.Open(gdbFolderPath);

		GDB = new SystemTables(_gdb);
		Tables = new UserTables(_gdb);
	}

	public string FolderPath => _gdb.FolderPath;
	public IEnumerable<string> TableNames => _gdb.TableNames;
	public SystemTables GDB { get; }
	public UserTables Tables { get; }

	public class SystemTables : TableContainer
	{
		public SystemTables(FileGDB.Core.FileGDB gdb) : base(gdb) {}

		public @FooTable @Foo => GetTable<@FooTable>("Foo");
	}

	public class UserTables : TableContainer
	{
		public UserTables(FileGDB.Core.FileGDB gdb) : base(gdb) {}

		public @FooTable @Foo => GetTable<@FooTable>("Foo");
	}

	public abstract class TableContainer
	{
		private readonly FileGDB.Core.FileGDB _gdb;
		private readonly IDictionary<string, TableBase> _cache;

		protected TableContainer(FileGDB.Core.FileGDB gdb)
		{
			_gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
			_cache = new Dictionary<string, TableBase>();
		}

		protected T GetTable<T>(string tableName) where T : TableBase, new()
		{
			if (!_cache.TryGetValue(tableName, out var table))
			{
				var inner = _gdb.OpenTable(tableName);
				table = new T().SetTable(inner);
				_cache.Add(tableName, table);
			}
			return (T)table;
		}
	}

	public class TableError : IEnumerable<TableError.Row>
	{
		private readonly string _message;

		public TableError(string message)
		{
			_message = message ?? "Unknown error";
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<Row> GetEnumerator()
		{
			throw new Exception(_message);
		}

		public class Row { }
	}

	public abstract class TableBase
	{
		private FileGDB.Core.Table? _table;

		public TableBase SetTable(FileGDB.Core.Table table)
		{
			_table = table ?? throw new ArgumentNullException(nameof(table));
			return this;
		}

		public int FieldCount => Table.FieldCount;
		public int RowCount => Table.RowCount;
		public FileGDB.Core.GeometryType GeometryType => Table.GeometryType;
		public bool HasZ => Table.HasZ;
		public bool HasM => Table.HasM;
		public int Version => Table.Version;
		public bool UseUtf8 => Table.UseUtf8;
		public int MaxOID => Table.MaxObjectID;
		public IReadOnlyList<FileGDB.Core.FieldInfo> Fields => Table.Fields;

		protected FileGDB.Core.Table Table =>
			_table ?? throw new InvalidOperationException("This table wrapper has not been initialized");
	}

	public class FooTable : TableBase, IEnumerable<FooTable.Row>
	{
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<Row> GetEnumerator()
		{
			var cursor = Table.Search(null, null, null);

			while (cursor.Step())
			{
				var row = new Row();
				// Initialize each property like this:
				//row.@Bar = (@BarType)cursor.GetValue("Bar");
				yield return row;
			}
		}

		public class Row
		{
			// Property for each table field like this:
			//public BarType @Bar { get; set; }
		}
	}
}
