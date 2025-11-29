namespace FileGDB.Core;

/// <remarks>
/// Names and values as for ArcObjects esriGeometryType constants.
/// The File Geodatabase API's GeometryType enum only has Null, Point,
/// Multipoint, Polyline, Polygon, MultiPatch (with the same values
/// as here), which are the "high-level geometries".
/// </remarks>
public enum GeometryType
{
	Null = 0,
	Point = 1,
	Multipoint = 2,
	Polyline = 3,
	Polygon = 4,
	Envelope = 5,
	//Path = 6,
	Any = 7,
	MultiPatch = 9,
	//Ring = 11, // closed path
	//Line = 13, // linear segment
	//CircularArc = 14,
	//Bezier3Curve = 15,
	//EllipticArc = 16,
	Bag = 17,
	//TriangleStrip = 18,
	//TriangleFan = 19,
	//Ray = 20,
	//Sphere = 21
}
