using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FileGDB.Core;
using JetBrains.Annotations;
using LINQPad;

namespace FileGDB.LinqPadDriver;

public class FileGdbContext : IDisposable
{
	// ctor(Core.FileGDB)
	// FolderPath: string
	//
	// Table[name] (enumerable)
	// Table[name].Fields (IEnum<FieldInfo>)
	// Table[name].GeometryType/.HasZ/.HasM
	// Table[name].RowCount/.MaxOID
	//
	// GDB.SystemCatalog (like Table[name])
	// GDB.DBTune
	// etc. (SpatialRefs, Items, ItemTypes, ...)

	private readonly Core.FileGDB _gdb;

	public FileGdbContext(string folderPath)
	{
		if (string.IsNullOrEmpty(folderPath))
			throw new ArgumentNullException(nameof(folderPath));

		_gdb = Core.FileGDB.Open(folderPath);

		GDB = new SystemTables(_gdb);
		Table = new Tables(_gdb);
	}

	public void Dispose()
	{
		((IDisposable)GDB).Dispose();
		((IDisposable)Table).Dispose();

		GC.SuppressFinalize(this);
	}

	public string FolderPath => _gdb.FolderPath;

	[PublicAPI]
	public IEnumerable<string> TableNames => _gdb.TableNames;

	[PublicAPI]
	public SystemTables GDB { get; }

	[PublicAPI]
	public Tables Table { get; }

	public class Tables : IDisposable
	{
		private readonly Core.FileGDB _gdb;
		private readonly IDictionary<string, TableProxy> _cache;

		public Tables(Core.FileGDB gdb)
		{
			_gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
			_cache = new Dictionary<string, TableProxy>();
		}

		[PublicAPI]
		public TableProxy this[string tableName] => GetTable(tableName);

		protected TableProxy GetTable(string tableName)
		{
			if (!_cache.TryGetValue(tableName, out var table))
			{
				var inner = _gdb.OpenTable(tableName);
				table = new TableProxy(inner);
				_cache.Add(tableName, table);
			}

			return table;
		}

		void IDisposable.Dispose()
		{
			foreach (var table in _cache.Values.OfType<IDisposable>())
			{
				table.Dispose();
			}

			_cache.Clear();

			GC.SuppressFinalize(this);
		}
	}

	public class SystemTables : Tables
	{
		public SystemTables(Core.FileGDB gdb) : base(gdb) { }

		[PublicAPI]
		public TableProxy SystemCatalog => GetTable("GDB_SystemCatalog");

		[PublicAPI]
		public TableProxy DBTune => GetTable("GDB_DBTune");

		[PublicAPI]
		public TableProxy SpatialRefs => GetTable("GDB_SpatialRefs");

		[PublicAPI]
		public TableProxy Items => GetTable("GDB_Items");

		[PublicAPI]
		public TableProxy ItemTypes => GetTable("GDB_ItemTypes");

		[PublicAPI]
		public TableProxy ItemRelationships => GetTable("GDB_ItemRelationships");

		[PublicAPI]
		public TableProxy ItemRelationshipTypes => GetTable("GDB_ItemRelationshipTypes");

		[PublicAPI]
		public TableProxy ReplicaLog => GetTable("GDB_ReplicaLog");
	}

	public class RowProxy : ICustomMemberProvider
	{
		private readonly IReadOnlyList<FieldInfo> _fields;
		private readonly object?[] _values;

		public RowProxy(IReadOnlyList<FieldInfo> fields, IEnumerable<object?> values)
		{
			if (values is null)
				throw new ArgumentNullException(nameof(values));

			_fields = fields ?? throw new ArgumentNullException(nameof(fields));
			_values = values.ToArray();

			if (_fields.Count != _values.Length)
			{
				throw new ArgumentException("fields and values are not of equal length");
			}
		}

		public IEnumerable<string> GetNames()
		{
			return _fields.Select(fi => fi.Name);
		}

		public IEnumerable<Type> GetTypes()
		{
			return _fields.Select(fi => Core.Table.GetDataType(fi.Type));
		}

		public IEnumerable<object?> GetValues()
		{
			return _values;
		}
	}

	public class TableProxy : IEnumerable<RowProxy>, IDisposable
	{
		private readonly Table _table;

		public TableProxy(Table table)
		{
			_table = table ?? throw new ArgumentNullException(nameof(table));
		}

		void IDisposable.Dispose()
		{
			_table.Dispose();

			GC.SuppressFinalize(this);
		}

		[PublicAPI] public int FieldCount => _table.FieldCount;
		[PublicAPI] public int RowCount => _table.RowCount;
		[PublicAPI] public GeometryType GeometryType => _table.GeometryType;
		[PublicAPI] public bool HasZ => _table.HasZ;
		[PublicAPI] public bool HasM => _table.HasM;
		[PublicAPI] public int Version => _table.Version;
		[PublicAPI] public bool UseUtf8 => _table.UseUtf8;
		[PublicAPI] public int MaxOID => _table.MaxObjectID;
		[PublicAPI] public IReadOnlyList<FieldInfo> Fields => _table.Fields;

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<RowProxy> GetEnumerator()
		{
			//System.Diagnostics.Debugger.Launch();

			var fields = _table.Fields;
			var values = new object?[fields.Count];

			var rows = _table.Search(null, null, null);
			while (rows.Step())
			{
				for (int i = 0; i < values.Length; i++)
				{
					values[i] = rows.GetValue(fields[i].Name);
				}

				yield return new RowProxy(_table.Fields, values);
			}
		}
	}
}
