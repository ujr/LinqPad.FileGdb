using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace FileGDB.Core.Shapes;

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
