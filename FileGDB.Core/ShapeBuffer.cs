using System.Text;

namespace FileGDB.Core;

/// <summary>
/// An Esri Extended Shape Buffer formatted byte array
/// </summary>
public class ShapeBuffer
{
	/// <summary>
	/// Flags if the shape is of one of the "General" types
	/// </summary>
	public enum Flags : uint
	{
		HasZ =        0x80000000,
		HasM =        0x40000000,
		HasCurves =   0x20000000,
		HasID =       0x10000000,
		HasNormals =   0x8000000,
		HasTextures =  0x4000000,
		HasPartIDs =   0x2000000,
		HasMaterials = 0x1000000,
		IsCompressed =  0x800000, // from FileGDBCore.h of FileGDB API, not in ext shp buf fmt white paper
		BasicTypeMask = 0xFF,
		ShapeFlagsMask = 0xFF000000 // in FileGDBCore.h this is -16777216
	}

	private readonly byte[] _bytes;
	private readonly uint _shapeType;

	public const double DefaultZ = 0.0;
	public const double DefaultM = double.NaN;
	public const int DefaultID = 0;

	public ShapeBuffer(byte[] bytes)
	{
		_bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
		_shapeType = _bytes.Length < 4 ? 0 : unchecked((uint)ReadInt32(0));
	}

	public int Length => _bytes.Length;

	public byte this[int offset] => _bytes[offset];

	public IReadOnlyList<byte> Bytes => _bytes;

	public ShapeType ShapeType => GetShapeType(_shapeType);

	public GeometryType GeometryType => GetGeometryType(_shapeType);

	public bool HasZ => GetHasZ(_shapeType);
	public bool HasM => GetHasM(_shapeType);
	public bool HasID => GetHasID(_shapeType);
	public bool HasCurves => GetHasCurves(_shapeType);

	public int NumPoints => GetPointCount();

	public int NumParts => GetPartCount();

	public bool IsEmpty => GetIsEmpty();

	public int GetPartStartIndex(int part)
	{
		var geometryType = GeometryType;
		if (geometryType != GeometryType.Polyline &&
		    geometryType != GeometryType.Polygon)
		{
			throw new InvalidOperationException($"{nameof(GeometryType)} must be Polyline or Polygon");
		}

		int numParts = GetPartCount();
		if (part < 0 || part >= numParts)
		{
			throw new ArgumentOutOfRangeException(nameof(part));
		}

		int offset = 44 + part * 4;
		return ReadInt32(offset);
	}

