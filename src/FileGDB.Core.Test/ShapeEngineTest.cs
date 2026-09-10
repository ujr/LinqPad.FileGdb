using System;
using System.Collections.Generic;
using FileGDB.Core.Geometry;
using FileGDB.Core.Shapes;
using Xunit;

namespace FileGDB.Core.Test;

/// <summary>
/// Unit tests for the default <see cref="IShapeEngine"/> implementation
/// (with OGC conventions for ring orientation / area computation).
/// </summary>
public class ShapeEngineTest
{
	private IShapeEngine Engine => ShapeEngine.Default;

	[Fact]
	public void CanGetLength()
	{
		const int precision = 9;

		Assert.Equal(0.0, Engine.GetLength(Shape.Null));
		Assert.Equal(0.0, Engine.GetLength(Box()));
		Assert.Equal(4.0, Engine.GetLength(Box(0, 0, 1, 1)));
		Assert.Equal(0.0, Engine.GetLength(Point()));
		Assert.Equal(0.0, Engine.GetLength(Point(1.2, 3.4)));
		Assert.Equal(0.0, Engine.GetLength(Multipoint()));
		Assert.Equal(0.0, Engine.GetLength(Multipoint(0, 0, 1, 1)));
		Assert.Equal(0.0, Engine.GetLength(Polyline()));
		Assert.Equal(Math.Sqrt(2.0), Engine.GetLength(Polyline(0, 0, 1, 1)), precision);
		Assert.Equal(4.0, Engine.GetLength(Polyline(0, 0, 1, 0, 1, 1, NewPart, 0, 2, 2, 2)), precision);
		Assert.Equal(0.0, Engine.GetLength(Polyline(5, 5, 5, 5, 5, 5)));
		Assert.Equal(0.0, Engine.GetLength(Polygon()));
		Assert.Equal(4.0, Engine.GetLength(Polygon(0, 0, 1, 0, 1, 1, 0, 1, 0, 0)));
		// Polygons are NOT automatically closed for length computation:
		Assert.Equal(4.0, Engine.GetLength(Polygon(0, 0, 1, 0, 1, 1, NewPart, 1, 0, 2, 0, 2, 1)));
		// TODO Polyline/Polygon with non-linear segments!
	}

	[Fact]
	public void CanGetArea()
	{
		Assert.Equal(0.0, Engine.GetArea(Shape.Null));
		Assert.Equal(0.0, Engine.GetArea(Box()));
		Assert.Equal(1.0, Engine.GetArea(Box(0, 0, 1, 1)));
		Assert.Equal(0.0, Engine.GetArea(Point()));
		Assert.Equal(0.0, Engine.GetArea(Point(1.2, 3.4)));
		Assert.Equal(0.0, Engine.GetArea(Multipoint()));
		Assert.Equal(0.0, Engine.GetArea(Multipoint(0, 0, 1, 1)));
		Assert.Equal(0.0, Engine.GetArea(Polyline()));
		Assert.Equal(0.0, Engine.GetArea(Polyline(0, 0, 1, 1, 2, 0)));

		Assert.Equal(0.0, Engine.GetArea(Polygon()));

		Assert.Equal(1.0, Engine.GetArea(Polygon(0, 0, 1, 0, 1, 1, 0, 1, 0, 0)));

		// Polygons are implicitly closed with a straight line for area computation:
		Assert.Equal(1.0, Engine.GetArea(Polygon(0, 0, 1, 0, 1, 1, 0, 1)));

		// Polygon with two disjoint parts:
		Assert.Equal(5.0, Engine.GetArea(Polygon(0, 0, 1, 0, 1, 1, 0, 1, 0, 0, NewPart, 2, 2, 4, 2, 4, 4, 2, 4, 2, 2)));

		// Polygon with a hole (orientation is important):
		Assert.Equal(8.0, Engine.GetArea(Polygon(0, 0, 3, 0, 3, 3, 0, 3, 0, 0, NewPart, 1, 1, 1, 2, 2, 2, 2, 1, 1, 1)));

		// Polygon with an "additive hole" (GetArea does not simplify the shape):
		Assert.Equal(10.0, Engine.GetArea(Polygon(0, 0, 3, 0, 3, 3, 0, 3, 0, 0, NewPart, 1, 1, 2, 1, 2, 2, 1, 2, 1, 1)));

		// TODO Polyline/Polygon with non-linear segments! (not yet implemented --> densify? assume straight? full math?)
	}

