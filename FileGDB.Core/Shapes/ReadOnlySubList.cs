using System;
using System.Collections;
using System.Collections.Generic;

namespace FileGDB.Core.Shapes;

public class ReadOnlySubList<T> : IReadOnlyList<T>
{
	private readonly IReadOnlyList<T> _parent;
	private readonly int _start;
	private readonly int _count;

	public ReadOnlySubList(IReadOnlyList<T> parent, int start, int count)
	{
		_parent = parent ?? throw new ArgumentNullException(nameof(parent));

		if (start < 0 || start > _parent.Count)
			throw new ArgumentOutOfRangeException(nameof(start), start, null);
		if (count < 0 || start + count > _parent.Count)
			throw new ArgumentOutOfRangeException(nameof(count), count, null);

		_start = start;
		_count = count;
	}

	public int Count => _count;

	public T this[int index] => _parent[_start + index];

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (int i = 0; i < _count; i++)
		{
			yield return _parent[_start + i];
		}
	}
}
