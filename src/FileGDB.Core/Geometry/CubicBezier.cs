using System;

namespace FileGDB.Core.Geometry;

public static class CubicBezier
{
	/// <returns>The point of the cubic Bézier curve at
	/// the given parameter value <paramref name="t"/></returns>
	public static XY Compute(XY p0, XY p1, XY p2, XY p3, double t)
	{
		var tt = t * t;
		var ttt = tt * t;
		var s = 1 - t;
		var ss = s * s;
		var sss = ss * s;
		return p0 * sss + 3 * p1 * ss * t + 3 * p2 * s * tt + p3 * ttt;
	}

	/// <returns>The point of the quadratic Bézier curve at
	/// the given parameter value <paramref name="t"/></returns>
	public static XY Compute(XY q0, XY q1, XY q2, double t)
	{
		var tt = t * t;
		var s = 1 - t;
		var ss = s * s;
		return q0 * ss + q1 * 2 * s * t + q2 * tt;
	}

	/// <returns>The first derivative at parameter value <paramref name="t"/>
	/// of the cubic Bézier curve given by the four control points.</returns>
	public static XY Derivative(XY p0, XY p1, XY p2, XY p3, double t)
	{
		// B′(t) = 3(1−t)^2 (p1-p0) + 6(1-t)t (p2-p1) + 3t^2 (p3-p2),
		// which is three times the quadratic Bezier of the differences
		// p1-p0, p2-p1, p3-p2
		return 3 * Compute(p1 - p0, p2 - p1, p3 - p2, t);
	}

	/// <returns>The normalized (length 1) tangent vector (first
	/// derivative) at parameter value <paramref name="t"/> of the
	/// cubic Bézier curve given by the four control points.</returns>
	public static XY Tangent(XY p0, XY p1, XY p2, XY p3, double t)
	{
		var d = Compute(p1 - p0, p2 - p1, p3 - p2, t); // B'(t)/3
		var m = Math.Sqrt(d.X * d.X + d.Y * d.Y); // magnitude
		return new XY(d.X / m, d.Y / m); // normalized
	}

	/// <returns>The normalized (length 1) normal vector at parameter
	/// value <paramref name="t"/> of the cubic Bézier curve given by
	/// the four control points.</returns>
	public static XY Normal(XY p0, XY p1, XY p2, XY p3, double t)
	{
		var d = Compute(p1 - p0, p2 - p1, p3 - p2, t); // B'(t)/3
		var m = Math.Sqrt(d.X * d.X + d.Y * d.Y); // magnitude
		return new XY(-d.Y / m, d.X / m); // rotated 90° ccw and normalized
	}

	/// <returns>The centroid (center of gravity) of the cubic Bézier
	/// curve given by the four control points, assuming point density
	/// varies inversely with "traversal speed" as the parameter goes
	/// uniformly from 0 to 1.</returns>
	public static XY Centroid(XY p0, XY p1, XY p2, XY p3)
	{
		// The centroid of a curve is the average of all the points
		// on the curve. The centroid of a parametric curve B(t) over
		// the interval [0,1] is the integral $\int_0^1 B(t) dt$
		// (assuming uniform parameterization!) Computing the integral
		// for the cubic Bézier the result is the average of the four
		// control points, that is:
		return 0.25 * (p0 + p1 + p2 + p3);
	}

	// Alternative implementation for splitting using a triangle matrix.
	// Elegant and probably extends to higher order Bézier curves. But
	// for our cubic Bézier purposes, stick to the specific case only.
	//
	//public static void Split(XY[] orig, double t, XY[] left, XY[] right)
	//{
	//	var temp = new XY[4, 4]; // triangle matrix (consider stackalloc)
	//
	//	for (int i = 0; i < 4; ++i)
	//	{
	//		temp[0, i] = orig[i];
	//	}
	//
	//	double s = 1.0 - t;
	//
	//	for (int i = 1; i <= 3; ++i)
	//	{
	//		for (int j = 0; j <= 3 - i; ++j)
	//		{
	//			temp[i, j] = s * temp[i - 1, j] + t * temp[i - 1, j + 1];
	//		}
	//	}
	//
	//	for (int i = 0; i < 4; ++i)
	//	{
	//		left[i] = temp[i, 0];
	//		right[i] = temp[3 - i, i];
	//	}
	//}

	/// <summary>
	/// Split the given cubic Bézier curve at parameter value <paramref name="t"/>
	/// (between 0 and 1). The resulting two sub curves are cubic Béziers as well,
	/// given by control points p0,p11,p12,ps and by control points ps,p21,p22,p3.
	/// </summary>
	public static void Split(XY p0, XY p1, XY p2, XY p3, double t,
		out XY p11, out XY p12, out XY ps, out XY p21, out XY p22)
	{
		var s = 1 - t;

		var a = s * p0 + t * p1;
		var b = s * p1 + t * p2;
		var c = s * p2 + t * p3;

		var d = s * a + t * b;
		var e = s * b + t * c;

		var f = s * d + t * e;

		p11 = a;
		p12 = d;
		ps = f;
		p21 = e;
		p22 = c;
	}

