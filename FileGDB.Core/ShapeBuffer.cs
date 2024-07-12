using System.Collections;
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

public static class ShapeBuffer
{
	/// <summary>
	/// Flags if the shape is of one of the "General" types
	/// </summary>
	public enum Flags : uint
	{
		HasZ = 0x80000000,
		HasM = 0x40000000,
		HasCurves = 0x20000000,
		HasID = 0x10000000,
		HasNormals = 0x8000000,
		HasTextures = 0x4000000,
		HasPartIDs = 0x2000000,
		HasMaterials = 0x1000000,
		IsCompressed = 0x800000, // from FileGDBCore.h of FileGDB API, not in ext shp buf fmt white paper
		BasicTypeMask = 0xFF,
		ShapeFlagsMask = 0xFF000000 // in FileGDBCore.h this is -16777216
	}

	public static ShapeType GetShapeType(ulong shapeType)
	{
		return (ShapeType)(shapeType & 255);
	}

	public static GeometryType GetGeometryType(ulong shapeType)
	{
		return GetGeometryType(GetShapeType(shapeType));
	}

	public static GeometryType GetGeometryType(ShapeType shapeType)
	{
		switch (shapeType)
		{
			case ShapeType.Null:
				return GeometryType.Null;
			case ShapeType.Point:
			case ShapeType.PointZ:
			case ShapeType.PointZM:
			case ShapeType.PointM:
				return GeometryType.Point;
			case ShapeType.Multipoint:
			case ShapeType.MultipointZ:
			case ShapeType.MultipointZM:
			case ShapeType.MultipointM:
				return GeometryType.Multipoint;
			case ShapeType.Polyline:
			case ShapeType.PolylineZ:
			case ShapeType.PolylineZM:
			case ShapeType.PolylineM:
				return GeometryType.Polyline;
			case ShapeType.Polygon:
			case ShapeType.PolygonZ:
			case ShapeType.PolygonZM:
			case ShapeType.PolygonM:
				return GeometryType.Polygon;
			case ShapeType.MultiPatch:
			case ShapeType.MultiPatchM:
				return GeometryType.MultiPatch;
			case ShapeType.GeneralPolyline:
				return GeometryType.Polyline;
			case ShapeType.GeneralPolygon:
				return GeometryType.Polygon;
			case ShapeType.GeneralPoint:
				return GeometryType.Point;
			case ShapeType.GeneralMultipoint:
				return GeometryType.Multipoint;
			case ShapeType.GeneralMultiPatch:
				return GeometryType.MultiPatch;
			case ShapeType.Box:
				return GeometryType.Envelope;
			default:
				throw new ArgumentOutOfRangeException(nameof(shapeType), shapeType, null);
		}
	}

	public static ShapeFlags GetShapeFlags(uint shapeType)
	{
		var flags = ShapeFlags.None;
		if (HasZs(shapeType)) flags |= ShapeFlags.HasZ;
		if (HasMs(shapeType)) flags |= ShapeFlags.HasM;
		if (HasIDs(shapeType)) flags |= ShapeFlags.HasID;
		return flags;
	}

	public static bool HasZs(uint shapeType)
	{
		switch ((ShapeType)(shapeType & (uint)Flags.BasicTypeMask))
		{
			case ShapeType.PointZ:
			case ShapeType.PointZM:
			case ShapeType.MultipointZ:
			case ShapeType.MultipointZM:
			case ShapeType.PolylineZ:
			case ShapeType.PolylineZM:
			case ShapeType.PolygonZ:
			case ShapeType.PolygonZM:
			case ShapeType.MultiPatch:
			case ShapeType.MultiPatchM:
				return true;
			case ShapeType.GeneralPolyline:
			case ShapeType.GeneralPolygon:
			case ShapeType.GeneralPoint:
			case ShapeType.GeneralMultipoint:
			case ShapeType.GeneralMultiPatch:
				return (shapeType & (uint)Flags.HasZ) != 0U;
			default:
				return false;
		}
	}

