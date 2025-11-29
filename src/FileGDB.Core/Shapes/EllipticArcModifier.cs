using System;
using FileGDB.Core.Geometry;

namespace FileGDB.Core.Shapes;

public class EllipticArcModifier : SegmentModifier
{
	public override int CurveType => CurveTypeEllipticArc;

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
