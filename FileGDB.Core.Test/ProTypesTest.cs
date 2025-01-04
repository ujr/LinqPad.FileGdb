using System;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace FileGDB.Core.Test;

/// <summary>
/// Since ArcGIS Pro 3.2, the File Geodatabase supports
/// 64bit Object IDs (previously 32bit) and new data types:
/// Big Integer (64 bit), DateOnly, TimeOnly, DateTimeOffset
/// (the last three correspond to the like-named .NET 6 types).
/// </summary>
public class ProTypesTest : IDisposable
{
	private readonly string _myTempPath;

	public ProTypesTest()
	{
		// TestPro32.gdb was created with ArcGIS Pro 3.3 and has two
		// tables: TABLE32 (with 32bit OIDs) and TABLE64 (with 64bit OIDs).
		var archivePath = TestUtils.GetTestDataPath("TestPro32.gdb.zip");
		Assert.True(File.Exists(archivePath));

		_myTempPath = TestUtils.CreateTempFolder();

		ZipFile.ExtractToDirectory(archivePath, _myTempPath);
	}

	public void Dispose()
	{
		// remove test data from temp folder
		const bool recursive = true;
		Directory.Delete(_myTempPath, recursive);
	}

	[Fact]
	public void HasLongOID()
	{
		var gdbPath = GetTempDataPath("TestPro32.gdb");
		using var gdb = FileGDB.Open(gdbPath);

		// Table using classic 32bit Object IDs:
		using var table32 = gdb.OpenTable("TABLE32");
		Assert.Equal(4, table32.Version);

		// Table using new 64bit Object IDs:
		using var table64 = gdb.OpenTable("TABLE64");
		Assert.Equal(6, table64.Version);
	}

	[Fact]
	public void CanReadBigInt()
	{
		object? value1 = ReadRowValue("TABLE32", 1, "BigInt");
		Assert.NotNull(value1);
		long number1 = Assert.IsAssignableFrom<long>(value1);
		Assert.Equal(64, number1);

		object? value2 = ReadRowValue("TABLE32", 2, "BigInt");
		Assert.NotNull(value2);
		long number2 = Assert.IsAssignableFrom<long>(value2);
		Assert.Equal(9007199254740992, number2); // Int64.MaxValue
	}

	[Fact]
	public void CanReadDateOnly()
	{
		var expected = new DateOnly(2024, 12, 31);

		object? value1 = ReadRowValue("TABLE32", 2, "DateOnly");
		Assert.NotNull(value1);
		var dateOnly1 = Assert.IsAssignableFrom<DateOnly>(value1);
		Assert.Equal(expected, dateOnly1);

		object? value2 = ReadRowValue("TABLE64", 2, "DateOnly");
		Assert.NotNull(value2);
		var dateOnly2 = Assert.IsAssignableFrom<DateOnly>(value2);
		Assert.Equal(expected, dateOnly2);
	}

	[Fact]
	public void CanReadTimeOnly()
	{
		var expected = new TimeOnly(12, 41, 53);

		object? value1 = ReadRowValue("TABLE32", 2, "TimeOnly");
		Assert.NotNull(value1);
		var timeOnly1 = Assert.IsAssignableFrom<TimeOnly>(value1);
		Assert.Equal(expected, timeOnly1);

		object? value2 = ReadRowValue("TABLE64", 2, "TimeOnly");
		Assert.NotNull(value2);
		var timeOnly2 = Assert.IsAssignableFrom<TimeOnly>(value2);
		Assert.Equal(expected, timeOnly2);
	}

	[Fact]
	public void CanReadDateTimeOffset()
	{
		// 2025-01-04 12:34:56.789 +01:00
		var expectedDateTime = new DateTime(2025, 1, 4, 12, 34, 56, 789);
		var expectedOffset = new TimeSpan(0, 1, 0, 0);
		var expected = new DateTimeOffset(expectedDateTime, expectedOffset);

		var value1 = ReadRowValue("TABLE32", 2, "DateTimeOffset");
		Assert.NotNull(value1);
		var dateTimeOffset1 = Assert.IsAssignableFrom<DateTimeOffset>(value1);
		Assert.Equal(expected, dateTimeOffset1);

		var value2 = ReadRowValue("TABLE64", 2, "DateTimeOffset");
		Assert.NotNull(value2);
		var dateTimeOffset2 = Assert.IsAssignableFrom<DateTimeOffset>(value2);
		Assert.Equal(expected, dateTimeOffset2);
	}

	private object? ReadRowValue(string tableName, long oid, string fieldName)
	{
		var gdbPath = GetTempDataPath("TestPro32.gdb");
		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable(tableName);
		int fieldIndex = table.FindField(fieldName);
		var values = table.ReadRow(oid);
		return values?[fieldIndex];
	}

	private string GetTempDataPath(string fileName)
	{
		return Path.Combine(_myTempPath, fileName);
	}
}
