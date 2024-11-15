using System.Diagnostics;

namespace FileGDB.Core;

public class GeometryBlobReader
{
	private readonly GeometryDef _geomDef;
	private readonly byte[] _blob;
	private int _blobIndex;

	public GeometryBlobReader(GeometryDef geomDef, byte[]? blob)
	{
		_geomDef = geomDef ?? throw new ArgumentNullException(nameof(geomDef));
		_blob = blob ?? Array.Empty<byte>();
		_blobIndex = 0;
	}

	private double XOrigin => _geomDef.XOrigin;
	private double YOrigin => _geomDef.YOrigin;
	private double XYScale => _geomDef.XYScale;

	private bool HasZ => _geomDef.HasZ;
	private double ZOrigin => _geomDef.ZOrigin;
	private double ZScale => _geomDef.ZScale;

	private bool HasM => _geomDef.HasM;
	private double MOrigin => _geomDef.ZOrigin;
	private double MScale => _geomDef.ZScale;

	public bool EntireBlobConsumed(out int bytesConsumed)
	{
		bytesConsumed = _blobIndex;
		return _blobIndex == _blob.Length;
	}

	/// <summary>
	/// Decode the given File GDB geometry blob into a shape buffer byte array
	/// </summary>
	/// <returns>An Esri Shape Buffer byte array (or null if the given blob is null)</returns>
	public ShapeBuffer? ReadAsShapeBuffer()
	{
		if (_blob.Length < 1)
		{
			return null;
		}

		// first is geometry type:
		var typeValue = ReadVarUnsigned();
		if (typeValue > uint.MaxValue)
			throw Error($"Shape type value too large: {typeValue}");

		var shapeType = ShapeBuffer.GetShapeType(typeValue);

		switch (shapeType)
		{
			case ShapeType.Null:
				return null;
			case ShapeType.Point:
			case ShapeType.PointZ:
			case ShapeType.PointM:
			case ShapeType.PointZM:
			case ShapeType.GeneralPoint:
				return new ShapeBuffer(ReadPoint((uint)typeValue));
			case ShapeType.Multipoint:
			case ShapeType.MultipointZ:
			case ShapeType.MultipointM:
			case ShapeType.MultipointZM:
			case ShapeType.GeneralMultipoint:
				return new ShapeBuffer(ReadMultipoint((uint)typeValue));
			case ShapeType.Polyline:
			case ShapeType.PolylineZ:
			case ShapeType.PolylineM:
			case ShapeType.PolylineZM:
			case ShapeType.GeneralPolyline:
				return new ShapeBuffer(ReadMultipart((uint)typeValue));
			case ShapeType.Polygon:
			case ShapeType.PolygonZ:
			case ShapeType.PolygonM:
			case ShapeType.PolygonZM:
			case ShapeType.GeneralPolygon:
				return new ShapeBuffer(ReadMultipart((uint)typeValue));
			case ShapeType.MultiPatch:
			case ShapeType.MultiPatchM:
			case ShapeType.GeneralMultiPatch:
				throw new NotImplementedException("MultiPatch not yet implemented, sorry");
			case ShapeType.GeometryBag: // can this occur in FGDB at all?
				throw new NotImplementedException("GeometryBag not yet implemented");
		}

		throw Error($"Unknown shape type: {shapeType}");
	}