	public static bool HasMs(uint shapeType)
	{
		switch ((ShapeType)(shapeType & (uint)Flags.BasicTypeMask))
		{
			case ShapeType.PointM:
			case ShapeType.PointZM:
			case ShapeType.MultipointM:
			case ShapeType.MultipointZM:
			case ShapeType.PolylineM:
			case ShapeType.PolylineZM:
			case ShapeType.PolygonM:
			case ShapeType.PolygonZM:
			case ShapeType.MultiPatch:
			case ShapeType.MultiPatchM:
				return true;
			case ShapeType.GeneralPolyline:
			case ShapeType.GeneralPolygon:
			case ShapeType.GeneralPoint:
			case ShapeType.GeneralMultipoint:
			case ShapeType.GeneralMultiPatch:
				return (shapeType & (uint)Flags.HasM) != 0U;
			default:
				return false;
		}
	}

	public static bool HasIDs(uint shapeType)
	{
		return (shapeType & (uint)Flags.HasID) != 0;
	}

	public static bool HasCurves(uint shapeType)
	{
		// special case mentioned in Ext Shp Buf Fmt p.4:
		var basicType = (ShapeType)(shapeType & 255);
		var shapeFlags = shapeType & (uint)Flags.ShapeFlagsMask;
		var noModifierBits = shapeFlags == 0;

		if (basicType is ShapeType.GeneralPolygon or ShapeType.GeneralPolyline &&
		    noModifierBits)
		{
			return true;
		}

		return (shapeType & (uint)Flags.HasCurves) != 0;
	}

	public static uint GetPointShapeType(bool hasZ, bool hasM, bool hasID)
	{
		var type = (uint)ShapeType.GeneralPoint;
		if (hasZ) type |= (uint)Flags.HasZ;
		if (hasM) type |= (uint)Flags.HasM;
		if (hasID) type |= (uint)Flags.HasID;
		return type;
	}

	public static uint GetPointShapeType(ShapeFlags flags)
	{
		bool hasZ = (flags & ShapeFlags.HasZ) != 0;
		bool hasM = (flags & ShapeFlags.HasM) != 0;
		bool hasID = (flags & ShapeFlags.HasID) != 0;
		return GetPointShapeType(hasZ, hasM, hasID);
	}

	public static uint GetMultipointShapeType(ShapeFlags flags)
	{
		var type = (uint)ShapeType.GeneralMultipoint;
		if ((flags & ShapeFlags.HasZ) != 0) type |= (uint)Flags.HasZ;
		if ((flags & ShapeFlags.HasM) != 0) type |= (uint)Flags.HasM;
		if ((flags & ShapeFlags.HasID) != 0) type |= (uint)Flags.HasID;
		return type;
	}

	public static uint GetBoxShapeType(bool hasZ, bool hasM)
	{
		var type = (uint)ShapeType.Box;
		if (hasZ) type |= (uint)Flags.HasZ;
		if (hasM) type |= (uint)Flags.HasM;
		return type;
	}
}

public class GeometryBlobReader
{
	private readonly ShapeFactory _factory;
	private readonly GeometryDef _geomDef;
	private readonly byte[] _bytes;
	private int _index;

	public GeometryBlobReader(GeometryDef geomDef, byte[] bytes)
	{
		_factory = new ShapeFactory();
		_geomDef = geomDef ?? throw new ArgumentNullException(nameof(geomDef));
		_bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
		_index = 0;
	}

	public bool HasZ => _geomDef.HasZ;
	public bool HasM => _geomDef.HasM;
	public bool HasID => false; // TODO

	public double XOrigin => _geomDef.XOrigin;
	public double YOrigin => _geomDef.YOrigin;
	public double XYScale => _geomDef.XYScale;

	public double ZOrigin => _geomDef.ZOrigin;
	public double ZScale => _geomDef.ZScale;

