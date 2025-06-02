using System;
using System.Collections.Generic;
using System.Linq;

namespace FileGDB.Core.Shapes;

public class ShapeEngine : IShapeEngine
{
	public static IShapeEngine Default { get; } = new ShapeEngine();

	private static double DefaultZ => ShapeBuffer.DefaultZ;
	private static double DefaultM => ShapeBuffer.DefaultM;
	private static int DefaultID => ShapeBuffer.DefaultID;

	#region Length

	public double GetLength(Shape shape)
	{
		if (shape is BoxShape box)
			return GetLength(box);
		if (shape is MultipartShape multipart)
			return GetLength(multipart);
		return 0; // null or Point or Multipoint
	}

	private static double GetLength(BoxShape? box)
	{
		if (box is null) return 0;
		if (box.IsEmpty) return 0;
		return (box.Width + box.Height) * 2;
	}

	private static double GetLength(MultipartShape? multipart)
	{
		if (multipart is null) return 0;
		if (multipart.IsEmpty) return 0;

		var curves = multipart.Curves;

		double len = 0.0;
		var a = new XY(0, 0);
		int j = 0; // part index
		int c = 0; // segment modifier index

		for (int i = 0; i < multipart.NumPoints; i++)
		{
			int k = multipart.GetPartStart(j);
			if (i == k) // first vertex of new part
			{
				a = multipart.CoordsXY[i];
				j += 1;
			}
			else
			{
				var b = multipart.CoordsXY[i];

				if (c < curves.Count && curves[c].SegmentIndex == i - 1)
				{
					var modifier = curves[c];
					len += GetLength(a, b, modifier);
					c += 1;
				}
				else
				{
					var dx = b.X - a.X;
					var dy = b.Y - a.Y;
					len += Math.Sqrt(dx * dx + dy * dy);
				}

				a = b;
			}
		}

		return len;
	}

	private static double GetLength(XY a, XY b, SegmentModifier? modifier)
	{
		if (modifier is null)
		{
			var dx = a.X - b.X;
			var dy = a.Y - b.Y;
			return Math.Sqrt(dx * dx + dy * dy);
		}

		return modifier.GetLength(a, b);
	}

	#endregion

	#region Area

	public double GetArea(Shape shape)
	{
		if (shape is BoxShape box)
			return GetArea(box);
		if (shape is PolygonShape polygon)
			return GetArea(polygon);
		return 0; // null or point(s) or line(s)
	}

	private static double GetArea(BoxShape? box)
	{
		if (box is null) return 0;
		if (box.IsEmpty) return 0;
		return box.Width * box.Height;
	}

	private static double GetArea(PolygonShape? polygon)
	{
		if (polygon is null) return 0;
		if (polygon.IsEmpty) return 0;

		if (polygon.NumCurves > 0)
			throw new NotImplementedException("Area for polygon with non-linear segments is not yet implemented");

		double area = 0.0;
		var coords = polygon.CoordsXY;

		for (int j = 0; j < polygon.NumParts; j++)
		{
			int i = polygon.GetPartStart(j);
			int n = polygon.GetPartStart(j + 1) - i;

			area += Area2(coords, i, n);
		}

		return area / 2;
	}

	#endregion

	#region Boundary

	public Shape GetBoundary(Shape? shape)
	{
		if (shape is null)
			return null!;
		if (shape is PointShape)
			return Shape.Null;
		if (shape is MultipointShape)
			return Shape.Null;
		if (shape is BoxShape box)
			return GetBoundary(box);
		if (shape is PolylineShape polyline)
			return GetBoundary(polyline);
		if (shape is PolygonShape polygon)
			return GetBoundary(polygon);
		throw new InvalidOperationException($"Unknown shape type {shape.GetType().Name}");
	}

	public PolylineShape GetBoundary(BoxShape? box)
	{
		const ShapeFlags flags = ShapeFlags.None; // sic

		if (box is null)
			return null!;

		if (box.IsEmpty)
			return new PolylineShape(flags, Array.Empty<XY>());

		var xys = new XY[5];
		xys[0] = xys[4] = new XY(box.XMin, box.YMin);
		xys[1] = new XY(box.XMax, box.YMin);
		xys[2] = new XY(box.XMax, box.YMax);
		xys[3] = new XY(box.XMin, box.YMax);

		return new PolylineShape(flags, xys);
	}

