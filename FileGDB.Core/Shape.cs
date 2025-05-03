using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace FileGDB.Core;

[Flags]
public enum ShapeFlags
{
	None = 0,
	HasZ = 1,
	HasM = 2,
	HasID = 4
}

public abstract class Shape
{
	private readonly uint _shapeType;
	private BoxShape? _box;

	public GeometryType GeometryType => ShapeBuffer.GetGeometryType(_shapeType);
	public ShapeFlags Flags => GetShapeFlags(_shapeType);
	public bool HasZ => ShapeBuffer.GetHasZ(_shapeType);
	public bool HasM => ShapeBuffer.GetHasM(_shapeType);
	public bool HasID => ShapeBuffer.GetHasID(_shapeType);

	protected const double DefaultZ = ShapeBuffer.DefaultZ;
	protected const double DefaultM = ShapeBuffer.DefaultM;
	protected const int DefaultID = ShapeBuffer.DefaultID;

	protected Shape(uint shapeType)
	{
		_shapeType = shapeType;
	}

	public uint GetShapeType() => _shapeType;

	public BoxShape Box => _box ??= GetBox();

	public abstract bool IsEmpty { get; }

	public string ToWKT(int decimalDigits = -1)
	{
		var buffer = new StringBuilder();
		ToWKT(buffer, decimalDigits);
		return buffer.ToString();
	}

	public void ToWKT(StringBuilder buffer, int decimalDigits = -1)
	{
		var writer = new StringWriter(buffer);
		ToWKT(writer, decimalDigits);
		writer.Flush();
	}

	public void ToWKT(TextWriter writer, int decimalDigits = -1)
	{
		var wkt = new WKTWriter(writer) { DecimalDigits = decimalDigits };
		ToWKT(wkt);
		wkt.Flush();
	}

	protected abstract void ToWKT(WKTWriter wkt);

	public byte[] ToShapeBuffer()
	{
		int length = ToShapeBuffer(null);
		var bytes = new byte[length];
		ToShapeBuffer(bytes);
		return bytes;
	}

	public abstract int ToShapeBuffer(byte[]? bytes, int offset = 0);

	public static Shape FromShapeBuffer(byte[] bytes)
	{
		if (bytes is null)
			throw new ArgumentNullException(nameof(bytes));

		throw new NotImplementedException();
	}

	public Shape SetBox(BoxShape box)
	{
		_box = box; // null is ok (box will be lazily computed)
		return this;
	}

	public static NullShape Null => NullShape.Instance;

	protected abstract BoxShape GetBox();

	protected static uint GetShapeType(GeometryType type, ShapeFlags flags)
	{
		uint result;

		switch (type)
		{
			case GeometryType.Null:
				result = (uint)ShapeType.Null;
				break;
			case GeometryType.Point:
				result = (uint)ShapeType.GeneralPoint;
				break;
			case GeometryType.Multipoint:
				result = (uint)ShapeType.GeneralMultipoint;
				break;
			case GeometryType.Polyline:
				result = (uint)ShapeType.GeneralPolyline;
				break;
			case GeometryType.Polygon:
				result = (uint)ShapeType.GeneralPolygon;
				break;
			case GeometryType.Envelope:
				result = (uint)ShapeType.Box;
				break;
			case GeometryType.MultiPatch:
				result = (uint)ShapeType.GeneralMultiPatch;
				break;
			case GeometryType.Any:
			case GeometryType.Bag:
				throw new ArgumentOutOfRangeException(nameof(type), type, "Any and Bag are not supported");
			default:
				throw new ArgumentOutOfRangeException(nameof(type), type, null);
		}

		if ((flags & ShapeFlags.HasZ) != 0)
			result |= (uint)ShapeBuffer.Flags.HasZ;
		if ((flags & ShapeFlags.HasM) != 0)
			result |= (uint)ShapeBuffer.Flags.HasM;
		if ((flags & ShapeFlags.HasID) != 0)
			result |= (uint)ShapeBuffer.Flags.HasID;
		// MultiPatch flags: Materials, Normals, Textures, PartIDs

		if (type == GeometryType.Polyline || type == GeometryType.Polygon)
		{
			result |= (uint)ShapeBuffer.Flags.HasCurves; // TODO unsure
		}

		return result;
	}