	/// <summary>
	/// Split the given cubic Bézier curve at the given two parameter values
	/// <paramref name="t1"/> and <paramref name="t2"/>. Return the control
	/// points for the sub-curve in-between. If <paramref name="t1"/> &gt;
	/// <paramref name="t2"/>, the result will be reversed.
	/// </summary>
	public static void Split(XY p0, XY p1, XY p2, XY p3, double t1, double t2,
		out XY r0, out XY r1, out XY r2, out XY r3)
	{
		bool reverse = t1 > t2;

		if (reverse)
		{
			(t1, t2) = (t2, t1);
		}

		if (t1 < 0)
		{
			t1 = 0;
			if (t2 < 0) t2 = 0;
		}

		if (t2 > 1)
		{
			t2 = 1;
			if (t1 > 1) t1 = 1;
		}

		if (Math.Abs(t2 - t1) < double.Epsilon)
		{
			r0 = r1 = r2 = r3 = Compute(p0, p1, p2, p3, t1);
			return;
		}

		// Split original curve at t1, then right part again at adjusted t2:
		// - Split(p0, p1, p2, p3, t1, out _, out _, out r0, out var q1, out var q2)
		// - t2 := (t2 - t1) / (1 - t1)
		// - Split(r0, q1, q2, p3, t2, out r1, out r2, out r3, out _, out _)
		
		var s1 = 1 - t1;
		var a1 = s1 * p0 + t1 * p1;
		var b1 = s1 * p1 + t1 * p2;
		var c1 = s1 * p2 + t1 * p3;
		var d1 = s1 * a1 + t1 * b1;
		var e1 = s1 * b1 + t1 * c1;
		var f1 = s1 * d1 + t1 * e1;
		// Left of t1: B(p0,a1,d1,f1) (ignore)
		// Right of t1: B(f1,e1,c1,p3) (split again below)

		// Adjust t2 to make it relative to right part:
		t2 = (t2 - t1) / (1 - t1);
		// no div 0 here because t1=1 was handled above

		var s2 = 1 - t2;
		var a2 = s2 * f1 + t2 * e1;
		var b2 = s2 * e1 + t2 * c1;
		var c2 = s2 * c1 + t2 * p3;
		var d2 = s2 * a2 + t2 * b2;
		var e2 = s2 * b2 + t2 * c2;
		var f2 = s2 * d2 + t2 * e2;
		// Left of adjusted t2: B(f1,a2,d2,f2) (our result)
		// Right of adjusted t2: B(f2,e2,c2,p3) (ignore)

		r0 = f1;
		r1 = a2;
		r2 = d2;
		r3 = f2;

		if (reverse)
		{
			(r0, r1, r2, r3) = (r3, r2, r1, r0);
		}
	}

	/// <summary>
	/// Split a cubic Bézier at a given distance along the curve.
	/// This is a simple convenience method calling Split using
	/// the parameter value from <see cref="ArcTime"/>.
	/// </summary>
	public static void SplitAtDistance(XY p0, XY p1, XY p2, XY p3, double distance,
		out XY p11, out XY p12, out XY ps, out XY p21, out XY p22)
	{
		var t = ArcTime(p0, p1, p2, p3, distance);

		Split(p0, p1, p2, p3, t, out p11, out p12, out ps, out p21, out p22);
	}

	/// <summary>
	/// Degree elevation from quadratic to cubic: given a quadratic
	/// (2nd order) Bézier curve, find the cubic (3rd order) Bézier
	/// curve that has the same shape. Let the quadratic be defined
	/// by points Q0 Q1 Q2, and the cubic by points P0 P1 P2 P3.
	/// Since the first and last points must be the same, we have
	/// P0 = Q0 and P3 = Q2. This function computes the intermediate
	/// points P1 and P2, given the three points of the quadratic curve.
	/// See https://en.wikipedia.org/wiki/B%C3%A9zier_curve#Degree_elevation
	/// </summary>
	public static void FromQuadratic(XY q0, XY q1, XY q2, out XY p1, out XY p2)
	{
		const double c1 = 1.0 / 3.0;
		const double c2 = 2.0 / 3.0;
		p1 = c1 * q0 + c2 * q1;
		p2 = c2 * q1 + c1 * q2;
	}

