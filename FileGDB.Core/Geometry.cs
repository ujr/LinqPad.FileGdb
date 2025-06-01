using System;
using System.Collections.Generic;
using System.Linq;

namespace FileGDB.Core;

public interface IGeometry
{
	double GetLength(Shape shape);
	double GetArea(Shape shape);
	void QueryCentroid(Shape shape, out double cx, out double cy);
	PointShape GetCentroid(Shape shape);
	Shape GetBoundary(Shape shape);
	PolylineShape GetBoundary(BoxShape box);
	PolylineShape GetBoundary(PolygonShape polygon);
	MultipointShape GetBoundary(PolylineShape polyline);
}

public class Geometry : IGeometry
{
	public static IGeometry Default { get; } = new Geometry();

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

	#region Basics

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
	/// <param name="a">Point on circle</param>
	/// <param name="b">Point on circle</param>
	/// <param name="c">Point on circle</param>
	/// <param name="center">Center of circumcircle</param>
	/// <returns>Radius of circumcircle</returns>
	/// <remarks>For collinear points the circumcircle does not exist:
	/// return an empty center point and an infinite radius.</remarks>
	public static double Circumcircle(XY a, XY b, XY c, out XY center)
	{
		// Following a solution described at StackOverflow:
		// https://stackoverflow.com/questions/52990094/calculate-circle-given-3-points-code-explanation
		// Less computation and better results (less round-off) than my old solution below

		var aux = b.X * b.X + b.Y * b.Y;
		var ab = (a.X * a.X + a.Y * a.Y - aux) / 2;
		var bc = (aux - c.X * c.X - c.Y * c.Y) / 2;
		var det = (a.X - b.X) * (b.Y - c.Y) - (b.X - c.X) * (a.Y - b.Y);

		if (Math.Abs(det) < 1e-10)
		{
			center = XY.Empty;
			return double.PositiveInfinity;
		}

		double cx = (ab * (b.Y - c.Y) - bc * (a.Y - b.Y)) / det;
		double cy = ((a.X - b.X) * bc - (b.X - c.X) * ab) / det;

		double radius = Math.Sqrt((cx - a.X) * (cx - a.X) + (cy - a.Y) * (cy - a.Y));

		center = new XY(cx, cy);
		return radius;
	}

	/// <summary>
	/// Given three points, compute center and radius of the circumcircle.
	/// </summary>
	/// <param name="a">Point on circle</param>
	/// <param name="b">Point on circle</param>
	/// <param name="c">Point on circle</param>
	/// <param name="center">Center of circumcircle</param>
	/// <returns>Radius of circumcircle</returns>
	/// <remarks>For collinear points the circumcircle does not exist:
	/// return an empty center point and an infinite radius.</remarks>
	[Obsolete("Use the other method here; this one is less precise")]
	public static double CircumcircleOld(XY a, XY b, XY c, out XY center)
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

	#endregion
}
