using System;
using System.Collections.Generic;
using System.Linq;

namespace FileGDB.Core;

public interface IGeometry
{
}

public class Geometry : IGeometry
{
	public static IGeometry Default { get; } = new Geometry();

	private static double DefaultZ => ShapeBuffer.DefaultZ;

	/// <summary>
	/// Area of the triangle given by the three points, computed using Heron's
	/// formula, see <see href="https://en.wikipedia.org/wiki/Heron%27s_formula"/>.
	/// </summary>
	/// <remarks>Prefer <see cref="SignedArea"/></remarks>
	public static double HeronArea(XY p0, XY p1, XY p2)
	{
		// A = sqrt(s(s-a)(s-b)(s-c)) where a,b,c are side lengths
		// and s = (a+b+c)/2; numerically unstable for small angles.
		// More stable alternative, assuming a >= b >= c:
		// A = 1/4 sqrt((a+(b+c))(c-(a-b))(c+(a-b))(a+(b-c)))

		var a = (p1 - p0).Magnitude;
		var b = (p2 - p1).Magnitude;
		var c = (p0 - p2).Magnitude;

		if (a < b)
		{
			(a, b) = (b, a);
		}

		if (a < c)
		{
			(a, c) = (c, a);
		}

		if (b < c)
		{
			(b, c) = (c, b);
		}

		var t = (a + (b + c)) * (c - (a - b)) * (c + (a - b)) * (a + (b - c));
		var area = 0.25 * Math.Sqrt(t);
		return area;
	}

	/// <summary>
	/// Area of the triangle given by the three points p0, p1, p2;
	/// positive if p0 p1 p2 are ccw, zero if collinear, else negative.
	/// </summary>
	/// <returns>Signed area of the triangle</returns>
	public static double SignedArea(XY p0, XY p1, XY p2)
	{
		var t = (p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y);
		return t / 2;
	}

	/// <summary>
	/// Get the angle at <paramref name="center"/> between
	/// <paramref name="start"/> and <paramref name="end"/>.
	/// The angle is measured counter-clockwise (ccw) or
	/// clockwise (cw) as specified by <param name="wantCW"/>.
	/// </summary>
	/// <returns>The central angle, between 0 and 2 pi</returns>
	public static double CentralAngle(XY start, XY center, XY end, bool wantCW = false)
	{
		var a = start - center;
		var b = end - center;
		var dot = a.X * b.X + a.Y * b.Y;
		var arg = dot / a.Magnitude / b.Magnitude;
		arg = Math.Max(-1, Math.Min(1, arg)); // clamp to -1..1
		var angle = Math.Acos(arg);
		bool isCW = SignedArea(center, start, end) < 0;
		const double twoPi = 2.0 * Math.PI;
		return isCW != wantCW ? twoPi - angle : angle;

		// Alternative approach using atan2:
		//var startAngle = Math.Atan2(a.Y, a.X);
		//var endAngle = Math.Atan2(b.Y, b.X);
		//if (!wantCW && endAngle < startAngle)
		//	endAngle += 2.0 * Math.PI;
		//if (wantCW && endAngle > startAngle)
		//	endAngle -= 2.0 * Math.PI;
		//return endAngle - startAngle;

		// TODO treat start==end as full circle or zero?
	}

	/// <summary>
	/// Compute the radius of the circumcircle of the given triangle,
	/// using the formula R = a*b*c/4A where a,b,c are the lengths of
	/// the sides and A is the area of the triangle.
	/// </summary>
	public static double CircumcircleRadius(XY p0, XY p1, XY p2)
	{
		var a = (p0 - p1).Magnitude;
		var b = (p1 - p2).Magnitude;
		var c = (p2 - p0).Magnitude;
		var area2 = Math.Abs((p1.X - p0.X) * (p2.Y - p0.Y) - (p2.X - p0.X) * (p1.Y - p0.Y));
		return a * b * c / area2 / 2;
	}

	/// <summary>
	/// Given three points, compute center and radius of the circumcircle.
	/// </summary>
	/// <param name="a"></param>
	/// <param name="b"></param>
	/// <param name="c"></param>
	/// <param name="center">Center of circumcircle</param>
	/// <returns>Radius of circumcircle</returns>
	public static double Circumcircle(XY a, XY b, XY c, out XY center)
	{
		// The circle with radius r and center o is given by
		//    (x - o.X)^2 + (y - o.Y)^2 == r^2
		// Plug in the three points a, b, c
		//    (a.X - o.X)^2 + (a.Y - o.Y)^2 == r^2
		//    (b.X - o.X)^2 + (b.Y - o.Y)^2 == r^2
		//    (c.X - o.X)^2 + (c.Y - o.Y)^2 == r^2
		// and solve for o.X, o.Y, r (I did so using Mathics)
		// to get the formula implemented below:

		var aX = a.X;
		var aY = a.Y;
		var bX = b.X;
		var bY = b.Y;
		var cX = c.X;
		var cY = c.Y;

		var nx = aX * aX * bY
		         - aX * aX * cY
		         + aY * aY * bY
		         - aY * aY * cY
		         - aY * bX * bX
		         - aY * bY * bY
		         + aY * cX * cX
		         + aY * cY * cY
		         + bX * bX * cY
		         + bY * bY * cY
		         - bY * cX * cX
		         - bY * cY * cY;

		var ny = aX * aX * bX
		         - aX * aX * cX
		         - aX * bX * bX
		         - aX * bY * bY
		         + aX * cX * cX
		         + aX * cY * cY
		         + aY * aY * bX
		         - aY * aY * cX
		         + bX * bX * cX
		         - bX * cX * cX
		         - bX * cY * cY
		         + bY * bY * cX;

		var d = aX * bY - aX * cY - aY * bX + aY * cX + bX * cY - bY * cX;

		center = new XY(nx / d / 2, -ny / d / 2);

		var r1 = aX * aX - 2 * aX * bX + aY * aY - 2 * aY * bY + bX * bX + bY * bY;
		var r2 = aX * aX - 2 * aX * cX + aY * aY - 2 * aY * cY + cX * cX + cY * cY;
		var r3 = bX * bX - 2 * bX * cX + bY * bY - 2 * bY * cY + cX * cX + cY * cY;

		return Math.Abs(Math.Sqrt(r1 * r2 * r3) / d / 2); // radius
	}
}