	[Fact]
	public void CanGetCentroid()
	{
		// Centroid computation itself is tested in a CentroidTest.
		// Focus here is on ShapeEngine and Shape classes.

		var c0 = Engine.GetCentroid(Shape.Null);
		Assert.NotNull(c0);
		Assert.True(c0.IsEmpty);

		var c1 = Engine.GetCentroid(Box());
		Assert.NotNull(c1);
		Assert.True(c1.IsEmpty);

		var c2 = Engine.GetCentroid(Box(0, 0, 1, 1));
		Assert.NotNull(c2);
		Assert.False(c2.IsEmpty);
		Assert.Equal(0.5, c2.X);
		Assert.Equal(0.5, c2.Y);

		var c3 = Engine.GetCentroid(Point());
		Assert.NotNull(c3);
		Assert.True(c3.IsEmpty);

		var c4 = Engine.GetCentroid(Point(1.2, 3.4));
		Assert.NotNull(c4);
		Assert.False(c4.IsEmpty);
		Assert.Equal(1.2, c4.X);
		Assert.Equal(3.4, c4.Y);

		var c5 = Engine.GetCentroid(Multipoint());
		Assert.NotNull(c5);
		Assert.True(c5.IsEmpty);

		var c6 = Engine.GetCentroid(Multipoint(0, 0, 1, 1));
		Assert.NotNull(c6);
		Assert.False(c6.IsEmpty);
		Assert.Equal(0.5, c6.X);
		Assert.Equal(0.5, c6.Y);

		// repeated point in multipoint (gets more weight)
		var c7 = Engine.GetCentroid(Multipoint(0, 0, 3, 3, 0, 0));
		Assert.NotNull(c7);
		Assert.False(c7.IsEmpty);
		Assert.Equal(1.0, c7.X);
		Assert.Equal(1.0, c7.Y);

		var c8 = Engine.GetCentroid(Polyline());
		Assert.NotNull(c8);
		Assert.True(c8.IsEmpty);

		var c9 = Engine.GetCentroid(Polyline(0, 0, 1, 1, 2, 0));
		Assert.NotNull(c9);
		Assert.False(c9.IsEmpty);
		Assert.Equal(1.0, c9.X);
		Assert.Equal(0.5, c9.Y);

		var c10 = Engine.GetCentroid(Polygon());
		Assert.NotNull(c10);
		Assert.True(c10.IsEmpty);

		var c11 = Engine.GetCentroid(Polygon(0, 0, 1, 0, 1, 1, 0, 1, 0, 0));
		Assert.NotNull(c11);
		Assert.False(c11.IsEmpty);
		Assert.Equal(0.5, c11.X);
		Assert.Equal(0.5, c11.Y);

		// multipart polygon
		var c12 = Engine.GetCentroid(Polygon(0, 0, 1, 0, 1, 1, 0, 1, 0, 0, NewPart, 2, 2, 4, 2, 4, 4, 2, 4, 2, 2));
		Assert.NotNull(c12);
		Assert.False(c12.IsEmpty);
		// .5 .5 with weight 1 and 3 3 with weight 4 gives 2.5 2.5
		Assert.Equal(2.5, c12.X);
		Assert.Equal(2.5, c12.Y);
	}

