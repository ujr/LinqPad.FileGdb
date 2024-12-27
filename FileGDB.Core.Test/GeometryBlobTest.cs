using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace FileGDB.Core.Test;

public class GeometryBlobTest : IDisposable
{
	private readonly ITestOutputHelper _output;
	//private readonly string _myTempPath;

	private const double NoZ = ShapeBuffer.DefaultZ;
	private const double NoM = ShapeBuffer.DefaultM;
	private const int NoID = ShapeBuffer.DefaultID;

	public GeometryBlobTest(ITestOutputHelper output)
	{
		_output = output ?? throw new ArgumentNullException(nameof(output));

		// TODO Once tests and data are assembled, zip the FGDB to data/GeomTest.gdb.zip and unzip on-the-fly
		//var zipArchivePath = TestUtils.GetTestDataPath("GeomTest.gdb.zip");
		//Assert.True(File.Exists(zipArchivePath));

		//_myTempPath = TestUtils.CreateTempFolder();

		//ZipFile.ExtractToDirectory(zipArchivePath, _myTempPath);
	}

	public void Dispose()
	{
		// remove test data from temp folder
		//const bool recursive = true;
		//Directory.Delete(_myTempPath, recursive);
	}

	[Fact]
	public void CanReadPoint()
	{
		var blob1 = ReadGeometryBlob("TestGeom.gdb", "POINTS", 1);

		var buffer1 = blob1.ShapeBuffer;
		TestShapeBuffer(buffer1, GeometryType.Point, false, false, false, false, 1, 1, 0);
		TestCoords(buffer1, 2, 2653655.49, 1223141.56, NoZ, NoM);
		TestIDs(buffer1, 0);

		var shape1 = blob1.Shape;
		var point1 = TestShape<PointShape>(shape1, GeometryType.Point, false, false, false, false);
		TestPoint(point1, 2, 2653655.49, 1223141.56, NoZ, NoM, NoID);

		var blob2 = ReadGeometryBlob("TestGeom.gdb", "POINTS", 2);

		var buffer2 = blob2.ShapeBuffer;
		TestShapeBuffer(buffer2, GeometryType.Point, false, false, true, false, 1, 1, 0);
		TestCoords(buffer2, 2, 2653655.49, 1223073.83, NoZ, NoM);
		TestIDs(buffer2, 1);

		var shape2 = blob2.Shape;
		var point2 = TestShape<PointShape>(shape2, GeometryType.Point, false, false, true, false);
		TestPoint(point2, 2, 2653655.49, 1223073.83, NoZ, NoM, 1);
	}

	[Fact]
	public void CanReadPointZM()
	{
		var blob1 = ReadGeometryBlob("TestGeom.gdb", "POINTSZM", 1);

		var buffer1 = blob1.ShapeBuffer;
		TestShapeBuffer(buffer1, GeometryType.Point, true, true, false, false, 1, 1, 0);
		TestCoords(buffer1, 2, 2653769.79, 1223141.56, 444.4, 12.5);
		TestIDs(buffer1, 0);

		var shape1 = blob1.Shape;
		var point1 = TestShape<PointShape>(shape1, GeometryType.Point, true, true, false, false);
		TestPoint(point1, 2, 2653769.79, 1223141.56, 444.4, 12.5, NoID);

		var blob2 = ReadGeometryBlob("TestGeom.gdb", "POINTSZM", 2);

		var buffer2 = blob2.ShapeBuffer;
		TestShapeBuffer(buffer2, GeometryType.Point, true, true, true, false, 1, 1, 0);
		TestCoords(buffer2, 2, 2653769.79, 1223067.48, 444.4, 12.5);
		TestIDs(buffer2, 1);

		var shape2 = blob2.Shape;
		var point2 = TestShape<PointShape>(shape2, GeometryType.Point, true, true, true, false);
		TestPoint(point2, 2, 2653769.79, 1223067.48, 444.4, 12.5, 1);
	}