	/// <summary>
	/// Compute the arc length of the given cubic Bézier curve by approximating
	/// it with <paramref name="steps"/> line segments, that is, by sampling at
	/// <paramref name="steps"/>+1 points.
	/// </summary>
	public static double ArcLength(XY p0, XY p1, XY p2, XY p3, int steps)
	{
		double length = 0.0;
		double dx, dy;
		XY p = p0, q;

		for (int i = 1; i < steps; i++)
		{
			var t = (double)i / steps;

			// The following lines are Compute(p0,p1,p2,p3,t) inlined:
			var tt = t * t;
			var ttt = tt * t;
			var s = 1 - t;
			var ss = s * s;
			var sss = ss * s;
			q = p0 * sss + 3 * p1 * ss * t + 3 * p2 * s * tt + p3 * ttt;

			dx = q.X - p.X;
			dy = q.Y - p.Y;
			length += Math.Sqrt(dx * dx + dy * dy);
			p = q;
		}

		q = p3;
		dx = q.X - p.X;
		dy = q.Y - p.Y;
		length += Math.Sqrt(dx * dx + dy * dy);

		return length;
	}

	/// <summary>
	/// Compute the arc length of the given cubic Bézier curve to within
	/// <paramref name="maxError"/> of the true value, using a recursive
	/// procedure proposed by Jens Gravesen, as described here:
	/// <see href="https://steve.hollasch.net/cgindex/curves/cbezarclen.html"/>
	/// </summary>
	/// <remarks>If <paramref name="maxError"/> is very tiny, this function
	/// may take a lot of time. Down to 1E-10 is still very fast, though.
	/// Stack depth probably no deeper than 25 for small allowed errors.</remarks>
	public static double ArcLength(XY p0, XY p1, XY p2, XY p3, double maxError = -1)
	{
		if (!(maxError > 0)) maxError = 1E-8; // even with lat/lng in the mm range

		// Recursive length computation based on Jens Gravesen's idea that
		// if L0=|P0P3| (chord length) and L1=|P0P1|+|P1P2|+|P2P3| (scaffold
		// length), then 0.5*(L0+L1) approximates the curve's length, and
		// L1-L0 is a measure of the approximation's error. See
		// https://steve.hollasch.net/cgindex/curves/cbezarclen.html

		double length = 0.0;
		ArcLength(p0, p1, p2, p3, ref length, maxError);
		return length;
	}

	private static void ArcLength(XY p0, XY p1, XY p2, XY p3, ref double length, double maxError)
	{
		double scaffoldLength = 0.0;
		scaffoldLength += XY.Distance(p0, p1);
		scaffoldLength += XY.Distance(p1, p2);
		scaffoldLength += XY.Distance(p2, p3);

		double chordLength = XY.Distance(p0, p3);

		if (scaffoldLength - chordLength > maxError)
		{
			Split(p0, p1, p2, p3, 0.5, out XY p11, out XY p12, out XY ps, out XY p21, out XY p22);
			ArcLength(p0, p11, p12, ps, ref length, maxError);
			ArcLength(ps, p21, p22, p3, ref length, maxError);
		}
		else
		{
			length += 0.5 * (scaffoldLength + chordLength);
		}
	}

	/// <summary>
	/// Get the parameter value (“time”) of the point on the
	/// cubic Bézier curve at the given distance from the start.
	/// </summary>
	/// <remarks>Here we use the same algorithm as for computing
	/// arc length, but we track the time values through recursion
	/// and stop when length reaches the given distance (goal).
	/// <para/>
	/// A Primer on Bézier Curves recommends using a look-up-table:
	/// <see href="https://pomax.github.io/bezierinfo/#tracing"/>
	/// <para/>
	/// MetaPost calls this "arctime dist of path" and computes
	/// it internally with a far more advanced algorithm.
	/// </remarks>
	public static double ArcTime(XY p0, XY p1, XY p2, XY p3, double distance, double maxError = -1)
	{
		if (double.IsNaN(distance)) return double.NaN;
		if (distance <= 0) return 0; // distance 0 is reached at t=0
		if (!(maxError > 0)) maxError = 1E-8; // default precision
		double length = 0.0; // accumulator
		double t = ArcTime(p0, p1, p2, p3, 0.0, 1.0, ref length, distance, maxError);
		if (t > 1.0) t = 1.0; // don't go beyond 1 even if distance > arc length
		return t;
	}

