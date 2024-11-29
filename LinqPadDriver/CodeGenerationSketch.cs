// Sketch of code to generate for the data context:

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FileGDB.Core;
using FileGDB.LinqPadDriver;

namespace NAMESPACE;

public class TYPENAME
{
	private readonly FileGDB.Core.FileGDB _gdb;
	private readonly bool _debugMode;

	public TYPENAME(string gdbFolderPath, bool debugMode)
	{
		if (string.IsNullOrEmpty(gdbFolderPath))
			throw new ArgumentNullException(nameof(gdbFolderPath));

		_gdb = FileGDB.Core.FileGDB.Open(gdbFolderPath);
		_debugMode = debugMode;

		GDB = new SystemTables(_gdb, debugMode);
		Tables = new UserTables(_gdb, debugMode);
	}

	public string FolderPath => _gdb.FolderPath;
	public IEnumerable<FileGDB.Core.FileGDB.CatalogEntry> Catalog => _gdb.Catalog;
	public SystemTables GDB { get; }
	public UserTables Tables { get; }

	public FileGDB.Core.Table OpenTable(int id)
	{
		if (_debugMode)
		{
			Debugger.Launch();
		}

		return _gdb.OpenTable(id);
	}

	public FileGDB.Core.Table OpenTable(string name)
	{
		if (_debugMode)
		{
			Debugger.Launch();
		}

		return _gdb.OpenTable(name);
	}

	public class SystemTables : TableContainer
	{
		public SystemTables(FileGDB.Core.FileGDB gdb, bool debugMode) : base(gdb, debugMode) {}

		public @FooTable @Foo => GetTable<@FooTable>("Foo");
	}

	public class UserTables : TableContainer
	{
		public UserTables(FileGDB.Core.FileGDB gdb, bool debugMode) : base(gdb, debugMode) {}

		public @FooTable @Foo => GetTable<@FooTable>("Foo");
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
		public int MaxEntrySize => Table.MaxEntrySize;
		public int OffsetSize => Table.OffsetSize;
		public long DataFileSize => Table.FileSizeBytes;
		public string DataFileName => Table.GetDataFilePath();
		public string IndexFileName => Table.GetIndexFilePath();
		public IReadOnlyList<FileGDB.Core.FieldInfo> Fields => Table.Fields;
		public IReadOnlyList<FileGDB.Core.IndexInfo> Indexes => Table.Indexes;

		protected FileGDB.Core.Table Table =>
			_table ?? throw new InvalidOperationException("This table wrapper has not been initialized");
	}

	public class FooTable : TableBase, IEnumerable<FooTable.Row>
	{
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<Row> GetEnumerator()
		{
			if (DebugMode)
			{
				Debugger.Launch();
			}

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