	public void QueryCoords(int globalIndex, out double x, out double y, out double z, out double m, out int id)
	{
		if (globalIndex < 0)
			throw new ArgumentOutOfRangeException(nameof(globalIndex));

		switch (GeometryType)
		{
			case GeometryType.Null:
				x = y = m = double.NaN;
				z = 0.0;
				id = 0;
				return;

			case GeometryType.Point:
				if (globalIndex > 0)
					throw new ArgumentOutOfRangeException(nameof(globalIndex));
				QueryPointCoords(out x, out y, out z, out m, out id);
				return;

			case GeometryType.Multipoint:
				QueryMultipointCoords(globalIndex, out x, out y, out z, out m, out id);
				return;

			case GeometryType.Polyline:
			case GeometryType.Polygon:
				QueryMultipartCoords(globalIndex, out x, out y, out z, out m, out id);
				return;

			case GeometryType.Envelope:
				throw new InvalidOperationException("Envelope does not have coordinates");

			case GeometryType.MultiPatch:
				throw new NotImplementedException();

			case GeometryType.Bag:
				throw new NotSupportedException();

			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	private void QueryPointCoords(out double x, out double y, out double z, out double m, out int id)
	{
		x = ReadDouble(4);
		y = ReadDouble(12);

		int offset = 20;

		if (HasZ)
		{
			z = ReadDouble(offset);
			offset += 8;
		}
		else
		{
			z = DefaultZ;
		}

		if (HasM)
		{
			m = ReadDouble(offset);
			offset += 8;
		}
		else
		{
			m = DefaultM;
		}

		id = HasID ? ReadInt32(offset) : DefaultID;
	}

	private void QueryMultipointCoords(int index, out double x, out double y, out double z, out double m, out int id)
	{
		var numPoints = GetPointCount();
		if (index < 0 || index >= numPoints)
			throw new ArgumentOutOfRangeException(nameof(index));

		int start = 40;
		int offset = start + index * 16;
		x = ReadDouble(offset + 0);
		y = ReadDouble(offset + 8);
		start += numPoints * 16;

		if (HasZ)
		{
			start += 16; // zmin,zmax
			offset = start + index * 8;
			z = ReadDouble(offset);
			start += numPoints * 8;
		}
		else
		{
			z = DefaultZ;
		}

		if (HasM)
		{
			start += 16; // mmin,mmax
			offset = start + index * 8;
			m = ReadDouble(offset);
			start += numPoints * 8;
		}
		else
		{
			m = DefaultM;
		}

		if (HasID)
		{
			offset = start + index * 4;
			id = ReadInt32(offset);
		}
		else
		{
			id = DefaultID;
		}
	}

	private void QueryMultipartCoords(int index, out double x, out double y, out double z, out double m, out int id)
	{
		var numPoints = GetPointCount();
		if (index < 0 || index >= numPoints)
			throw new ArgumentOutOfRangeException(nameof(index));

		var numParts = GetPartCount();

		int start = 44 + numParts * 4;

		int offset = start + index * 16;
		x = ReadDouble(offset + 0);
		y = ReadDouble(offset + 8);

		start += numPoints * 16;

		if (HasZ)
		{
			start += 16; // zmin,zmax
			offset = start + index * 8;
			z = ReadDouble(offset);
			start += numPoints * 8;
		}
		else
		{
			z = DefaultZ;
		}

		if (HasM)
		{
			start += 16; // mmin,mmax
			offset = start + index * 8;
			m = ReadDouble(offset);
			start += numPoints * 8;
		}
		else
		{
			m = DefaultM;
		}

		if (HasID)
		{
			offset = start + index * 4;
			id = ReadInt32(offset);
		}
		else
		{
			id = DefaultID;
		}
	}

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
		WriteWKT(wkt);
		wkt.Flush();
	}

	#region WKT methods

	private void WriteWKT(WKTWriter writer)
	{
		switch (GeometryType)
		{
			case GeometryType.Null:
				break;

			case GeometryType.Point:
				writer.BeginPoint(HasZ, HasM, HasID);
				WritePointCoords(writer, IsEmpty);
				writer.EndShape();
				break;

			case GeometryType.Multipoint:
				writer.BeginMultipoint(HasZ, HasM, HasID);
				WriteMultipointCoords(writer, NumPoints);
				writer.EndShape();
				break;

			case GeometryType.Polyline:
				writer.BeginMultiLineString(HasZ, HasM, HasID);
				WriteMultipartCoords(writer, NumPoints, NumParts);
				writer.EndShape();
				break;

			case GeometryType.Polygon:
				writer.BeginMultiPolygon(HasZ, HasM, HasID);
				WriteMultipartCoords(writer, NumPoints, NumParts);
				writer.EndShape();
				break;

			case GeometryType.Envelope:
				throw new NotImplementedException("Envelope to WKT is not implemented");

			case GeometryType.Any:
				throw new InvalidOperationException("GeometryType Any is invalid for this operation");

			case GeometryType.MultiPatch:
				throw new NotSupportedException("MultiPatch to WKT is not supported");

			case GeometryType.Bag:
				throw new NotSupportedException("GeometryBag to WKT is not supported");

			default:
				throw new InvalidOperationException($"Unknown geometry type: {GeometryType}");
		}
	}

	private void WritePointCoords(WKTWriter writer, bool isEmpty)
	{
		if (isEmpty) return;
		QueryPointCoords(out var x, out var y, out var z, out var m, out var id);
		writer.AddVertex(x, y, z, m, id);
	}

	private void WriteMultipointCoords(WKTWriter writer, int numPoints)
	{
		for (int i = 0; i < numPoints; i++)
		{
			QueryMultipointCoords(i, out var x, out var y, out var z, out var m, out int id);
			writer.AddVertex(x, y, z, m, id);
		}
	}

	private void WriteMultipartCoords(WKTWriter writer, int numPoints, int numParts)
	{
		for (int i = 0, j = 0, k = 0; i < numPoints; i++)
		{
			if (i == k) // first vertex of new part
			{
				writer.NewPart();
				j += 1;
				k = j < numParts ? GetPartStartIndex(j) : int.MaxValue;
			}

			QueryMultipartCoords(i, out var x, out var y, out var z, out var m, out int id);

			writer.AddVertex(x, y, z, m, id);
		}
	}

	#endregion

	public override string ToString()
	{
		var sb = new StringBuilder();

		sb.Append(GeometryType);

		var dim = GetDim(HasZ, HasM, HasID);
		if (!string.IsNullOrEmpty(dim))
		{
			sb.Append(' ');
			sb.Append(dim);
		}

		if (IsEmpty)
		{
			sb.Append(" empty");
		}
		else
		{
			switch (GeometryType)
			{
				case GeometryType.Multipoint:
					sb.Append($", {NumPoints} point{(NumPoints == 1 ? "" : "s")}");
					break;
				case GeometryType.Polyline:
				case GeometryType.Polygon:
					sb.Append($", {NumPoints} point{(NumPoints == 1 ? "" : "s")}");
					sb.Append($", {NumParts} part{(NumParts == 1 ? "" : "s")}");
					if (HasCurves) sb.Append(", curves");
					break;
			}
		}

		return sb.ToString();
	}

	private static string GetDim(bool hasZ, bool hasM, bool hasID)
	{
		int key = 0;

		if (hasZ) key += 1;
		if (hasM) key += 2;
		if (hasID) key += 4;

		switch (key)
		{
			case 1:
				return "Z";
			case 2:
				return "M";
			case 3:
				return "ZM";
			case 4:
				return "ID";
			case 5:
				return "ZID";
			case 6:
				return "MID";
			case 7:
				return "ZMID";
		}

		return string.Empty;
	}

	private bool GetIsEmpty()
	{
		// point: X and Y are NaN
		// multipoint: numPoints is zero
		// multipart: numPoints is zero
		switch (GeometryType)
		{
			case GeometryType.Null:
				return true;
			case GeometryType.Point:
				var x = ReadDouble(4);
				var y = ReadDouble(12);
				return double.IsNaN(x) || double.IsNaN(y);
			case GeometryType.Multipoint:
				return ReadInt32(36) == 0;
			case GeometryType.Polyline:
			case GeometryType.Polygon:
				return ReadInt32(40) == 0;
			case GeometryType.Envelope:
				throw new NotImplementedException(); // TODO find out how empty env is represented
			case GeometryType.Any:
				throw new InvalidOperationException();
			case GeometryType.MultiPatch:
				return ReadInt32(40) == 0;
			case GeometryType.Bag:
				throw new NotImplementedException();
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	private int GetPointCount()
	{
		switch (GeometryType)
		{
			case GeometryType.Null:
				return 0;
			case GeometryType.Point:
				return 1; // even if point is empty (for speed and consistency with Esri)
			case GeometryType.Multipoint:
				return ReadInt32(36);
			case GeometryType.Polyline:
			case GeometryType.Polygon:
				return ReadInt32(40);
			case GeometryType.MultiPatch:
				return ReadInt32(40);
			case GeometryType.Envelope:
				return 0;
			case GeometryType.Any:
				throw new InvalidOperationException();
			case GeometryType.Bag:
				throw new NotImplementedException();
			default:
				throw new NotSupportedException($"Unknown geometry type: {GeometryType}");
		}
	}

	private int GetPartCount()
	{
		switch (GeometryType)
		{
			case GeometryType.Null:
				return 0;
			case GeometryType.Point:
				return 1; // even if point is empty (for speed and consistency with Esri)
			case GeometryType.Multipoint:
				return ReadInt32(36); // point count is also part count
			case GeometryType.Polyline:
			case GeometryType.Polygon:
				return ReadInt32(36);
			case GeometryType.MultiPatch:
				return ReadInt32(36);
			case GeometryType.Envelope:
				return 1;
			case GeometryType.Any:
				throw new InvalidOperationException();
			case GeometryType.Bag:
				throw new NotImplementedException(); // part count at offset 8?
			default:
				throw new NotSupportedException($"Unknown geometry type: {GeometryType}");
		}
	}

	private int ReadInt32(int offset)
	{
		try
		{
			int b0 = _bytes[offset + 0];
			int b1 = _bytes[offset + 1];
			int b2 = _bytes[offset + 2];
			int b3 = _bytes[offset + 3];
			return (b3 << 24) | (b2 << 16) | (b1 << 8) | b0;
		}
		catch (IndexOutOfRangeException)
		{
			throw InvalidShapeBuffer(
				$"Attempt to read an integer (4 bytes) at offset {offset} " +
				$"in a shape buffer of length {_bytes.Length}");
		}
	}

	private double ReadDouble(int offset)
	{
		try
		{
			uint b0 = _bytes[offset + 0];
			uint b1 = _bytes[offset + 1];
			uint b2 = _bytes[offset + 2];
			uint b3 = _bytes[offset + 3];
			uint b4 = _bytes[offset + 4];
			uint b5 = _bytes[offset + 5];
			uint b6 = _bytes[offset + 6];
			uint b7 = _bytes[offset + 7];
			// read bytes into uint; by default, C# casts shifts on byte to int
			ulong lo = (b3 << 24) | (b2 << 16) | (b1 << 8) | b0;
			ulong hi = (b7 << 24) | (b6 << 16) | (b5 << 8) | b4;
			double value = BitConverter.UInt64BitsToDouble((hi << 32) | lo);
			return value < -1E+38 ? double.NaN : value;
		}
		catch (IndexOutOfRangeException)
		{
			throw InvalidShapeBuffer(
				$"Attempt to read a double (8 bytes) at offset {offset} " +
				$"in a shape buffer of length {_bytes.Length}");
		}
	}

	//private static double FromAVNaN(double value) =>
	//	value < -1E+38 ? double.NaN : value;

	//private static double ToAVNaN(double value) =>
	//	double.IsNaN(value) ? double.MinValue : value;

	private static Exception InvalidShapeBuffer(string? message)
	{
		return new FormatException(message ?? "Invalid shape buffer");
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
			case ShapeType.GeometryBag:
				return GeometryType.Bag;
			case ShapeType.Box:
				return GeometryType.Envelope;
			default:
				throw new ArgumentOutOfRangeException(nameof(shapeType), shapeType, null);
		}
	}

	public static bool GetHasZ(uint shapeType)
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

	public static bool GetHasM(uint shapeType)
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

	public static bool GetHasID(uint shapeType)
	{
		return (shapeType & (uint)Flags.HasID) != 0;
	}

	public static bool GetHasCurves(uint shapeType)
	{
		// special case mentioned in Ext Shp Buf Fmt p.4:
		var basicType = (ShapeType)(shapeType & 255);
		var shapeFlags = shapeType & (uint)Flags.ShapeFlagsMask;
		var noModifierBits = shapeFlags == 0;

		if (basicType is ShapeType.GeneralPolygon or ShapeType.GeneralPolyline &&
		    noModifierBits)
		{
			return true; // TODO also check numCurves? often the count is 0
		}

		return (shapeType & (uint)Flags.HasCurves) != 0;
	}

	public static int GetShapeType(uint shapeType, bool hasZ, bool hasM, bool hasID, bool hasCurves = false)
	{
		var generalType = GetGeneralType(shapeType);
		var type = (uint)generalType;
		if (hasZ) type |= (uint)Flags.HasZ;
		if (hasM) type |= (uint)Flags.HasM;
		if (hasID) type |= (uint)Flags.HasID;
		if (hasCurves) type |= (uint)Flags.HasCurves;
		return unchecked((int)type);
	}

	private static ShapeType GetGeneralType(uint shapeType)
	{
		var basicType = (ShapeType)(shapeType & 255);

		switch (basicType)
		{
			case ShapeType.Null:
				return ShapeType.Null;
			case ShapeType.Point:
			case ShapeType.PointZ:
			case ShapeType.PointZM:
			case ShapeType.PointM:
			case ShapeType.GeneralPoint:
				return ShapeType.GeneralPoint;
			case ShapeType.Multipoint:
			case ShapeType.MultipointZ:
			case ShapeType.MultipointZM:
			case ShapeType.MultipointM:
			case ShapeType.GeneralMultipoint:
				return ShapeType.GeneralMultipoint;
			case ShapeType.Polyline:
			case ShapeType.PolylineZ:
			case ShapeType.PolylineZM:
			case ShapeType.PolylineM:
			case ShapeType.GeneralPolyline:
				return ShapeType.GeneralPolyline;
			case ShapeType.Polygon:
			case ShapeType.PolygonZ:
			case ShapeType.PolygonZM:
			case ShapeType.PolygonM:
			case ShapeType.GeneralPolygon:
				return ShapeType.GeneralPolygon;
			case ShapeType.MultiPatch:
			case ShapeType.MultiPatchM:
			case ShapeType.GeneralMultiPatch:
				return ShapeType.GeneralMultiPatch;
			case ShapeType.GeometryBag:
			case ShapeType.Box:
			default:
				throw new InvalidOperationException($"No general type for shape type {basicType}");
		}
	}
}
