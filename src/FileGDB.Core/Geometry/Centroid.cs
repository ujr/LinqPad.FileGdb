using System;
using System.Collections.Generic;

namespace FileGDB.Core.Geometry;

/// <summary>
/// Compute the centroid (center of gravity) of geometries.
/// </summary>
/// <remarks>For areas, use the algorithm described by Paul Bourke;
/// for lines, average segment midpoints weighted by segment length;
/// for points, use the average. Can add mixed dimension geometries;
/// areas trump lines, and lines trump points. A degenerate area may
/// still count as a line, and a degenerate line as a point.
/// Paul Bourke's algorithm for polygons in Python:
/// <code>
/// def get_centroid(vertices):
///   n = len(vertices)
///   signed_area = 0
///   Cx, Cy = 0, 0
///	  
///   for i in range(n) :
///     x0, y0 = vertices[i]
///     x1, y1 = vertices[(i + 1) % n]
///     A = x0* y1 - x1* y0
///     signed_area += A
///     Cx += (x0 + x1) * A
///     Cy += (y0 + y1) * A
///   
///   signed_area *= 0.5
///   Cx /= (6 * signed_area)
///   Cy /= (6 * signed_area)
///   return Cx, Cy
/// </code>
/// </remarks>
/// <seealso href="https://paulbourke.net/geometry/polygonmesh/"/>
/// <seealso href="https://en.wikipedia.org/wiki/Centroid"/>
public class Centroid
{
	private CompSum _pointSumX;
	private CompSum _pointSumY;
	private int _pointCount;

	private CompSum _lineSumX;
	private CompSum _lineSumY;
	private CompSum _lineLength;

	private CompSum _polySum3X;
	private CompSum _polySum3Y;
	private CompSum _polyArea2;

	public static Centroid Begin => new();

	public Centroid AddPoint(XY point)
	{
		// if HasArea or HasLength: ignore
		if (point.IsEmpty) return this; // ignore empty
		_pointCount += 1;
		_pointSumX += point.X;
		_pointSumY += point.Y;
		return this;
	}

	public Centroid AddLine(XY p0, XY p1)
	{
		var length = XY.Distance(p0, p1);

		if (length > 0)
		{
			var mid = 0.5 * (p0 + p1);
			_lineSumX += length * mid.X;
			_lineSumY += length * mid.Y;
			_lineLength += length;
		}
		else
		{
			// degenerate line still counts as a point:
			AddPoint(p0);
		}

		return this;
	}

	public Centroid AddLine(IList<XY> vertices)
	{
		if (vertices is null)
			throw new ArgumentNullException(nameof(vertices));

		int count = vertices.Count;
		if (count <= 0) return this; // empty

		XY p1 = vertices[0];
		CompSum lineLength = 0.0;

		for (int i = 1; i < count; i++)
		{
			XY p0 = p1;
			p1 = vertices[i];
			var segmentLength = XY.Distance(p0, p1);

			if (segmentLength > 0)
			{
				var mid = 0.5 * (p0 + p1);
				_lineSumX += segmentLength * mid.X;
				_lineSumY += segmentLength * mid.Y;
				lineLength += segmentLength;
			}
		}

		if (lineLength > 0)
		{
			_lineLength += lineLength;
		}
		else
		{
			// degenerate line still counts as a point:
			AddPoint(vertices[0]);
		}

		return this;
	}

