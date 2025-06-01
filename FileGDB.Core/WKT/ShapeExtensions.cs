using System;
using System.IO;
using System.Text;

namespace FileGDB.Core.WKT;

/// <summary>
/// Writing a <see cref="Shape"/> as WKT (well-known text)
/// </summary>
public static class ShapeExtensions
{
	public static string ToWKT(this Shape shape, int decimalDigits = -1)
	{
		var buffer = new StringBuilder();
		ToWKT(shape, buffer, decimalDigits);
		return buffer.ToString();
	}

	public static void ToWKT(this Shape shape, StringBuilder buffer, int decimalDigits = -1)
	{
		var writer = new StringWriter(buffer);
		ToWKT(shape, writer, decimalDigits);
		writer.Flush();
	}

	public static void ToWKT(this Shape shape, TextWriter writer, int decimalDigits = -1)
	{
		var wkt = new WKTWriter(writer) { DecimalDigits = decimalDigits };
		ToWKT(shape, wkt);
		wkt.Flush();
	}

	private static void ToWKT(Shape shape, WKTWriter wkt)
	{
		if (shape is null)
			throw new ArgumentNullException(nameof(shape));
		if (wkt is null)
			throw new ArgumentNullException(nameof(wkt));

		switch (shape)
		{
			case NullShape:
				throw new NotSupportedException("Cannot write Null shape as WKT");

			case BoxShape box:
				wkt.WriteBox(box.XMin, box.YMin, box.XMax, box.YMax,
					box.ZMin, box.ZMax, box.MMin, box.MMax);
				break;

			case PointShape point:
				wkt.BeginPoint(point.HasZ, point.HasM, point.HasID);
				wkt.AddVertex(point.X, point.Y, point.Z, point.M, point.ID);
				wkt.EndShape();
				break;

			case MultipointShape multipoint:
				wkt.BeginMultipoint(multipoint.HasZ, multipoint.HasM, multipoint.HasID);
				WriteCoordinates(multipoint, wkt);
				wkt.EndShape();
				break;

			case PolylineShape polyline:
				wkt.BeginMultiLineString(polyline.HasZ, polyline.HasM, polyline.HasID);
				WriteCoordinates(polyline, wkt);
				wkt.EndShape();
				break;

			case PolygonShape polygon:
				wkt.BeginMultiPolygon(polygon.HasZ, polygon.HasM, polygon.HasID);
				WriteCoordinates(polygon, wkt);
				wkt.EndShape();
				break;

			default:
				throw new NotSupportedException($"Unknown shape type: {shape.GetType().Name}");
		}
	}

	private static void WriteCoordinates(MultipointShape multipoint, WKTWriter wkt)
	{
		for (int i = 0; i < multipoint.NumPoints; i++)
		{
			var xy = multipoint.CoordsXY[i];
			var z = multipoint.CoordsZ?[i] ?? Shape.DefaultZ;
			var m = multipoint.CoordsM?[i] ?? Shape.DefaultM;
			var id = multipoint.CoordsID?[i] ?? Shape.DefaultID;
			wkt.AddVertex(xy.X, xy.Y, z, m, id);
		}
	}

	private static void WriteCoordinates(MultipartShape multipart, WKTWriter wkt)
	{
		for (int i = 0, j = 0; i < multipart.NumPoints; i++)
		{
			int k = multipart.GetPartStart(j);
			if (i == k) // first vertex of new part
			{
				wkt.NewPart();
				j += 1;
			}

			var xy = multipart.CoordsXY[i];
			var z = multipart.CoordsZ?[i] ?? Shape.DefaultZ;
			var m = multipart.CoordsM?[i] ?? Shape.DefaultM;
			var id = multipart.CoordsID?[i] ?? Shape.DefaultID; // null unless HasID

			wkt.AddVertex(xy.X, xy.Y, z, m, id);
		}
	}
}