	private byte[] ReadPoint(uint shapeType)
	{
		// ShapeBuffer (output):
		// - I32 ShapeType
		// - F64 X, Y
		// - if hasZ: F64 Z
		// - if hasM: F64 M
		// - if hasID: I32 ID
		// Empty point: NaN for X and Y, zero(!) for Z if hasZ, NaN for M if hasM, zero for ID if hasID

		bool hasZ = ShapeBuffer.HasZs(shapeType);
		bool hasM = ShapeBuffer.HasMs(shapeType);
		bool hasID = ShapeBuffer.HasIDs(shapeType);

		int length = 4 + 8 + 8;
		if (hasZ) length += 8;
		if (hasM) length += 8;
		if (hasID) length += 4;

		var bytes = new byte[length];
		int offset = 0;

		int type = ShapeBuffer.GetShapeType(shapeType, hasZ, hasM, hasID);
		offset += WriteInt32(type, bytes, offset);

		ulong ix = ReadVarUnsigned();
		double x = ix < 1 ? double.NaN : XOrigin + (ix - 1) / XYScale;
		offset += WriteDouble(x, bytes, offset);

		ulong iy = ReadVarUnsigned();
		double y = iy < 1 ? double.NaN : YOrigin + (iy - 1) / XYScale;
		offset += WriteDouble(y, bytes, offset);

		if (hasZ)
		{
			if (!HasZ)
			{
				throw Error("Geometry BLOB has Z values but GeometryDef has no ZOrigin and ZScale for decoding");
			}

			ulong iz = ReadVarUnsigned();
			double z = iz < 1 ? double.NaN : ZOrigin + (iz - 1) / ZScale;
			offset += WriteDouble(z, bytes, offset);
		}

		if (hasM)
		{
			if (!HasM)
			{
				throw Error("Geometry BLOB has M values but GeometryDef has no MOrigin and MScale for decoding");
			}

			ulong im = ReadVarUnsigned();
			double m = im < 1 ? double.NaN : MOrigin + (im - 1) / MScale;
			offset += WriteDouble(m, bytes, offset);
		}

		if (hasID)
		{
			long v = ReadVarInteger();
			var id = unchecked((int)v);
			offset += WriteInt32(id, bytes, offset);
		}

		Debug.Assert(bytes.Length == offset);

		return bytes;
	}

	private byte[] ReadMultipoint(uint shapeType)
	{
		// ShapeBuffer (output):
		// I32 type
		// D64 xmin,ymin,xmax,ymax
		// I32 numPoints
		// D64[2*numPoints] xy coords
		// if hasZ: D64 zmin,zmax; D64[numPoints] z coords
		// if hasM: D64 mmin,mmax; D64[numPoints] m coords
		// if hasID: I32[numPoints] id values
		// Empty multipoint: 4x NaN for box, 0 for numPoints (total 40 bytes)
		//   plus 2x NaN for Zmin,Zmax if hasZ, plus 2x NaN for Mmin,Mmax if hasM

		bool hasZ = ShapeBuffer.HasZs(shapeType);
		bool hasM = ShapeBuffer.HasMs(shapeType);
		bool hasID = ShapeBuffer.HasIDs(shapeType);

		ulong vu = ReadVarUnsigned();
		if (vu > int.MaxValue)
			throw Error($"Multipoint geometry claims to have {vu} points, which is too big for this API");
		int numPoints = (int)vu;

		int length = 4 + 4 * 8 + 4;
		length += numPoints * (8 + 8);
		if (hasZ) length += 8 + 8 + numPoints * 8;
		if (hasM) length += 8 + 8 + numPoints * 8;
		if (hasID) length += numPoints * 4;

		var bytes = new byte[length];
		int offset = 0;

		int type = ShapeBuffer.GetShapeType(shapeType, hasZ, hasM, hasID);

		offset += WriteInt32(type, bytes, offset);

		ReadBoxXY(out double xmin, out double ymin, out double xmax, out double ymax);

		offset += WriteDouble(xmin, bytes, offset);
		offset += WriteDouble(ymin, bytes, offset);
		offset += WriteDouble(xmax, bytes, offset);
		offset += WriteDouble(ymax, bytes, offset);

		offset += WriteInt32(numPoints, bytes, offset);

		Debug.Assert(40 == offset);

		offset += CopyXYs(numPoints, bytes, offset);

		if (hasZ)
		{
			offset += CopyZs(numPoints, bytes, offset);
		}

		if (hasM)
		{
			offset += CopyMs(numPoints, bytes, offset);
		}

		if (hasID)
		{
			offset += CopyIDs(numPoints, bytes, offset);
		}

		Debug.Assert(bytes.Length == offset);

		return bytes;
	}

