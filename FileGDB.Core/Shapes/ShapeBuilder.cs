using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FileGDB.Core.Geometry;

namespace FileGDB.Core.Shapes;

/// <summary>
/// Utility to create <see cref="ShapeBuffer"/> and
/// <see cref="Shape"/> objects. Start with a call to
/// <see cref="Initialize(uint)"/>, then add coordinates,
/// parts, curves as needed, then call <see cref="ToShape"/>
/// or/and <see cref="ToShapeBuffer"/>.
/// </summary>
public class ShapeBuilder
{
	private readonly List<XY> _xys = new();
	private readonly List<double> _zs = new();
	private readonly List<double> _ms = new();
	private readonly List<int> _ids = new();
	private readonly List<int> _partVertexCounts = new();
	private readonly List<SegmentModifier> _curves = new();

	private uint _shapeType;

	private double _xmin = double.NaN;
	private double _ymin = double.NaN;
	private double _xmax = double.NaN;
	private double _ymax = double.NaN;
	private double _zmin = double.NaN;
	private double _zmax = double.NaN;
	private double _mmin = double.NaN;
	private double _mmax = double.NaN;

	public GeometryType Type => ShapeBuffer.GetGeometryType(_shapeType);

	public bool HasZ => ShapeBuffer.GetHasZ(_shapeType);
	public bool HasM => ShapeBuffer.GetHasM(_shapeType);
	public bool HasID => ShapeBuffer.GetHasID(_shapeType);
	public bool MayHaveCurves => ShapeBuffer.GetMayHaveCurves(_shapeType);

	public int NumPoints => _xys.Count;
	public int NumParts => _partVertexCounts.Count;
	public int NumCurves => _curves.Count;

	public bool ValidateUnorderedSegmentModifiers { get; set; }

	private static double DefaultZ => ShapeBuffer.DefaultZ;
	private static double DefaultM => ShapeBuffer.DefaultM;
	private static int DefaultID => ShapeBuffer.DefaultID;

	public void Initialize(uint shapeType)
	{
		_shapeType = shapeType;

		_xys.Clear();
		_zs.Clear();
		_ms.Clear();
		_ids.Clear();

		_partVertexCounts.Clear();

		_curves.Clear();

		_xmin = _xmax = double.NaN;
		_ymin = _ymax = double.NaN;
		_zmin = _zmax = DefaultZ; // TODO or NaN?
		_mmin = _mmax = DefaultM;
	}

	public void Initialize(
		GeometryType type, bool hasZ, bool hasM, bool hasID, bool mayHaveCurves = false)
	{
		uint shapeType = ShapeBuffer.GetShapeType(type, hasZ, hasM, hasID, mayHaveCurves);

		Initialize(shapeType);
	}

	public void AddPart(int numPointsInPart)
	{
		//if (_parts.Count < 1)
		//	throw new Exception("Bug: expect _parts.Count > 0");
		//// Invariant: last part count is always NumPoints - (sum of _parts except last)
		//int index = _parts.Count - 1;
		//_parts[index] -= count;
		//_parts.Insert(index, count);

		_partVertexCounts.Add(numPointsInPart);
	}

	public void AddXY(double x, double y)
	{
		_xys.Add(new XY(x, y));
	}

	public void AddZ(double z)
	{
		_zs.Add(z);
	}

	public void AddM(double m)
	{
		_ms.Add(m);
	}

	public void AddID(int id)
	{
		_ids.Add(id);
	}

	public void AddCurve(SegmentModifier curve)
	{
		_curves.Add(curve);
	}

	public void SetMinMaxXY(double xmin, double ymin, double xmax, double ymax)
	{
		_xmin = xmin;
		_ymin = ymin;
		_xmax = xmax;
		_ymax = ymax;
	}

	public void SetMinMaxZ(double zmin, double zmax)
	{
		_zmin = zmin;
		_zmax = zmax;
	}

	public void SetMinMaxM(double mmin, double mmax)
	{
		_mmin = mmin;
		_mmax = mmax;
	}