	[Fact]
	public void CanReadMultipoint()
	{
		var geometryBlob = ReadGeometryBlob("TestGeom.gdb", "MULTIPOINTS", 1);

		var buffer = geometryBlob.ShapeBuffer;
		TestShapeBuffer(buffer, GeometryType.Multipoint, false, false, true, false, 7, 7, 0);
		TestCoords(buffer, 2,
			2652527.31, 1222814.01, NoZ, NoM,
			2652680.76, 1222851.05, NoZ, NoM,
			2652794.53, 1222795.49, NoZ, NoM,
			2652691.35, 1222713.47, NoZ, NoM,
			2652466.45, 1222583.82, NoZ, NoM,
			2652559.06, 1222697.59, NoZ, NoM,
			2652656.95, 1222538.84, NoZ, NoM);
		TestIDs(buffer, 0, 0, 1, 1, 0, 1, 0);

		var shape = geometryBlob.Shape;
		var multipoint = TestShape<MultipointShape>(shape, GeometryType.Multipoint, false, false, true, false);
		TestPointsZM(multipoint.Points, 2,
			2652527.31, 1222814.01, NoZ, NoM,
			2652680.76, 1222851.05, NoZ, NoM,
			2652794.53, 1222795.49, NoZ, NoM,
			2652691.35, 1222713.47, NoZ, NoM,
			2652466.45, 1222583.82, NoZ, NoM,
			2652559.06, 1222697.59, NoZ, NoM,
			2652656.95, 1222538.84, NoZ, NoM);
		TestPointIDs(multipoint.Points, 0, 0, 1, 1, 0, 1, 0);
	}

	[Fact]
	public void CanReadMultipointZM()
	{
		var geometryBlob = ReadGeometryBlob("TestGeom.gdb", "MULTIPOINTSZM", 1);

		var buffer = geometryBlob.ShapeBuffer;
		TestShapeBuffer(buffer, GeometryType.Multipoint, true, true, true, false, 9, 9, 0);
		TestCoords(buffer, 2,
			2653503.62, 1223356.41, 0, double.NaN,
			2653596.22, 1223406.68, 10, 100,
			2653691.47, 1223425.20, 20, 120,
			2653792.02, 1223398.74, 30, 120,
			2653834.35, 1223335.24, 40, 130,
			2653762.91, 1223292.90, 50, 120,
			2653688.83, 1223271.74, -12, double.NaN,
			2653612.10, 1223284.97, 70, 150,
			2653548.60, 1223308.78, 80, 200);

		var shape = geometryBlob.Shape;
		var multipoint = TestShape<MultipointShape>(shape, GeometryType.Multipoint, true, true, true, false);
		TestPointsZM(multipoint.Points, 2,
			2653503.62, 1223356.41, 0, double.NaN,
			2653596.22, 1223406.68, 10, 100,
			2653691.47, 1223425.20, 20, 120,
			2653792.02, 1223398.74, 30, 120,
			2653834.35, 1223335.24, 40, 130,
			2653762.91, 1223292.90, 50, 120,
			2653688.83, 1223271.74, -12, double.NaN,
			2653612.10, 1223284.97, 70, 150,
			2653548.60, 1223308.78, 80, 200);
	}

	[Fact]
	public void CanReadPolylineZM()
	{
		var gdbPath = GetTempDataPath("TestGeom.gdb");
		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable("LINESZM");

		// This table has two rows: OID 1 is a zigzag line (no curves)

		var geometryBlob = ReadGeometryBlob(table, 1);

		var buffer = geometryBlob.ShapeBuffer;
		TestShapeBuffer(buffer, GeometryType.Polyline, true, true, true, false, 6, 1, 0);
		TestCoords(buffer, 2,
			2652556.41, 1223107.70, 0.0, double.NaN,
			2652715.16, 1223239.99, -12.0, double.NaN,
			2652691.35, 1223110.34, 403.0, 12.34,
			2652852.74, 1223247.93, 404.0, 23.45,
			2652799.83, 1223105.05, 405.0, double.NaN,
			2652979.74, 1223237.34, 0.0, double.NaN);
		TestIDs(buffer, 0, 0, 1, 0, 0, 0);

		var shape = geometryBlob.Shape;
		var polyline = TestShape<PolylineShape>(shape, GeometryType.Polyline, true, true, true, false);
		Assert.Equal(6, polyline.NumPoints);
		Assert.Equal(1, polyline.NumParts);
		Assert.Equal(0, polyline.NumCurves);
		TestPointsZM(polyline.Points, 2,
			2652556.41, 1223107.70, 0.0, double.NaN,
			2652715.16, 1223239.99, -12.0, double.NaN,
			2652691.35, 1223110.34, 403.0, 12.34,
			2652852.74, 1223247.93, 404.0, 23.45,
			2652799.83, 1223105.05, 405.0, double.NaN,
			2652979.74, 1223237.34, 0.0, double.NaN);
		TestPointIDs(polyline.Points, 0, 0, 1, 0, 0, 0);
	}