	public double MOrigin => _geomDef.ZOrigin;
	public double MScale => _geomDef.ZScale;

	public Shape ReadShape()
	{
		// first is geometry type:
		var type = ReadVuint();
		var geometryType = ShapeBuffer.GetGeometryType(type);
		if (geometryType is GeometryType.Point)
		{
			var ix = ReadVuint();
			if (ix < 1) return PointShape.Empty;
			double x = XOrigin + (ix - 1) / XYScale;

			var iy = ReadVuint();
			if (iy < 1) return PointShape.Empty;
			double y = YOrigin + (iy - 1) / XYScale;

			var z = double.NaN;
			if (HasZ)
			{
				var iz = ReadVuint();
				z = ZOrigin + (iz - 1) / ZScale;
			}

			var m = double.NaN;
			if (HasM)
			{
				var im = ReadVuint();
				m = MOrigin + (im - 1) / MScale;
			}

			return new PointShape(x, y, z, m);
		}

		if (geometryType is GeometryType.Multipoint)
		{
			var inum = ReadVuint();
			int numPoints = (int)inum;

			ReadBoxXY(out double xmin, out double ymin, out double xmax, out double ymax);

			_factory.Initialize(GeometryType.Multipoint, HasZ, HasM, HasID, numPoints);
			_factory.SetMinMaxXY(xmin, ymin, xmax, ymax);

			long dx = 0, dy = 0;
			for (uint i = 0; i < inum; i++)
			{
				var ix = ReadVint();
				dx += ix;
				var iy = ReadVint();
				dy += iy;
				_factory.AddXY(XOrigin + dx / XYScale, XOrigin + dy / XYScale);
			}

			if (HasZ)
			{
				// TODO really no Zmin ZMax?
				// TODO Z coords as for XY
				throw new NotImplementedException("multipoint with Z: not yet implemented");
			}

			if (HasM)
			{
				// TODO
				throw new NotImplementedException("multipoint with M: not yet implemented");
			}

			return _factory.ToShape();
		}

		if (geometryType is GeometryType.Polyline or GeometryType.Polygon)
		{
			// TODO Not sure if shape types GeneralPolyline and (classic) Polyline[Z][M] are really stored the same way!
			// TODO Ditto for Polygons...

			int numPoints = (int) ReadVuint();
			int numParts = (int) ReadVuint();

			ReadBoxXY(out double xmin, out double ymin, out double xmax, out double ymax);

			_factory.Initialize(geometryType, HasZ, HasM, HasID, numPoints, numParts);
			_factory.SetMinMaxXY(xmin, ymin, xmax, ymax);

			for (int k = 0; k < numParts - 1; k++)
			{
				var ipcnt = ReadVuint();
				_factory.AddPartCount((int)ipcnt);
			}

			long dx = 0, dy = 0;
			for (uint i = 0; i < numPoints; i++)
			{
				var ix = ReadVint();
				dx += ix;
				var iy = ReadVint();
				dy += iy;
				_factory.AddXY(XOrigin + dx / XYScale, XOrigin + dy / XYScale);
			}

			if (HasZ)
			{
				// TODO really no Zmin ZMax?
				// TODO Z coords as for XY
				throw new NotImplementedException("polyline/polygon with Z: not yet implemented");
			}

			if (HasM)
			{
				// TODO
				throw new NotImplementedException("polyline/polygon with M: not yet implemented");
			}

			return _factory.ToShape();
		}

		throw new NotImplementedException($"Geometry of type {geometryType} is not yet implemented");
	}

	private void ReadBoxXY(out double xmin, out double ymin, out double xmax, out double ymax)
	{
		var ixmin = ReadVuint();
		xmin = XOrigin + ixmin / XYScale;
		var iymin = ReadVuint();
		ymin = YOrigin + iymin / XYScale;
		var ixmax = ReadVuint();
		xmax = xmin + ixmax / XYScale;
		var iymax = ReadVuint();
		ymax = ymin + iymax / XYScale;
	}

