namespace FileGDB.Core;

public class Envelope
{
	public double XMin { get; set; }
	public double YMin { get; set; }
	public double XMax { get; set; }
	public double YMax { get; set; }

	public bool HasZ { get; set; }
	public double ZMin { get; set; }
	public double ZMax { get; set; }

	public bool HasM { get; set; }
	public double MMin { get; set; }
	public double MMax { get; set; }

	public Envelope()
	{
		XMin = XMax = double.NaN;
		YMin = YMax = double.NaN;

		ZMin = ZMax = double.NaN;
		MMin = MMax = double.NaN;
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