	[Fact]
	public void CanReadPolylineCurves()
	{
		var gdbPath = GetTempDataPath("TestGeom.gdb");
		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable("LINESZM");

		// Row with OID 2 has curves
		var geometryBlob = ReadGeometryBlob(table, 2);

		var buffer = geometryBlob.ShapeBuffer;
		TestShapeBuffer(buffer, GeometryType.Polyline, true, true, true, false, 5, 1, 3);
		TestCoords(buffer, 2,
			2652360.62, 1222880.15, 0.0, double.NaN,
			2652564.35, 1223025.68, 0.0, double.NaN,
			2652807.76, 1223009.80, 0.0, double.NaN,
			2652982.39, 1222888.09, 0.0, double.NaN,
			2652927.28, 1222898.41, 0.0, double.NaN);
		TestIDs(buffer, 0, 0, 0, 0, 0);

		var shape = geometryBlob.Shape;
		var polyline = TestShape<PolylineShape>(shape, GeometryType.Polyline, true, true, true, false);
		Assert.Equal(5, polyline.NumPoints);
		Assert.Equal(1, polyline.NumParts);
		Assert.Equal(3, polyline.NumCurves);
		TestPointsZM(polyline.Points, 2,
			2652360.62, 1222880.15, 0.0, double.NaN,
			2652564.35, 1223025.68, 0.0, double.NaN,
			2652807.76, 1223009.80, 0.0, double.NaN,
			2652982.39, 1222888.09, 0.0, double.NaN,
			2652927.28, 1222898.41, 0.0, double.NaN);
		TestPointIDs(polyline.Points, 0, 0, 0, 0, 0);
		Assert.NotNull(polyline.Curves);
		Assert.Equal(3, polyline.Curves.Count);
		var bezier = Assert.IsAssignableFrom<CubicBezierModifier>(polyline.Curves[0]);
		Assert.Equal(1, bezier.SegmentIndex);
		Assert.Equal(4, bezier.CurveType);
		var arc1 = Assert.IsAssignableFrom<CircularArcModifier>(polyline.Curves[1]);
		Assert.Equal(2, arc1.SegmentIndex);
		Assert.Equal(1, arc1.CurveType);
		var arc2 = Assert.IsAssignableFrom<CircularArcModifier>(polyline.Curves[2]);
		Assert.Equal(3, arc2.SegmentIndex);
		Assert.Equal(1, arc2.CurveType);
	}

