namespace FileGDB.Core;

/// <summary>
/// The different segment types, as used in the Extended Shape
/// Buffer Format (but not in Shapefiles) and in the Geometry Blob
/// of the File Geodatabase.
/// </summary>
/// <remarks>
/// A straight line between two vertices is the default and
/// does not occur as a segment modifier; the Spiral segment
/// type was probably never used. CircularArc, CubicBezier, and
/// EllipticArc are the only values that occur in practice.
/// </remarks>
public enum SegmentType
{
	CircularArc = 1,
	StraightLine = 2,
	Spiral = 3,
	CubicBezier = 4,
	EllipticArc = 5
}
