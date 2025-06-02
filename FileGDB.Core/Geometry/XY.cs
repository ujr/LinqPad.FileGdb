using System;

namespace FileGDB.Core.Geometry;

public readonly struct XY : IEquatable<XY>
{
	public readonly double X;
	public readonly double Y;

	public XY(double x, double y)
	{
		X = x;
		Y = y;
	}

	public static XY Empty => new(double.NaN, double.NaN);

	public bool IsEmpty => double.IsNaN(X) || double.IsNaN(Y);

	/// <summary>
	/// The magnitude (length) of this vector.
	/// </summary>
	public double Magnitude => Math.Sqrt(X * X + Y * Y);

	/// <summary>
	/// The angle of this vector in radians, measured counterclockwise
	/// from the positive x-axis, in the range -pi (exclusive) to pi
	/// (inclusive). The angle of a zero vector is (arbitrarily) zero.
	/// </summary>
	public double Angle => Math.Atan2(Y, X);

	/// <returns>A vector with the same direction but unit length;
	/// if this vector has length zero, an empty (NaN) vector</returns>
	public XY Normalized()
	{
		var m = Magnitude;
		return new XY(X / m, Y / m);
	}

	public static XY operator +(XY a, XY b)
	{
		return new XY(a.X + b.X, a.Y + b.Y);
	}

	public static XY operator -(XY a, XY b)
	{
		return new XY(a.X - b.X, a.Y - b.Y);
	}

	public static XY operator *(XY pair, double s)
	{
		return new XY(s * pair.X, s * pair.Y);
	}

	public static XY operator *(double s, XY pair)
	{
		return new XY(s * pair.X, s * pair.Y);
	}

	public static bool operator ==(XY a, XY b)
	{
		// ReSharper disable CompareOfFloatsByEqualityOperator
		bool sameX = a.X == b.X || double.IsNaN(a.X) && double.IsNaN(b.X);
		bool sameY = a.Y == b.Y || double.IsNaN(a.Y) && double.IsNaN(b.Y);
		// ReSharper restore CompareOfFloatsByEqualityOperator

		return sameX && sameY;
	}

	public static bool operator !=(XY a, XY b)
	{
		return !(a == b);
	}

	public static double Distance(XY a, XY b)
	{
		var dx = b.X - a.X;
		var dy = b.Y - a.Y;
		return Math.Sqrt(dx * dx + dy * dy);
	}

	public bool Equals(XY other)
	{
		return other == this;
	}

	public override bool Equals(object? obj)
	{
		return obj is XY other && other == this;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(X, Y);
	}

	public override string ToString()
	{
		return $"X={X}, Y={Y}";
	}
}
