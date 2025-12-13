using System;
using FileGDB.Core.Shapes;

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
	private double MOrigin => _geomDef.MOrigin;
	private double MScale => _geomDef.MScale;

	public bool EntireBlobConsumed(out int bytesConsumed)
	{
		bytesConsumed = _blobIndex;
		return _blobIndex == _blob.Length;
	}

	/// <summary>
	/// Decode File GDB geometry blob into the given shape builder
	/// </summary>
	public void Read(ShapeBuilder builder)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		if (_blob.Length < 1)
		{
			throw Error("Geometry blob is empty; cannot decode");
		}

		uint typeValue = ReadShapeType();
		var shapeType = ShapeBuffer.GetShapeType(typeValue);

		switch (shapeType)
		{
			case ShapeType.Null:
				ReadNull(builder);
				break;

			case ShapeType.Point:
			case ShapeType.PointZ:
			case ShapeType.PointM:
			case ShapeType.PointZM:
			case ShapeType.GeneralPoint:
				ReadPoint(builder, typeValue);
				break;

			case ShapeType.Multipoint:
			case ShapeType.MultipointZ:
			case ShapeType.MultipointM:
			case ShapeType.MultipointZM:
			case ShapeType.GeneralMultipoint:
				ReadMultipoint(builder, typeValue);
				break;

			case ShapeType.Polyline:
			case ShapeType.PolylineZ:
			case ShapeType.PolylineM:
			case ShapeType.PolylineZM:
			case ShapeType.GeneralPolyline:
				ReadMultipart(builder, typeValue);
				break;

			case ShapeType.Polygon:
			case ShapeType.PolygonZ:
			case ShapeType.PolygonM:
			case ShapeType.PolygonZM:
			case ShapeType.GeneralPolygon:
				ReadMultipart(builder, typeValue);
				break;

			case ShapeType.MultiPatch:
			case ShapeType.MultiPatchM:
			case ShapeType.GeneralMultiPatch:
				throw new NotImplementedException("MultiPatch not yet implemented, sorry");

			case ShapeType.GeometryBag: // can this occur in FGDB at all?
				throw new NotSupportedException("GeometryBag is not supported");

			default:
				throw Error($"Unknown shape type: {shapeType}");
		}
	}

	private static void ReadNull(ShapeBuilder builder)
	{
		builder.Initialize((uint)ShapeType.Null);
	}

	private void ReadPoint(ShapeBuilder builder, uint shapeType)
	{
		var geometryType = ShapeBuffer.GetGeometryType(shapeType);
		if (geometryType != GeometryType.Point)
			throw new ArgumentException("Shape is not a Point", nameof(shapeType));

		bool hasZ = ShapeBuffer.GetHasZ(shapeType);
		bool hasM = ShapeBuffer.GetHasM(shapeType);
		bool hasID = ShapeBuffer.GetHasID(shapeType);

		builder.Initialize(shapeType);

		ulong ix = ReadVarUnsigned();
		double x = ix < 1 ? double.NaN : XOrigin + (ix - 1) / XYScale;

		ulong iy = ReadVarUnsigned();
		double y = iy < 1 ? double.NaN : YOrigin + (iy - 1) / XYScale;

		builder.AddXY(x, y);

		if (hasZ)
		{
			if (!HasZ)
			{
				throw Error("Geometry BLOB has Z values but GeometryDef has no ZOrigin and ZScale for decoding");
			}

			ulong iz = ReadVarUnsigned();
			double z = iz < 1 ? double.NaN : ZOrigin + (iz - 1) / ZScale;

			builder.AddZ(z);
		}

		if (hasM)
		{
			if (!HasM)
			{
				throw Error("Geometry BLOB has M values but GeometryDef has no MOrigin and MScale for decoding");
			}

			ulong im = ReadVarUnsigned();
			double m = im < 1 ? double.NaN : MOrigin + (im - 1) / MScale;

			builder.AddM(m);
		}

		if (hasID)
		{
			long v = ReadVarInteger();
			var id = unchecked((int)v);

			builder.AddID(id);
		}
	}

	private void ReadMultipoint(ShapeBuilder builder, uint shapeType)
	{
		var geometryType = ShapeBuffer.GetGeometryType(shapeType);
		if (geometryType != GeometryType.Multipoint)
			throw new ArgumentException("Shape is not a Multipoint", nameof(shapeType));

		bool hasZ = ShapeBuffer.GetHasZ(shapeType);
		bool hasM = ShapeBuffer.GetHasM(shapeType);
		bool hasID = ShapeBuffer.GetHasID(shapeType);

		ulong vu = ReadVarUnsigned();
		if (vu > int.MaxValue)
			throw Error($"Multipoint geometry claims to have {vu} points, which is too big for this API");
		int numPoints = (int)vu;

		builder.Initialize(shapeType);

		ReadBoxXY(out double xmin, out double ymin, out double xmax, out double ymax);

		builder.SetMinMaxXY(xmin, ymin, xmax, ymax);

		ReadXYs(builder, numPoints);

		if (hasZ)
		{
			ReadZs(builder, numPoints);
		}

		if (hasM)
		{
			ReadMs(builder, numPoints);
		}

		if (hasID)
		{
			ReadIDs(builder, numPoints);
		}
	}

	private void ReadMultipart(ShapeBuilder builder, uint shapeType)
	{
		// GeometryBlob (input):
		// - numPoints (vu)
		// - numParts (vu)
		// - numCurves (vu) if general type and MayHaveCurves -- TODO or as in Ext Shp Buf?
		// - box (4x vu)
		// - perPartCounts (numParts-1 vu; num points in last part is not stored)
		// - XY coords
		// - if hasZ: Z coords (but it seems no min/max)
		// - if hasM: M coords (but it seems no min/max)
		// - if hasCurves: segment modifiers (can only read sequentially)
		// - if hasID: ID values (vi)

		var geometryType = ShapeBuffer.GetGeometryType(shapeType);
		if (geometryType != GeometryType.Polyline && geometryType != GeometryType.Polygon)
			throw new ArgumentException("Shape is not a Polyline or a Polygon", nameof(shapeType));

		bool hasZ = ShapeBuffer.GetHasZ(shapeType);
		bool hasM = ShapeBuffer.GetHasM(shapeType);
		bool hasID = ShapeBuffer.GetHasID(shapeType);
		bool mayHaveCurves = ShapeBuffer.GetMayHaveCurves(shapeType); // TODO unsure if 100% correct for GeomBlob

		ulong vu = ReadVarUnsigned();
		if (vu == 0) return; // empty
		if (vu > int.MaxValue)
			throw Error($"Multipart geometry claims to have {vu} points, which is too big for this API");
		int numPoints = (int)vu;

		vu = ReadVarUnsigned();
		if (vu > (uint)numPoints)
			throw Error($"Multipart geometry claims to have {vu} parts, which is more than it has points ({numPoints})");
		int numParts = (int)vu;

		int numCurves = -1;
		if (mayHaveCurves)
		{
			vu = ReadVarUnsigned();
			if (vu > (uint)numPoints)
				throw Error($"Multipart geometry claims to have {vu} curves but has only {numPoints} points");
			numCurves = (int)vu;
		}

		builder.Initialize(shapeType);

		ReadBoxXY(out double xmin, out double ymin, out double xmax, out double ymax);

		builder.SetMinMaxXY(xmin, ymin, xmax, ymax);

		int pointTally = 0;
		for (int k = 0; k < numParts - 1; k++)
		{
			vu = ReadVarUnsigned();
			if (vu > (uint)numPoints)
				throw Error($"Multipart geometry claims to have {vu} points in part {k} but only {numPoints} points in total");
			// geometry blob stores number of points in part (but omits this for the last part)
			var pointCount = (int)vu;
			pointTally += pointCount;
			builder.AddPart(pointCount);
		}

		builder.AddPart(numPoints - pointTally);

		ReadXYs(builder, numPoints);

		if (hasZ)
		{
			ReadZs(builder, numPoints);
		}

		if (hasM)
		{
			ReadMs(builder, numPoints);
		}

		if (numCurves >= 0)
		{
			ReadCurves(builder, numCurves);
		}

		if (hasID)
		{
			ReadIDs(builder, numPoints);
		}
	}

	private void ReadXYs(ShapeBuilder builder, int numPoints)
	{
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

			builder.AddXY(x, y);
		}
	}

	private void ReadZs(ShapeBuilder builder, int numPoints)
	{
		if (numPoints > 0 && !HasZ)
		{
			throw Error("Geometry BLOB has Z values but GeometryDef has no ZOrigin and ZScale for decoding");
		}

		// Unlike extended shape buffer format, the FGDB seems
		// to not store min and max Z values, so we compute them.
		// Shape Buffer stores Zmin,Zmax (as two NaNs) even if
		// the shape is empty (i.e., if numPoints is zero).

		double zmin = double.MaxValue;
		double zmax = double.MinValue;

		long dz = 0;
		for (int i = 0; i < numPoints; i++)
		{
			long iz = ReadVarInteger();
			dz += iz;

			// TODO check NaN (unsure; cannot enter NaN for Z in Pro UI)
			double z = dz < 0 ? double.NaN : ZOrigin + dz / ZScale;

			builder.AddZ(z);

			if (z < zmin) zmin = z;
			if (z > zmax) zmax = z;
		}

		if (numPoints > 0)
		{
			builder.SetMinMaxZ(zmin, zmax);
		}
	}

	private void ReadMs(ShapeBuilder builder, int numPoints)
	{
		if (numPoints > 0 && !HasM)
		{
			throw Error("Geometry BLOB has M values but GeometryDef has no MOrigin and MScale for decoding");
		}

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

			builder.AddM(m);

			if (m < mmin) mmin = m;
			if (m > mmax) mmax = m;
		}

		if (double.IsFinite(mmin) && double.IsFinite(mmax))
		{
			builder.SetMinMaxM(mmin, mmax);
		}
	}

	private void ReadIDs(ShapeBuilder builder, int numPoints)
	{
		for (int i = 0; i < numPoints; i++)
		{
			long id = ReadVarInteger();
			builder.AddID(unchecked((int)id));
		}
	}

	private void ReadCurves(ShapeBuilder builder, int numCurves)
	{
		for (int i = 0; i < numCurves; i++)
		{
			ulong vu = ReadVarUnsigned();

			if (vu > int.MaxValue)
				throw Error($"Curve start index {vu} is too big for this API");
			int startIndex = (int)vu;
			
			ulong segTypeValue = ReadVarUnsigned();
			var segmentType = (SegmentType)(segTypeValue & 255);

			switch (segmentType)
			{
				case SegmentType.CircularArc:
					var d1 = ReadDouble();
					var d2 = ReadDouble();
					var cFlags = ReadInt32();
					builder.AddCurve(new CircularArcModifier(startIndex, d1, d2, cFlags));
					break;
				case SegmentType.StraightLine:
					throw Error($"Segment type {segmentType} (line) should not occur");
				case SegmentType.Spiral:
					throw Error($"Segment type {segmentType} (spiral arc) is not supported");
				case SegmentType.CubicBezier:
					var x1 = ReadDouble();
					var y1 = ReadDouble();
					var x2 = ReadDouble();
					var y2 = ReadDouble();
					builder.AddCurve(new CubicBezierModifier(startIndex, x1, y1, x2, y2));
					break;
				case SegmentType.EllipticArc:
					var e1 = ReadDouble();
					var e2 = ReadDouble();
					var e3 = ReadDouble();
					var e4 = ReadDouble();
					var e5 = ReadDouble();
					var eFlags = ReadInt32();
					builder.AddCurve(new EllipticArcModifier(startIndex, e1, e2, e3, e4, e5, eFlags));
					break;
				default:
					throw Error($"Unknown segment type: {segTypeValue}");
			}
		}
	}

	private uint ReadShapeType()
	{
		ulong value = ReadVarUnsigned();
		if (value > uint.MaxValue)
			throw Error($"Shape type value too large: {value}");
		return (uint)value;
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
			result |= (long)(b & 0x7F) << shift;
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
			result |= (ulong)(b & 0x7F) << shift;
			if ((b & 0x80) == 0) return result;
			if (shift <= 49) shift += 7;
			else throw VarIntOverflow();
		}

		throw ReadingBeyondBlob();
	}

	private int ReadInt32()
	{
		if (_blobIndex + 4 > _blob.Length)
		{
			throw ReadingBeyondBlob();
		}

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
		{
			throw ReadingBeyondBlob();
		}

		// read bytes into uint (C# casts shifts on byte to (signed) int)
		uint b0 = _blob[_blobIndex++];
		uint b1 = _blob[_blobIndex++];
		uint b2 = _blob[_blobIndex++];
		uint b3 = _blob[_blobIndex++];
		uint b4 = _blob[_blobIndex++];
		uint b5 = _blob[_blobIndex++];
		uint b6 = _blob[_blobIndex++];
		uint b7 = _blob[_blobIndex++];

		// little endian
		ulong lo = b0 | (b1 << 8) | (b2 << 16) | (b3 << 24);
		ulong hi = b4 | (b5 << 8) | (b6 << 16) | (b7 << 24);

		return BitConverter.UInt64BitsToDouble((hi << 32) | lo);
	}

	private FileGDBException Error(string? message)
	{
		return new FileGDBException(message ?? "Malformed geometry BLOB");
	}

	private FileGDBException ReadingBeyondBlob()
	{
		return Error("Reading beyond the end of the geometry blob");
	}

	private FileGDBException VarIntOverflow()
	{
		return Error("File GDB VarInt overflows a 64bit integer");
	}
}