	[Fact]
	public void CanReadMultipartPolyline()
	{
		// Row with OID 4 has three parts
		var geometryBlob = ReadGeometryBlob("TestGeom.gdb", "LINES", 4);

		var buffer = geometryBlob.ShapeBuffer;
		TestShapeBuffer(buffer, GeometryType.Polyline, false, false, false, false, 6, 3, 0);
		TestCoords(buffer, 2,
			2653090.34, 1222972.23, NoZ, NoM,
			2653316.82, 1223088.65, NoZ, NoM,
			2653316.82, 1223088.65, NoZ, NoM,
			2653327.41, 1223325.71, NoZ, NoM,
			2653517.91, 1223086.53, NoZ, NoM,
			2653316.82, 1223088.65, NoZ, NoM);
		TestIDs(buffer, 0, 0, 0, 0, 0, 0);
		Assert.Equal(0, buffer.GetPartStartIndex(0));
		Assert.Equal(2, buffer.GetPartStartIndex(1));
		Assert.Equal(4, buffer.GetPartStartIndex(2));

		var shape = geometryBlob.Shape;
		var polyline = TestShape<PolylineShape>(shape, GeometryType.Polyline, false, false, false, false);
		Assert.Equal(3, polyline.Parts.Count);
		TestPointsZM(polyline.Parts[0].Points, 2,
			2653090.34, 1222972.23, NoZ, NoM,
			2653316.82, 1223088.65, NoZ, NoM);
		TestPointsZM(polyline.Parts[1].Points, 2,
			2653316.82, 1223088.65, NoZ, NoM,
			2653327.41, 1223325.71, NoZ, NoM);
		TestPointsZM(polyline.Parts[2].Points, 2,
			2653517.91, 1223086.53, NoZ, NoM,
			2653316.82, 1223088.65, NoZ, NoM);
		TestPointIDs(polyline.Points, 0, 0, 0, 0, 0, 0);
	}

	[Fact]
	public void CanReadPolygonZ()
	{
		// Row with OID 6 has a polygon with one part and 5 vertices:
		var geometryBlob = ReadGeometryBlob("TestGeom.gdb", "POLYSZ", 6);

		var buffer = geometryBlob.ShapeBuffer;
		TestShapeBuffer(buffer, GeometryType.Polygon, true, false, true, false, 5, 1, 0);
		TestCoords(buffer, 2,
			2653027.37, 1223181.78, 0.0, NoM,
			2653170.24, 1223110.34, -12.5, NoM,
			2653056.47, 1223044.20, 0.0, NoM,
			2652958.58, 1223102.40, 2.25, NoM,
			2653027.37, 1223181.78, 0.0, NoM); // last vertex has same coordinates as first vertex
		TestIDs(buffer, 0, 0, 1, 1, 0);

		var shape = geometryBlob.Shape;
		var polygon = TestShape<PolygonShape>(shape, GeometryType.Polygon, true, false, true, false);
		Assert.Equal(5, polygon.NumPoints);
		Assert.Equal(1, polygon.NumParts);
		Assert.Equal(0, polygon.NumCurves);
		TestPointsZM(polygon.Points, 2,
			2653027.37, 1223181.78, 0.0, NoM,
			2653170.24, 1223110.34, -12.5, NoM,
			2653056.47, 1223044.20, 0.0, NoM,
			2652958.58, 1223102.40, 2.25, NoM,
			2653027.37, 1223181.78, 0.0, NoM); // last vertex equals first vertex
		TestPointIDs(polygon.Points, 0, 0, 1, 1, 0);
		Assert.Same(polygon, polygon.Parts.Single()); // an implementation detail: single part is same as shape
	}

	[Fact]
	public void CanReadMultipartPolygonZ()
	{
		// Row with OID 11 has 2 parts
		var geometryBlob = ReadGeometryBlob("TestGeom.gdb", "POLYSZ", 11);

		var buffer = geometryBlob.ShapeBuffer;
		TestShapeBuffer(buffer, GeometryType.Polygon, true, false, false, false, 8, 2, 0);
		TestCoords(buffer, 2,
			// 1st part:
			2653318.94, 1223059.01, 0, NoM,
			2653505.21, 1223061.13, 0.0, NoM,
			2653302.01, 1222923.55, 0.0, NoM,
			2653318.94, 1223059.01, 0, NoM, // last in part equals first in part
			// 2nd part:
			2653340.11, 1223118.28, 0.0, NoM,
			2653352.81, 1223310.90, 0.0, NoM,
			2653505.21, 1223122.51, 0.0, NoM,
			2653340.11, 1223118.28, 0.0, NoM); // last in part equals first in part
		TestIDs(buffer, 0, 0, 0, 0, 0, 0, 0, 0);

		var shape = geometryBlob.Shape;
		var polygon = TestShape<PolygonShape>(shape, GeometryType.Polygon, true, false, false, false);
		Assert.Equal(8, polygon.NumPoints); // each part has 4 vertices
		Assert.Equal(2, polygon.NumParts);
		Assert.Equal(0, polygon.NumCurves);
		TestPointsZM(polygon.Parts[0].Points, 2,
			2653318.94, 1223059.01, 0.0, NoM,
			2653505.21, 1223061.13, 0.0, NoM,
			2653302.01, 1222923.55, 0.0, NoM,
			2653318.94, 1223059.01, 0.0, NoM);
		TestPointsZM(polygon.Parts[1].Points, 2,
			2653340.11, 1223118.28, 0.0, NoM,
			2653352.81, 1223310.90, 0.0, NoM,
			2653505.21, 1223122.51, 0.0, NoM,
			2653340.11, 1223118.28, 0.0, NoM);
		TestPointIDs(polygon.Points, 0, 0, 0, 0, 0, 0, 0, 0);
	}