	public bool Validate(out string message)
	{
		bool valid = true;
		message = string.Empty;

		if (Type == GeometryType.Null && NumPoints > 0)
		{
			AddMessage(ref message, $"Null shape cannot have points but there are {NumPoints}");
			valid = false;
		}

		if (Type == GeometryType.Point && NumPoints > 1)
		{
			AddMessage(ref message, $"Point shape can have at most one point but there are {NumPoints}");
			valid = false;
		}

		if (NumCurves > 0 && !MayHaveCurves)
		{
			AddMessage(ref message, $"{Type} shape cannot have curves but there are {NumCurves}");
			valid = false;
		}

		if (Type is GeometryType.Polyline or GeometryType.Polygon)
		{
			if (_partVertexCounts.Any(count => count < 0))
			{
				AddMessage(ref message, "part counts must not be negative");
				valid = false;
			}
			else
			{
				var partSum = _partVertexCounts.Sum();
				if (partSum != NumPoints)
				{
					AddMessage(ref message, $"sum over part counts ({partSum}) does not match actual point count ({NumPoints})");
					valid = false;
				}
			}
		}

		if (ValidateUnorderedSegmentModifiers)
		{
			// nothing to do: segment modifiers may be out of order
		}
		else
		{
			for (int j = 1; j < _curves.Count; j++)
			{
				int prev = _curves[j - 1].SegmentIndex;
				int current = _curves[j].SegmentIndex;
				if (current <= prev)
				{
					// This is nowhere documented, but highly desirable.
					// TODO Could sort instead... study real data: ever out of order?
					AddMessage(ref message, "segment modifiers (curves) are not in strictly increasing order of their segment index");
					valid = false;
					break;
				}
			}
		}

		message = string.Empty;
		return valid;
	}

	public void Validate()
	{
		if (!Validate(out var message))
		{
			throw new Exception(message);
		}
	}

	public Shape ToShape()
	{
		if (Type is GeometryType.Null)
		{
			return Shape.Null;
		}

		if (Type is GeometryType.Point)
		{
			var flags = GetFlags(HasZ, HasM, HasID);
			double x = _xys.Count > 0 ? _xys[0].X : double.NaN;
			double y = _xys.Count > 0 ? _xys[0].Y : double.NaN;
			double z = HasZ && _zs.Count > 0 ? _zs[0] : DefaultZ;
			double m = HasM && _ms.Count > 0 ? _ms[0] : DefaultM;
			int id = HasID && _ids.Count > 0 ? _ids[0] : DefaultID;
			return new PointShape(flags, x, y, z, m, id);
		}

		if (Type is GeometryType.Multipoint)
		{
			var flags = GetFlags(HasZ, HasM, HasID);

			var count = _xys.Count;
			var xys = _xys.ToArray();
			var zs = HasZ ? TrimOrExtendWith(_zs, count, DefaultZ).ToArray() : null;
			var ms = HasM ? TrimOrExtendWith(_ms, count, DefaultM).ToArray() : null;
			var ids = HasID ? TrimOrExtendWith(_ids, count, DefaultID).ToArray() : null;

			var box = new BoxShape(flags, _xmin, _ymin, _xmax, _ymax, _zmin, _zmax, _mmin, _mmax);
			return new MultipointShape(flags, xys, zs, ms, ids).SetBox(box);
		}

		if (Type is GeometryType.Polyline or GeometryType.Polygon)
		{
			var flags = GetFlags(HasZ, HasM, HasID);

			// NB. ToArray() so the shape gets its own coordinate collections (shape ctor does NOT copy lists)
			var count = _xys.Count;
			var xys = _xys.ToArray();
			var zs = HasZ ? TrimOrExtendWith(_zs, count, DefaultZ).ToArray() : null;
			var ms = HasM ? TrimOrExtendWith(_ms, count, DefaultM).ToArray() : null;
			var ids = HasID ? TrimOrExtendWith(_ids, count, DefaultID).ToArray() : null;
			var parts = _partVertexCounts.Count < 2 ? null : _partVertexCounts.ToArray();
			var curves = _curves.Count < 1 ? null : _curves.ToArray();

			Shape shape = Type is GeometryType.Polyline
				? new PolylineShape(flags, xys, zs, ms, ids, parts, curves)
				: new PolygonShape(flags, xys, zs, ms, ids, parts, curves);

			var box = new BoxShape(flags, _xmin, _ymin, _xmax, _ymax, _zmin, _zmax, _mmin, _mmax);

			shape.SetBox(box);
			return shape;
		}

		throw new NotSupportedException($"Shape of type {Type} is not supported");
	}

