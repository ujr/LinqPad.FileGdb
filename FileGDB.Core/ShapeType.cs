namespace FileGDB.Core;

/// <summary>
/// Shape types, as used in Shapefiles and in the Geometry Blob
/// of the File Geodatabase.
/// </summary>
/// <remarks>
/// Names and values as for the ArcObjects esriShapeType constants,
/// which are described as the "Esri Shapefile shape types". The
/// general types are not supported in shapefiles but described in
/// the "Extended Shape Buffer Format" white paper (2012).
/// </remarks>
public enum ShapeType
{
	// From the original Shapefile specification
	Null = 0,
	Point = 1,
	PointZ = 9,
	PointZM = 11,
	PointM = 21,
	Multipoint = 8,
	MultipointZ = 20,
	MultipointZM = 18,
	MultipointM = 28,
	Polyline = 3,
	PolylineZ = 10,
	PolylineZM = 13,
	PolylineM = 23,
	Polygon = 5,
	PolygonZ = 19,
	PolygonZM = 15,
	PolygonM = 25,
	MultiPatch = 32,
	MultiPatchM = 31,

	// From the Extended Shape Buffer Format specification
	GeneralPolyline = 50,
	GeneralPolygon = 51,
	GeneralPoint = 52,
	GeneralMultipoint = 53,
	GeneralMultiPatch = 54,

	// Undocumented
	GeometryBag = 17,

	// Custom additions
	Box = 254
}