	[Theory]
	[InlineData("TestGeom.gdb", "POINTS", 1, "Point.bin")]
	[InlineData("TestGeom.gdb", "POINTS", 2, "PointID.bin")]
	[InlineData("TestGeom.gdb", "POINTSZM", 1, "PointZM.bin")]
	[InlineData("TestGeom.gdb", "POINTSZM", 2, "PointZMID.bin")]
	[InlineData("TestGeom.gdb", "MULTIPOINTS", 1, "Multipoint.bin")]
	[InlineData("TestGeom.gdb", "MULTIPOINTSZM", 1, "MultipointZM.bin")]
	[InlineData("TestGeom.gdb", "LINESZM", 1, "LineLinear.bin")]
	[InlineData("TestGeom.gdb", "LINESZM", 2, "LineCurves.bin")]
	[InlineData("TestGeom.gdb", "LINES", 4, "LineMultipart.bin")]
	[InlineData("TestGeom.gdb", "POLYSZ", 6, "PolygonZ.bin")]
	[InlineData("TestGeom.gdb", "POLYSZ", 11, "PolygonMultipartZ.bin")]
	[InlineData("TestGeom.gdb", "POLYSZ", 9, "PolygonCircle.bin")]
	[InlineData("TestGeom.gdb", "POLYSZ", 10, "PolygonEllipse.bin")]
	public void TestShapeBufferByteByByte(string gdbName, string tableName, long oid, string expectedDataFile)
	{
		var actual = ReadGeometryBlob(gdbName, tableName, oid).ShapeBuffer;
		var expected = ReadTestDataBytes(expectedDataFile, "shpbuf");
		// It seems the ShapeBuffers exported from Pro use general types (e.g. GeneralPolygon)
		// with shape flags, whereas the FGDB stores "classic" types (e.g. PolygonZ)
		AssertEqualShapeBuffer(expected, actual, 0.0001);
	}

	#region Private test utils

	private string GetTempDataPath(string fileName)
	{
		// TODO return Path.Combine(_myTempPath, fileName);
		return Path.Combine("C:\\Temp", fileName);
	}

	private static byte[] ReadTestDataBytes(string fileName, string? folder = null)
	{
		if (!string.IsNullOrEmpty(folder))
		{
			fileName = Path.Combine(folder, fileName);
		}

		string filePath = TestUtils.GetTestDataPath(fileName);

		return File.ReadAllBytes(filePath);
	}

	private static GeometryBlob ReadGeometryBlob(Table table, long oid)
	{
		if (table is null)
			throw new ArgumentNullException(nameof(table));

		int shapeIndex = table.GetShapeIndex();
		Assert.True(shapeIndex >= 0, "No shape field");

		var values = table.ReadRow(oid);
		var shapeValue = values?[shapeIndex];
		Assert.NotNull(shapeValue);
		return Assert.IsAssignableFrom<GeometryBlob>(shapeValue);
	}

