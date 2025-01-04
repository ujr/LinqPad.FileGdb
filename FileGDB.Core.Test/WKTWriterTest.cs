using System;
using System.Text;
using Xunit;

namespace FileGDB.Core.Test;

public class WKTWriterTest
{
	[Fact]
	public void CanWritePoint()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		buffer.Clear();
		writer.BeginPoint();
		writer.AddVertex(12.3, 45.6);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("POINT (12.3 45.6)", buffer.ToString());

		buffer.Clear();
		writer.WritePoint(1.1, 2.2, 3.3);
		writer.Flush();
		Assert.Equal("POINT Z (1.1 2.2 3.3)", buffer.ToString());

		buffer.Clear();
		writer.WritePoint(1.1, 2.2, 3.3, 99.9);
		writer.Flush();
		Assert.Equal("POINT ZM (1.1 2.2 3.3 99.9)", buffer.ToString());

		buffer.Clear();
		writer.WritePoint(1.1, 2.2);
		writer.Flush();
		Assert.Equal("POINT (1.1 2.2)", buffer.ToString());

		buffer.Clear();
		writer.BeginPoint(true, true, true);
		writer.AddVertex(1.1, 2.2); // omit Z, M, ID
		writer.EndShape();
		writer.Flush();
		// default values are provided, ID is omitted because EmitID is false:
		Assert.Equal("POINT ZM (1.1 2.2 0 NaN)", buffer.ToString());

