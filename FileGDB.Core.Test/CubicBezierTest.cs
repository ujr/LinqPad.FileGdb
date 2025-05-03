using System;
using Xunit;

namespace FileGDB.Core.Test;

public class CubicBezierTest
{
	[Fact]
	public void CanCompute()
	{
		var p0 = new XY(0, 0);  //  1------2
		var p1 = new XY(0, 1);  //  |      |
		var p2 = new XY(1, 1);  //  |      |
		var p3 = new XY(1, 0);  //  0      3

		var p = CubicBezier.Compute(p0, p1, p2, p3, 0.0);
		Assert.Equal(p0, p);

		p = CubicBezier.Compute(p0, p1, p2, p3, 0.5);
		Assert.Equal(new XY(0.5, 0.75), p);

		p = CubicBezier.Compute(p0, p1, p2, p3, 1.0);
		Assert.Equal(p3, p);
	}

	[Fact]
	public void CanComputeQuadratic()
	{
		var p0 = new XY(0, 0);  //  1---2
		var p1 = new XY(0, 1);  //  |
		var p2 = new XY(1, 1);  //  0   +

		var p = CubicBezier.Compute(p0, p1, p2, 0.0);
		Assert.Equal(p0, p);

		p = CubicBezier.Compute(p0, p1, p2, 0.5);
		Assert.Equal(new XY(0.25, 0.75), p);

		p = CubicBezier.Compute(p0, p1, p2, 1.0);
		Assert.Equal(p2, p);
	}

	[Fact]
	public void CanFromQuadratic()
	{
		var q0 = new XY(0, 3);
		var q1 = new XY(3, 1.5);
		var q2 = new XY(6, 0);
		CubicBezier.FromQuadratic(q0, q1, q2, out XY p1, out XY p2);
		Assert.Equal(new XY(2, 2), p1);
		Assert.Equal(new XY(4, 1), p2);
	}

	[Fact]
	public void CanBezierArcLength()
	{
		// values: https://pomax.github.io/bezierinfo/#arclengthapprox
		var p0 = new XY(110, 150);
		var p1 = new XY(25, 190);
		var p2 = new XY(210, 250);
		var p3 = new XY(210, 30);

		// using Jens Gravesen's approach (recursive subdivision):
		Assert.Equal(272.87002978, CubicBezier.Length(p0, p1, p2, p3), precision: 8);

		// the simpler iterative subdivision (requires a step count):
		const int precision = 2; // decimal places
		Assert.Equal(156.20, CubicBezier.Length(p0, p1, p2, p3, 0), precision);
		Assert.Equal(156.20, CubicBezier.Length(p0, p1, p2, p3, 1), precision);
		Assert.Equal(219.16, CubicBezier.Length(p0, p1, p2, p3, 2), precision);
		Assert.Equal(262.70, CubicBezier.Length(p0, p1, p2, p3, 4), precision);
		Assert.Equal(270.26, CubicBezier.Length(p0, p1, p2, p3, 8), precision);
		Assert.Equal(271.72, CubicBezier.Length(p0, p1, p2, p3, 12), precision);
		Assert.Equal(272.70, CubicBezier.Length(p0, p1, p2, p3, 31), precision);
	}

	[Fact]
	public void CanGetExtent()
	{
		var p0 = new XY(0, 0);
		var p1 = new XY(0, 1);
		var p2 = new XY(1, 1);
		var p3 = new XY(1, 0);
		var box = CubicBezier.GetExtent(p0, p1, p2, p3);
		Assert.Equal(0.0, box.XMin);
		Assert.Equal(0.0, box.YMin);
		Assert.Equal(1.0, box.XMax);
		Assert.Equal(0.75, box.YMax);
	}

	[Fact]
	public void CanSplit()
	{
		var p0 = new XY(0, 0);
		var p1 = new XY(0, 1);
		var p2 = new XY(1, 1);
		var p3 = new XY(1, 0);
		const double t = 0.5;
		CubicBezier.Split(p0,p1,p2,p3, t, out XY c11, out XY c12, out XY ps, out XY c21, out XY c22);
		Assert.Equal(new XY(0.00, 0.50), c11);
		Assert.Equal(new XY(0.25, 0.75), c12);
		Assert.Equal(new XY(0.50, 0.75), ps);
		Assert.Equal(new XY(0.75, 0.75), c21);
		Assert.Equal(new XY(1.00, 0.50), c22);

		// edge case: split at t=0 (expect 1st part to be the point p0 and 2nd part to be the original curve)
		CubicBezier.Split(p0, p1, p2, p3, 0.0, out c11, out c12, out ps, out c21, out c22);
		Assert.Equal(p0, c11);
		Assert.Equal(p0, c12);
		Assert.Equal(p0, ps);
		Assert.Equal(p1, c21);
		Assert.Equal(p2, c22);

		// edge case: split at t=1 (expect 1st part to be the original curve and 2nd part to be the point p3)
		CubicBezier.Split(p0, p1, p2, p3, 1.0, out c11, out c12, out ps, out c21, out c22);
		Assert.Equal(p1, c11);
		Assert.Equal(p2, c12);
		Assert.Equal(p3, ps);
		Assert.Equal(p3, c21);
		Assert.Equal(p3, c22);

		// Note: splitting at t<0 and t>0 is allowed but the result is undefined
	}

	[Fact(Skip = "Not yet implemented")]
	public void CanSplit2()
	{
		var p0 = new XY(0, 0);
		var p1 = new XY(0, 1);
		var p2 = new XY(1, 1);
		var p3 = new XY(1, 0);

		CubicBezier.Split(p0, p1, p2, p3, 0.25, 0.75, out XY r0, out XY r1, out XY r2, out XY r3);

		throw new NotImplementedException();
	}