	protected BoxShape GetBox(IEnumerable<XY>? xys, IEnumerable<double>? zs, IEnumerable<double>? ms)
	{
		var xmin = double.NaN;
		var xmax = double.NaN;
		var ymin = double.NaN;
		var ymax = double.NaN;

		if (xys is not null)
		{
			foreach (var xy in xys)
			{
				if (double.IsNaN(xmin) || xy.X < xmin) xmin = xy.X;
				if (double.IsNaN(xmax) || xy.X > xmax) xmax = xy.X;

				if (double.IsNaN(ymin) || xy.Y < ymin) ymin = xy.Y;
				if (double.IsNaN(ymax) || xy.Y > ymax) ymax = xy.Y;
			}
		}

		var zmin = double.NaN;
		var zmax = double.NaN;

		if (HasZ && zs is not null)
		{
			foreach (var z in zs)
			{
				if (double.IsNaN(zmin) || z < zmin) zmin = z;
				if (double.IsNaN(zmax) || z > zmax) zmax = z;
			}
		}

		var mmin = double.NaN;
		var mmax = double.NaN;

		if (HasM && ms is not null)
		{
			foreach (var m in ms)
			{
				if (double.IsNaN(mmin) || m < mmin) mmin = m;
				if (double.IsNaN(mmax) || m > mmax) mmax = m;
			}
		}

		return new BoxShape(Flags, xmin, ymin, xmax, ymax, zmin, zmax, mmin, mmax);
	}

	private static ShapeFlags GetShapeFlags(uint shapeType)
	{
		var flags = ShapeFlags.None;
		if (ShapeBuffer.GetHasZ(shapeType)) flags |= ShapeFlags.HasZ;
		if (ShapeBuffer.GetHasM(shapeType)) flags |= ShapeFlags.HasM;
		if (ShapeBuffer.GetHasID(shapeType)) flags |= ShapeFlags.HasID;
		return flags;
	}

	protected static Exception Bug(string message)
	{
		return new InvalidOperationException($"Bug: {message}");
	}
}

public class BoxShape : Shape
{
	public double XMin { get; }
	public double YMin { get; }
	public double XMax { get; }
	public double YMax { get; }
	// Z and M bounds are NaN if not available
	public double ZMin { get; }
	public double ZMax { get; }
	public double MMin { get; }
	public double MMax { get; }
	// Use extension methods for convenience stuff like Center, LowerLeft, etc.

	public BoxShape(ShapeFlags flags,
		double xmin, double ymin, double xmax, double ymax,
		double zmin = double.NaN, double zmax = double.NaN,
		double mmin = double.NaN, double mmax = double.NaN)
		: base(GetShapeType(GeometryType.Envelope, flags))
	{
		XMin = xmin;
		YMin = ymin;
		XMax = xmax;
		YMax = ymax;

		ZMin = zmin;
		ZMax = zmax;

		MMin = mmin;
		MMax = mmax;
	}

	protected override BoxShape GetBox()
	{
		return this;
	}

	public override bool IsEmpty => double.IsNaN(XMin) || double.IsNaN(XMax) ||
									double.IsNaN(YMin) || double.IsNaN(YMax) ||
									XMin > XMax || YMin > YMax;

	public double Width => Math.Abs(XMax - XMin);
	public double Height => Math.Abs(YMax - YMin);

	protected override void ToWKT(WKTWriter wkt)
	{
		wkt.WriteBox(XMin, YMin, XMax, YMax, ZMin, ZMax, MMin, MMax);
	}

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		// We could emit a 5-point Polygon instead, but then we loose
		// the information that this is indeed a bounding box and, worse,
		// we would have to invent Z,M,ID from the envelope's min/max values.
		throw new NotSupportedException("There is no shape buffer defined for a bounding box (aka envelope)");
	}
}

public class NullShape : Shape
{
	private NullShape() : base((uint)ShapeType.Null) { }

	public override bool IsEmpty => true;

	protected override void ToWKT(WKTWriter wkt)
	{
		throw new NotSupportedException("Cannot write Null shape as WKT");
	}

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		const int length = 4;
		if (bytes is null || bytes.Length - offset < length) return length;
		return ShapeBuffer.WriteShapeType((uint)ShapeType.Null, bytes, offset);
	}

	protected override BoxShape GetBox()
	{
		return NullBox;
	}

	public static readonly NullShape Instance = new();
	private static readonly BoxShape NullBox = new(ShapeFlags.None, double.NaN, double.NaN, double.NaN, double.NaN);
}

public class PointShape : Shape
{
	public double X { get; }
	public double Y { get; }
	public double Z { get; }
	public double M { get; }
	public int ID { get; }