	private byte[] ReadMultipart(uint shapeType)
	{
		// ShapeBuffer (output):
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

		// GeometryBlob (input):
		// numPoints (vu)
		// numParts (vu)
		// numCurves (vu) if general type and HasCurves -- TODO or as in Ext Shp Buf?
		// box (4x vu)
		// perPartCounts (numParts-1 vu; num points in last part is not stored)
		// XY coords
		// if hasZ: Z coords (but it seems no min/max)
		// if hasM: M coords (but it seems no min/max)
		// if hasCurves: segment modifiers (can only read sequentially)
		// if hasID: ID values (vi)

		bool hasZ = ShapeBuffer.HasZs(shapeType);
		bool hasM = ShapeBuffer.HasMs(shapeType);
		bool hasCurves = ShapeBuffer.HasCurves(shapeType);
		bool hasID = ShapeBuffer.HasIDs(shapeType);

		ulong vu = ReadVarUnsigned();
		if (vu > int.MaxValue)
			throw Error($"Multipart geometry claims to have {vu} points, which is too big for this API");
		int numPoints = (int)vu;

		vu = ReadVarUnsigned();
		if (vu > (uint)numPoints)
			throw Error($"Multipart geometry claims to have {vu} parts, which is more than it has points ({numPoints})");
		int numParts = (int)vu;

		int numCurves = 0;
		if (hasCurves)
		{
			vu = ReadVarUnsigned();
			if (vu > (uint)numPoints)
				throw Error($"Multipart geometry claims to have {vu} curves but has only {numPoints} points");
			numCurves = (int)vu;
		}

		ReadBoxXY(out double xmin, out double ymin, out double xmax, out double ymax);

		int length = GetMultipartBufferSize(hasZ, hasM, hasID, numPoints, numParts, numCurves);

		var bytes = new byte[length];
		int offset = 0;

		int type = ShapeBuffer.GetShapeType(shapeType, hasZ, hasM, hasID, hasCurves);
		offset += WriteInt32(type, bytes, offset);

		offset += WriteDouble(xmin, bytes, offset);
		offset += WriteDouble(ymin, bytes, offset);
		offset += WriteDouble(xmax, bytes, offset);
		offset += WriteDouble(ymax, bytes, offset);

		offset += WriteInt32(numParts, bytes, offset);
		offset += WriteInt32(numPoints, bytes, offset);

		Debug.Assert(44 == offset);

		int firstPointIndex = 0;
		offset += WriteInt32(firstPointIndex, bytes, offset);
		for (int k = 0; k < numParts - 1; k++)
		{
			vu = ReadVarUnsigned();
			if (vu > (uint)numPoints)
				throw Error($"Multipart geometry claims to have {vu} points in part {k} but only {numPoints} points in total");
			firstPointIndex += (int)vu;
			offset += WriteInt32(firstPointIndex, bytes, offset);
		}

		offset += CopyXYs(numPoints, bytes, offset);

		if (hasZ)
		{
			offset += CopyZs(numPoints, bytes, offset);
		}

		if (hasM)
		{
			offset += CopyMs(numPoints, bytes, offset);
		}

		if (hasCurves)
		{
			offset += CopyCurves(numCurves, bytes, offset);
		}

		if (hasID)
		{
			offset += CopyIDs(numPoints, bytes, offset);
		}

		Debug.Assert(bytes.Length == offset);

		return bytes;
	}

	private int GetMultipartBufferSize(bool hasZ, bool hasM, bool hasID, int numPoints, int numParts, int numCurves)
	{
		int length = 4 + 4 * 8 + 4 + 4; // type, box, numParts, numPoints
		length += numParts * 4; // part index array
		length += numPoints * 16; // xy coords
		if (hasZ) length += 8 + 8 + numPoints * 8; // zmin, zmax, z coords
		if (hasM) length += 8 + 8 + numPoints * 8; // mmin, mmax, m coords
		if (hasID) length += numPoints * 4; // id values (integers)

		if (numCurves > 0)
		{
			// The trouble is that size depends on curve segment type,
			// so we must first read all curve segments before we can
			// compute shape buffer size...

			int restore = _blobIndex;

			// Skip per part counts:
			for (int k = 0; k < numParts - 1; k++)
			{
				ulong dummy = ReadVarUnsigned();
			}

			// Skip XY coordinates:
			for (var j = 0; j < 2 * numPoints; j++)
			{
				long dummy = ReadVarInteger();
			}

			// Skip Z coordinates:
			if (hasZ)
			{
				for (int j = 0; j < numPoints; j++)
				{
					long dummy = ReadVarInteger();
				}
			}

			// Skip M coordinates:
			if (hasM && numPoints > 0)
			{
				long dummy = ReadVarInteger();
				// Cope with special case -2 meaning "all Ms are NaN" (?)
				if (dummy != -2)
				{
					for (int j = 1; j < numPoints; j++)
					{
						_ = ReadVarInteger();
					}
				}
			}

			// to get to the segment modifiers:
			for (int j = 0; j < numCurves; j++)
			{
				var startIndex = ReadVarUnsigned(); // startIndex
				var segmentType = ReadVarUnsigned();

				length += 4 + 4; // startIndex and curveType (both I32)

				var modifierSize = GetSegmentModifierSize(segmentType);

				length += modifierSize;

				_blobIndex += modifierSize;
			}

			// Vertex IDs may follow, but their size is already known

			_blobIndex = restore; // rewind
		}

		return length; // bytes for Shape Buffer
	}