	[Fact]
	public void CanArcTime()
	{
		var p00 = new XY(0, 0);
		var p11 = new XY(1, 1);
		var p22 = new XY(2, 2);
		var p33 = new XY(3, 3);

		// To begin with: a straight line (requires no recursion):
		double t = CubicBezier.GetTime(p00, p11, p22, p33, Math.Sqrt(2));
		Assert.Equal(0.33333333, t, precision: 8);

		// Straight line with uneven speed (still no recursion):
		t = CubicBezier.GetTime(p00, p00, p33, p33, Math.Sqrt(2) * 3 / 2);
		Assert.Equal(0.5, t, precision: 8);
	}

	[Fact]
	public void CanArcTime2()
	{
		var p0 = new XY(0, 0);   //  1--2
		var p1 = new XY(0, 1);   //  0  |
		var p2 = new XY(1, 1);   //     |
		var p3 = new XY(1, -3);  //     3

		// Distance 0 is reached at time 0:
		double t = CubicBezier.GetTime(p0, p1, p2, p3, 0.0);
		Assert.Equal(0.0, t, precision: 8);

		// Distance Length(p) is reached at time 1:
		var length = CubicBezier.Length(p0, p1, p2, p3);
		t = CubicBezier.GetTime(p0, p1, p2, p3, length);
		Assert.Equal(1.0, t, precision: 8);
	}

	[Fact]
	public void CanArcTime3()
	{
		var p0 = new XY(50, 190);
		var p1 = new XY(170, 30);
		var p2 = new XY(30, 30);
		var p3 = new XY(190, 250);

		// B(p0,p1,p2,p3;t) has a cusp near (105,75)

		var length = CubicBezier.Length(p0, p1, p2, p3); // 322.63

		const double dist = 123.45;
		double t = CubicBezier.GetTime(p0, p1, p2, p3, dist); // 0.375

		CubicBezier.Split(p0, p1, p2, p3, t, out var p11, out var p12, out var ps, out var p21, out var p22);

		double len1 = CubicBezier.Length(p0, p11, p12, ps);
		Assert.Equal(dist, len1, precision: 4);

		double len2 = CubicBezier.Length(ps, p21, p22, p3);
		Assert.Equal(length, len1 + len2, precision: 5);
	}

	[Fact]
	public void CanArcTime4()
	{
		var p0 = new XY(170, 150);
		var p1 = new XY(10, 10);
		var p2 = new XY(230, 70);
		var p3 = new XY(70, 170);

		// B(p0,p1,p2,p3;t) has loop (self intersection near (130,110)

		var length = CubicBezier.Length(p0, p1, p2, p3); // 245.93

		for (double d = 0; d < length; d += 10.0)
		{
			double t = CubicBezier.GetTime(p0, p1, p2, p3, d);

			CubicBezier.Split(p0, p1, p2, p3, t, out var p11, out var p12, out var ps, out _, out _);

			var len = CubicBezier.Length(p0, p11, p12, ps);

			// TODO fails with better precision... we should improve GetTime()
			Assert.Equal(d, len, precision: 3);
		}
	}

	[Fact]
	public void CanArcTime5()
	{
		// reusing the curve from the previous test
		var p0 = new XY(170, 150);
		var p1 = new XY(10, 10);
		var p2 = new XY(230, 70);
		var p3 = new XY(70, 170);

		var length = CubicBezier.Length(p0, p1, p2, p3); // 245.93

		// test distance out of range: less than 0 (expect t=0)
		// or greater than arc length (expect t=1)

		double t = CubicBezier.GetTime(p0, p1, p2, p3, -10.0);
		Assert.Equal(0.0, t);

		t = CubicBezier.GetTime(p0, p1, p2, p3, length + 10.0);
		Assert.Equal(1.0, t);
	}

	[Fact]
	public void CanFindRoots()
	{
		// Solutions of the equation a*t*t + b*t + c = 0; there are
		// 0, 1, or 2 solutions, depending on the coefficients a,b,c

		double t1, t2;

		// 0*t*t + 0*t + 0 = 0 is true for all t (but we return NaN)
		CubicBezier.FindRoots(0, 0, 0, out t1, out t2);
		Assert.True(double.IsNaN(t1));
		Assert.True(double.IsNaN(t2));

		// if a=0 this is a linear equation -2t+1=0 and thus t=1/2
		CubicBezier.FindRoots(0, -2, 1, out t1, out t2);
		Assert.Equal(0.5, t1);
		Assert.True(double.IsNaN(t2));

		CubicBezier.FindRoots(1, -1, -1, out t1, out t2);
		double phi = 0.5 * (1.0 + Math.Sqrt(5)); // golden ratio
		Assert.Equal(phi, t1, precision: 9);
		Assert.Equal(-1 / phi, t2, precision: 9);

		CubicBezier.FindRoots(4, -12, -40, out t1, out t2);
		Assert.Equal(5.0, t1, precision: 9);
		Assert.Equal(-2.0, t2, precision: 9);

		CubicBezier.FindRoots(1, -4, 4, out t1, out t2);
		Assert.Equal(2.0, t1, precision: 9);
		Assert.True(double.IsNaN(t2));

		CubicBezier.FindRoots(1, 12, 37, out t1, out t2);
		Assert.True(double.IsNaN(t1));
		Assert.True(double.IsNaN(t2));

		CubicBezier.FindRoots(1, 2, -35, out t1, out t2);
		Assert.Equal(5.0, t1, precision: 9);
		Assert.Equal(-7.0, t2, precision: 9);
	}
}
