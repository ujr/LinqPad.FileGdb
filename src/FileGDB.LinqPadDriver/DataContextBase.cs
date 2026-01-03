using FileGDB.Core;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using FieldInfo = FileGDB.Core.FieldInfo;

namespace FileGDB.LinqPadDriver;

[UsedImplicitly]
public abstract class RowBase
{
	// just tagging
}

public abstract class TableBase
{
	private readonly Core.FileGDB _gdb;
	private RowResult? _rowResult; // cache

	protected TableBase(Core.FileGDB gdb, string tableName, bool debugMode)
	{
		_gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
		TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
		DebugMode = debugMode;

		using var table = OpenTable();

		BaseName = table.BaseName;
		RowCount = table.RowCount;
		MaxObjectID = table.MaxObjectID;
		GeometryType = table.GeometryType;
		HasZ = table.HasZ;
		HasM = table.HasM;
		Fields = table.Fields;
		Indexes = table.Indexes;
		Version = table.Version;
	}

	protected bool DebugMode { get; }

	[PublicAPI]
	public string TableName { get; }

	[PublicAPI]
	public string BaseName { get; }

	[PublicAPI]
	public long RowCount { get; }

	[PublicAPI]
	public long MaxObjectID { get; }

	[PublicAPI]
	public GeometryType GeometryType { get; }

	[PublicAPI]
	public bool HasZ { get; }

	[PublicAPI]
	public bool HasM { get; }

	[PublicAPI]
	public int Version { get; }

	[PublicAPI]
	public IReadOnlyList<FieldInfo> Fields { get; }

	[PublicAPI]
	public IReadOnlyList<IndexInfo> Indexes { get; }

	protected RowResult? ReadRow(long oid)
	{
		if (DebugMode)
		{
			Debugger.Launch();
		}

		using var table = OpenTable();
		var values = table.ReadRow(oid);
		if (values is null) return null;

		_rowResult ??= new RowResult(table.Fields);
		_rowResult.SetValues(values);

		return _rowResult;
	}

	protected Table OpenTable()
	{
		return _gdb.OpenTable(TableName);
	}

	protected static T? Populate<T>(T row, IRowValues? cursor) where T : RowBase
	{
		if (cursor is null)
			return null;
		if (row is null)
			throw new ArgumentNullException(nameof(row));

		var type = row.GetType();

		const BindingFlags binding = BindingFlags.Public | BindingFlags.Instance;
		var properties = type.GetProperties(binding);

		foreach (var property in properties)
		{
			if (!property.CanRead || !property.CanWrite) continue;

			var attribute = property.GetCustomAttribute<DatabaseFieldAttribute>();
			if (attribute is null) continue;

			var value = cursor.GetValue(attribute.FieldName);

			property.SetValue(row, value);
		}

		return row;
	}
}

[UsedImplicitly]
public abstract class TableBase<T> : TableBase, IEnumerable<T> where T : RowBase
{
	protected TableBase(Core.FileGDB gdb, string tableName, bool debugMode)
		: base(gdb, tableName, debugMode) { }

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public IEnumerator<T> GetEnumerator()
	{
		if (DebugMode)
		{
			Debugger.Launch();
		}

		var table = OpenTable();

		try
		{
			var rows = table.ReadRows(null, null);

			while (rows.Step())
			{
				var row = CreateRow();
				yield return Populate(row, rows)!;
			}
		}
		finally
		{
			// Code here ends up in the generated enumerator's Dispose method
			// and will be called by foreach; dispose the table so we don't
			// leave any files open after enumeration:
			table.Dispose();
		}
	}

	[PublicAPI]
	public T? GetRow(long oid)
	{
		var row = CreateRow();
		var values = ReadRow(oid);
		return Populate(row, values);
	}

	protected abstract T CreateRow();
}