	public static PointShape Empty { get; } = new(double.NaN, double.NaN);

	public PointShape(ShapeFlags flags, double x, double y, double z, double m, int id)
		: base(GetShapeType(GeometryType.Point, flags))
	{
		X = x;
		Y = y;
		Z = z;
		M = m;
		ID = id;
	}

	public PointShape(double x, double y, double z = DefaultZ, double m = DefaultM, int id = -1)
		: this(GuessFlags(z, m, id), x, y, z, m, id) { }

	private static ShapeFlags GuessFlags(double z, double m, int id)
	{
		var flags = ShapeFlags.None;
		if (!double.IsNaN(z)) flags |= ShapeFlags.HasZ;
		if (!double.IsNaN(m)) flags |= ShapeFlags.HasM;
		if (id >= 0) flags |= ShapeFlags.HasID;
		return flags;
	}

	protected override BoxShape GetBox()
	{
		return new BoxShape(Flags, X, Y, X, Y, Z, Z, M, M);
	}

	public override bool IsEmpty => double.IsNaN(X) || double.IsNaN(Y);

	protected override void ToWKT(WKTWriter wkt)
	{
		wkt.BeginPoint(HasZ, HasM, HasID);
		wkt.AddVertex(X, Y, Z, M, ID);
		wkt.EndShape();
	}

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));

		int length = ShapeBuffer.GetPointBufferSize(HasZ, HasM, HasID);
		if (bytes is null || bytes.Length - offset < length)
			return length;

		var shapeType = GetShapeType();
		offset += ShapeBuffer.WriteShapeType(shapeType, bytes, offset);

		if (IsEmpty)
		{
			offset += ShapeBuffer.WriteDouble(double.NaN, bytes, offset);
			offset += ShapeBuffer.WriteDouble(double.NaN, bytes, offset);
			if (HasZ) offset += ShapeBuffer.WriteDouble(DefaultZ, bytes, offset);
			if (HasM) offset += ShapeBuffer.WriteDouble(DefaultM, bytes, offset);
			if (HasID) offset += ShapeBuffer.WriteInt32(DefaultID, bytes, offset);
		}
		else
		{
			offset += ShapeBuffer.WriteDouble(X, bytes, offset);
			offset += ShapeBuffer.WriteDouble(Y, bytes, offset);
			if (HasZ) offset += ShapeBuffer.WriteDouble(Z, bytes, offset);
			if (HasM) offset += ShapeBuffer.WriteDouble(M, bytes, offset);
			if (HasID) offset += ShapeBuffer.WriteInt32(ID, bytes, offset);
		}

		Debug.Assert(bytes.Length == offset);

		return length;
	}
}

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

public class MultipointShape : PointListShape
{
	public MultipointShape(ShapeFlags flags,
		IReadOnlyList<XY> xys, IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null, IReadOnlyList<int>? ids = null)
		: base(GetShapeType(GeometryType.Multipoint, flags), xys, zs, ms, ids)
	{ }

	public MultipointShape(ShapeFlags flags, IReadOnlyList<PointShape> points)
		: base(GetShapeType(GeometryType.Multipoint, flags), points)
	{ }

	protected override void ToWKT(WKTWriter wkt)
	{
		wkt.BeginMultipoint(HasZ, HasM, HasID);
		for (int i = 0; i < NumPoints; i++)
		{
			var xy = CoordsXY[i];
			var z = CoordsZ is null ? DefaultZ : CoordsZ[i];
			var m = CoordsM is null ? DefaultM : CoordsM[i];
			var id = CoordsID is null ? DefaultID : CoordsID[i];
			wkt.AddVertex(xy.X, xy.Y, z, m, id);
		}
		wkt.EndShape();
	}

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));

		int length = ShapeBuffer.GetMultipointBufferSize(HasZ, HasM, HasID, NumPoints);
		if (bytes is null || bytes.Length - offset < length)
			return length;

		// Empty Multipoint is handled implicitly: zero for numPoints,
		// NaN for all min/max values (including Z and M if hasZ/M)

		var shapeType = GetShapeType();
		offset += ShapeBuffer.WriteShapeType(shapeType, bytes, offset);

		var box = Box;
		offset += ShapeBuffer.WriteDouble(box.XMin, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.YMin, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.XMax, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.YMax, bytes, offset);

		var numPoints = NumPoints;
		offset += ShapeBuffer.WriteInt32(numPoints, bytes, offset);

		var xys = CoordsXY;
		for (int i = 0; i < numPoints; i++)
		{
			offset += ShapeBuffer.WriteDouble(xys[i].X, bytes, offset);
			offset += ShapeBuffer.WriteDouble(xys[i].Y, bytes, offset);
		}

		if (HasZ)
		{
			offset += ShapeBuffer.WriteDouble(box.ZMin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(box.ZMax, bytes, offset);

			var zs = CoordsZ ?? throw Bug("HasZ is true but CoordsZ is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteDouble(zs[i], bytes, offset);
			}
		}

		if (HasM)
		{
			offset += ShapeBuffer.WriteDouble(box.MMin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(box.MMax, bytes, offset);

			var ms = CoordsM ?? throw Bug("HasM is true but CoordsM is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteDouble(ms[i], bytes, offset);
			}
		}

		if (HasID)
		{
			var ids = CoordsID ?? throw Bug("HasID is true but CoordsID is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteInt32(ids[i], bytes, offset);
			}
		}

		Debug.Assert(bytes.Length == offset);

		return length;
	}
}

