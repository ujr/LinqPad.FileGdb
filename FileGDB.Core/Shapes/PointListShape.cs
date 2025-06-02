using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FileGDB.Core.Shapes;

public abstract class PointListShape : Shape
{
	private readonly IReadOnlyList<XY> _xys;
	private readonly IReadOnlyList<double>? _zs;
	private readonly IReadOnlyList<double>? _ms;
	private readonly IReadOnlyList<int>? _ids;
	private ReadOnlyPoints? _points;

	protected PointListShape(uint shapeType,
		IReadOnlyList<XY> xys, IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null, IReadOnlyList<int>? ids = null)
		: base(shapeType)
	{
		_xys = xys ?? throw new ArgumentNullException(nameof(xys));
		_zs = HasZ ? zs ?? throw new ArgumentNullException(nameof(zs)) : null;
		_ms = HasM ? ms ?? throw new ArgumentNullException(nameof(ms)) : null;
		_ids = HasID ? ids ?? throw new ArgumentNullException(nameof(ids)) : null;
	}

	protected PointListShape(uint shapeType, IReadOnlyList<PointShape>? points)
		: base(shapeType)
	{
		points ??= Array.Empty<PointShape>();
		_xys = points.Select(p => new XY(p.X, p.Y)).ToArray();
		_zs = HasZ ? points.Select(p => p.Z).ToArray() : null;
		_ms = HasM ? points.Select(p => p.M).ToArray() : null;
		_ids = HasID ? points.Select(p => p.ID).ToArray() : null;
	}

	public override bool IsEmpty => _xys.Count <= 0 || _xys.All(xy => double.IsNaN(xy.X) || double.IsNaN(xy.Y));

	public int NumPoints => _xys.Count;

	public IReadOnlyList<XY> CoordsXY => _xys;
	public IReadOnlyList<double>? CoordsZ => _zs;
	public IReadOnlyList<double>? CoordsM => _ms;
	public IReadOnlyList<int>? CoordsID => _ids;

	public IReadOnlyList<PointShape> Points => _points ??= new ReadOnlyPoints(this);

	protected override BoxShape GetBox()
	{
		return GetBox(_xys, _zs, _ms);
	}

	private PointShape GetPoint(int index)
	{
		if (index < 0 || index >= NumPoints)
			throw new ArgumentOutOfRangeException(nameof(index));

		var x = _xys[index].X;
		var y = _xys[index].Y;
		var z = _zs is null ? DefaultZ : _zs[index];
		var m = _ms is null ? DefaultM : _ms[index];
		var id = _ids is null ? DefaultID : _ids[index];

		return new PointShape(Flags, x, y, z, m, id);
	}

	private class ReadOnlyPoints : IReadOnlyList<PointShape>
	{
		private readonly PointListShape _parent;

		public ReadOnlyPoints(PointListShape parent)
		{
			_parent = parent ?? throw new ArgumentNullException(nameof(parent));
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<PointShape> GetEnumerator()
		{
			var count = _parent.NumPoints;

			for (int i = 0; i < count; i++)
			{
				yield return _parent.GetPoint(i);
			}
		}

		public int Count => _parent.NumPoints;

		public PointShape this[int index] => _parent.GetPoint(index);
	}
}