// Generate code like this for each table in database:
//public class FooTable : TableBase<FooTable.Row>
//{
//	public FooTable(Core.FileGDB gdb, string tableName, bool debugMode = false)
//		: base(gdb, tableName, debugMode) { }
//
//	protected override Row CreateRow()
//	{
//		return new Row();
//	}
//
//	public class Row : RowBase
//	{
//		// Property per field...
//      [DatabaseField("FOO")]
//		public int Foo { get; set; }
//	}
//}

public abstract class TableContainerBase
{
	private readonly Core.FileGDB _gdb;
	private readonly bool _debugMode;

	protected TableContainerBase(Core.FileGDB gdb, bool debugMode)
	{
		_gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
		_debugMode = debugMode;
	}

	[PublicAPI]
	protected T GetTable<T>(string tableName) where T : TableBase
	{
		var args = new object[] { _gdb, tableName, _debugMode };
		var wrapper = (T?)Activator.CreateInstance(typeof(T), args, null);
		return wrapper ?? throw new Exception("Got null from Activator");
	}
}

[PublicAPI]
public abstract class DataContextBase : IDisposable
{
	private readonly bool _debugMode;

	protected DataContextBase(string gdbFolderPath, bool debugMode)
	{
		if (string.IsNullOrEmpty(gdbFolderPath))
			throw new ArgumentNullException(nameof(gdbFolderPath));
		GDB = Core.FileGDB.Open(gdbFolderPath);
		_debugMode = debugMode;
	}

	public Core.FileGDB GDB { get; }

	public string FolderPath => GDB.FolderPath;

	public IEnumerable<CatalogWrapper> Catalog => WrapCatalog();

	public Table OpenTable(int id)
	{
		return GDB.OpenTable(id);
	}

	public Table OpenTable(string name)
	{
		return GDB.OpenTable(name);
	}

	public static bool LaunchDebugger()
	{
		return Debugger.Launch();
	}

	public void Dispose()
	{
		GDB.Dispose();
	}

	private IEnumerable<CatalogWrapper> WrapCatalog()
	{
		return GDB.Catalog.Select(e => new CatalogWrapper(e));
	}
}

[UsedImplicitly]
[AttributeUsage(AttributeTargets.Property)]
public class DatabaseFieldAttribute : Attribute
{
	public DatabaseFieldAttribute(string fieldName)
	{
		FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
	}

	public string FieldName { get; }
}

/// <summary>
/// A wrapper around a <see cref="CatalogEntry"/> that returns
/// a <see cref="TableWrapper"/> from <see cref="OpenTable"/>
/// (instead of a plain <see cref="Table"/>).
/// </summary>
public readonly struct CatalogWrapper
{
	private readonly CatalogEntry _entry;

	public CatalogWrapper(CatalogEntry entry)
	{
		_entry = entry ?? throw new ArgumentNullException(nameof(entry));
	}

	[PublicAPI] public int ID => _entry.ID;
	[PublicAPI] public string Name => _entry.Name;
	[PublicAPI] public int Format => _entry.Format;

	[PublicAPI]
	public bool IsUserTable() => _entry.IsUserTable();

	[PublicAPI]
	public bool IsSystemTable() => _entry.IsSystemTable();

	[PublicAPI]
	public bool TableExists() => _entry.TableExists();

	[PublicAPI]
	public TableWrapper OpenTable() => new(_entry.OpenTable(), Name);

	public override string ToString()
	{
		return $"{Name} (ID={ID})";
	}
}

/// <summary>
/// A wrapper around a File GDB <see cref="Table"/> that
/// is enumerable and exposes the table's name, both for
/// convenient usage in LINQPad.
/// </summary>
public class TableWrapper : IEnumerable<RowWrapper>
{
	private readonly Table _table;

	public TableWrapper(Table table, string tableName)
	{
		_table = table ?? throw new ArgumentNullException(nameof(table));
		Name = tableName;
	}

	[PublicAPI] public string Name { get; }

