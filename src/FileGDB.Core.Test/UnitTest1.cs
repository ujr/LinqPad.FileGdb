using System;
using System.IO;
using System.IO.Compression;
using FileGDB.Core.Shapes;
using Xunit;
using Xunit.Abstractions;

namespace FileGDB.Core.Test;

public sealed class UnitTest1 : IDisposable
{
	private readonly string _myTempPath;
	private readonly ITestOutputHelper _output;

	public UnitTest1(ITestOutputHelper output)
	{
		_output = output ?? throw new ArgumentNullException(nameof(output));

		var zipArchivePath = TestUtils.GetTestDataPath("Test1.gdb.zip");
		Assert.True(File.Exists(zipArchivePath), $"File does not exist: {zipArchivePath}");

		_myTempPath = TestUtils.CreateTempFolder();

		ZipFile.ExtractToDirectory(zipArchivePath, _myTempPath);
	}

	public void Dispose()
	{
		// remove test data from temp folder
		const bool recursive = true;
		Directory.Delete(_myTempPath, recursive);
	}

	[Fact]
	public void DumpTable1()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");
		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable("Table1");

		foreach (var field in table.Fields)
		{
			_output.WriteLine($"Field {field.Name}, alias \"{field.Alias}\", type {field.Type}, length={field.Length}, nullable={field.Nullable}");
		}

		_output.WriteLine("");
		_output.WriteLine($"RowCount = {table.RowCount}, MaxObjectID = {table.MaxObjectID}");
		_output.WriteLine("");

		for (int oid = 1; oid <= table.MaxObjectID; oid++)
		{
			var row = table.ReadRow(oid);
			if (row is null) continue;

			_output.WriteLine($"{oid,3:N0}  {row[0]}  {row[1]}  {row[2]}  {row[3]}");
		}
	}

	[Fact]
	public void DumpPoints1()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");
		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable("Point1");

		foreach (var field in table.Fields)
		{
			_output.WriteLine($"Field {field.Name}, type {field.Type}, length={field.Length}, nullable={field.Nullable}");
		}

		_output.WriteLine("");
		_output.WriteLine($"RowCount = {table.RowCount}, MaxObjectID = {table.MaxObjectID}");
		_output.WriteLine("");

		for (int oid = 1; oid <= table.MaxObjectID; oid++)
		{
			var row = table.ReadRow(oid);
			if (row is null) continue;

			_output.WriteLine($"{oid,3:N0}  {row[0]}  {row[1]}  {row[2]}  {row[3]}  {row[4]}");
		}
	}

	[Fact]
	public void CanReadPoints1()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");
		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable("Point1");

		// This table has one row: Shape=Point, Code=1, Text="One", Size=12.3

		var rows = table.ReadRows(null, null);

		Assert.True(rows.Step());

		Assert.True(rows.HasShape);

		var shapeBuffer = rows.Shape?.ShapeBuffer ?? throw new Exception("Shape is null");
		Assert.Equal(GeometryType.Point, shapeBuffer.GeometryType);
		Assert.False(shapeBuffer.HasZ);
		Assert.False(shapeBuffer.HasM);
		Assert.False(shapeBuffer.HasID);
		Assert.Equal(1, shapeBuffer.NumParts);
		Assert.Equal(1, shapeBuffer.NumPoints);

		var shape = rows.Shape?.Shape ?? throw new Exception("Shape is null");
		var point = Assert.IsAssignableFrom<PointShape>(shape);
		Assert.Equal(GeometryType.Point, point.GeometryType);
		Assert.False(point.IsEmpty);
		Assert.Equal(684219.55, point.X, 2);
		Assert.Equal(244089.29, point.Y, 2);
		Assert.False(point.HasZ);
		Assert.False(point.HasM);
		Assert.False(point.HasID);

		object? value = rows.GetValue("Code");
		Assert.IsType<int>(value);
		Assert.Equal(1, value);

		value = rows.GetValue("Text");
		Assert.IsType<string>(value);
		Assert.Equal("One", value);

		value = rows.GetValue("Size");
		Assert.IsType<double>(value);
		Assert.Equal(12.3, value);

		value = rows.GetValue("SHAPE");
		Assert.IsType<GeometryBlob>(value);

		Assert.Throws<FileGDBException>(() => rows.GetValue("NoSuchFieldHere"));

		Assert.False(rows.Step()); // no more rows
	}

	[Fact]
	public void CanReadTable1()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");
		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable("Table1");

		// This table has one row: Code=1, Text="One", Size=12.3

		var rows = table.ReadRows(null, null);

		Assert.True(rows.Step());

		Assert.False(rows.HasShape);
		Assert.Throws<FileGDBException>(() => rows.GetValue("SHAPE"));

		object? value = rows.GetValue("Code");
		Assert.IsType<int>(value);
		Assert.Equal(1, value);

		value = rows.GetValue("Text");
		Assert.IsType<string>(value);
		Assert.Equal("One", value);

		value = rows.GetValue("Size");
		Assert.IsType<double>(value);
		Assert.Equal(12.3, value);

		Assert.False(rows.Step()); // no more rows
	}

	private string GetTempDataPath(string fileName)
	{
		return Path.Combine(_myTempPath, fileName);
	}
}
