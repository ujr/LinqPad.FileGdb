using System;
using System.Collections.Generic;

namespace FileGDB.Core;


public interface IRowValues
{
	IReadOnlyList<FieldInfo> Fields { get; }
	int FindField(string fieldName);
	object? GetValue(string fieldName);
	object? GetValue(int fieldIndex);
}

public class RowResult : IRowValues
{
	private object?[]? _values;
	private readonly IDictionary<string, int> _fieldIndices;

	public RowResult(IReadOnlyList<FieldInfo> fields, object?[]? values = null)
	{
		Fields = fields ?? throw new ArgumentNullException(nameof(fields));

		if (values is not null && values.Length != fields.Count)
			throw new ArgumentException($"Expect {Fields.Count} values, got {values.Length}", nameof(values));
		_values = values;

		_fieldIndices = new Dictionary<string, int>();
	}

	public void SetValues(object?[] values)
	{
		if (values is null)
			throw new ArgumentNullException(nameof(values));
		if (values.Length != Fields.Count)
			throw new ArgumentException($"Expect {Fields.Count} values, got {values.Length}", nameof(values));

		_values = values;
	}

	public IReadOnlyList<FieldInfo> Fields { get; }

	public int FindField(string fieldName)
	{
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

		if (!_fieldIndices.TryGetValue(fieldName, out int index))
		{
			index = FindField(fieldName);

			if (index < 0)
			{
				throw new FileGDBException($"No such field: {fieldName}");
			}

			_fieldIndices.Add(fieldName, index);
		}

		return _values?[index];
	}

	public object? GetValue(int fieldIndex)
	{
		return _values?[fieldIndex];
	}
}

public abstract class RowsResult : IRowValues
{
	// concrete subclasses: e.g. FullScanResult, TableSubsetResult, ...?

	private readonly IDictionary<string, int> _fieldIndices;

	protected RowsResult(bool hasShape, IReadOnlyList<FieldInfo> fields)
	{
		HasShape = hasShape;
		Fields = fields ?? throw new ArgumentNullException(nameof(fields));

		_fieldIndices = new Dictionary<string, int>();
	}

	public IReadOnlyList<FieldInfo> Fields { get; }

	public int FindField(string fieldName)
	{
		for (int i = 0; i < Fields.Count; i++)
		{
			if (string.Equals(fieldName, Fields[i].Name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return -1; // not found
	}

	public bool HasShape { get; }

	/// <summary>Advance to next row (including the first row)</summary>
	public abstract bool Step();

	#region IEnumerable & IEnumerator

	//IEnumerator IEnumerable.GetEnumerator()
	//{
	//	return GetEnumerator();
	//}

	//public IEnumerator<int> GetEnumerator()
	//{
	//	return this;
	//}

	//bool IEnumerator.MoveNext()
	//{
	//	return Step();
	//}

	//void IEnumerator.Reset()
	//{
	//	throw new NotImplementedException();
	//}

	//object IEnumerator.Current => OID;

	//void IDisposable.Dispose()
	//{
	//	// nothing to dispose
	//}

	//int IEnumerator<int>.Current => OID;

	#endregion

	public abstract long OID { get; }

	public abstract GeometryBlob? Shape { get; } // null if no shape

	public virtual object? GetValue(string fieldName)
	{
		if (fieldName is null)
			throw new ArgumentNullException(nameof(fieldName));

		if (!_fieldIndices.TryGetValue(fieldName, out int index))
		{
			index = FindField(fieldName);
			if (index < 0) throw new FileGDBException($"No such field: {fieldName}");
			_fieldIndices.Add(fieldName, index);
		}

		return GetValue(index);
	}

	public abstract object? GetValue(int fieldIndex);
}

public class TableScanResult : RowsResult
{
	private long _oid;
	private readonly object?[] _values;
	private readonly long _maxOid;
	private readonly Table _table;
	private readonly int _shapeFieldIndex;

	public TableScanResult(Table table)
		: base(HasGeometry(table), GetFields(table))
	{
		_table = table ?? throw new ArgumentNullException(nameof(table));
		_oid = 0;
		_maxOid = table.MaxObjectID;
		_values = new object[Fields.Count];
		_shapeFieldIndex = table.GetShapeIndex();
	}

	public override bool Step()
	{
		_oid += 1;

		while (_oid <= _maxOid)
		{
			var row = _table.ReadRow(_oid, _values);
			if (row is not null) return true;
			_oid += 1;
		}

		return false; // exhausted
	}

	public override long OID => _oid;

	public override GeometryBlob? Shape => GetShape();

	public override object? GetValue(int fieldIndex)
	{
		return _values[fieldIndex];
	}

	private GeometryBlob? GetShape()
	{
		if (_shapeFieldIndex < 0)
		{
			return null; // table has no shape field
		}

		return _values[_shapeFieldIndex] as GeometryBlob;
	}

	private static bool HasGeometry(Table? table)
	{
		if (table is null) return false;
		return table.GeometryType != GeometryType.Null;
	}

	private static IReadOnlyList<FieldInfo> GetFields(Table? table)
	{
		return table?.Fields ?? throw new ArgumentNullException(nameof(table));
	}
}