public abstract class MultipartShape : PointListShape
{
	private readonly IReadOnlyList<int>? _partStarts;
	private readonly IReadOnlyList<SegmentModifier>? _curves;

	protected MultipartShape(uint shapeType,
		IReadOnlyList<XY> xys, IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null, IReadOnlyList<int>? ids = null,
		IReadOnlyList<int>? partVertexCounts = null,
		IReadOnlyList<SegmentModifier>? curves = null)
		: base(shapeType, xys, zs, ms, ids)
	{
		_partStarts = GetPartStarts(partVertexCounts, NumPoints);
		_curves = curves; // can be null
		// TODO verify curves are ordered by increasing segmentIndex!
	}

	public IReadOnlyList<SegmentModifier> Curves => _curves ?? Array.Empty<SegmentModifier>();

	private static int[]? GetPartStarts(IReadOnlyList<int>? partVertexCounts, int totalVertexCount)
	{
		if (partVertexCounts is null)
		{
			return null;
		}

		int startIndex = 0;
		int numParts = partVertexCounts.Count;

		var starts = new int[numParts];

		for (int i = 0; i < numParts; i++)
		{
			starts[i] = startIndex;
			startIndex += partVertexCounts[i];
		}

		if (totalVertexCount >= 0 && startIndex != totalVertexCount)
		{
			throw new ArgumentException("Given part vertex counts don't sum up to NumPoints");
		}

		return starts;
	}

	protected internal List<int> GetPartCounts(List<int>? result = null)
	{
		result ??= new List<int>(_partStarts?.Count ?? 1);
		return GetPartCounts(result, _partStarts, NumPoints);
	}

	private static List<int> GetPartCounts(List<int> result, IReadOnlyList<int>? partStarts, int numPoints)
	{
		result.Clear();

		if (partStarts is null || partStarts.Count < 1)
		{
			result.Add(numPoints);
			return result;
		}

		//var numParts = partStarts.Count;
		//var result = new int[numParts];

		int i = partStarts[0]; // always 0
		int n = partStarts.Count;

		for (int j = 1; j < n; j++)
		{
			int k = partStarts[j];
			//result[j - 1] = k - i;
			result.Add(k - i);
			i = k;
		}

		//result[n - 1] = numPoints - i;
		result.Add(numPoints - i);

		return result;
	}

	public int NumParts => _partStarts?.Count ?? 1;

	public int NumCurves => _curves?.Count ?? 0;

	/// <returns>Index into coordinate lists where part <paramref name="partIndex"/> starts</returns>
	/// <remarks>Returns 0 or NumPoints if <paramref name="partIndex"/> is out of range </remarks>
	public int GetPartStart(int partIndex)
	{
		if (partIndex <= 0) return 0;
		if (_partStarts is null) return NumPoints;
		if (partIndex >= _partStarts.Count) return NumPoints;
		return _partStarts[partIndex];
	}

	/// <summary>
	/// Get start index and point count for part <paramref name="partIndex"/>.
	/// Throw an exception if <paramref name="partIndex"/> is out of range.
	/// </summary>
	protected void GetPartStartAndCount(int partIndex, out int start, out int count)
	{
		if (partIndex < 0 || partIndex >= NumParts)
			throw new ArgumentOutOfRangeException(nameof(partIndex), partIndex, null);

		start = _partStarts?[partIndex] ?? 0;

		int nextStart = partIndex < NumParts - 1
			? _partStarts![partIndex + 1]
			: NumPoints;

		count = nextStart - start;
	}

