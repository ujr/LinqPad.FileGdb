using FileGDB.Core.Geometry;
using System.Collections.Generic;
using Xunit;

namespace FileGDB.Core.Test;

public class CentroidTest
{
	[Fact]
	public void CanCentroidPoints()
	{
		XY c1 = Centroid.Begin.AddPoint(Pt(3, 5)).Result;
		Assert.Equal(3.0, c1.X);
		Assert.Equal(5.0, c1.Y);

		XY c2 = Centroid.Begin
			.AddPoint(Pt(1, 1))
			.AddPoint(Pt(3, 3))
			.AddPoint(Pt(3, 1))
			.Result;
		Assert.Equal(2.333333333, c2.X, precision: 9);
		Assert.Equal(1.666666667, c2.Y, precision: 9);

		XY c3 = Centroid.Begin
			.AddPoint(Pt(1, 1))
			.AddPoint(Pt(3, 3))
			.AddPoint(Pt(1, 1)) // again: double weight
			.AddPoint(Pt(1, 1)) // again: triple weight
			.Result;
		Assert.Equal(1.5, c3.X);
		Assert.Equal(1.5, c3.Y);
	}

	[Fact]
	public void CanCentroidLines()
	{
		XY c1 = Centroid.Begin.AddLine(Pt(1, 1), Pt(3, 3)).Result;
		Assert.Equal(2.0, c1.X);
		Assert.Equal(2.0, c1.Y);

		XY c2 = Centroid.Begin
			.AddLine(Pt(0, 0), Pt(8, 0))
			.AddLine(Pt(8, 0), Pt(8, 4))
			.Result;
		Assert.Equal((8 * 4 + 4 * 8)/12.0, c2.X);
		Assert.Equal((8 * 0 + 4 * 2)/12.0, c2.Y);

		XY c3 = Centroid.Begin
			.AddLine(Pts(0, 0, 8, 0, 8, 4))
			.Result;
		Assert.Equal(5.333333333, c3.X, precision: 9);
		Assert.Equal(0.666666667, c3.Y, precision: 9);

		// Zero-length line still counts as a point:

		XY c4 = Centroid.Begin.AddLine(Pt(2, 2), Pt(2, 2)).Result;
		Assert.Equal(2.0, c4.X);
		Assert.Equal(2.0, c4.Y);

		// Added points have no effect if line exists:

		XY c5 = Centroid.Begin
			.AddLine(Pt(0, 0), Pt(4, 2))
			.AddPoint(Pt(10, 10))
			.Result;
		Assert.Equal(2.0, c5.X);
		Assert.Equal(1.0, c5.Y);
	}

	[Fact]
	public void CanCentroidTriangle()
	{
		// Empty shape has no centroid:
		XY c0 = Centroid.Begin.Result;
		Assert.Equal(double.NaN, c0.X);
		Assert.Equal(double.NaN, c0.Y);

		XY c1 = Centroid.Begin
			.AddTriangle(Pt(0, 0), Pt(1, 0), Pt(0, 1))
			.Result;
		Assert.Equal(0.333333333, c1.X, precision: 9);
		Assert.Equal(0.333333333, c1.Y, precision: 9);

		// Negative orientation, still compute areal centroid:
		XY c2 = Centroid.Begin
			.AddTriangle(Pt(0, 0), Pt(0, 1), Pt(1, 0))
			.Result;
		Assert.Equal(0.333333333, c2.X, precision: 9);
		Assert.Equal(0.333333333, c2.Y, precision: 9);

		// Two triangles, both with positive orientation:
		XY c3 = Centroid.Begin
			.AddTriangle(Pt(0, 0), Pt(1, 0), Pt(0, 1))
			.AddTriangle(Pt(1, 0), Pt(1, 1), Pt(0, 1))
			.Result;
		Assert.Equal(0.5, c3.X, precision: 12);
		Assert.Equal(0.5, c3.Y, precision: 12);

		// Two disjoint triangles, both with positive orientation:
		XY c4 = Centroid.Begin
			.AddTriangle(Pt(0, 0), Pt(1, 0), Pt(0, 1))
			.AddTriangle(Pt(1, 1), Pt(2, 0), Pt(2, 1))
			.Result;
		Assert.Equal(1.0, c4.X, precision: 12);
		Assert.Equal(0.5, c4.Y, precision: 12);

		// Same two triangles, but claim one is negative (a hole):
		// Areas cancel out, but both triangles are used as lines:
		XY c5 = Centroid.Begin
			.AddTriangle(Pt(0, 0), Pt(1, 0), Pt(0, 1), false)
			.AddTriangle(Pt(1, 1), Pt(2, 0), Pt(2, 1), true)
			.Result;
		Assert.Equal(1.0, c5.X, precision: 12);
		Assert.Equal(0.5, c5.Y, precision: 12);

		// Triangle with a "hole" creating a trapezoid (draw a picture):
		var c6 = Centroid.Begin
			.AddTriangle(Pt(0, 0), Pt(1, 0), Pt(0, 1), false)
			.AddTriangle(Pt(0, 0), Pt(0.5, 0), Pt(0, 0.5), true)
			.Result;
		Assert.Equal(7.0 / 18.0, c6.X, precision: 9);
		Assert.Equal(7.0 / 18.0, c6.Y, precision: 9);

		// Degenerate triangle counts as point and thus still has a centroid:
		var c7 = Centroid.Begin
			.AddTriangle(Pt(2, 3), Pt(2, 3), Pt(2, 3))
			.Result;
		Assert.Equal(2.0, c7.X);
		Assert.Equal(3.0, c7.Y);
	}

