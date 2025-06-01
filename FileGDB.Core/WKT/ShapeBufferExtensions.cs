using System;
using System.IO;
using System.Text;

namespace FileGDB.Core.WKT;

/// <summary>
/// Writing a <see cref="ShapeBuffer"/> as WKT (well-known text)
/// </summary>
public static class ShapeBufferExtensions
{
	public static string ToWKT(this ShapeBuffer shape, int decimalDigits = -1)
	{
		var buffer = new StringBuilder();
		ToWKT(shape, buffer, decimalDigits);
		return buffer.ToString();
	}

	public static void ToWKT(this ShapeBuffer shape, StringBuilder buffer, int decimalDigits = -1)
	{
		var writer = new StringWriter(buffer);
		ToWKT(shape, writer, decimalDigits);
		writer.Flush();
	}

	public static void ToWKT(this ShapeBuffer shape, TextWriter writer, int decimalDigits = -1)
	{
		var wkt = new WKTWriter(writer) { DecimalDigits = decimalDigits };
		WriteWKT(shape, wkt);
		wkt.Flush();
	}

	private static void WriteWKT(ShapeBuffer shape, WKTWriter writer)
	{
		switch (shape.GeometryType)
		{
			case GeometryType.Null:
				break;

			case GeometryType.Point:
				writer.BeginPoint(shape.HasZ, shape.HasM, shape.HasID);
				WritePointCoords(shape, writer, shape.IsEmpty);
				writer.EndShape();
				break;

			case GeometryType.Multipoint:
				writer.BeginMultipoint(shape.HasZ, shape.HasM, shape.HasID);
				WriteMultipointCoords(shape, writer, shape.NumPoints);
				writer.EndShape();
				break;

			case GeometryType.Polyline:
				writer.BeginMultiLineString(shape.HasZ, shape.HasM, shape.HasID);
				WriteMultipartCoords(shape, writer, shape.NumPoints, shape.NumParts);
				writer.EndShape();
				break;

			case GeometryType.Polygon:
				writer.BeginMultiPolygon(shape.HasZ, shape.HasM, shape.HasID);
				WriteMultipartCoords(shape, writer, shape.NumPoints, shape.NumParts);
				writer.EndShape();
				break;

			case GeometryType.Envelope:
				// WKT has no representation for Envelope
				// PostGIS writes a 5 vertex POLYGON (or 2 vertex LINESTRING if no dx or dy)
				// Pro's ToEsriShape() writes a 5 vertex Polygon if called on an Envelope
				throw new InvalidOperationException("GeometryType Envelope is invalid for this operation");

			case GeometryType.Any:
				throw new InvalidOperationException("GeometryType Any is invalid for this operation");

			case GeometryType.MultiPatch:
				throw new NotSupportedException("MultiPatch to WKT is not supported");

			case GeometryType.Bag:
				throw new NotSupportedException("GeometryBag to WKT is not supported");

			default:
				throw new InvalidOperationException($"Unknown geometry type: {shape.GeometryType}");
		}
	}

	private static void WritePointCoords(ShapeBuffer shape, WKTWriter writer, bool isEmpty)
	{
		if (isEmpty) return;
		shape.QueryPointCoords(out var x, out var y, out var z, out var m, out var id);
		writer.AddVertex(x, y, z, m, id);
	}

	private static void WriteMultipointCoords(ShapeBuffer shape, WKTWriter writer, int numPoints)
	{
		for (int i = 0; i < numPoints; i++)
		{
			shape.QueryMultipointCoords(i, out var x, out var y, out var z, out var m, out int id);
			writer.AddVertex(x, y, z, m, id);
		}
	}

	private static void WriteMultipartCoords(ShapeBuffer shape, WKTWriter writer, int numPoints, int numParts)
	{
		for (int i = 0, j = 0, k = 0; i < numPoints; i++)
		{
			if (i == k) // first vertex of new part
			{
				writer.NewPart();
				j += 1;
				k = j < numParts ? shape.GetPartStartIndex(j) : int.MaxValue;
			}

			shape.QueryMultipartCoords(i, out var x, out var y, out var z, out var m, out int id);

			writer.AddVertex(x, y, z, m, id);
		}
	}
}
