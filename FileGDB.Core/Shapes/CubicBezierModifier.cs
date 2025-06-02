namespace FileGDB.Core.Shapes;

public class CubicBezierModifier : SegmentModifier
{
	public override int CurveType => CurveTypeCubicBezier;

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
		return CubicBezier.ArcLength(startPoint, p1, p2, endPoint);
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