	[Fact]
	public void CanCentroidPolygon()
	{
		// The unit square

		XY c1 = Centroid.Begin
			.AddPolygon(Pts(0, 0, 1, 0, 1, 1, 0, 1))
			.Result;
		Assert.Equal(0.5, c1.X);
		Assert.Equal(0.5, c1.Y);

		// L-shaped polygon

		XY c2 = Centroid.Begin
			.AddPolygon(Pts(0, 0, 2, 0, 2, 2, 1, 2, 1, 1, 0, 1))
			.Result;
		Assert.Equal(1.166666667, c2.X, precision: 9);
		Assert.Equal(0.833333333, c2.Y, precision: 9);

		// TODO much more: multipart, holes, degenerate, lower dims
	}

	[Fact]
	public void CanSignedArea2()
	{
		// open polygons (must wrap around to v0 after v_{N-1}:

		Assert.Equal(1.0, Centroid.SignedArea2(Pts(0, 0, 1, 0, 0, 1)));
		Assert.Equal(-1.0, Centroid.SignedArea2(Pts(0, 0, 0, 1, 1, 0)));
		Assert.Equal(2.0, Centroid.SignedArea2(Pts(0, 0, 1, 0, 1, 1, 0, 1)));
		Assert.Equal(-2.0, Centroid.SignedArea2(Pts(0, 0, 0, 1, 1, 1, 1, 0)));

		// closed polygons (vN==v0):

		Assert.Equal(1.0, Centroid.SignedArea2(Pts(0, 0, 1, 0, 0, 1, 0, 0)));
		Assert.Equal(-1.0, Centroid.SignedArea2(Pts(0, 0, 0, 1, 1, 0, 0, 0)));
		Assert.Equal(2.0, Centroid.SignedArea2(Pts(0, 0, 1, 0, 1, 1, 0, 1, 0, 0)));
		Assert.Equal(-2.0, Centroid.SignedArea2(Pts(0, 0, 0, 1, 1, 1, 1, 0, 0, 0)));

		// same polygons translated by (10,10):

		Assert.Equal(1.0, Centroid.SignedArea2(Pts(10, 10, 11, 10, 10, 11)));
		Assert.Equal(-1.0, Centroid.SignedArea2(Pts(10, 10, 10, 11, 11, 10)));
		Assert.Equal(2.0, Centroid.SignedArea2(Pts(10, 10, 11, 10, 11, 11, 10, 11)));
		Assert.Equal(-2.0, Centroid.SignedArea2(Pts(10, 10, 10, 11, 11, 11, 11, 10)));

		// same polygons with rolled vertices (still same orientation):

		Assert.Equal(1.0, Centroid.SignedArea2(Pts(1, 0, 0, 1, 0, 0)));
		Assert.Equal(-1.0, Centroid.SignedArea2(Pts(0, 1, 1, 0, 0, 0)));
		Assert.Equal(2.0, Centroid.SignedArea2(Pts(1, 0, 1, 1, 0, 1, 0, 0)));
		Assert.Equal(-2.0, Centroid.SignedArea2(Pts(0, 1, 1, 1, 1, 0, 0, 0)));

		// degenerate polygons:

		Assert.Equal(0.0, Centroid.SignedArea2(Pts())); // empty
		Assert.Equal(0.0, Centroid.SignedArea2(Pts(12, 13))); // point
		Assert.Equal(0.0, Centroid.SignedArea2(Pts(11, 12, 21, 22))); // line
		Assert.Equal(0.0, Centroid.SignedArea2(Pts(1, 0, 2, 0, 3, 0))); // collinear

		// duplicate and collinear vertices:

		Assert.Equal(1.0, Centroid.SignedArea2(Pts(0, 0, 1, 0, 1, 0, 0.5, 0.5, 0, 1, 0, 1)));

		// non-simple should not be used; this one yield zero area:

		Assert.Equal(0.0, Centroid.SignedArea2(Pts(0, 0, 1, 0, 1, 1, 1, 0)));
	}

	private static XY Pt(double x, double y)
	{
		return new XY(x, y);
	}

	private static IList<XY> Pts(params double[] coords)
	{
		int count = coords.Length;
		var list = new List<XY>(count / 2);
		for (int i = 0; i < count; i += 2)
		{
			list.Add(new XY(coords[i], coords[i + 1]));
		}

		return list;
	}
}