	protected IReadOnlyList<SegmentModifier>? GetPartCurves(int partIndex)
	{
		if (_curves is null) return null;
		int start = GetPartStart(partIndex);
		int limit = GetPartStart(partIndex + 1);
		return _curves
			.Where(c => start <= c.SegmentIndex && c.SegmentIndex < limit)
			.ToList();
	}

	protected void WriteCoordinates(WKTWriter writer)
	{
		for (int i = 0, j = 0; i < NumPoints; i++)
		{
			int k = GetPartStart(j);
			if (i == k) // first vertex of new part
						//if (j < _partStarts.Count && i == _partStarts[j])
			{
				writer.NewPart();
				j += 1;
			}

			var xy = CoordsXY[i];
			var z = CoordsZ is null ? DefaultZ : CoordsZ[i];
			var m = CoordsM is null ? DefaultM : CoordsM[i];
			var id = CoordsID?[i] ?? DefaultID; // null unless HasID

			writer.AddVertex(xy.X, xy.Y, z, m, id);
		}
	}

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));

		var shapeType = GetShapeType();
		int length = ShapeBuffer.GetMultipartBufferSize(HasZ, HasM, HasID, NumParts, NumPoints);

		bool mayHaveCurves = ShapeBuffer.GetMayHaveCurves(shapeType);
		int numCurves = _curves?.Count ?? 0;

		if (mayHaveCurves || numCurves > 0) length += 4;

		foreach (var modifier in Curves)
		{
			length += 4 + 4; // startIndex and curveType

			switch (modifier)
			{
				case CircularArcModifier:
					length += 8 + 8 + 4;
					break;
				case CubicBezierModifier:
					length += 8 + 8 + 8 + 8;
					break;
				case EllipticArcModifier:
					length += 8 + 8 + 8 + 8 + 8 + 4;
					break;
				default:
					throw new NotSupportedException(
						$"Unknown segment modifier: {modifier.GetType().Name} " +
						$"(curve type {modifier.CurveType})");
			}
		}

		if (bytes is null || bytes.Length - offset < length)
			return length;

		// Empty Multipart is handled implicitly: zero for numPoints and numParts,
		// NaN for all min/max values (including Zmin/max if hasZ and Mmin/max if hasM)

		offset += ShapeBuffer.WriteShapeType(shapeType, bytes, offset);

		var box = Box;
		offset += ShapeBuffer.WriteDouble(box.XMin, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.YMin, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.XMax, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.YMax, bytes, offset);

		var numPoints = NumPoints;
		offset += ShapeBuffer.WriteInt32(numPoints, bytes, offset);

		var xys = CoordsXY;
		for (int i = 0; i < numPoints; i++)
		{
			offset += ShapeBuffer.WriteDouble(xys[i].X, bytes, offset);
			offset += ShapeBuffer.WriteDouble(xys[i].Y, bytes, offset);
		}

		if (HasZ)
		{
			offset += ShapeBuffer.WriteDouble(box.ZMin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(box.ZMax, bytes, offset);

			var zs = CoordsZ ?? throw Bug("HasZ is true but CoordsZ is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteDouble(zs[i], bytes, offset);
			}
		}

		if (HasM)
		{
			offset += ShapeBuffer.WriteDouble(box.MMin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(box.MMax, bytes, offset);

			var ms = CoordsM ?? throw Bug("HasM is true but CoordsM is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteDouble(ms[i], bytes, offset);
			}
		}

		if (mayHaveCurves || numCurves > 0)
		{
			offset += ShapeBuffer.WriteInt32(numCurves, bytes, offset);

			Debug.Assert(_curves is not null);

			for (int i = 0; i < numCurves; i++)
			{
				var modifier = _curves[i];
				offset += modifier.WriteShapeBuffer(bytes, offset);
			}
		}

		if (HasID)
		{
			var ids = CoordsID ?? throw Bug("HasID is true but CoordsID is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteInt32(ids[i], bytes, offset);
			}
		}

		Debug.Assert(bytes.Length == offset);

		return length;
	}
}

public class PolylineShape : MultipartShape
{
	private PolylineShape?[]? _partsCache;
	private ReadOnlyParts? _parts;