	[Fact]
	public void CanGetBox()
	{
		var nullBox = Engine.GetBox(Shape.Null);
		Assert.NotNull(nullBox);
		Assert.True(nullBox.IsEmpty);

		Assert.True(Engine.GetBox(Box()).IsEmpty);

		var boxBox = Engine.GetBox(Box(0, 1, 4, 3));
		Assert.NotNull(boxBox);
		Assert.Equal(0, boxBox.XMin);
		Assert.Equal(1, boxBox.YMin);
		Assert.Equal(4, boxBox.XMax);
		Assert.Equal(3, boxBox.YMax);
		Assert.False(boxBox.IsEmpty);

		// The box of an empty point is empty:
		Assert.True(Engine.GetBox(Point()).IsEmpty);

		// The box of a non-empty point is a box:
		var point = Point(1.2, 3.4);
		var pointBox = Engine.GetBox(point);
		Assert.Equal(point.X, pointBox.XMin);
		Assert.Equal(point.Y, pointBox.YMin);
		Assert.Equal(point.X, pointBox.XMax);
		Assert.Equal(point.Y, pointBox.YMax);
		Assert.False(pointBox.IsEmpty);

		// The box of an empty multipoint is empty:
		Assert.True(Engine.GetBox(Multipoint()).IsEmpty);

		var multipointBox = Engine.GetBox(Multipoint(0, 0, 3, 3, 0, 0));
		Assert.Equal(0, multipointBox.XMin);
		Assert.Equal(0, multipointBox.YMin);
		Assert.Equal(3, multipointBox.XMax);
		Assert.Equal(3, multipointBox.YMax);
		Assert.False(multipointBox.IsEmpty);

		Assert.True(Engine.GetBox(Polyline()).IsEmpty);

		var lineBox = Engine.GetBox(Polyline(0, 0, 1, 1, 2, 0));
		Assert.Equal(0, lineBox.XMin);
		Assert.Equal(0, lineBox.YMin);
		Assert.Equal(2, lineBox.XMax);
		Assert.Equal(1, lineBox.YMax);
		Assert.False(lineBox.IsEmpty);
		Assert.Equal(2.0, lineBox.Width);
		Assert.Equal(1.0, lineBox.Height);

		Assert.True(Engine.GetBox(Polygon()).IsEmpty);

		var polyBox = Engine.GetBox(Polygon(0, 0, 1, 0, 1, 1, 0, 1, 0, 0));
		Assert.Equal(0, polyBox.XMin);
		Assert.Equal(0, polyBox.YMin);
		Assert.Equal(1, polyBox.XMax);
		Assert.Equal(1, polyBox.YMax);
		Assert.False(polyBox.IsEmpty);

		// multipart polygon
		var multiBox = Engine.GetBox(Polygon(0, 0, 1, 0, 1, 1, 0, 1, 0, 0, NewPart, 2, 2, 4, 2, 4, 4, 2, 4, 2, 2));
		Assert.Equal(0, multiBox.XMin);
		Assert.Equal(0, multiBox.YMin);
		Assert.Equal(4, multiBox.XMax);
		Assert.Equal(4, multiBox.YMax);
		Assert.False(multiBox.IsEmpty);
	}

	[Fact]
	public void SelfCheckCanCollect()
	{
		Collect([], out var xys0, out var parts0);
		Assert.Empty(xys0);
		Assert.Equal(1, parts0.Count);
		Assert.Equal(0, parts0[0]); // one empty part

		Collect([1, 2], out var xys1, out var parts1);
		Assert.Equal(1, xys1.Count);
		Assert.Equal(new XY(1,2), xys1[0]);
		Assert.Equal(1, parts1.Count);
		Assert.Equal(1, parts1[0]);

		Collect([1, 2, 3, 4, NewPart, 5, 6, 7, 8], out var xys2, out var parts2);
		Assert.Equal(4, xys2.Count);
		Assert.Equal(new XY(1, 2), xys2[0]);
		Assert.Equal(new XY(3, 4), xys2[1]);
		Assert.Equal(new XY(5, 6), xys2[2]);
		Assert.Equal(new XY(7, 8), xys2[3]);
		Assert.Equal(2, parts2.Count);
		Assert.Equal(2, parts2[0]);
		Assert.Equal(2, parts2[1]);

		// empty parts:
		Collect([NewPart, 1, 2, NewPart, NewPart, 3, 4, NewPart], out var xys3, out var parts3);
		Assert.Equal(2, xys3.Count);
		Assert.Equal(new XY(1, 2), xys3[0]);
		Assert.Equal(new XY(3, 4), xys3[1]);
		Assert.Equal(5, parts3.Count);
		Assert.Equal(0, parts3[0]);
		Assert.Equal(1, parts3[1]);
		Assert.Equal(0, parts3[2]);
		Assert.Equal(1, parts3[3]);
		Assert.Equal(0, parts3[4]);
	}