	public long ReadVint()
	{
		if (_index >= _bytes.Length)
			throw ReadingBeyondBlob();
		byte b = _bytes[_index++];
		ulong result = (ulong)(b & 0x3F);
		int sign = (b & 0x40) != 0 ? -1 : 1;
		if ((b & 0x80) == 0) return sign * (long) result;
		int shift = 6; // without continuation bit and sign bit
		// TODO check for overflow
		while (_index < _bytes.Length)
		{
			b = _bytes[_index++];
			result |= (ulong)(b & 127) << shift;
			if ((b & 0x80) != 0) return sign * (long) result;
			shift += 7;
		}

		throw ReadingBeyondBlob();
	}

	public ulong ReadVuint()
	{
		ulong result = 0;
		int shift = 0;

		// TODO check for overflow

		while (_index < _bytes.Length)
		{
			byte b = _bytes[_index++];
			result |= (ulong)(b & 127) << shift;
			if ((b & 128) == 0) return result;
			shift += 7;
		}

		throw ReadingBeyondBlob();
	}

	private FormatException ReadingBeyondBlob()
	{
		return new FormatException("Reading beyond the end of the geometry blob");
	}
}

public readonly struct XY
{
	public readonly double X;
	public readonly double Y;

	public XY(double x, double y)
	{
		X = x;
		Y = y;
	}
}

public abstract class Shape
{
	private readonly uint _shapeType;
	private BoxShape? _box;

	public GeometryType Type => ShapeBuffer.GetGeometryType(_shapeType);
	public ShapeFlags Flags => ShapeBuffer.GetShapeFlags(_shapeType);
	public bool HasZ => ShapeBuffer.HasZs(_shapeType);
	public bool HasM => ShapeBuffer.HasMs(_shapeType);
	public bool HasID => ShapeBuffer.HasIDs(_shapeType);

	public const double DefaultZ = double.NaN;
	public const double DefaultM = double.NaN;
	public const int DefaultID = 0;

	protected Shape(uint shapeType)
	{
		_shapeType = shapeType;
	}

	public BoxShape Box => _box ??= GetBox();

	public abstract bool IsEmpty { get; }

	public abstract string ToWKT(); // TODO overloads void ToWKT(StringBuilder) and ToWKT(TextWriter)

	// Shape FromShapeBuffer(byte[] bytes)
	// byte[] ToShapeBuffer(byte[] bytes = null)

	public Shape SetBox(BoxShape box)
	{
		_box = box; // null is ok (box will be lazily computed)
		return this;
	}

	protected abstract BoxShape GetBox();

	protected static uint GetShapeType(GeometryType type, ShapeFlags flags)
	{
		uint result;

		switch (type)
		{
			case GeometryType.Null:
				result = (uint) ShapeType.Null;
				break;
			case GeometryType.Point:
				result = (uint) ShapeType.GeneralPoint;
				break;
			case GeometryType.Multipoint:
				result = (uint) ShapeType.GeneralMultipoint;
				break;
			case GeometryType.Polyline:
				result = (uint) ShapeType.GeneralPolyline;
				break;
			case GeometryType.Polygon:
				result = (uint) ShapeType.GeneralPolygon;
				break;
			case GeometryType.Envelope:
				result = (uint) ShapeType.Box;
				break;
			case GeometryType.MultiPatch:
				result = (uint) ShapeType.GeneralMultiPatch;
				break;
			case GeometryType.Any:
			case GeometryType.Bag:
				throw new ArgumentOutOfRangeException(nameof(type), type, "Any and Bag are not supported");
			default:
				throw new ArgumentOutOfRangeException(nameof(type), type, null);
		}

		if ((flags & ShapeFlags.HasZ) != 0) result |= (uint)ShapeBuffer.Flags.HasZ;
		if ((flags & ShapeFlags.HasM) != 0) result |= (uint)ShapeBuffer.Flags.HasM;
		if ((flags & ShapeFlags.HasID) != 0) result |= (uint)ShapeBuffer.Flags.HasID;
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

	public override string ToWKT()
	{
		var buffer = new StringBuilder();
		var writer = new WKTWriter(buffer);
		writer.WriteBox(XMin, YMin, XMax, YMax, ZMin, ZMax, MMin, MMax);
		writer.Flush();
		return buffer.ToString();
	}
}

public class PointShape : Shape
{
	public double X { get; }
	public double Y { get; }
	public double Z { get; } // NaN if not HasZ
	public double M { get; } // NaN if not HasM
	public int ID { get; } // zero if not HasID

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

