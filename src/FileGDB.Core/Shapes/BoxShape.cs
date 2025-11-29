using System;

namespace FileGDB.Core.Shapes;

public class BoxShape : Shape
{
	public double XMin { get; }
	public double YMin { get; }
	public double XMax { get; }
	public double YMax { get; }
	public double ZMin { get; }
	public double ZMax { get; }
	public double MMin { get; }
	public double MMax { get; }
	// Use extension methods for convenience stuff like Center, LowerLeft, etc.

	public BoxShape(ShapeFlags flags,
		double xmin, double ymin, double xmax, double ymax,
		double zmin = DefaultZ, double zmax = DefaultZ,
		double mmin = DefaultM, double mmax = DefaultM)
		: base(GetShapeType(GeometryType.Envelope, flags))
	{
		XMin = xmin;
		YMin = ymin;
		XMax = xmax;
		YMax = ymax;

		ZMin = zmin;
		ZMax = zmax;

		MMin = mmin;
		MMax = mmax;
	}

	protected override BoxShape GetBox()
	{
		return this;
	}

	public override bool IsEmpty => double.IsNaN(XMin) || double.IsNaN(XMax) ||
	                                double.IsNaN(YMin) || double.IsNaN(YMax) ||
	                                XMin > XMax || YMin > YMax;

	public double Width => Math.Abs(XMax - XMin);
	public double Height => Math.Abs(YMax - YMin);

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		// We could emit a 5-point Polygon instead, but then we loose
		// the information that this is indeed a bounding box and, worse,
		// we would have to invent Z,M,ID from the envelope's min/max values.
		throw new NotSupportedException("There is no shape buffer defined for a bounding box (aka envelope)");
	}
}
