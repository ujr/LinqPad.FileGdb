using FileGDB.Core;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
	private IReadOnlyList<FieldInfo>? _fields; // cache

	protected TableBase(Core.FileGDB gdb, string tableName, bool debugMode)
	{
		_gdb = gdb ?? throw new ArgumentNullException(nameof(gdb));
		TableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
		DebugMode = debugMode;
	}

	[PublicAPI]
	public string TableName { get; }

	protected bool DebugMode { get; }

	[PublicAPI]
	public IReadOnlyList<FieldInfo> Fields => GetFields();

	private IReadOnlyList<FieldInfo> GetFields()
	{
		if (_fields is null)
		{
			using var table = OpenTable();

			_fields = table.Fields;
		}

		return _fields;
	}

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

	public IEnumerable<CatalogEntry> Catalog => GDB.Catalog;

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
