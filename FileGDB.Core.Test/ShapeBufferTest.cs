using System.IO;
using System.Text;
using FileGDB.Core.Test.TestData;
using FileGDB.Core.WKT;
using Xunit;

namespace FileGDB.Core.Test;

public class ShapeBufferTest
{
	[Fact]
	public void CanEmptyPointBuffer()
	{
		var bytes = ShapeBuffers.GetBytesPointEmpty();
		var buffer = new ShapeBuffer(bytes);

		Assert.Equal(20, buffer.Length);
		Assert.Equal(ShapeType.GeneralPoint, buffer.ShapeType);
		Assert.Equal(GeometryType.Point, buffer.GeometryType);
		Assert.False(buffer.HasZ);
		Assert.False(buffer.HasM);
		Assert.False(buffer.HasID);
		Assert.False(buffer.MayHaveCurves);
		Assert.True(buffer.IsEmpty);
		Assert.Equal(1, buffer.NumPoints); // sic
		Assert.Equal(1, buffer.NumParts); // sic
		Assert.Equal(0, buffer.NumCurves);
		Assert.Equal("POINT EMPTY", buffer.ToWKT());
		buffer.QueryCoords(0, out var x, out var y, out var z, out var m, out int id);
		Assert.True(double.IsNaN(x));
		Assert.True(double.IsNaN(y));
		Assert.Equal(0.0, z);
		Assert.True(double.IsNaN(m));
		Assert.Equal(0, id);
	}

	[Fact]
	public void CanEmptyPointBufferZMID()
	{
		var bytes = ShapeBuffers.GetBytesPointEmptyZMID();
		var buffer = new ShapeBuffer(bytes);

		Assert.Equal(40, buffer.Length);
		Assert.Equal(ShapeType.GeneralPoint, buffer.ShapeType);
		Assert.Equal(GeometryType.Point, buffer.GeometryType);
		Assert.True(buffer.HasZ);
		Assert.True(buffer.HasM);
		Assert.True(buffer.HasID);
		Assert.False(buffer.MayHaveCurves);
		Assert.True(buffer.IsEmpty);
		Assert.Equal(1, buffer.NumPoints); // sic
		Assert.Equal(1, buffer.NumParts); // sic
		Assert.Equal(0, buffer.NumCurves);
		Assert.Equal("POINT ZM EMPTY", buffer.ToWKT());
		buffer.QueryCoords(0, out var x, out var y, out var z, out var m, out int id);
		Assert.True(double.IsNaN(x));
		Assert.True(double.IsNaN(y));
		Assert.Equal(0.0, z);
		Assert.True(double.IsNaN(m));
		Assert.Equal(0, id);
	}

	[Fact]
	public void CanPolylineZMID()
	{
		var bytes = ShapeBuffers.GetBytesPolylineZM1();
		var buffer = new ShapeBuffer(bytes);

		Assert.Equal(296, buffer.Length);
		Assert.Equal(ShapeType.GeneralPolyline, buffer.ShapeType);
		Assert.Equal(GeometryType.Polyline, buffer.GeometryType);
		Assert.True(buffer.HasZ);
		Assert.True(buffer.HasM);
		Assert.True(buffer.HasID);
		Assert.False(buffer.MayHaveCurves); // this shape does not have the Curves flag
		Assert.False(buffer.IsEmpty);
		Assert.Equal(6, buffer.NumPoints);
		Assert.Equal(1, buffer.NumParts);
		Assert.Equal(0, buffer.NumCurves);
		Assert.Equal(
			"MULTILINESTRING ZM ((2652556.4 1223107.7 0 NaN, 2652715.2 1223240.0 -12 NaN, 2652691.3 1223110.3 403 NaN, 2652852.7 1223247.9 404 NaN, 2652799.8 1223105.1 405 NaN, 2652979.7 1223237.3 0 NaN))",
			buffer.ToWKT(1));
	}

