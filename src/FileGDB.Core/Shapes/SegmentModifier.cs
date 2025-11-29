using System;
using FileGDB.Core.Geometry;

namespace FileGDB.Core.Shapes;

public abstract class SegmentModifier
{
	public int SegmentIndex { get; }
	public abstract int CurveType { get; }

	public const int CurveTypeCircularArc = 1;
	public const int CurveTypeLine = 2; // does not occur (is default, not a modifier)
	public const int CurveTypeSpiral = 3; // undocumented
	public const int CurveTypeCubicBezier = 4;
	public const int CurveTypeEllipticArc = 5;

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