	public override string ToWKT()
	{
		var buffer = new StringBuilder();
		var writer = new WKTWriter(buffer);
		writer.WritePoint(X, Y, Z, M, ID);
		writer.Flush();
		return buffer.ToString();
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

	public MultipointShape(XY[] xys, double[]? zs = null, double[]? ms = null, int[]? ids = null)
		: this(GuessFlags(zs, ms, ids), xys, zs, ms, ids) { }

	private static ShapeFlags GuessFlags(double[]? zs, double[]? ms, int[]? ids)
	{
		var flags = ShapeFlags.None;
		if (zs is not null) flags |= ShapeFlags.HasZ;
		if (ms is not null) flags |= ShapeFlags.HasM;
		if (ids is not null) flags |= ShapeFlags.HasID;
		return flags;
	}

	public override string ToWKT()
	{
		var buffer = new StringBuilder();
		var writer = new WKTWriter(buffer);
		writer.BeginMultipoint(HasZ, HasM, HasID);
		for (int i = 0; i < NumPoints; i++)
		{
			var xy = CoordsXY[i];
			var z = CoordsZ is null ? DefaultZ : CoordsZ[i];
			var m = CoordsM is null ? DefaultM : CoordsM[i];
			var id = CoordsID is null ? DefaultID : CoordsID[i];
			writer.AddVertex(xy.X, xy.Y, z, m, id);
		}
		writer.EndShape();
		writer.Flush();
		return buffer.ToString();
	}
}

public abstract class MultipartShape : PointListShape
{
	private readonly IReadOnlyList<int>? _partStarts;

	protected MultipartShape(uint shapeType,
		IReadOnlyList<XY> xys, IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null, IReadOnlyList<int>? ids = null,
		IReadOnlyList<int>? partStarts = null)
		: base(shapeType, xys, zs, ms, ids)
	{
		if (partStarts is not null)
		{
			int lastPartStart = 0;
			for (int k = 0; k < partStarts.Count; k++)
			{
				int i = partStarts[k];
				if (i < 0)
					throw new ArgumentException("part starts must not be negative", nameof(partStarts));
				if (i >= NumPoints)
					throw new ArgumentException("part starts must be less than point count", nameof(partStarts));
				if (i < lastPartStart)
					throw new ArgumentException("part starts must be monotonically increasing", nameof(partStarts));
				lastPartStart = i;
			}
		}

		_partStarts = partStarts;// ?? new[] { 0 };
	}

	public int NumParts => _partStarts?.Count ?? 1;

	/// <returns>Index into coordinate lists where part <paramref name="partIndex"/> starts</returns>
	/// <remarks>Returns 0 or NumPoints if <paramref name="partIndex"/> is out of range </remarks>
	private int GetPartStart(int partIndex)
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
			var id = CoordsID?[i]; // null unless HasID

			writer.AddVertex(xy.X, xy.Y, z, m, id);
		}
	}
}

public class PolylineShape : MultipartShape
{
	private PolylineShape?[]? _partsCache;
	private ReadOnlyParts? _parts;

	public PolylineShape(ShapeFlags flags,
		IReadOnlyList<XY> xys, IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null, IReadOnlyList<int>? ids = null,
		IReadOnlyList<int>? partStarts = null)
		: base(GetShapeType(GeometryType.Polyline, flags), xys, zs, ms, ids, partStarts)
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