	private int GetSegmentModifierSize(ulong segmentType)
	{
		switch (segmentType)
		{
			case 1: // circular arc
				return 8 + 8 + 4;
			case 2: // linear (must not occur here)
				throw Error($"Segment type {segmentType} (line) should not occur amongst segment modifiers");
			case 3: // spiral arc (undocumented)
				throw Error($"Segment type {segmentType} (spiral arc) is not supported");
			case 4: // cubic bezier arc
				return 8 + 8 + 8 + 8;
			case 5: // elliptic arc
				return 8 + 8 + 8 + 8 + 8 + 4;
			default:
				throw Error($"Unknown segment type {segmentType}");
		}
	}

	private int CopyXYs(int numPoints, byte[] bytes, int offset)
	{
		int startOffset = offset;

		long dx = 0;
		long dy = 0;

		for (int i = 0; i < numPoints; i++)
		{
			// TODO verify NaN encoding

			long ix = ReadVarInteger();
			dx += ix;
			double x = dx < 0 ? double.NaN : XOrigin + dx / XYScale;

			long iy = ReadVarInteger();
			dy += iy;
			double y = dy < 0 ? double.NaN : YOrigin + dy / XYScale;

			offset += WriteDouble(x, bytes, offset);
			offset += WriteDouble(y, bytes, offset);
		}

		return offset - startOffset;
	}

	private int CopyZs(int numPoints, byte[] bytes, int offset)
	{
		if (numPoints > 0 && !HasZ)
		{
			throw Error("Geometry BLOB has Z values but GeometryDef has no ZOrigin and ZScale for decoding");
		}

		int startOffset = offset;

		// Unlike extended shape buffer format, the FGDB seems
		// to not store min and max Z values, so we compute them.
		// Shape Buffer stores Zmin,Zmax (as two NaNs) even if
		// the shape is empty (i.e., if numPoints is zero).

		offset += 16; // leave room for min/max Z

		double zmin = double.MaxValue;
		double zmax = double.MinValue;

		long dz = 0;
		for (int i = 0; i < numPoints; i++)
		{
			long iz = ReadVarInteger();
			dz += iz;

			// TODO check NaN (unsure; cannot enter NaN for Z in Pro UI)
			double z = dz < 0 ? double.NaN : ZOrigin + dz / ZScale;

			offset += WriteDouble(z, bytes, offset);

			if (z < zmin) zmin = z;
			if (z > zmax) zmax = z;
		}

		WriteDouble(numPoints > 0 ? zmin : double.NaN, bytes, startOffset + 0);
		WriteDouble(numPoints > 0 ? zmax : double.NaN, bytes, startOffset + 8);

		return offset - startOffset;
	}