	[PublicAPI] public string BaseName => _table.BaseName;
	[PublicAPI] public int Version => _table.Version;
	[PublicAPI] public long RowCount => _table.RowCount;
	[PublicAPI] public GeometryType GeometryType => _table.GeometryType;
	[PublicAPI] public bool HasZ => _table.HasZ;
	[PublicAPI] public bool HasM => _table.HasM;
	[PublicAPI] public long MaxObjectID => _table.MaxObjectID;
	[PublicAPI] public IReadOnlyList<FieldInfo> Fields => _table.Fields;
	[PublicAPI] public IReadOnlyList<IndexInfo> Indexes => _table.Indexes;

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public IEnumerator<RowWrapper> GetEnumerator()
	{
		return new TableEnumerator(_table);
	}

	private class TableEnumerator : IEnumerator<RowWrapper>
	{
		private readonly Table _table;
		private readonly long _maxOid;
		private readonly int _shapeFieldIndex;
		private IDictionary<string, int>? _indices;
		private long _oid;

		public TableEnumerator(Table table)
		{
			_table = table ?? throw new ArgumentNullException(nameof(table));
			_maxOid = table.MaxObjectID;
			_shapeFieldIndex = table.GetShapeIndex();
			Current = null!;
			_oid = 0;
			_indices = null;
		}

		object IEnumerator.Current => Current;

		public RowWrapper Current { get; private set; }

		public bool MoveNext()
		{
			_oid += 1;

			while (_oid <= _maxOid)
			{
				var values = _table.ReadRow(_oid);
				if (values is not null)
				{
					_indices ??= CreateIndices();
					Current = CreateRow(values);
					return true;
				}
				// else: skip deleted oid
				_oid += 1;
			}

			Current = null!;
			return false; // exhausted
		}

		public void Reset()
		{
			_oid = 0;
		}

		public void Dispose()
		{
			// nothing to dispose (we don't own the table)
		}

		private RowWrapper CreateRow(object?[] values)
		{
			return new RowWrapper(_oid, values, _table.Fields, _shapeFieldIndex, lenient: true);
		}

		private IDictionary<string, int> CreateIndices()
		{
			var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

			for (int i = 0; i < _table.Fields.Count; i++)
			{
				dict.TryAdd(_table.Fields[i].Name, i);
			}

			return dict;
		}
	}
}

/// <summary>
/// The row objects returned when a <see cref="TableWrapper"/> is
/// enumerated. The shape field can be conveniently accessed through
/// the <see cref="Shape"/> property (null for non-spatial tables); all
/// other fields are read through the <see cref="GetValue(string)"/> method.
/// </summary>
public class RowWrapper : IRowValues
{
	private readonly object?[] _values;
	private readonly IDictionary<string, int>? _indices;
	private readonly bool _lenient;

	public RowWrapper(
		long oid, object?[] values, IReadOnlyList<FieldInfo>? fields,
		int shapeIndex, IDictionary<string, int>? indices = null, bool lenient = false)
	{
		OID = oid;
		_values = values ?? throw new ArgumentNullException(nameof(values));
		Fields = fields ?? throw new ArgumentNullException(nameof(fields));
		Shape = shapeIndex < 0 ? null : _values[shapeIndex] as GeometryBlob;
		_indices = indices; // can be null
		_lenient = lenient; // GetValue(name) returns null if no such field
	}

	[PublicAPI]
	public long OID { get; }

	[PublicAPI]
	public GeometryBlob? Shape { get; }

	public IReadOnlyList<FieldInfo> Fields { get; }

	public int FindField(string fieldName)
	{
		if (_indices is not null)
		{
			return _indices.TryGetValue(fieldName, out int index) ? index : -1;
		}

		for (int i = 0; i < Fields.Count; i++)
		{
			if (string.Equals(fieldName, Fields[i].Name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return -1; // not found
	}

	public object? GetValue(string fieldName)
	{
		if (fieldName is null)
			throw new ArgumentNullException(nameof(fieldName));

		int index = FindField(fieldName);

		if (index < 0)
		{
			if (_lenient) return null;
			throw new FileGDBException($"No such field: {fieldName}");
		}

		return _values[index];
	}

	public object? GetValue(int fieldIndex)
	{
		return _values[fieldIndex];
	}
}