	public PolylineShape(ShapeFlags flags,
		IReadOnlyList<XY> xys,
		IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null,
		IReadOnlyList<int>? ids = null,
		IReadOnlyList<int>? partStarts = null,
		IReadOnlyList<SegmentModifier>? curves = null)
		: base(GetShapeType(GeometryType.Polyline, flags), xys, zs, ms, ids, partStarts, curves)
	{ }

	public IReadOnlyList<PolylineShape> Parts => _parts ??= new ReadOnlyParts(this);

	private PolylineShape GetPart(int partIndex)
	{
		if (partIndex < 0 || partIndex >= NumParts)
			throw new ArgumentOutOfRangeException(nameof(partIndex), partIndex, null);

		if (NumParts == 1)
		{
			return this;
		}

		if (_partsCache is null)
		{
			_partsCache = new PolylineShape[NumParts];
		}

		if (_partsCache[partIndex] is null)
		{
			GetPartStartAndCount(partIndex, out int start, out int count);

			var xys = new ReadOnlySubList<XY>(CoordsXY, start, count);
			var zs = CoordsZ is not null ? new ReadOnlySubList<double>(CoordsZ, start, count) : null;
			var ms = CoordsM is not null ? new ReadOnlySubList<double>(CoordsM, start, count) : null;
			var ids = CoordsID is not null ? new ReadOnlySubList<int>(CoordsID, start, count) : null;
			var curves = GetPartCurves(partIndex);

			_partsCache[partIndex] = new PolylineShape(Flags, xys, zs, ms, ids, null, curves);
		}

		return _partsCache[partIndex]!;
	}

	protected override void ToWKT(WKTWriter wkt)
	{
		wkt.BeginMultiLineString(HasZ, HasM, HasID);
		WriteCoordinates(wkt);
		wkt.EndShape();
	}

	private class ReadOnlyParts : IReadOnlyList<PolylineShape>
	{
		private readonly PolylineShape _parent;

		public ReadOnlyParts(PolylineShape parent)
		{
			_parent = parent ?? throw new ArgumentNullException(nameof(parent));
		}

		public int Count => _parent.NumParts;

		public PolylineShape this[int index] => _parent.GetPart(index);

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<PolylineShape> GetEnumerator()
		{
			for (int i = 0; i < Count; i++)
			{
				yield return _parent.GetPart(i);
			}
		}
	}
}

public class PolygonShape : MultipartShape
{
	private PolygonShape?[]? _partsCache;
	private ReadOnlyParts? _parts;

	public PolygonShape(ShapeFlags flags,
		IReadOnlyList<XY> xys,
		IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null,
		IReadOnlyList<int>? ids = null,
		IReadOnlyList<int>? partStarts = null,
		IReadOnlyList<SegmentModifier>? curves = null)
		: base(GetShapeType(GeometryType.Polygon, flags), xys, zs, ms, ids, partStarts, curves)
	{ }

	public IReadOnlyList<PolygonShape> Parts => _parts ??= new ReadOnlyParts(this);

	private PolygonShape GetPart(int partIndex)
	{
		if (partIndex < 0 || partIndex >= NumParts)
			throw new ArgumentOutOfRangeException(nameof(partIndex), partIndex, null);

		if (NumParts == 1)
		{
			return this;
		}

		if (_partsCache is null)
		{
			_partsCache = new PolygonShape[NumParts];
		}

		if (_partsCache[partIndex] is null)
		{
			GetPartStartAndCount(partIndex, out int start, out int count);

			var xys = new ReadOnlySubList<XY>(CoordsXY, start, count);
			var zs = CoordsZ is not null ? new ReadOnlySubList<double>(CoordsZ, start, count) : null;
			var ms = CoordsM is not null ? new ReadOnlySubList<double>(CoordsM, start, count) : null;
			var ids = CoordsID is not null ? new ReadOnlySubList<int>(CoordsID, start, count) : null;
			var curves = GetPartCurves(partIndex);

			_partsCache[partIndex] = new PolygonShape(Flags, xys, zs, ms, ids, null, curves);
		}

		return _partsCache[partIndex]!;
	}

	protected override void ToWKT(WKTWriter wkt)
	{
		wkt.BeginMultiPolygon(HasZ, HasM, HasID);
		WriteCoordinates(wkt);
		wkt.EndShape();
	}

	private class ReadOnlyParts : IReadOnlyList<PolygonShape>
	{
		private readonly PolygonShape _parent;

		public ReadOnlyParts(PolygonShape parent)
		{
			_parent = parent ?? throw new ArgumentNullException(nameof(parent));
		}

