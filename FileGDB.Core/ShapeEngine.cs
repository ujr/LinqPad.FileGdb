namespace FileGDB.Core;

#region From Esri

public sealed class CubicBezierSegment
{
	public XY StartPoint { get; }
	public XY EndPoint { get; }
	public double Length { get; } // compute once and cache

	public XY ControlPoint1 { get; }
	public XY ControlPoint2 { get; }

	public CubicBezierSegment(XY start, XY controlPoint1, XY controlPoint2, XY end)
	{
		StartPoint = start;
		ControlPoint1 = controlPoint1;
		ControlPoint2 = controlPoint2;
		EndPoint = end;
	}

	public void SplitAtDistance(
	  double distance,
	  out CubicBezierSegment segment1,
	  out CubicBezierSegment segment2)
	{
		double segmentRatio = Length == 0.0 ? 0.0 : distance / Length;

		int length = 10000;
		int num = (int)(Length / 10.0);
		if (num > 0) length *= num;
		double[] arcLengths = new double[length];
		GetBezierArcLengths(arcLengths);

		double bezierArcLength = MapParameterToBezierArcLength(segmentRatio, arcLengths);

		XY[] v = { StartPoint, ControlPoint1, ControlPoint2, EndPoint };
		var left = new XY[4];
		var right = new XY[4];
		SplitCurve(v, bezierArcLength, left, right);
		var splitPoint = new XY(left[3].X, left[3].Y);
		segment1 = new CubicBezierSegment(StartPoint, left[1], left[2], splitPoint);
		segment2 = new CubicBezierSegment(splitPoint, right[1], right[2], EndPoint);
	}

	private double GetBezierArcLengths(double[] arcLengths)
	{
		int last = arcLengths.Length - 1;
		XY p0 = StartPoint;
		double length = arcLengths[0] = 0.0;
		for (int i = 1; i < last; ++i)
		{
			var t = (double)i / last;
			XY p1 = GetCurvePoint(t);
			length += XY.Distance(p0, p1);
			arcLengths[i] = length;
			p0 = p1;
		}

		length += XY.Distance(p0, EndPoint);
		arcLengths[last] = length;
		return length;
	}

	private double MapParameterToBezierArcLength(double t, double[] arcLengths)
	{
		double length = Length;
		double num1 = arcLengths.Length - 1;
		double key = t * length;
		int index = BinarySearchLargestValueSmallerThan(arcLengths, key);
		double arcLength = arcLengths[index];
		if (arcLength == key)
			return index / num1;
		double num2 = arcLengths[index + 1] - arcLength;
		double num3 = (key - arcLength) / num2;
		return (index + num3) / num1;
	}

	private static int BinarySearchLargestValueSmallerThan(double[] array, double key)
	{
		int lo = 0;
		int hi = array.Length - 1;
		while (lo <= hi)
		{
			int mid = (lo + hi) / 2;
			double input = array[mid];
			if (key == input || input < key && key < array[mid + 1])
				return mid;
			if (key < input)
				hi = mid - 1;
			else
				lo = mid + 1;
		}
		return -1;
	}

	private XY GetCurvePoint(double t)
	{
		double s = 1.0 - t;
		double s2 = s * s;
		double s3 = s2 * s;
		double t2 = t * t;
		double t3 = t2 * t;
		var c0 = StartPoint;
		var c1 = ControlPoint1;
		var c2 = ControlPoint2;
		var c3 = EndPoint;
		return s3 * c0 + 3.0 * s2 * t * c1 + 3.0 * s * t2 * c2 + t3 * c3;
	}

	private static void SplitCurve(XY[] v, double t, XY[] left, XY[] right)
	{
		// This is "bezsplit" from "Schneider's Bezier curve-fitter", so apparently
		// Esri lifted this from https://steve.hollasch.net/cgindex/curves/cbezarclen.html
		// Draw the 4x4 matrix to understand how this Bezier split works

		var matrix = new XY[4, 4];
		for (int index = 0; index < 4; ++index)
			matrix[0, index] = v[index];
		for (int index1 = 1; index1 < 4; ++index1)
		{
			for (int index2 = 0; index2 <= 3 - index1; ++index2)
			{
				matrix[index1, index2] = (1.0 - t) * matrix[index1 - 1, index2] + t * matrix[index1 - 1, index2 + 1];
			}
		}
		for (int index = 0; index < 4; ++index)
		{
			left[index] = matrix[index, 0];
			right[index] = matrix[3 - index, index];
		}
	}
}

#endregion
