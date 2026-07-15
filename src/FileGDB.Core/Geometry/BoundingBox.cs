using System;

namespace FileGDB.Core.Geometry;

/// <summary>
/// Represents a 2D bounding box by its minimum and maximum
/// coordinates in X and Y. Considered empty if any of the
/// X or Y extrema are NaN, or if the maximum is less than
/// the minimum in X or Y.
/// <para/>
/// Optionally also tracks minimum and maximum Z coordinates,
/// but they are ignored when determining emptiness, containment,
/// or intersection.
/// </summary>
public class BoundingBox
{
	public double XMin { get; set; }
	public double YMin { get; set; }
	public double XMax { get; set; }
	public double YMax { get; set; }
	public double ZMin { get; set; }
	public double ZMax { get; set; }

	public BoundingBox()
	{
		XMin = XMax = double.NaN;
		YMin = YMax = double.NaN;
		ZMin = ZMax = double.NaN;
	}

	public BoundingBox(double x, double y) : this()
	{
		Add(x, y);
	}

	public BoundingBox(double x0, double y0, double x1, double y1) : this()
	{
		Add(x0, y0);
		Add(x1, y1);
	}

	public double Width
	{
		get
		{
			var d = XMax - XMin;
			return d >= 0 ? d : 0.0; // clamp neg and NaN to zero
		}
	}

	public double Height
	{
		get
		{
			var d = YMax - YMin;
			return d >= 0 ? d : 0.0; // clamp neg and NaN to zero
		}
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

	public bool Contains(BoundingBox other)
	{
		if (other is null)
			throw new ArgumentNullException(nameof(other));

		// The empty set is a subset of any set,
		// including the empty set itself, therefore:
		if (other.IsEmpty) return true;
		if (IsEmpty) return false;

		return XMin <= other.XMin && other.XMax <= XMax &&
		       YMin <= other.YMin && other.YMax <= YMax;
	}

	public bool Intersects(BoundingBox other)
	{
		if (other is null)
			throw new ArgumentNullException(nameof(other));

		if (IsEmpty || other.IsEmpty) return false;

		return XMin <= other.XMax && XMax >= other.XMin &&
		       YMin <= other.YMax && YMax >= other.YMin;
	}

	public bool Equals(BoundingBox? other)
	{
		if (other is null) return false;
		if (ReferenceEquals(this, other)) return true;

		if (IsEmpty) return other.IsEmpty;

		// ReSharper disable CompareOfFloatsByEqualityOperator
		return other.XMin == XMin && other.YMin == YMin &&
		       other.XMax == XMax && other.YMax == YMax;
		// ReSharper restore CompareOfFloatsByEqualityOperator
	}

	public void Add(double x, double y)
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

	public void AddZ(double z)
	{
		// Notice that if z is NaN, then ZMin and ZMax remain untouched:
		if (z < ZMin || double.IsNaN(ZMin)) ZMin = z;
		if (z > ZMax || double.IsNaN(ZMax)) ZMax = z;
	}
}
