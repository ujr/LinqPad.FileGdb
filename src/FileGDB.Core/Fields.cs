using System;
using System.Collections;
using System.Collections.Generic;

namespace FileGDB.Core;

public class Fields : IReadOnlyList<FieldInfo>
{
	private readonly FieldInfo[] _fields;

	internal Fields(FieldInfo[] fields)
	{
		_fields = fields ?? throw new ArgumentNullException(nameof(fields));
	}

	public int Count => _fields.Length;

	public FieldInfo this[int index]
	{
		get
		{
			if (index < 0 || index >= _fields.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			return _fields[index];
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public IEnumerator<FieldInfo> GetEnumerator()
	{
		return ((IEnumerable<FieldInfo>)_fields).GetEnumerator();
	}
}