	private int CopyMs(int numPoints, byte[] bytes, int offset)
	{
		if (numPoints > 0 && !HasM)
		{
			throw Error("Geometry BLOB has M values but GeometryDef has no MOrigin and MScale for decoding");
		}

		int startOffset = offset;

		// Unlike extended shape buffer format, the FGDB seems
		// to not store min and max M values, so we compute them.
		// Shape Buffer stores Mmin,Mmax (as two NaNs) even if
		// the shape is empty (i.e., if numPoints is zero).

		offset += 16; // leave room for min/max M

		double mmin = double.PositiveInfinity;
		double mmax = double.NegativeInfinity;

		long dm = 0;
		for (int i = 0; i < numPoints; i++)
		{
			if (dm != -2)
			{
				long im = ReadVarInteger();
				dm += im;
			}

			// -1 means this M is NaN (?)
			// -2 means all (remaining?) Ms are NaN (?)
			// similar thing for Zs? with respect to zero?

			double m = dm < 0 ? double.NaN : MOrigin + dm / MScale;

			offset += WriteDouble(m, bytes, offset);

			if (m < mmin) mmin = m;
			if (m > mmax) mmax = m;
		}

		WriteDouble(double.IsFinite(mmin) ? mmin : double.NaN, bytes, startOffset + 0);
		WriteDouble(double.IsFinite(mmax) ? mmax : double.NaN, bytes, startOffset + 8);

		return offset - startOffset;
	}

	private int CopyIDs(int numPoints, byte[] bytes, int offset)
	{
		int startOffset = offset;

		for (int i = 0; i < numPoints; i++)
		{
			long id = ReadVarInteger();
			offset += WriteInt32(unchecked((int)id), bytes, offset);
		}

		return offset - startOffset;
	}

	private int CopyCurves(int numCurves, byte[] bytes, int offset)
	{
		int startOffset = offset;

		for (int i = 0; i < numCurves; i++)
		{
			ulong vu = ReadVarUnsigned();

			if (vu > int.MaxValue)
				throw Error($"Curve start index {vu} is too big for this API");

			int startIndex = (int)vu;
			offset += WriteInt32(startIndex, bytes, offset);

			ulong curveType = ReadVarUnsigned();

			// TODO just copy bytes!?

			switch (curveType)
			{
				case 1: // circular arc
					var d1 = ReadDouble();
					var d2 = ReadDouble();
					var cFlags = ReadInt32();
					offset += WriteInt32(1, bytes, offset);
					offset += WriteDouble(d1, bytes, offset);
					offset += WriteDouble(d2, bytes, offset);
					offset += WriteInt32(cFlags, bytes, offset);
					break;
				case 2: // linear segment
					throw Error($"Segment type {curveType} (line) should not occur");
				case 3: // spiral arc
					throw Error($"Segment type {curveType} (spiral arc) is not supported");
				case 4: // bezier arc
					var x1 = ReadDouble();
					var y1 = ReadDouble();
					var x2 = ReadDouble();
					var y2 = ReadDouble();
					offset += WriteInt32((int)curveType, bytes, offset);
					offset += WriteDouble(x1, bytes, offset);
					offset += WriteDouble(y1, bytes, offset);
					offset += WriteDouble(x2, bytes, offset);
					offset += WriteDouble(y2, bytes, offset);
					break;
				case 5: // elliptic arc
					var e1 = ReadDouble();
					var e2 = ReadDouble();
					var e3 = ReadDouble();
					var e4 = ReadDouble();
					var e5 = ReadDouble();
					var eFlags = ReadInt32();
					offset += WriteInt32((int)curveType, bytes, offset);
					offset += WriteDouble(e1, bytes, offset);
					offset += WriteDouble(e2, bytes, offset);
					offset += WriteDouble(e3, bytes, offset);
					offset += WriteDouble(e4, bytes, offset);
					offset += WriteDouble(e5, bytes, offset);
					offset += WriteInt32(eFlags, bytes, offset);
					break;
				default:
					throw Error($"Unknown segment type: {curveType}");
			}
		}

		return offset - startOffset;
	}