		buffer.Clear();
		writer.BeginPoint();
		writer.NewPart(); // an allowed no-op, even for a point
		writer.AddVertex(1, 2);
		Assert.Throws<InvalidOperationException>(() => writer.NewPart());
		writer.EndShape();
	}

	[Fact]
	public void CanWriteEmptyPoint()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		buffer.Clear();
		writer.BeginPoint();
		writer.EndShape();
		writer.Flush();
		Assert.Equal("POINT EMPTY", buffer.ToString());

		buffer.Clear();
		writer.BeginPoint(hasM: true);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("POINT M EMPTY", buffer.ToString());
	}

	[Fact]
	public void CanWriteBox()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		buffer.Clear();
		writer.WriteBox(1, 2, 51, 42);
		writer.Flush();
		Assert.Equal("BOX (1 2, 51 42)", buffer.ToString());

		buffer.Clear();
		writer.WriteBox(1, 2, 51, 52, 400, 432);
		writer.Flush();
		Assert.Equal("BOX Z (1 2 400, 51 52 432)", buffer.ToString());

		buffer.Clear();
		writer.WriteBox(1, 2, 51, 42, mmin: 55.25, mmax: 94.5);
		writer.Flush();
		Assert.Equal("BOX M (1 2 55.25, 51 42 94.5)", buffer.ToString());

		buffer.Clear();
		writer.WriteBox(1, 2, 51, 42, 400, 432, 55.25, 94.5);
		writer.Flush();
		Assert.Equal("BOX ZM (1 2 400 55.25, 51 42 432 94.5)", buffer.ToString());
	}

	[Fact]
	public void CanWriteEmptyBox()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		buffer.Clear();
		writer.WriteBox(double.NaN, double.NaN, double.NaN, double.NaN);
		writer.Flush();
		Assert.Equal("BOX EMPTY", buffer.ToString());

		buffer.Clear();
		writer.WriteBox(double.NaN, double.NaN, double.NaN, double.NaN, mmin: 55.25, mmax: 94.5);
		writer.Flush();
		Assert.Equal("BOX M EMPTY", buffer.ToString());
	}

	[Fact]
	public void CanWriteMultipoint()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		writer.BeginMultipoint(hasZ: true);
		writer.AddVertex(1.1, 1.2, 401.5);
		writer.AddVertex(2.1, 2.2, 402.5);
		writer.AddVertex(3.1, 3.2); // Z defaults to zero
		writer.EndShape();

		writer.Flush();
		Assert.Equal("MULTIPOINT Z ((1.1 1.2 401.5), (2.1 2.2 402.5), (3.1 3.2 0))", buffer.ToString());

		buffer.Clear();
		writer.BeginMultipoint();
		writer.NewPart(); // optional
		writer.NewPart(); // idempotent
		writer.AddVertex(1.1, 1.2);
		writer.NewPart();
		writer.AddVertex(2.1, 2.2);
		writer.NewPart();
		writer.AddVertex(3.1, 3.2);
		writer.NewPart(); // empty parts are not emitted
		writer.NewPart(); // idempotent
		writer.EndShape();
		writer.Flush();
		Assert.Equal("MULTIPOINT ((1.1 1.2), (2.1 2.2), (3.1 3.2))", buffer.ToString());
	}

	[Fact]
	public void CanDefaultAttributeAwareness()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		writer.DecimalDigits = 1;

		writer.WritePoint(1.2, 3.4);
		writer.Flush();
		Assert.Equal("POINT (1.2 3.4)", buffer.ToString());

		writer.DefaultHasZ = true;
		writer.DefaultHasM = true;
		writer.DefaultHasID = true;

		buffer.Clear();
		writer.WritePoint(1.2, 3.4);
		writer.Flush();
		Assert.Equal("POINT ZM (1.2 3.4 0 NaN)", buffer.ToString());

		writer.EmitID = true;

		buffer.Clear();
		writer.WritePoint(1.2, 3.4);
		writer.Flush();
		Assert.Equal("POINT ZMID (1.2 3.4 0 NaN 0)", buffer.ToString());
	}

	[Fact]
	public void CanCurrentAttributeAwareness()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		writer.BeginPoint();
		Assert.False(writer.CurrentHasZ);
		Assert.False(writer.CurrentHasM);
		Assert.False(writer.CurrentHasID);
		writer.EndShape();

		writer.BeginPolygon(hasZ: true, hasID: true);
		Assert.True(writer.CurrentHasZ);
		Assert.False(writer.CurrentHasM);
		Assert.True(writer.CurrentHasID);
		writer.EndShape();

		writer.DefaultHasM = true;
		writer.BeginMultipoint(hasZ: true);
		Assert.True(writer.CurrentHasZ);
		Assert.True(writer.CurrentHasM);
		Assert.False(writer.CurrentHasID);
		writer.EndShape();
	}

	[Fact]
	public void CanWriteLineString()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		buffer.Clear();
		writer.BeginLineString();
		writer.AddVertex(0, 0);
		writer.AddVertex(1, 1);
		writer.AddVertex(1, 2);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("LINESTRING (0 0, 1 1, 1 2)", buffer.ToString());

		buffer.Clear();
		writer.BeginLineString(hasZ: true, hasID: true);
		writer.NewPart(); // an allowed no-op
		writer.AddVertex(0, 0);
		writer.AddVertex(1, 1, 1, id: 1); // id ignored because EmitID is false
		Assert.Throws<InvalidOperationException>(() => writer.NewPart());
		writer.AddVertex(1, 2, 3);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("LINESTRING Z (0 0 0, 1 1 1, 1 2 3)", buffer.ToString());

		buffer.Clear();
		writer.BeginLineString(hasZ: true, hasM: true);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("LINESTRING ZM EMPTY", buffer.ToString());
	}

	[Fact]
	public void CanWriteMultiLineString()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		buffer.Clear();
		writer.BeginMultiLineString();
		writer.AddVertex(0, 0);
		writer.AddVertex(1, 1);
		writer.AddVertex(1, 2);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("MULTILINESTRING ((0 0, 1 1, 1 2))", buffer.ToString());

		buffer.Clear();
		writer.BeginMultiLineString();
		writer.AddVertex(0, 0);
		writer.AddVertex(1, 1);
		writer.AddVertex(1, 2);
		writer.NewPart();
		writer.AddVertex(2, 3);
		writer.AddVertex(3, 2);
		writer.NewPart(); // a no-op because empty
		writer.EndShape();
		writer.Flush();
		Assert.Equal("MULTILINESTRING ((0 0, 1 1, 1 2), (2 3, 3 2))", buffer.ToString());

		buffer.Clear();
		writer.BeginMultiLineString(hasZ: true);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("MULTILINESTRING Z EMPTY", buffer.ToString());
	}

	[Fact]
	public void CanWritePolygon()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		buffer.Clear();
		writer.BeginPolygon();
		writer.AddVertex(0, 0);
		writer.AddVertex(4, 0);
		writer.AddVertex(4, 4);
		writer.AddVertex(0, 4);
		writer.AddVertex(0, 0);
		writer.NewPart(); // for a WKT POLYGON, new part must be a hole
		writer.AddVertex(1, 1);
		writer.AddVertex(2, 1);
		writer.AddVertex(2, 2);
		writer.AddVertex(1, 2);
		writer.AddVertex(1, 1);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("POLYGON ((0 0, 4 0, 4 4, 0 4, 0 0), (1 1, 2 1, 2 2, 1 2, 1 1))", buffer.ToString());

		buffer.Clear();
		writer.BeginPolygon(true, true, true);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("POLYGON ZM EMPTY", buffer.ToString());
	}

	[Fact]//[Fact(Skip="not yet implemented")]
	public void CanWriteMultiPolygon()
	{
		var buffer = new StringBuilder();
		using var writer = new WKTWriter(buffer);

		buffer.Clear();
		writer.BeginMultiPolygon();
		writer.EndShape();
		writer.Flush();
		Assert.Equal("MULTIPOLYGON EMPTY", buffer.ToString());

		buffer.Clear();
		writer.BeginMultiPolygon();
		writer.AddVertex(0, 0);
		writer.AddVertex(4, 0);
		writer.AddVertex(4, 4);
		writer.AddVertex(0, 4);
		writer.AddVertex(0, 0);
		writer.NewPart();
		writer.AddVertex(1, 1);
		writer.AddVertex(2, 1);
		writer.AddVertex(2, 2);
		writer.AddVertex(1, 2);
		writer.AddVertex(1, 1);
		writer.NewPolygon();
		writer.AddVertex(-1, -1);
		writer.AddVertex(-1, -2);
		writer.AddVertex(-2, -2);
		writer.AddVertex(-2, -1);
		writer.AddVertex(-1, -1);
		writer.EndShape();
		writer.Flush();
		Assert.Equal("MULTIPOLYGON (((0 0, 4 0, 4 4, 0 4, 0 0), (1 1, 2 1, 2 2, 1 2, 1 1)), ((-1 -1, -1 -2, -2 -2, -2 -1, -1 -1)))",
			buffer.ToString());
	}
}
