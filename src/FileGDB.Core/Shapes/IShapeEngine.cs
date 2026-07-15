using FileGDB.Core.Geometry;

namespace FileGDB.Core.Shapes;

public interface IShapeEngine
{
	double GetLength(Shape shape);

	double GetArea(Shape shape);

	/// <summary>Compute the centroid of the given shape</summary>
	/// <returns>True if centroid was computed, false if shape has
	/// no centroid (e.g. because it is empty)</returns>
	bool QueryCentroid(Shape shape, out double cx, out double cy);

	/// <summary>Compute the centroid of the given shape</summary>
	/// <returns>The centroid of the given shape, or an empty point
	/// if the shape has no centroid (e.g. because it is empty)</returns>
	PointShape GetCentroid(Shape shape);

	/// <summary>Compute and add the bounding box of the given shape</summary>
	/// <returns>True if the bounding box was computed and added, false if
	/// the shape has no bounding box (e.g. because it is empty)</returns>
	bool QueryBox(Shape shape, BoundingBox box);

	/// <summary>Compute the bounding box of the given shape</summary>
	/// <returns>The bounding box of the given shape, or an empty box
	/// if the shape has no bounding box (e.g. because it is empty)</returns>
	BoxShape GetBox(Shape shape);

	Shape GetBoundary(Shape shape);
	PolylineShape GetBoundary(BoxShape box);
	PolylineShape GetBoundary(PolygonShape polygon);
	MultipointShape GetBoundary(PolylineShape polyline);
}