	private static int WriteInt32(int value, byte[] bytes, int offset)
	{
		if (bytes is null)
			throw new ArgumentNullException(nameof(bytes));
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));
		if (offset + 4 > bytes.Length)
			throw new ArgumentException("overflow", nameof(bytes));

		// little endian
		bytes[offset+0] = (byte)(value & 255);
		bytes[offset+1] = (byte)((value >> 8) & 255);
		bytes[offset+2] = (byte)((value >> 16) & 255);
		bytes[offset+3] = (byte)((value >> 24) & 255);

		return 4; // bytes written
	}

	private static int WriteDouble(double value, byte[] bytes, int offset = 0)
	{
		if (bytes is null)
			throw new ArgumentNullException(nameof(bytes));
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));
		if (offset + 8 > bytes.Length)
			throw new ArgumentException("overflow", nameof(bytes));

		// Empirical: Pro SDK Geometry.ToEsriShape() writes NaN as double.MinValue (FF FF FF FF FF FF EF FF)
		// Disassembly: ShapeBufferHelper methods have a flag "writeTrueNaNs" (controls NaN vs MinValue)

		var bits = BitConverter.DoubleToUInt64Bits(value);

		// little endian
		bytes[offset+0] = (byte)(bits & 255);
		bytes[offset+1] = (byte)((bits >> 8) & 255);
		bytes[offset+2] = (byte)((bits >> 16) & 255);
		bytes[offset+3] = (byte)((bits >> 24) & 255);
		bytes[offset+4] = (byte)((bits >> 32) & 255);
		bytes[offset+5] = (byte)((bits >> 40) & 255);
		bytes[offset+6] = (byte)((bits >> 48) & 255);
		bytes[offset+7] = (byte)((bits >> 56) & 255);

		return 8; // bytes written
	}

	private void ReadBoxXY(out double xmin, out double ymin, out double xmax, out double ymax)
	{
		ulong vu = ReadVarUnsigned();
		xmin = XOrigin + vu / XYScale;

		vu = ReadVarUnsigned();
		ymin = YOrigin + vu / XYScale;

		vu = ReadVarUnsigned();
		xmax = xmin + vu / XYScale;

		vu = ReadVarUnsigned();
		ymax = ymin + vu / XYScale;
	}

	private long ReadVarInteger()
	{
		if (_blobIndex >= _blob.Length)
		{
			throw ReadingBeyondBlob();
		}

		byte b = _blob[_blobIndex++];
		long result = b & 0x3F;
		long sign = (b & 0x40) != 0 ? -1 : 1;
		if ((b & 0x80) == 0) return sign * result;
		int shift = 6; // without continuation bit and sign bit

		while (_blobIndex < _blob.Length)
		{
			b = _blob[_blobIndex++];
			result |= (long)(b & 0x3F) << shift;
			if ((b & 0x80) == 0) return sign * result;
			if (shift <= 49) shift += 7;
			else throw VarIntOverflow();
		}

		throw ReadingBeyondBlob();
	}

	private ulong ReadVarUnsigned()
	{
		ulong result = 0;
		int shift = 0;

		while (_blobIndex < _blob.Length)
		{
			byte b = _blob[_blobIndex++];
			result |= (ulong)(b & 0x3F) << shift;
			if ((b & 0x80) == 0) return result;
			if (shift <= 49) shift += 7;
			else throw VarIntOverflow();
		}

		throw ReadingBeyondBlob();
	}

	private int ReadInt32()
	{
		if (_blobIndex + 4 > _blob.Length)
			throw ReadingBeyondBlob();

		byte b0 = _blob[_blobIndex++];
		byte b1 = _blob[_blobIndex++];
		byte b2 = _blob[_blobIndex++];
		byte b3 = _blob[_blobIndex++];

		// little endian
		return b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
	}

	private double ReadDouble()
	{
		if (_blobIndex + 8 > _blob.Length)
			throw ReadingBeyondBlob();

		byte b0 = _blob[_blobIndex++];
		byte b1 = _blob[_blobIndex++];
		byte b2 = _blob[_blobIndex++];
		byte b3 = _blob[_blobIndex++];
		byte b4 = _blob[_blobIndex++];
		byte b5 = _blob[_blobIndex++];
		byte b6 = _blob[_blobIndex++];
		byte b7 = _blob[_blobIndex++];

		// little endian
		long lo = b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
		long hi = b4 | (b5 << 8) | (b6 << 16) | (b7 << 24);

		return BitConverter.Int64BitsToDouble((hi << 32) | lo);
	}

	private FormatException Error(string? message)
	{
		return new FormatException(message ?? "Malformed geometry BLOB");
	}

	private FormatException ReadingBeyondBlob()
	{
		return Error("Reading beyond the end of the geometry blob");
	}

	private FormatException VarIntOverflow()
	{
		return Error("File GDB Variable Integer overflows a 64bit integer");
	}
}