	private GeometryBlob ReadGeometryBlob(string gdbName, string tableName, long oid)
	{
		var gdbPath = GetTempDataPath(gdbName);
		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable(tableName);

		int shapeIndex = table.GetShapeIndex();
		Assert.True(shapeIndex >= 0, "No shape field");

		var values = table.ReadRow(oid);
		var shapeValue = values?[shapeIndex];
		Assert.NotNull(shapeValue);
		return Assert.IsAssignableFrom<GeometryBlob>(shapeValue);
	}

	private static void TestShapeBuffer(ShapeBuffer candidate,
		GeometryType geometryType, bool hasZ, bool hasM, bool hasID,
		bool isEmpty, int numPoints, int numParts, int numCurves)
	{
		Assert.Equal(geometryType, candidate.GeometryType);
		Assert.Equal(hasZ, candidate.HasZ);
		Assert.Equal(hasM, candidate.HasM);
		Assert.Equal(hasID, candidate.HasID);
		Assert.Equal(isEmpty, candidate.IsEmpty);
		Assert.Equal(numPoints, candidate.NumPoints);
		Assert.Equal(numParts, candidate.NumParts);
		Assert.Equal(numCurves, candidate.NumCurves);
	}

	private static void TestCoords(ShapeBuffer candidate,
		int decimalDigits, params double[] coordinates)
	{
		if (candidate is null)
			throw new ArgumentNullException(nameof(candidate));
		if (coordinates is null)
			throw new ArgumentNullException(nameof(coordinates));
		if (candidate.NumPoints * 4 != coordinates.Length)
			throw new ArgumentException(
				$"Expect {candidate.NumPoints * 4} {nameof(coordinates)} for candidate with NumPoints={candidate.NumPoints}");

		for (int i = 0; i < candidate.NumPoints; i++)
		{
			candidate.QueryCoords(i, out double x, out double y, out double z, out double m, out _);
			Assert.Equal(coordinates[4 * i + 0], x, decimalDigits);
			Assert.Equal(coordinates[4 * i + 1], y, decimalDigits);
			Assert.Equal(coordinates[4 * i + 2], z, decimalDigits);
			Assert.Equal(coordinates[4 * i + 3], m, decimalDigits);
		}
	}

	private static void TestIDs(ShapeBuffer candidate, params int[] ids)
	{
		if (candidate is null)
			throw new ArgumentNullException(nameof(candidate));
		if (ids is null)
			throw new ArgumentNullException(nameof(ids));
		if (candidate.NumPoints != ids.Length)
			throw new ArgumentException(
				$"Expect {candidate.NumPoints} {nameof(ids)} for candidate with NumPoints={candidate.NumPoints}");

		for (int i = 0; i < candidate.NumPoints; i++)
		{
			candidate.QueryCoords(i, out _, out _, out _, out _, out int id);
			Assert.Equal(ids[i], id);
		}
	}

	// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
	private static T TestShape<T>(Shape candidate,
		GeometryType geometryType, bool hasZ, bool hasM, bool hasID,
		bool isEmpty) where T : Shape
	{
		Assert.Equal(geometryType, candidate.GeometryType);
		Assert.Equal(hasZ, candidate.HasZ);
		Assert.Equal(hasM, candidate.HasM);
		Assert.Equal(hasID, candidate.HasID);
		Assert.Equal(isEmpty, candidate.IsEmpty);
		return Assert.IsAssignableFrom<T>(candidate);
	}
	// ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local

	private static void TestPoint(PointShape point, int decimalDigits, double x, double y, double z, double m, int id)
	{
		Assert.NotNull(point);
		Assert.Equal(x, point.X, decimalDigits);
		Assert.Equal(y, point.Y, decimalDigits);
		Assert.Equal(z, point.Z, decimalDigits);
		Assert.Equal(m, point.M, decimalDigits);
		Assert.Equal(id, point.ID);
	}