	public ShapeBuffer ToShapeBuffer()
	{
		if (Type is GeometryType.Null)
		{
			var bytes = new byte[4];
			ShapeBuffer.WriteInt32((int)ShapeType.Null, bytes, 0);
			return new ShapeBuffer(bytes);
		}

		if (Type is GeometryType.Point)
		{
			// ShapeBuffer layout:
			// - I32 ShapeType
			// - F64 X, Y
			// - if hasZ: F64 Z
			// - if hasM: F64 M
			// - if hasID: I32 ID
			// Empty: NaN for X and Y, zero(!) for Z if hasZ, NaN for M if hasM, zero for ID if hasID

			double x = _xys.Count > 0 ? _xys[0].X : double.NaN;
			double y = _xys.Count > 0 ? _xys[0].Y : double.NaN;
			double z = HasZ && _zs.Count > 0 ? _zs[0] : DefaultZ;
			double m = HasM && _ms.Count > 0 ? _ms[0] : DefaultM;
			int id = HasID && _ids.Count > 0 ? _ids[0] : DefaultID;

			var length = ShapeBuffer.GetPointBufferSize(HasZ, HasM, HasID);
			var bytes = new byte[length];
			int offset = 0;

			offset += ShapeBuffer.WriteInt32(unchecked((int)_shapeType), bytes, offset);

			offset += ShapeBuffer.WriteDouble(x, bytes, offset);
			offset += ShapeBuffer.WriteDouble(y, bytes, offset);

			if (HasZ)
			{
				offset += ShapeBuffer.WriteDouble(z, bytes, offset);
			}

			if (HasM)
			{
				offset += ShapeBuffer.WriteDouble(m, bytes, offset);
			}

			if (HasID)
			{
				offset += ShapeBuffer.WriteInt32(id, bytes, offset);
			}

			Debug.Assert(bytes.Length == offset);

			return new ShapeBuffer(bytes);
		}

		if (Type is GeometryType.Multipoint)
		{
			// ShapeBuffer layout:
			// - I32 type
			// - D64 xmin,ymin,xmax,ymax
			// - I32 numPoints
			// - D64[2*numPoints] xy coords
			// - if hasZ: D64 zmin,zmax; D64[numPoints] z coords
			// - if hasM: D64 mmin,mmax; D64[numPoints] m coords
			// - if hasID: I32[numPoints] id values
			// Empty: 4x NaN for box, 0 for numPoints (total 40 bytes)
			//   plus 2x NaN for Zmin,Zmax if hasZ, plus 2x NaN for Mmin,Mmax if hasM

			int numPoints = _xys.Count;

			var length = ShapeBuffer.GetMultipointBufferSize(HasZ, HasM, HasID, numPoints);
			var bytes = new byte[length];
			int offset = 0;

			offset += ShapeBuffer.WriteInt32(unchecked((int)_shapeType), bytes, offset);

			offset += ShapeBuffer.WriteDouble(_xmin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(_ymin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(_xmax, bytes, offset);
			offset += ShapeBuffer.WriteDouble(_ymax, bytes, offset);

			offset += ShapeBuffer.WriteInt32(numPoints, bytes, offset);

			offset += WriteXYs(_xys, numPoints, bytes, offset);

			if (HasZ)
			{
				offset += ShapeBuffer.WriteDouble(_zmin, bytes, offset);
				offset += ShapeBuffer.WriteDouble(_zmax, bytes, offset);
				offset += WriteZs(_zs, numPoints, bytes, offset);
			}

			if (HasM)
			{
				offset += ShapeBuffer.WriteDouble(_mmin, bytes, offset);
				offset += ShapeBuffer.WriteDouble(_mmax, bytes, offset);
				offset += WriteMs(_ms, numPoints, bytes, offset);
			}

			if (HasID)
			{
				offset += WriteIDs(_ids, numPoints, bytes, offset);
			}

			Debug.Assert(bytes.Length == offset);

			return new ShapeBuffer(bytes);
		}

		if (Type is GeometryType.Polyline or GeometryType.Polygon)
		{
			// ShapeBuffer layout:
			// - I32 type
			// - F64 xmin,ymin,xmax,ymax
			// - I32 numParts
			// - I32 numPoints
			// - I32[numParts] parts (index to first point in part)
			// - F64[2*numPoints] xy coords
			// - if hasZ: F64 zmin,zmax,z...
			// - if hasM: F64 mmin,mmax,m...
			// - if hasCurves: I32 numCurves, segment modifiers (...)
			// - if hasIDs: I32[numPoints]
			// Empty polyline/polygon: 4x NaN for box, 0 numParts, 0 numPoints (total 44 bytes)
			//   plus 2x NaN for Zmin,Zmax (if hasZ), plus 2x NaN for Mmin,Mmax (if hasM)

			int numParts = _partVertexCounts.Count;
			int numPoints = _xys.Count;
			bool mayHaveCurves = ShapeBuffer.GetMayHaveCurves(_shapeType);

			var length = ShapeBuffer.GetMultipartBufferSize(HasZ, HasM, HasID, numParts, numPoints);

			if (mayHaveCurves)
			{
				length += 4; // num segment modifiers
				length += _curves.Sum(c => c.GetShapeBufferSize());
			}

			var bytes = new byte[length];
			int offset = 0;

			offset += ShapeBuffer.WriteShapeType(_shapeType, bytes, offset);

			offset += ShapeBuffer.WriteDouble(_xmin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(_ymin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(_xmax, bytes, offset);
			offset += ShapeBuffer.WriteDouble(_ymax, bytes, offset);

			offset += ShapeBuffer.WriteInt32(numParts, bytes, offset);
			offset += ShapeBuffer.WriteInt32(numPoints, bytes, offset);

			offset += WritePartIndices(_partVertexCounts, bytes, offset);

			offset += WriteXYs(_xys, numPoints, bytes, offset);

			if (HasZ)
			{
				offset += ShapeBuffer.WriteDouble(_zmin, bytes, offset);
				offset += ShapeBuffer.WriteDouble(_zmax, bytes, offset);
				offset += WriteZs(_zs, numPoints, bytes, offset);
			}

			if (HasM)
			{
				offset += ShapeBuffer.WriteDouble(_mmin, bytes, offset);
				offset += ShapeBuffer.WriteDouble(_mmax, bytes, offset);
				offset += WriteMs(_ms, numPoints, bytes, offset);
			}

			if (mayHaveCurves)
			{
				offset += ShapeBuffer.WriteInt32(_curves.Count, bytes, offset);
				offset += WriteCurves(_curves, bytes, offset);
			}

			if (HasID)
			{
				offset += WriteIDs(_ids, numPoints, bytes, offset);
			}

			Debug.Assert(bytes.Length == offset);

			return new ShapeBuffer(bytes);
		}

		throw new NotSupportedException($"Shape of type {Type} is not supported");
	}

	#region Private utils

	private static int WritePartIndices(List<int>? parts, byte[] bytes, int offset)
	{
		int startOffset = offset;

		if (parts is null || parts.Count < 1)
		{
			// The one and only part: points start at index 0:
			offset += ShapeBuffer.WriteInt32(0, bytes, offset);
		}
		else
		{
			int firstPointIndex = 0;

			foreach (var numPartPoints in parts)
			{
				offset += ShapeBuffer.WriteInt32(firstPointIndex, bytes, offset);

				firstPointIndex += numPartPoints;
			}

			//  assert firstPointIndex == numPoints
		}

		return offset - startOffset;
	}

	private static int WriteCurves(List<SegmentModifier> curves, byte[] bytes, int offset)
	{
		int startOffset = offset;

		foreach (var curve in curves)
		{
			offset += curve.WriteShapeBuffer(bytes, offset);
		}

		return offset - startOffset;
	}

	private static int WriteXYs(IReadOnlyList<XY> xys, int numPoints, byte[] bytes, int offset)
	{
		int startOffset = offset;

		int limit = Math.Min(xys.Count, numPoints);

		for (int i = 0; i < limit; i++)
		{
			offset += ShapeBuffer.WriteDouble(xys[i].X, bytes, offset);
			offset += ShapeBuffer.WriteDouble(xys[i].Y, bytes, offset);
		}

		for (int i = limit; i < numPoints; i++)
		{
			offset += ShapeBuffer.WriteDouble(double.NaN, bytes, offset);
			offset += ShapeBuffer.WriteDouble(double.NaN, bytes, offset);
		}

		return offset - startOffset;
	}

	private static int WriteZs(IReadOnlyList<double> zs, int numPoints, byte[] bytes, int offset)
	{
		int startOffset = offset;

		int limit = Math.Min(zs.Count, numPoints);

		for (int i = 0; i < limit; i++)
		{
			offset += ShapeBuffer.WriteDouble(zs[i], bytes, offset);
		}

		for (int i = limit; i < numPoints; i++)
		{
			offset += ShapeBuffer.WriteDouble(DefaultZ, bytes, offset);
		}

		return offset - startOffset;
	}

	private static int WriteMs(IReadOnlyList<double> ms, int numPoints, byte[] bytes, int offset)
	{
		int startOffset = offset;

		int limit = Math.Min(ms.Count, numPoints);

		for (int i = 0; i < limit; i++)
		{
			offset += ShapeBuffer.WriteDouble(ms[i], bytes, offset);
		}

		for (int i = limit; i < numPoints; i++)
		{
			offset += ShapeBuffer.WriteDouble(DefaultM, bytes, offset);
		}

		return offset - startOffset;
	}

	private static int WriteIDs(IReadOnlyList<int> ids, int numPoints, byte[] bytes, int offset)
	{
		int startOffset = offset;

		int limit = Math.Min(ids.Count, numPoints);

		for (int i = 0; i < limit; i++)
		{
			offset += ShapeBuffer.WriteInt32(ids[i], bytes, offset);
		}

		for (int i = limit; i < numPoints; i++)
		{
			offset += ShapeBuffer.WriteInt32(DefaultID, bytes, offset);
		}

		return offset - startOffset;
	}

	private static void AddMessage(ref string accumulator, string message)
	{
		if (string.IsNullOrEmpty(message)) return;
		if (string.IsNullOrEmpty(accumulator))
		{
			accumulator = message;
		}
		else
		{
			accumulator = string.Concat(accumulator, Environment.NewLine, message);
		}
	}

	private static ShapeFlags GetFlags(bool hasZ, bool hasM, bool hasID)
	{
		var flags = ShapeFlags.None;
		if (hasZ) flags |= ShapeFlags.HasZ;
		if (hasM) flags |= ShapeFlags.HasM;
		if (hasID) flags |= ShapeFlags.HasID;
		return flags;
	}

	private static IEnumerable<T> TrimOrExtendWith<T>(IList<T> list, int count, T value)
	{
		if (list is null)
			throw new ArgumentNullException(nameof(list));
		if (list.Count < count)
		{
			return list.Concat(Enumerable.Repeat(value, count - list.Count));
		}
		if (list.Count > count)
		{
			return list.Take(count);
		}
		return list;
	}

	#endregion
}
