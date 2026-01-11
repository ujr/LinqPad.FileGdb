using System;
using System.Collections;
using System.Collections.Generic;

namespace FileGDB.Core;

public class Indexes : IReadOnlyList<IndexInfo>
{
	private readonly IndexInfo[] _indexes;

	internal Indexes(IndexInfo[] indexes)
	{
		_indexes = indexes ?? throw new ArgumentNullException(nameof(indexes));
	}


	public int Count => _indexes.Length;

	public IndexInfo this[int index]
	{
		get
		{
			if (index < 0 || index >= _indexes.Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			return _indexes[index];
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public IEnumerator<IndexInfo> GetEnumerator()
	{
		return ((IEnumerable<IndexInfo>)_indexes).GetEnumerator();
	}
}
