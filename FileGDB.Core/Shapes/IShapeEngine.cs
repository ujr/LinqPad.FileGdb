namespace FileGDB.Core.Shapes;

public interface IShapeEngine
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