		public int Count => _parent.NumParts;

		public PolygonShape this[int index] => _parent.GetPart(index);

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<PolygonShape> GetEnumerator()
		{
			for (int i = 0; i < Count; i++)
			{
				yield return _parent.GetPart(i);
			}
		}
	}
}

public abstract class SegmentModifier
{
	public int SegmentIndex { get; }
	public abstract int CurveType { get; }

	protected SegmentModifier(int segmentIndex)
	{
		SegmentIndex = segmentIndex;
	}

	public virtual double GetLength(XY startPoint, XY endPoint)
	{
		var dx = startPoint.X - endPoint.X;
		var dy = startPoint.Y - endPoint.Y;
		return Math.Sqrt(dx * dx + dy * dy);
	}

	public abstract int GetShapeBufferSize();

	public int WriteShapeBuffer(byte[] bytes, int offset)
	{
		int startOffset = offset;
		offset += ShapeBuffer.WriteInt32(SegmentIndex, bytes, offset);
		offset += ShapeBuffer.WriteInt32(CurveType, bytes, offset);
		offset += WriteShapeBufferCore(bytes, offset);
		return offset - startOffset;
	}

	protected abstract int WriteShapeBufferCore(byte[] bytes, int offset);
}

public class CircularArcModifier : SegmentModifier
{
	public override int CurveType => 1;
	public double D1 { get; } // center point X | start angle
	public double D2 { get; } // center point Y | end angle
	public int Flags { get; } // see ext shp buf fmt

	public CircularArcModifier(int segmentIndex, double d1, double d2, int flags)
		: base(segmentIndex)
	{
		D1 = d1;
		D2 = d2;
		Flags = flags;
	}

	public override int GetShapeBufferSize()
	{
		return 4 + 4 + 8 + 8 + 4; // index, type, 2 doubles, flags
	}

	public bool IsEmpty => (Flags & 1) != 0; // bit 0, the arc is undefined
	public bool IsCCW => (Flags & 8) != 0; // bit 3, the arc is in ccw direction
	public bool IsMinor => (Flags & 16) != 0; // bit 4, central angle <= pi
	public bool IsLine => (Flags & 32) != 0; // bit 5, only SP and EP are defined (infinite radius)
	public bool IsPoint => (Flags & 64) != 0; // bit 6, CP, SP, EP are identical; angles are stored instead of CP
	public bool DefinedIP => (Flags & 128) != 0; // bit 7, interior point; ...
	// arcs persisted with 9.2 persist endpoints + one interior point, rather than
	// endpoints and center point so that the arc shape can be recovered after
	// projecting to another spatial reference and back again; point arcs still
	// replace the center point with SA and CA

	public override double GetLength(XY startXY, XY endXY)
	{
		if (IsEmpty)
		{
			return 0.0;
		}

		bool isPoint = IsPoint;
		bool isLine = IsLine;

		if (isPoint && startXY != endXY)
		{
			isPoint = false;
			isLine = true;
		}
		if (isLine && startXY == endXY)
		{
			isPoint = true;
			isLine = false;
		}

		if (isPoint)
		{
			// startAngle, centralAngle, endAngle = D1, D2, D1+D2
			// centerXY, radius = startXY, 0.0 (or 2pi?)
			// isMinor = Math.Abs(centralAngle) <= Math.PI
			return 0.0; // TODO unsure
		}

		if (isLine)
		{
			var dx = startXY.X - endXY.X;
			var dy = startXY.Y - endXY.Y;
			return Math.Sqrt(dx * dx + dy * dy);
		}

		XY centerXY;
		double radius;
		double centralAngle;
		bool wantCW = !IsCCW;

		if (DefinedIP)
		{
			if (isLine)
			{
				// startAngle = centralAngle = endAngle = 0.0
				// centerXY, radius = 0.5*(startXY+endXY), 1.0 (?)
				// isMinor = true
				centralAngle = 0.0;
				radius = 1.0; // unsure
			}
			else
			{
				var interiorXY = new XY(D1, D2);
				radius = GetCircleFromInteriorPoint(startXY, interiorXY, endXY, out centerXY);
				centralAngle = CentralAngle(startXY, centerXY, endXY, wantCW);
			}
		}
		else
		{
			centerXY = new XY(D1, D2);
			radius = GetRadius(startXY, centerXY, endXY);
			centralAngle = CentralAngle(startXY, centerXY, endXY, wantCW);
		}

		return Math.Abs(radius * centralAngle);
	}

