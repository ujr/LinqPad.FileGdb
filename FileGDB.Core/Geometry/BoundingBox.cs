namespace FileGDB.Core.Geometry;

public class BoundingBox
{
	public double XMin { get; set; }
	public double YMin { get; set; }
	public double XMax { get; set; }
	public double YMax { get; set; }

	public BoundingBox()
	{
		XMin = XMax = double.NaN;
		YMin = YMax = double.NaN;
	}

	public bool IsEmpty => double.IsNaN(XMin) || double.IsNaN(YMin) ||
	                       double.IsNaN(XMax) || double.IsNaN(YMax) ||
	                       XMax < XMin || YMax < YMin;

	public bool Contains(double x, double y)
	{
		// If any of the extrema or x or y is NaN, then Contains is false;
		// moreover, if max < min, then Contains is also false; therefore,
		// if this envelope or the given point is empty, Contains is false:
		return XMin <= x && x <= XMax && YMin <= y && y <= YMax;
	}

	public void Expand(double x, double y)
	{
		if (IsEmpty)
		{
			XMin = XMax = x;
			YMin = YMax = y;
		}
		else
		{
			if (x < XMin) XMin = x;
			if (x > XMax) XMax = x;
			if (y < YMin) YMin = y;
			if (y > YMax) YMax = y;
		}
	}
}