	private static double ArcTime(XY p0, XY p1, XY p2, XY p3, double t0, double t1, ref double length, double goal, double maxError)
	{
		double scaffoldLength = 0.0;
		scaffoldLength += XY.Distance(p0, p1);
		scaffoldLength += XY.Distance(p1, p2);
		scaffoldLength += XY.Distance(p2, p3);

		double chordLength = XY.Distance(p0, p3);

		double t;

		if (scaffoldLength - chordLength > maxError)
		{
			const double half = 0.5;

			Split(p0, p1, p2, p3, half, out XY p11, out XY p12, out XY ps, out XY p21, out XY p22);

			var t01 = half * (t0 + t1); // mid-time

			t = ArcTime(p0, p11, p12, ps, t0, t01, ref length, goal, maxError);

			if (length < goal) // or equivalently: t > 1
			{
				t = ArcTime(ps, p21, p22, p3, t01, t1, ref length, goal, maxError);
			}
		}
		else
		{
			var increment = 0.5 * (scaffoldLength + chordLength);
			var delta = goal - length; // may be negative! what then?
			var ratio = delta / increment; // may be outside [0,1]

			length += increment;
			t = t0 + ratio * (t1 - t0);
		}

		return t;
	}

	/// <summary>
	/// Get the bounding box around the cubic Bézier curve
	/// </summary>
	public static BoundingBox BoundingBox(XY p0, XY p1, XY p2, XY p3)
	{
		// Start with a box spanned by p0,p3 (start and end point):
		// the bounding box cannot be smaller than that; depending
		// on the curve's extrema, it may be grown below
		var extent = new BoundingBox
		{
			XMin = Math.Min(p0.X, p3.X),
			XMax = Math.Max(p0.X, p3.X),
			YMin = Math.Min(p0.Y, p3.Y),
			YMax = Math.Max(p0.Y, p3.Y)
		};

		// If the control points p1 and p2 are within bbox(p0,p3),
		// then the entire curve is within bbox(p0,p3)
		if (extent.Contains(p1.X, p1.Y) && extent.Contains(p2.X, p2.Y))
		{
			return extent;
		}

		// Coefficients of the derivative dB(t)/dt of the cubic B(t)
		// when re-arranged as a polynomial of the form a*tt+b*t+c:
		XY a = 3.0 * (-1.0 * p0 + 3.0 * p1 - 3.0 * p2 + p3);
		XY b = 6.0 * (p0 - 2.0 * p1 + p2);
		XY c = 3.0 * (p1 - p0);

		// Find extrema of the X component of the parametric equation:
		FindRoots(a.X, b.X, c.X, out double t1, out double t2);

		if (t1 is >= 0.0 and <= 1.0) // implies not NaN
		{
			XY p = Compute(p0, p1, p2, p3, t1);
			extent.Expand(p.X, p.Y);
		}

		if (t2 is >= 0.0 and <= 1.0) // implies not NaN
		{
			XY p = Compute(p0, p1, p2, p3, t2);
			extent.Expand(p.X, p.Y);
		}

		// Find extrema of the Y component of the parametric equation:
		FindRoots(a.Y, b.Y, c.Y, out double t3, out double t4);

		if (t3 is >= 0.0 and <= 1.0) // implies not NaN
		{
			XY p = Compute(p0, p1, p2, p3, t3);
			extent.Expand(p.X, p.Y);
		}

		if (t4 is >= 0.0 and <= 1.0) // implies not NaN
		{
			XY p = Compute(p0, p1, p2, p3, t4);
			extent.Expand(p.X, p.Y);
		}

		return extent;
	}

	/// <summary>
	/// Find the roots of a second order polynomial, that is, find the
	/// value(s) of t where a*t*t + b*t + c equals zero. There are 0, 1,
	/// or 2 solutions: if zero solutions, <paramref name="t1"/> and
	/// <paramref name="t2"/> are both NaN, if one solution it is
	/// returned in <paramref name="t1"/> and <paramref name="t2"/>
	/// is NaN, otherwise <paramref name="t1"/> and <paramref name="t2"/>
	/// are the two solutions. Also works for linear equation (a=0).
	/// </summary>
	/// <remarks>Public for unit testing</remarks>
	public static void FindRoots(double a, double b, double c, out double t1, out double t2)
	{
		// a,b,c are the coefficients of the quadratic equation: att+bt+c=0
		// roots are at t={-b\pm\sqrt{b^2-4ac}\over 2a}

		if (Math.Abs(a) < double.Epsilon)
		{
			// quadratic term is zero: cannot use quadratic formula,
			// but we have a linear equation bt+c=0 and therefore:
			t1 = -c / b; // NaN if b is 0
			t2 = double.NaN;
			return;
		}

		double bb = b * b;
		double ac4 = 4.0 * a * c;

		if (bb < ac4)
		{
			// negative sqrt: no solution
			t1 = t2 = double.NaN;
		}
		else if (Math.Abs(bb - ac4) < double.Epsilon)
		{
			// sqrt is zero: one solution
			t1 = -b / (2.0 * a);
			t2 = double.NaN;
		}
		else
		{
			t1 = (-b + Math.Sqrt(bb - ac4)) / (2.0 * a);
			t2 = (-b - Math.Sqrt(bb - ac4)) / (2.0 * a);
		}
	}
}