	private static void TestPointsZM(IReadOnlyList<PointShape> points,
		int decimalDigits, params double[] coordinates)
	{
		if (points is null)
			throw new ArgumentNullException(nameof(points));
		if (coordinates is null)
			throw new ArgumentNullException(nameof(coordinates));
		if (points.Count * 4 != coordinates.Length)
			throw new ArgumentException(
				$"Expect {points.Count * 4} {nameof(coordinates)} for {points.Count} {nameof(points)}");

		for (int i = 0; i < points.Count; i++)
		{
			Assert.Equal(coordinates[4 * i + 0], points[i].X, decimalDigits);
			Assert.Equal(coordinates[4 * i + 1], points[i].Y, decimalDigits);
			Assert.Equal(coordinates[4 * i + 2], points[i].Z, decimalDigits);
			Assert.Equal(coordinates[4 * i + 3], points[i].M, decimalDigits);
		}
	}

	private static void TestPointIDs(IReadOnlyList<PointShape> points, params int[] ids)
	{
		if (points is null)
			throw new ArgumentNullException(nameof(points));
		if (ids is null)
			throw new ArgumentNullException(nameof(ids));
		if (points.Count != ids.Length)
			throw new ArgumentException(
				$"Expect {points.Count} {nameof(ids)} for {points.Count} {nameof(points)}");

		for (int i = 0; i < points.Count; i++)
		{
			Assert.Equal(ids[i], points[i].ID);
		}
	}

	private void AssertEqualShapeBuffer(byte[] expected, ShapeBuffer actual, double boxTolerance)
	{
		if (expected is null)
			throw new ArgumentNullException(nameof(expected));
		if (actual is null)
			throw new ArgumentNullException(nameof(actual));

		Assert.Equal(expected.Length, actual.Length);

		// It seems Pro's ToEsriShape() writes general types with flags,
		// whereas the FGDB stores "classic" types (e.g. PolygonZ)

		var expectedType = BitConverter.ToUInt32(expected, 0);
		var actualType = BitConverter.ToUInt32(actual.Bytes.Take(4).ToArray());
		Assert.True(ShapeBuffer.ShapeTypeEqual(expectedType, actualType));

		if (actual.GeometryType == GeometryType.Point)
		{
			// skip 1st 4 bytes (shape type)
			Assert.Equal(expected.Skip(4).ToArray(), actual.Bytes.Skip(4).ToArray());
		}
		else
		{
			// Bounding boxes seem to differ, but within GeometryDef.XYScale:

			GetBox(expected, 4, out var x0e, out var y0e, out var x1e, out var y1e);
			GetBox(actual.Bytes.ToArray(), 4, out var x0a, out var y0a, out var x1a, out var y1a);

			double dx0 = x0e - x0a;
			double dy0 = y0e - y0a;
			double dx1 = x1e - x1a;
			double dy1 = y1e - y1a;

			if (dx0 > 0 || dy0 > 0 || dx1 > 0 || dy1 > 0)
			{
				_output.WriteLine($"Actual:   {x0a} {y0a} .. {x1a} {y1a}");
				_output.WriteLine($"Expected: {x0e} {y0e} .. {x1e} {y1e}");
				_output.WriteLine($"Exp-Act:  {dx0:F12} {dy0:F12} .. {dx1:F12} {dy1:F12}");

				if (dx0 > boxTolerance || dy0 > boxTolerance ||
				    dx1 > boxTolerance || dy1 > boxTolerance)
				{
					Assert.True(false, $"BBox differs beyond tolerance of {boxTolerance}");
				}
			}

			// skip 1st 4+32 bytes (shape type and bbox)
			int count = 4 + 4 * 8;
			Assert.Equal(expected.Skip(count).ToArray(), actual.Bytes.Skip(count).ToArray());
		}

		// Compare all bytes:
		//Assert.Equal(expected, actual);
	}

	private static void GetBox(byte[] bytes, int offset,
		out double xmin, out double ymin, out double xmax, out double ymax)
	{
		xmin = BitConverter.ToDouble(bytes, offset + 0);
		ymin = BitConverter.ToDouble(bytes, offset + 8);
		xmax = BitConverter.ToDouble(bytes, offset + 16);
		ymax = BitConverter.ToDouble(bytes, offset + 24);
	}

	#endregion
}
