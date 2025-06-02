using System;
using Xunit;

namespace FileGDB.Core.Test;

public class GeometryTest
{
	[Fact]
	public void CanEmptyXY()
	{
		Assert.True(XY.Empty.IsEmpty);
		Assert.True(double.IsNaN(XY.Empty.Magnitude));
	}

	[Fact]
	public void CanNormalizedXY()
	{
		var xy = new XY(1, 1); // magnitude is sqrt 2
		Assert.Equal(1.0, xy.Normalized().Magnitude, precision: 9);

		var zero = new XY(0, 0); // magnitude is zero
		Assert.True(zero.Normalized().IsEmpty);
	}

	[Fact]
	public void CanAngleXY()
	{
		Assert.Equal(0.0, new XY(12.34, 0.0).Angle);
		Assert.Equal(Math.PI / 4.0, new XY(1.0, 1.0).Angle);
		Assert.Equal(Math.PI, new XY(-1.0, 0.0).Angle);
		Assert.Equal(-Math.PI / 2.0, new XY(0.0, -1.0).Angle);

		// Special case: zero vector has angle zero (arbitrarily)
		Assert.Equal(0.0, new XY(0.0, 0.0).Angle);

		// Special case: empty vector has angle NaN
		Assert.True(double.IsNaN(XY.Empty.Angle));
	}

	[Fact]
	public void CanBoundingBox()
	{
		var box = new BoundingBox();

		Assert.True(box.IsEmpty);

		Assert.False(box.Contains(0, 0));
		Assert.False(box.Contains(double.NaN, double.NaN));

		box.Expand(0, 0);
		Assert.False(box.IsEmpty);
		Assert.True(box.Contains(0, 0));

		box.Expand(2, -1);
		Assert.False(box.IsEmpty);
		Assert.True(box.Contains(2, -1));
		Assert.False(box.Contains(2, -1.0000001));
	}

	[Fact]
	public void CanSignedAndHeronArea()
	{
		var a = new XY(1, 1);
		var b = new XY(2, 4);
		var c = new XY(5, 3);

		const int precision = 7; // decimal places

		Assert.Equal(5.0, Geometry.HeronArea(a, b, c), precision);
		Assert.Equal(5.0, Geometry.HeronArea(c, b, a), precision);

		Assert.Equal(-5.0, Geometry.SignedArea(a, b, c), precision);
		Assert.Equal(5.0, Geometry.SignedArea(c, b, a), precision);
	}

	[Fact]
	public void CanCircumcircle()
	{
		// Data and expected results from
		// https://math.stackexchange.com/questions/213658/get-the-equation-of-a-circle-when-given-3-points

		var a = new XY(1, 1);
		var b = new XY(2, 4);
		var c = new XY(5, 3);
		// Circumference: radius=sqrt(5), center=(3,2)

		const int precision = 9; // decimal places

		Assert.Equal(Math.Sqrt(5), Geometry.CircumcircleRadius(a, b, c), precision);

		var r = Geometry.Circumcircle(a, b, c, out var center);
		Assert.Equal(Math.Sqrt(5), r, precision);
		Assert.Equal(3.0, center.X, precision);
		Assert.Equal(2.0, center.Y, precision);

		r = Geometry.CircumcircleOld(a, b, c, out center);
		Assert.Equal(Math.Sqrt(5), r, precision);
		Assert.Equal(3.0, center.X, precision);
		Assert.Equal(2.0, center.Y, precision);

		// Collinear points (circumcircle does not exist):
		r = Geometry.Circumcircle(new XY(1, 1), new XY(2, 2), new XY(4, 4), out center);
		Assert.True(double.IsInfinity(r) && center.IsEmpty);

		// All three points the same (circumcircle does not exist):
		r = Geometry.Circumcircle(a, a, a, out center);
		Assert.True(double.IsInfinity(r) && center.IsEmpty);

		// TODO different quadrants
	}

	[Fact]
	public void CanCentralAngle()
	{
		var a = new XY(2, 1);
		var b = new XY(1, 2);
		var o = new XY(0, 0);

		const int precision = 4; // decimal places

		Assert.Equal(0.6435, Geometry.CentralAngle(a, o, b), precision);
		Assert.Equal(5.6397, Geometry.CentralAngle(a, o, b, true), precision);
		Assert.Equal(5.6397, Geometry.CentralAngle(b, o, a), precision);
		Assert.Equal(0.6435, Geometry.CentralAngle(b, o, a, true), precision);


		Assert.Equal(0.0, Geometry.CentralAngle(a, o, a), 9);
		Assert.Equal(2 * Math.PI, Geometry.CentralAngle(a, o, a, true), 9);

		// TODO much more: different quadrants, degenerate cases
	}
}