	#region Test shape factory

	private static PointShape Point()
	{
		return new PointShape(double.NaN, double.NaN);
	}

	private static PointShape Point(double x, double y)
	{
		return new PointShape(x, y);
	}

	private static BoxShape Box()
	{
		return new BoxShape(ShapeFlags.None, double.NaN, double.NaN, double.NaN, double.NaN);
	}

	private static BoxShape Box(double xmin, double ymin, double xmax, double ymax)
	{
		return new BoxShape(ShapeFlags.None, xmin, ymin, xmax, ymax);
	}

	private static MultipointShape Multipoint()
	{
		return new MultipointShape(ShapeFlags.None, Array.Empty<XY>());
	}

	private static MultipointShape Multipoint(params double[] coords)
	{
		if (coords is null)
			throw new ArgumentNullException(nameof(coords));
		if (coords.Length % 2 != 0)
			throw new ArgumentException("Coordinates must be in pairs of X and Y", nameof(coords));
		var xys = new XY[coords.Length / 2];
		for (int i = 0; i < xys.Length; i++)
		{
			xys[i] = new XY(coords[i * 2], coords[i * 2 + 1]);
		}
		return new MultipointShape(ShapeFlags.None, xys);
	}

	private static PolylineShape Polyline()
	{
		return new PolylineShape(ShapeFlags.None, Array.Empty<XY>());
	}

	/// <param name="coords">Alternating x and y coordinate values;
	/// <see cref="double.NaN"/> starts a new part</param>
	private static PolylineShape Polyline(params double[] coords)
	{
		Collect(coords, out var xys, out var parts);

		return new PolylineShape(ShapeFlags.None, xys, partVertexCounts: parts);
	}

	private static PolygonShape Polygon()
	{
		return new PolygonShape(ShapeFlags.None, Array.Empty<XY>());
	}

	/// <param name="coords">Alternating x and y coordinate values;
	/// <see cref="double.NaN"/> starts a new part</param>
	private static PolygonShape Polygon(params double[] coords)
	{
		Collect(coords, out var xys, out var parts);

		return new PolygonShape(ShapeFlags.None, xys, partVertexCounts: parts);
	}

	private static double NewPart => double.NaN;

	private static void Collect(double[] coords, out List<XY> xys, out List<int> parts)
	{
		if (coords is null)
			throw new ArgumentNullException(nameof(coords));

		double? x = null;
		xys = new List<XY>();
		parts = new List<int>();
		var tally = 0;

		foreach (var value in coords)
		{
			if (double.IsNaN(value))
			{
				// start new part

				if (x.HasValue)
				{
					throw new ArgumentException("Coordinates must be in pairs of X and Y", nameof(coords));
				}

				parts.Add(xys.Count - tally);
				tally = xys.Count;
			}
			else if (x is null)
			{
				x = value;
			}
			else
			{
				xys.Add(new XY(x.Value, value));
				x = null;
			}
		}

		if (x.HasValue)
		{
			throw new ArgumentException("Coordinates must be in pairs of X and Y", nameof(coords));
		}

		parts.Add(xys.Count - tally);
	}

	#endregion
}