	public Centroid AddTriangle(XY p0, XY p1, XY p2, bool? isHole = null)
	{
		// isHole: null use orientation, true assume hole, false assume shell

		CompSum area2 = 0.0;
		CompSum cx3 = 0.0;
		CompSum cy3 = 0.0;

		var a2 = p0.X * p1.Y - p0.Y * p1.X;
		area2 += a2;
		cx3 += (p0.X + p1.X) * a2;
		cy3 += (p0.Y + p1.Y) * a2;

		a2 = p1.X * p2.Y - p1.Y * p2.X;
		area2 += a2;
		cx3 += (p1.X + p2.X) * a2;
		cy3 += (p1.Y + p2.Y) * a2;

		a2 = p2.X * p0.Y - p2.Y * p0.X;
		area2 += a2;
		cx3 += (p2.X + p0.X) * a2;
		cy3 += (p2.Y + p0.Y) * a2;

		if (isHole.HasValue)
		{
			if (isHole == true && area2 > 0 || isHole == false && area2 < 0)
			{
				area2 *= -1;
				cx3 *= -1;
				cy3 *= -1;
			}
		}

		// XY centroid = new XY(cx3, cy3) / (3 * area2);

		_polyArea2 += area2;
		_polySum3X += cx3;
		_polySum3Y += cy3;

		// Even if degenerate as an area, triangle still contribute as a line:

		AddLine(p0, p1);
		AddLine(p1, p2);
		AddLine(p2, p0);

		return this;
	}

	public Centroid AddPolygon(IList<XY> vertices, bool? isHole = null)
	{
		if (vertices is null)
			throw new ArgumentNullException(nameof(vertices));

		int count = vertices.Count;

		if (count > 1 && vertices[0] == vertices[count - 1])
		{
			count -= 1; // vN==v0 (closed polygon)
		}

		CompSum area2 = 0.0;
		CompSum cx3 = 0.0;
		CompSum cy3 = 0.0;

		for (int i = 0; i < count; i++)
		{
			var p0 = vertices[i];
			var p1 = vertices[(i + 1) % count];
			var a2 = p0.X * p1.Y - p0.Y * p1.X;
			area2 += a2;
			cx3 += (p0.X + p1.X) * a2;
			cy3 += (p0.Y + p1.Y) * a2;
		}

		if (isHole.HasValue)
		{
			// isHole: null use orientation, true assume hole, false assume shell

			if (isHole == true && area2 > 0 || isHole == false && area2 < 0)
			{
				area2 *= -1;
				cx3 *= -1;
				cy3 *= -1;
			}
		}

		_polyArea2 += area2;
		_polySum3X += cx3;
		_polySum3Y += cy3;

		// Even if degenerate, polygon may still contribute as a line:

		AddLine(vertices);

		if (count == vertices.Count)
		{
			// not closed: add the implicit closing segment:
			AddLine(vertices[count - 1], vertices[0]);
		}

		return this;
	}

	public XY Result
	{
		get
		{
			GetCentroid(out double x, out double y);
			return new XY(x, y);
		}
	}

	public bool GetCentroid(out double x, out double y)
	{
		if (_polyArea2 > 0 || _polyArea2 < 0)
		{
			// if actual area, return areal centroid
			x = _polySum3X / (3 * _polyArea2);
			y = _polySum3Y / (3 * _polyArea2);
			return true;
		}

		if (_lineLength > 0)
		{
			// average of segment midpoints, weighted by segment length
			x = _lineSumX / _lineLength;
			y = _lineSumY / _lineLength;
			return true;
		}

		if (_pointCount > 0)
		{
			// average of points:
			x = _pointSumX / _pointCount;
			y = _pointSumY / _pointCount;
			return true;
		}

		// empty geometry has no centroid:
		x = y = double.NaN;
		return false;
	}

	public static double SignedArea2(IList<XY> vertices)
	{
		int count = vertices.Count;
		if (count < 3) return 0.0;

		if (vertices[0] == vertices[count - 1])
		{
			count -= 1; // vN==v0 (closed polygon)
		}

		CompSum area2 = 0.0;

		for (int i = 0; i < count; i++)
		{
			int j = (i + 1) % count;
			area2 += vertices[i].X * vertices[j].Y;
			area2 -= vertices[i].Y * vertices[j].X;
		}

		return area2; // actual area: Math.Abs(0.5*area2)
	}
}