	public MultipointShape GetBoundary(PolylineShape? polyline)
	{
		if (polyline is null)
			return null!;

		if (polyline.IsEmpty)
			return new MultipointShape(polyline.Flags, Array.Empty<XY>());

		// All endpoints that are on an odd number of segments are the boundary,
		// those on an even number are (by definition) not on the boundary.
		// E.g. a single ring: start=end is on two segments and thus NOT boundary

		PointShape point;
		var dict = new Dictionary<PointShape, int>(PointComparerXY.Instance);

		var m = polyline.NumParts;
		for (int k = 0; k < m; k++)
		{
			int i = polyline.GetPartStart(k);

			// Start point of this part:
			point = polyline.Points[i];
			if (!dict.TryAdd(point, 1)) dict[point] += 1;

			if (i > 0)
			{
				// End point of previous part:
				point = polyline.Points[i - 1];
				if (!dict.TryAdd(point, 1)) dict[point] += 1;
			}
		}

		// End point of last part:
		point = polyline.Points[polyline.NumPoints - 1];
		if (!dict.TryAdd(point, 1)) dict[point] += 1;

		return new MultipointShape(
			polyline.Flags,
			dict.Where(kvp => kvp.Value % 2 == 1).Select(kvp => kvp.Key).ToList());
	}

	public PolylineShape GetBoundary(PolygonShape? polygon)
	{
		if (polygon is null)
			return null!;

		if (polygon.IsEmpty)
			return new PolylineShape(polygon.Flags, Array.Empty<XY>());

		IReadOnlyList<int> partCounts = polygon.GetPartCounts();

		return new PolylineShape(polygon.Flags, polygon.CoordsXY,
			polygon.CoordsZ, polygon.CoordsM, polygon.CoordsID,
			partCounts, polygon.Curves);
	}

	#endregion

	/// <returns>Twice the signed area of the polygon defined by the given coordinates</returns>
	/// <remarks>Implemented using the "shoelace formula" (see Wikipedia)</remarks>
	public static double Area2(IReadOnlyList<XY> coords, int first, int count)
	{
		// TODO ccw is positive, cw is negative -- should probably be the other way round with Esri shapes
		double a = 0.0;

		int n = coords.Count;
		for (int i = 0; i < n; i++)
		{
			int j = (i + 1) % n;
			a += coords[i].X * coords[j].Y;
			a -= coords[i].Y * coords[j].X;
		}

		return a;
	}

	public PointShape GetCentroid(Shape shape)
	{
		QueryCentroid(shape, out var cx, out var cy);
		return new PointShape(shape.Flags, cx, cy, DefaultZ, DefaultM, DefaultID);
	}

	public void QueryCentroid(Shape? shape, out double cx, out double cy)
	{
		cx = cy = double.NaN;

		if (shape is null) return;
		if (shape.IsEmpty) return;

		if (shape is PointShape point)
		{
			cx = point.X;
			cy = point.Y;
			return;
		}

		if (shape is BoxShape box)
		{
			cx = (box.XMin + box.XMax) / 2;
			cy = (box.YMin + box.YMax) / 2;
			return;
		}

		if (shape is MultipointShape multipoint)
		{
			QueryCentroid(multipoint, out cx, out cy);
			return;
		}

		if (shape is PolylineShape polyline)
		{
			// ?? Esri seems to do centroid of all segment midpoints
			throw new NotImplementedException();
		}

		if (shape is PolygonShape polygon)
		{
			// Polygon: see paper notes
			throw new NotImplementedException();
		}

		throw new NotSupportedException($"Unknown shape type: {shape.GetType().Name}");
	}

	private void QueryCentroid(MultipointShape? multipoint, out double cx, out double cy)
	{
		if (multipoint is null || multipoint.IsEmpty)
		{
			cx = cy = double.NaN;
			return;
		}

		// Centroid is mean of points in multipoint
		// TODO consider Kahan summation for numeric stability

		double sx = 0.0;
		double sy = 0.0;

		var coords = multipoint.CoordsXY;
		for (int i = 0; i < coords.Count; i++)
		{
			sx += coords[i].X;
			sy += coords[i].Y;
		}

		cx = sx / coords.Count;
		cy = sy / coords.Count;
	}

	private static double Mean(double[] data)
	{ // Knuth's TAOCP, also described in https://dassencio.org/68
		double mean = 0.0;
		int n = 0;

		foreach (var x in data)
		{
			n += 1;
			mean += (x - mean) / n;
		}

		return mean;
	}

	private class PointComparerXY : IEqualityComparer<PointShape>
	{
		public bool Equals(PointShape? a, PointShape? b)
		{
			if (a is null && b is null) return true;
			if (a is null || b is null) return false;

			var dx = a.X - b.X;
			var dy = a.Y - b.Y;
			return Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon;
		}

		public int GetHashCode(PointShape? obj)
		{
			if (obj is null) return 0;
			return HashCode.Combine(obj.X, obj.Y);
		}

		public static readonly PointComparerXY Instance = new();
	}
}
