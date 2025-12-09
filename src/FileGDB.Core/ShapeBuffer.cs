using System;
using System.Collections.Generic;
using System.Text;

namespace FileGDB.Core;

/// <summary>
/// An Esri Extended Shape Buffer formatted byte array
/// with convenient read-only accessors.
/// </summary>
public class ShapeBuffer
{
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

	public IReadOnlyList<byte> Bytes => _bytes;

	public ShapeType ShapeType => GetShapeType(_shapeType);

	public GeometryType GeometryType => GetGeometryType(_shapeType);

	public bool HasZ => GetHasZ(_shapeType);
	public bool HasM => GetHasM(_shapeType);
	public bool HasID => GetHasID(_shapeType);

	/// <summary>See <see cref="GetMayHaveCurves"/></summary>
	public bool MayHaveCurves => GetMayHaveCurves(_shapeType);

	public int NumPoints => GetPointCount();

	public int NumParts => GetPartCount();

	public int NumCurves => GetCurveCount();

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
		switch (GeometryType)
		{
			case GeometryType.Null:
				x = y = m = double.NaN;
				z = 0.0;
				id = 0;
				return;

			case GeometryType.Point:
				if (globalIndex != 0)
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
				throw new NotSupportedException("Geometry Bag is not supported");

			default:
				throw new NotSupportedException($"Unknown geometry type: {GeometryType}");
		}
	}

	public void QueryPointCoords(out double x, out double y, out double z, out double m, out int id)
	{
		if (GeometryType != GeometryType.Point)
			throw new InvalidOperationException($"{nameof(GeometryType)} is not {nameof(GeometryType.Point)}");

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

	public void QueryMultipointCoords(int index, out double x, out double y, out double z, out double m, out int id)
	{
		if (GeometryType != GeometryType.Multipoint)
			throw new InvalidOperationException($"{nameof(GeometryType)} is not {nameof(GeometryType.Multipoint)}");

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

	public void QueryMultipartCoords(int index, out double x, out double y, out double z, out double m, out int id)
	{
		if (GeometryType != GeometryType.Polyline && GeometryType != GeometryType.Polygon)
			throw new InvalidOperationException(
				$"{nameof(GeometryType)} is not {nameof(GeometryType.Polyline)} and not {GeometryType.Polygon}");

		var numPoints = GetPointCount();
		if (index < 0 || index >= numPoints)
			throw new ArgumentOutOfRangeException(nameof(index));

		var numParts = GetPartCount();
		var mayHaveCurves = GetMayHaveCurves(_shapeType);

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

		if (mayHaveCurves)
		{
			int numCurves = ReadInt32(start);
			start += 4;
			start += SkipCurves(start, numCurves);
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

	private int SkipCurves(int offset, int numCurves)
	{
		int startOffset = offset;

		for (int i = 0; i < numCurves; i++)
		{
			_ = ReadInt32(offset); // segment index
			int curveType = ReadInt32(offset + 4);

			switch (curveType)
			{
				case 1:
					offset += 4 + 4 + 8 + 8 + 4;
					break;
				case 4:
					offset += 4 + 4 + 4 * 8;
					break;
				case 5:
					offset += 4 + 4 + 5 * 8 + 4;
					break;
				default:
					throw InvalidShapeBuffer($"Unknown curve type: {curveType}");
			}
		}

		return offset - startOffset;
	}

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
					if (NumCurves > 0) sb.Append(", curves");
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
				// Envelope has no Shape Buffer representation
				// (Pro's ToEsriShape() writes a 5 vertex Polygon if called on an Envelope)
				throw new InvalidOperationException();
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

	private int GetCurveCount()
	{
		bool mayHaveCurves = GetMayHaveCurves(_shapeType);
		if (!mayHaveCurves) return 0;

		int numParts = NumParts;
		int numPoints = NumPoints;

		int offset = 4 + 32 + 4 + 4; // type, box, numParts, numPoints
		offset += numParts * 4; // part start indices
		offset += numPoints * 16; // XY coords
		if (HasZ) offset += (2 + numPoints) * 8; // zmin, zmax, Z coords
		if (HasM) offset += (2 + numPoints) * 8; // mmin, mmax, M coords
		return ReadInt32(offset);
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
			// Shapefile white paper, page 2: values less than -1E38 are
			// to be considered a "no data" value, which for us means NaN:
			return value < -1E+38 ? double.NaN : value;
		}
		catch (IndexOutOfRangeException)
		{
			throw InvalidShapeBuffer(
				$"Attempt to read a double (8 bytes) at offset {offset} " +
				$"in a shape buffer of length {_bytes.Length}");
		}
	}

	private static Exception InvalidShapeBuffer(string? message)
	{
		return new FormatException(message ?? "Invalid shape buffer");
	}

	#region Public utilities

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
		switch ((ShapeType)(shapeType & (uint)ShapeModifiers.BasicTypeMask))
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
				return (shapeType & (uint)ShapeModifiers.HasZs) != 0U;
			default:
				return false;
		}
	}

	public static bool GetHasM(uint shapeType)
	{
		switch ((ShapeType)(shapeType & (uint)ShapeModifiers.BasicTypeMask))
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
				return (shapeType & (uint)ShapeModifiers.HasMs) != 0U;
			default:
				return false;
		}
	}

	public static bool GetHasID(uint shapeType)
	{
		return (shapeType & (uint)ShapeModifiers.HasIDs) != 0;
	}

	/// <returns>True iff the given shape type can have curves</returns>
	/// <remarks>Curves may only occur in polylines and polygons that have the
	/// HasCurves modifier flag, or in a GeneralPolyline or a GeneralPolygon with
	/// no modifier flag at all. Here "curves" are non-linear segments.</remarks>
	public static bool GetMayHaveCurves(uint shapeType)
	{
		// special case mentioned in Ext Shp Buf Fmt p.4:
		var basicType = (ShapeType)(shapeType & 255);
		var shapeFlags = shapeType & (uint)ShapeModifiers.ModifierMask;
		var noModifierBits = shapeFlags == 0;

		if (basicType is ShapeType.GeneralPolygon or ShapeType.GeneralPolyline &&
		    noModifierBits)
		{
			return true;
		}

		var geomType = GetGeometryType(basicType);
		// only Polyline and Polygon can have curves:
		if (geomType is GeometryType.Polyline or GeometryType.Polygon)
		{
			return (shapeType & (uint)ShapeModifiers.HasCurves) != 0;
		}

		return false;
	}

	public static ShapeType GetShapeType(GeometryType geometryType)
	{
		switch (geometryType)
		{
			case GeometryType.Null:
				return ShapeType.Null;
			case GeometryType.Point:
				return ShapeType.GeneralPoint;
			case GeometryType.Multipoint:
				return ShapeType.GeneralMultipoint;
			case GeometryType.Polyline:
				return ShapeType.GeneralPolyline;
			case GeometryType.Polygon:
				return ShapeType.GeneralPolygon;
			case GeometryType.MultiPatch:
				return ShapeType.GeneralMultiPatch;
			case GeometryType.Bag:
				return ShapeType.GeometryBag;
			case GeometryType.Envelope:
				throw new NotSupportedException("No ShapeType corresponds to GeometryType Envelope");
			case GeometryType.Any:
				throw new ArgumentOutOfRangeException(nameof(geometryType),
					"No ShapeType corresponds to GeometryType Any");
			default:
				throw new ArgumentOutOfRangeException(nameof(geometryType),
					geometryType, $"Unknown geometry type: {geometryType}");
		}
	}

	public static uint GetShapeType(GeometryType geometryType, bool hasZ, bool hasM, bool hasID, bool hasCurves = false)
	{
		var shapeType = (uint) GetShapeType(geometryType);
		if (hasZ) shapeType |= (uint)ShapeModifiers.HasZs;
		if (hasM) shapeType |= (uint)ShapeModifiers.HasMs;
		if (hasID) shapeType |= (uint)ShapeModifiers.HasIDs;
		if (hasCurves) shapeType |= (uint)ShapeModifiers.HasCurves;
		return shapeType;
	}

	public static int GetPointBufferSize(bool hasZ, bool hasM, bool hasID)
	{
		int length = 4 + 8 + 8;
		if (hasZ) length += 8;
		if (hasM) length += 8;
		if (hasID) length += 4;
		return length;
	}

	public static int GetMultipointBufferSize(bool hasZ, bool hasM, bool hasID, int numPoints)
	{
		int length = 4 + 4 * 8 + 4;
		length += numPoints * (8 + 8);
		if (hasZ) length += 8 + 8 + numPoints * 8;
		if (hasM) length += 8 + 8 + numPoints * 8;
		if (hasID) length += numPoints * 4;
		return length;
	}

	/// <remarks>Assumes the shape does not and cannot have curves.
	/// If the shape does or may have curves, the buffer size must
	/// be enlarged accordingly</remarks>
	public static int GetMultipartBufferSize(bool hasZ, bool hasM, bool hasID, int numParts, int numPoints)
	{
		int length = 4 + 4 * 8 + 4 + 4; // type, box, numParts, numPoints
		length += numParts * 4; // part index array
		length += numPoints * 16; // xy coords
		if (hasZ) length += 8 + 8 + numPoints * 8; // zmin, zmax, z coords
		if (hasM) length += 8 + 8 + numPoints * 8; // mmin, mmax, m coords
		if (hasID) length += numPoints * 4; // id values (integers)
		return length;
	}

	/// <summary>
	/// Compare shape types for equality, equating modern "general types"
	/// (e.g. <see cref="Core.ShapeType.GeneralPolygon"/> with HasZ flag)
	/// with equivalent "classic" types (e.g. <see cref="Core.ShapeType.PolygonZ"/>).
	/// </summary>
	public static bool ShapeTypeEqual(uint shapeTypeA, uint shapeTypeB)
	{
		var aType = GetGeneralType(shapeTypeA);
		var bType = GetGeneralType(shapeTypeB);
		if (aType != bType) return false;

		bool aZ = GetHasZ(shapeTypeA);
		bool bZ = GetHasZ(shapeTypeB);
		if (aZ != bZ) return false;

		bool aM = GetHasM(shapeTypeA);
		bool bM = GetHasM(shapeTypeB);
		if (aM != bM) return false;

		bool aID = GetHasID(shapeTypeA);
		bool bID = GetHasID(shapeTypeB);
		if (aID != bID) return false;

		bool aCurves = GetMayHaveCurves(shapeTypeA);
		bool bCurves = GetMayHaveCurves(shapeTypeB);
		if (aCurves != bCurves) return false;

		return true;
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

	#endregion

	#region Writing a Shape Buffer

	public static int WriteShapeType(uint shapeType, byte[] bytes, int offset)
	{
		if (bytes is null)
			throw new ArgumentNullException(nameof(bytes));
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));
		if (offset + 4 > bytes.Length)
			throw new ArgumentException("overflow", nameof(bytes));

		// little endian
		bytes[offset + 0] = (byte)(shapeType & 255);
		bytes[offset + 1] = (byte)((shapeType >> 8) & 255);
		bytes[offset + 2] = (byte)((shapeType >> 16) & 255);
		bytes[offset + 3] = (byte)((shapeType >> 24) & 255);

		return 4; // bytes written
	}

	public static int WriteInt32(int value, byte[] bytes, int offset)
	{
		if (bytes is null)
			throw new ArgumentNullException(nameof(bytes));
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));
		if (offset + 4 > bytes.Length)
			throw new ArgumentException("overflow", nameof(bytes));

		// little endian
		bytes[offset + 0] = (byte)(value & 255);
		bytes[offset + 1] = (byte)((value >> 8) & 255);
		bytes[offset + 2] = (byte)((value >> 16) & 255);
		bytes[offset + 3] = (byte)((value >> 24) & 255);

		return 4; // bytes written
	}

	public static int WriteDouble(double value, byte[] bytes, int offset, bool writeRealNaN = false)
	{
		if (bytes is null)
			throw new ArgumentNullException(nameof(bytes));
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));
		if (offset + 8 > bytes.Length)
			throw new ArgumentException("overflow", nameof(bytes));

		// Empirical: Pro SDK Geometry.ToEsriShape() writes NaN as double.MinValue (FF:FF:FF:FF:FF:FF:EF:FF)
		// ShapeBufferHelper methods have a flag "writeTrueNaNs" (controls NaN vs MinValue), but don't know how this flag is used

		// Shapefile white paper says: Positive infinity, negative infinity,
		// and NaN values are not allowed in shapefiles. [...] "no data" values
		// [...] used only for measures. Any floating point number smaller than
		// –10^38 is considered [...] to represent a "no data" value [page 2].

		if (!writeRealNaN && double.IsNaN(value)) // 00 00 00 00 00 00 F8 FF
		{
			value = double.MinValue; // FF FF FF FF FF FF EF FF
		}

		var bits = BitConverter.DoubleToUInt64Bits(value);

		// little endian
		bytes[offset + 0] = (byte)(bits & 255);
		bytes[offset + 1] = (byte)((bits >> 8) & 255);
		bytes[offset + 2] = (byte)((bits >> 16) & 255);
		bytes[offset + 3] = (byte)((bits >> 24) & 255);
		bytes[offset + 4] = (byte)((bits >> 32) & 255);
		bytes[offset + 5] = (byte)((bits >> 40) & 255);
		bytes[offset + 6] = (byte)((bits >> 48) & 255);
		bytes[offset + 7] = (byte)((bits >> 56) & 255);

		return 8; // bytes written
	}

	#endregion
}