	private static double GetRadius(XY start, XY center, XY end)
	{
		var r1 = (start - center).Magnitude;
		var r2 = (end - center).Magnitude;
		return (r1 + r2) / 2.0; // average
	}

	private static double GetCircleFromInteriorPoint(XY startXY, XY interiorXY, XY endXY, out XY centerXY)
	{
		if (startXY == endXY)
		{
			centerXY = 0.5 * (startXY + interiorXY);
			return GetRadius(startXY, centerXY, endXY);
		}

		return Geometry.Circumcircle(startXY, interiorXY, endXY, out centerXY);
	}

	private static double CentralAngle(XY start, XY center, XY end, bool wantCW)
	{
		if (start == end)
		{
			// special case: assume full circle (not empty)
			return wantCW ? -2.0 * Math.PI : 2.0 * Math.PI;
		}

		return Geometry.CentralAngle(start, center, end, wantCW);
	}

	protected override int WriteShapeBufferCore(byte[] bytes, int offset)
	{
		ShapeBuffer.WriteDouble(D1, bytes, offset + 0);
		ShapeBuffer.WriteDouble(D2, bytes, offset + 8);
		ShapeBuffer.WriteInt32(Flags, bytes, offset + 16);
		return 8 + 8 + 4;
	}
}

public class CubicBezierModifier : SegmentModifier
{
	public override int CurveType => 4;
	public double ControlPoint1X { get; }
	public double ControlPoint1Y { get; }
	public double ControlPoint2X { get; }
	public double ControlPoint2Y { get; }

	public CubicBezierModifier(int segmentIndex, double cp1X, double cp1Y, double cp2X, double cp2Y)
		: base(segmentIndex)
	{
		ControlPoint1X = cp1X;
		ControlPoint1Y = cp1Y;
		ControlPoint2X = cp2X;
		ControlPoint2Y = cp2Y;
	}

	public override int GetShapeBufferSize()
	{
		return 4 + 4 + 4 * 8; // index, type, 4 doubles
	}

	public override double GetLength(XY startPoint, XY endPoint)
	{
		var p1 = new XY(ControlPoint1X, ControlPoint1Y);
		var p2 = new XY(ControlPoint2X, ControlPoint2Y);
		return CubicBezier.Length(startPoint, p1, p2, endPoint);
	}

	protected override int WriteShapeBufferCore(byte[] bytes, int offset)
	{
		ShapeBuffer.WriteDouble(ControlPoint1X, bytes, offset + 0);
		ShapeBuffer.WriteDouble(ControlPoint1Y, bytes, offset + 8);
		ShapeBuffer.WriteDouble(ControlPoint2X, bytes, offset + 16);
		ShapeBuffer.WriteDouble(ControlPoint2Y, bytes, offset + 24);
		return 4 * 8;
	}
}

public class EllipticArcModifier : SegmentModifier
{
	public override int CurveType => 5;
	public double D1 { get; } // centerPoint.X | V1
	public double D2 { get; } // centerPoint.Y | V2
	public double D3 { get; } // rotation | fromV
	public double D4 { get; } // semi major axis
	public double D5 { get; } // minor/major ratio | deltaV
	public int Flags { get; } // see ext shp buf fmt

	public EllipticArcModifier(int segmentIndex, double d1, double d2, double d3, double d4, double d5, int flags)
		: base(segmentIndex)
	{
		D1 = d1;
		D2 = d2;
		D3 = d3;
		D4 = d4;
		D5 = d5;
		Flags = flags;
	}

	public override int GetShapeBufferSize()
	{
		return 4 + 4 + 5 * 8 + 4; // index, type, 5 doubles, flags
	}

	public override double GetLength(XY startPoint, XY endPoint)
	{
		throw new NotImplementedException();
	}

	protected override int WriteShapeBufferCore(byte[] bytes, int offset)
	{
		ShapeBuffer.WriteDouble(D1, bytes, offset + 0);
		ShapeBuffer.WriteDouble(D2, bytes, offset + 8);
		ShapeBuffer.WriteDouble(D3, bytes, offset + 16);
		ShapeBuffer.WriteDouble(D4, bytes, offset + 24);
		ShapeBuffer.WriteDouble(D5, bytes, offset + 32);
		ShapeBuffer.WriteInt32(Flags, bytes, offset + 40);
		return 5 * 8 + 4;
	}
}

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