	[Fact]
	public void CanPolylineZMCurves()
	{
		var bytes = ShapeBuffers.GetBytesPolylineZM2();
		var buffer = new ShapeBuffer(bytes);

		Assert.Equal(280, buffer.Length);
		Assert.Equal(ShapeType.GeneralPolyline, buffer.ShapeType);
		Assert.Equal(GeometryType.Polyline, buffer.GeometryType);
		Assert.True(buffer.HasZ);
		Assert.True(buffer.HasM);
		Assert.False(buffer.HasID);
		Assert.True(buffer.MayHaveCurves); // this shape has the Curves flag
		Assert.False(buffer.IsEmpty);
		Assert.Equal(4, buffer.NumPoints);
		Assert.Equal(1, buffer.NumParts);
		Assert.Equal(2, buffer.NumCurves);
		// The curves (i.e., segment modifiers) don't show up in the WKT:
		Assert.Equal(
			"MULTILINESTRING ZM ((2652360.6 1222880.2 0 NaN, 2652564.3 1223025.7 0 NaN, 2652807.8 1223009.8 0 NaN, 2652982.4 1222888.1 0 NaN))",
			buffer.ToWKT(1));
	}

	[Fact]
	public void CanPointBuffer()
	{
		var bytes = ShapeBuffers.GetBytesPointXY();
		var buffer = new ShapeBuffer(bytes);

		Assert.Equal(20, buffer.Length);
		Assert.Equal(ShapeType.GeneralPoint, buffer.ShapeType);
		Assert.Equal(GeometryType.Point, buffer.GeometryType);
		Assert.False(buffer.HasZ);
		Assert.False(buffer.HasM);
		Assert.False(buffer.HasID);
		Assert.False(buffer.IsEmpty);
		Assert.Equal(1, buffer.NumPoints);
		Assert.Equal(1, buffer.NumParts);
		Assert.Equal(0, buffer.NumCurves);
		Assert.Equal("POINT (2696602.9 1233151.7)", buffer.ToWKT(1));
		buffer.QueryCoords(0, out double x, out double y, out double z, out double m, out int id);
		Assert.Equal(2696602.9, x, 1);
		Assert.Equal(1233151.7, y, 1);
		Assert.Equal(0.0, z);
		Assert.True(double.IsNaN(m));
		Assert.Equal(0, id);
	}

	[Fact]
	public void CanMultipointID()
	{
		var bytes = ShapeBuffers.GetBytesMultipointID();
		var buffer = new ShapeBuffer(bytes);

		Assert.Equal(180, buffer.Length);
		Assert.Equal(ShapeType.GeneralMultipoint, buffer.ShapeType);
		Assert.Equal(GeometryType.Multipoint, buffer.GeometryType);
		Assert.False(buffer.HasZ);
		Assert.False(buffer.HasM);
		Assert.True(buffer.HasID);
		Assert.False(buffer.IsEmpty);
		Assert.Equal(7, buffer.NumPoints);
		Assert.Equal(7, buffer.NumParts);
		Assert.Equal(0, buffer.NumCurves);
		Assert.Equal(
			"MULTIPOINT ((2652527.3 1222814.0), (2652680.8 1222851.0), (2652794.5 1222795.5), (2652691.3 1222713.5), (2652466.5 1222583.8), (2652559.1 1222697.6), (2652657.0 1222538.8))",
			buffer.ToWKT(1));
		int id;
		buffer.QueryCoords(2, out _, out _, out _, out _, out id);
		Assert.Equal(1, id);
		buffer.QueryCoords(3, out _, out _, out _, out _, out id);
		Assert.Equal(1, id);
		buffer.QueryCoords(5, out _, out _, out _, out _, out id);
		Assert.Equal(1, id);
	}

	[Fact]
	public void CanToWKT()
	{
		// Just test that all overloads yield the same result.
		// The underlying WKT writer has its own unit tests elsewhere.

		var bytes = ShapeBuffers.GetBytesPointXY();
		var buffer = new ShapeBuffer(bytes);

		Assert.Equal(20, buffer.Length);

		const string expected = "POINT (2696602.9 1233151.7)";

		string s = buffer.ToWKT(1);
		Assert.Equal(expected, s);

		var sb = new StringBuilder();
		buffer.ToWKT(sb, 1);
		Assert.Equal(expected, sb.ToString());

		var tw = new StringWriter();
		buffer.ToWKT(tw, 1);
		Assert.Equal(expected, tw.ToString());
	}
}