			_partsCache[partIndex] = new PolylineShape(Flags, xys, zs, ms, ids);
		}

		return _partsCache[partIndex]!;
	}

	public override string ToWKT()
	{
		var buffer = new StringBuilder();
		var writer = new WKTWriter(buffer);

		writer.BeginMultiLineString(HasZ, HasM, HasID);

		WriteCoordinates(writer);

		writer.EndShape();
		writer.Flush();

		return buffer.ToString();
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
		IReadOnlyList<XY> xys, IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null, IReadOnlyList<int>? ids = null,
		IReadOnlyList<int>? partStarts = null)
		: base(GetShapeType(GeometryType.Polygon, flags), xys, zs, ms, ids, partStarts)
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

			_partsCache[partIndex] = new PolygonShape(Flags, xys, zs, ms, ids);
		}

		return _partsCache[partIndex]!;
	}

	public override string ToWKT()
	{
		var buffer = new StringBuilder();
		var writer = new WKTWriter(buffer);

		writer.BeginMultiPolygon(HasZ, HasM, HasID);

		WriteCoordinates(writer);

		writer.EndShape();
		writer.Flush();

		return buffer.ToString();
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

/// <summary>
/// Names and values as for ArcObjects esriShapeType constants, which
/// are described as the "Esri Shapefile shape types". The general
/// types are not supported in shape files but described in the
/// "Extende Shape Buffer Format" white paper (2012).
/// </summary>
public enum ShapeType
{
	// From the original Shapefile specification
	Null = 0,
	Point = 1,
	PointZ = 9,
	PointZM = 11,
	PointM = 21,
	Multipoint = 8,
	MultipointZ = 20,
	MultipointZM = 18,
	MultipointM = 28,
	Polyline = 3,
	PolylineZ = 10,
	PolylineZM = 13,
	PolylineM = 23,
	Polygon = 5,
	PolygonZ = 19,
	PolygonZM = 15,
	PolygonM = 25,
	MultiPatch = 32,
	MultiPatchM = 31,

	// From the Extended Shape Buffer Format specification
	GeneralPolyline = 50,
	GeneralPolygon = 51,
	GeneralPoint = 52,
	GeneralMultipoint = 53,
	GeneralMultiPatch = 54,

	// Custom additions
	Box = 254
}

public class ShapeFactory
{
	private readonly List<int> _parts = new();
	private readonly List<XY> _xys = new();
	private readonly List<double> _zs = new();
	private readonly List<double> _ms = new();
	private readonly List<int> _ids = new();

	private double _xmin = double.NaN;
	private double _ymin = double.NaN;
	private double _xmax = double.NaN;
	private double _ymax = double.NaN;
	private double _zmin = double.NaN;
	private double _zmax = double.NaN;
	private double _mmin = double.NaN;
	private double _mmax = double.NaN;

	public GeometryType Type { get; private set; }
	public bool HasZ { get; private set; }
	public bool HasM { get; private set; }
	public bool HasID { get; private set; }
	public int NumParts { get; private set; }
	public int NumPoints { get; private set; }
	public int NumCurves { get; private set; }

	public void Initialize(
		GeometryType type, bool hasZ, bool hasM, bool hasID,
		int numPoints = 1, int numParts = 1, int numCurves = 0)
	{
		Type = type;
		HasZ = hasZ;
		HasM = hasM;
		HasID = hasID;
		NumPoints = numPoints;
		NumParts = numParts;
		NumCurves = numCurves;

		_xys.Clear();
		_zs.Clear();
		_ms.Clear();
		_ids.Clear();

		_parts.Clear();
		_parts.Add(numPoints);

		_xmin = _xmax = double.NaN;
		_ymin = _ymax = double.NaN;
		_zmin = _zmax = double.NaN;
		_mmin = _mmax = double.NaN;
	}

	public void AddPartCount(int count)
	{
		if (_parts.Count < 1)
			throw new Exception("Bug: expect _parts.Count > 0");
		// Invariant: last part count is always NumPoints - (sum of _parts except last)
		int index = _parts.Count - 1;
		_parts[index] -= count;
		_parts.Insert(index, count);
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

	public void AddCurve(object curve)
	{
		throw new NotImplementedException();
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

	public Shape ToShape()
	{
		if (Type is GeometryType.Point)
		{
			var flags = GetFlags(HasZ, HasM, HasID);
			double x = _xys.Count > 0 ? _xys[0].X : double.NaN;
			double y = _xys.Count > 0 ? _xys[0].Y : double.NaN;
			double z = HasZ && _zs.Count > 0 ? _zs[0] : double.NaN;
			double m = HasM && _ms.Count > 0 ? _ms[0] : double.NaN;
			int id = HasID && _ids.Count > 0 ? _ids[0] : -1;
			return new PointShape(flags, x, y, z, m, id);
		}

		if (Type is GeometryType.Multipoint)
		{
			var flags = GetFlags(HasZ, HasM, HasID);

			var count = _xys.Count;
			var xys = _xys.ToArray();
			var zs = HasZ ? TrimOrExtendWith(_zs, count, double.NaN).ToArray() : null;
			var ms = HasM ? TrimOrExtendWith(_ms, count, double.NaN).ToArray() : null;
			var ids = HasID ? TrimOrExtendWith(_ids, count, 0).ToArray() : null;

			var box = new BoxShape(flags, _xmin, _ymin, _xmax, _ymax, _zmin, _zmax, _mmin, _mmax);
			return new MultipointShape(flags, xys, zs, ms, ids).SetBox(box);
		}

		if (Type is GeometryType.Polyline or GeometryType.Polygon)
		{
			var flags = GetFlags(HasZ, HasM, HasID);

			// NB. ToArray() so the shape gets its own coordinate collections (shape ctor does NOT copy lists)
			var count = _xys.Count;
			var xys = _xys.ToArray();
			var zs = HasZ ? TrimOrExtendWith(_zs, count, double.NaN).ToArray() : null;
			var ms = HasM ? TrimOrExtendWith(_ms, count, double.NaN).ToArray() : null;
			var ids = HasID ? TrimOrExtendWith(_ids, count, 0).ToArray() : null;
			var parts = _parts.Count < 2 ? null : _parts.ToArray();

			var box = new BoxShape(flags, _xmin, _ymin, _xmax, _ymax, _zmin, _zmax, _mmin, _mmax);
			var shape = Type is GeometryType.Polyline
				? new PolylineShape(flags, xys, zs, ms, ids, parts).SetBox(box)
				: new PolygonShape(flags, xys, zs, ms, ids, parts).SetBox(box);
			return shape;
		}

		throw new NotSupportedException($"Shape of type {Type} is not supported");
	}

	public bool Validate(out string message)
	{
		bool valid = true;
		message = string.Empty;

		var numZs = _zs.Count;
		if (HasZ && numZs != NumPoints)
		{
			AddMessage(ref message, $"expect {NumPoints} Z coords but there are {numZs}");
			valid = false;
		}

		var numMs = _ms.Count;
		if (HasM && numMs != NumPoints)
		{
			AddMessage(ref message, $"expect {NumPoints} M coords but there are {numMs}");
			valid = false;
		}

		var numIDs = _ids.Count;
		if (HasID && numIDs != NumPoints)
		{
			AddMessage(ref message, $"expect {NumPoints} vertex IDs but there are {numIDs}");
			valid = false;
		}

		var numParts = _parts.Count;
		if (numParts != NumParts)
		{
			AddMessage(ref message, $"expect {NumParts} parts but there are {numParts}");
			valid = false;
		}

		var partSum = _parts.Sum();
		if (partSum != NumPoints)
		{
			AddMessage(ref message, $"sum over part counts ({partSum}) does not match actual point count ({NumPoints})");
			valid = false;
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
}
