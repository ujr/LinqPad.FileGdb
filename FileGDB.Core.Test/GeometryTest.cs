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
	public void CanEnvelope()
	{
		var env = new Envelope();

		Assert.True(env.IsEmpty);

		Assert.False(env.Contains(0, 0));
		Assert.False(env.Contains(double.NaN, double.NaN));

		env.Expand(0, 0);
		Assert.False(env.IsEmpty);
		Assert.True(env.Contains(0, 0));

		env.Expand(2, -1);
		Assert.False(env.IsEmpty);
		Assert.True(env.Contains(2, -1));
		Assert.False(env.Contains(2, -1.0000001));
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

		const int precision = 7; // decimal places

		Assert.Equal(Math.Sqrt(5), Geometry.CircumcircleRadius(a, b, c), precision);

		var r = Geometry.Circumcircle(a, b, c, out var center);
		Assert.Equal(Math.Sqrt(5), r, precision);
		Assert.Equal(3.0, center.X, precision);
		Assert.Equal(2.0, center.Y, precision);

		// TODO much more: different quadrants, degenerate cases
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
