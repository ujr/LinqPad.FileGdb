using System;

namespace FileGDB.Core.Shapes;

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
