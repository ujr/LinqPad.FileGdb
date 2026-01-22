using System;
using System.Collections;
using System.Collections.Generic;
using FileGDB.Core;
using JetBrains.Annotations;

namespace FileGDB.LinqPadDriver.Extensions;

/// <summary>
/// Convenience extension methods for use in LINQPad queries
/// </summary>
public static class Extensions
{
	// Extension methods here are only available in LINQPad queries
	// if their namespace is known to the query (this driver emits
	// it, otherwise the user can hit F4 to add them to the query)

	[PublicAPI]
	public static EnumerableTable Enumerable(this CatalogEntry entry)
	{
		var table = entry.OpenTable();
		return table.Enumerable(entry.Name);
	}

	[PublicAPI]
	public static EnumerableTable Enumerable(this Table table, string? tableName = null)
	{
		return new EnumerableTable(table, tableName);
	}

	#region Nested: EnumerableTable

	/// <summary>
	/// A wrapper around a File GDB <see cref="Table"/> that
	/// is enumerable and exposes the table's name, both for
	/// convenient usage in LINQPad.
	/// </summary>
	public class EnumerableTable : IEnumerable<EnumeratedRow>
	{
		private readonly Table _table;

		public EnumerableTable(Table table, string? tableName = null)
		{
			_table = table ?? throw new ArgumentNullException(nameof(table));
			Name = tableName ?? table.BaseName;
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

		public IEnumerator<EnumeratedRow> GetEnumerator()
		{
			return new TableEnumerator(_table);
		}

		private class TableEnumerator : IEnumerator<EnumeratedRow>
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

			public EnumeratedRow Current { get; private set; }

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

			private EnumeratedRow CreateRow(object?[] values)
			{
				return new EnumeratedRow(_oid, values, _table.Fields, _shapeFieldIndex, lenient: true);
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

	#endregion

	#region Nested: EnumeratedRow

	/// <summary>
	/// The row objects returned when a <see cref="EnumerableTable"/> is
	/// enumerated. The shape field can be conveniently accessed through
	/// the <see cref="Shape"/> property (null for non-spatial tables); all
	/// other fields are read through the <see cref="GetValue(string)"/> method.
	/// </summary>
	public class EnumeratedRow : IRowValues
	{
		private readonly Fields _fields;
		private readonly object?[] _values;
		private readonly IDictionary<string, int>? _indices;
		private readonly bool _lenient;

		public EnumeratedRow(
			long oid, object?[] values, Fields fields,
			int shapeIndex, IDictionary<string, int>? indices = null, bool lenient = false)
		{
			OID = oid;
			_values = values ?? throw new ArgumentNullException(nameof(values));
			_fields = fields ?? throw new ArgumentNullException(nameof(fields));
			Shape = shapeIndex < 0 ? null : _values[shapeIndex] as GeometryBlob;
			_indices = indices; // can be null
			_lenient = lenient; // GetValue(name) returns null if no such field
		}

		[PublicAPI]
		public long OID { get; }

		[PublicAPI]
		public GeometryBlob? Shape { get; }

		public IReadOnlyList<FieldInfo> Fields => _fields;

		public int FindField(string fieldName)
		{
			if (_indices is not null)
			{
				return _indices.TryGetValue(fieldName, out int index) ? index : -1;
			}

			for (int i = 0; i < _fields.Count; i++)
			{
				if (string.Equals(fieldName, _fields[i].Name, StringComparison.OrdinalIgnoreCase))
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

	#endregion
}
