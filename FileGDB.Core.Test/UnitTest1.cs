using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace FileGDB.Core.Test
{
	public class UnitTest1 : IDisposable
	{
		private readonly string _myTempPath;
		private readonly ITestOutputHelper _output;

		public UnitTest1(ITestOutputHelper output)
		{
			_output = output ?? throw new ArgumentNullException(nameof(output));

			var zipArchivePath = GetTestDataPath("Test1.gdb.zip");
			Assert.True(File.Exists(zipArchivePath), $"File does not exist: {zipArchivePath}");

			_myTempPath = CreateTempFolder();

			ZipFile.ExtractToDirectory(zipArchivePath, _myTempPath);
		}

		public void Dispose()
		{
			// remove test data from temp folder
			const bool recursive = true;
			Directory.Delete(_myTempPath, recursive);
		}

		[Fact]
		public void CanOpenFileGDB()
		{
			var gdbPath = GetTempDataPath("Test1.gdb");

			using var gdb = FileGDB.Open(gdbPath);

			Assert.NotNull(gdb);
			Assert.Equal(gdbPath, gdb.FolderPath);
			var tableNames = gdb.Catalog.Select(e => e.Name).ToArray();
			Assert.Contains("GDB_SystemCatalog", tableNames);
			Assert.Contains("GDB_DBTune", tableNames);
			Assert.Contains("GDB_SpatialRefs", tableNames);
		}

		[Fact]
		public void DumpTableSystemCatalog()
		{
			var gdbPath = GetTempDataPath("Test1.gdb");

			using var gdb = FileGDB.Open(gdbPath);
			Assert.Equal(gdbPath, gdb.FolderPath);

			using var table = gdb.OpenTable(1);
			Assert.Equal(gdbPath, table.FolderPath);
			Assert.Equal("a00000001", table.BaseName);
			// inspect table, especially table.Fields
			//long size = table.GetRowSize(1);
			//var bytes = table.ReadRowBytes(1);

			foreach (var field in table.Fields)
			{
				_output.WriteLine($"Field {field.Name}, alias \"{field.Alias}\", type {field.Type}, nullable={field.Nullable}, length={field.Length}");
			}

			_output.WriteLine("");

			for (int oid = 1; oid <= table.MaxObjectID; oid++)
			{
				var row = table.ReadRow(oid);
				if (row is null) continue; // deleted
				_output.WriteLine($"{oid,5:N0}  {row[1]}  (format={row[2]})");
			}

			// 1  GDB_SystemCatalog  ("a00000001")
			// 2  GDB_DBTune  ("a00000002")
			// 3  GDB_SpatialRefs
			// 4  GDB_Items
			// 5  GDB_ItemTypes
			// 6  GDB_ItemRelationships
			// 7  GDB_ItemRelationshipTypes
			// 8  GDB_ReplicaLog
			// 9+ User tables

			_output.WriteLine("");

			foreach (var index in table.Indexes)
			{
				_output.WriteLine($"Index {index.Name}, field {index.FieldName}");
			}
		}

		[Fact]
		public void DumpTableDbTune()
		{
			var gdbPath = GetTempDataPath("Test1.gdb");
			using var gdb = FileGDB.Open(gdbPath);
			using var table = gdb.OpenTable(2);

			foreach (var field in table.Fields)
			{
				_output.WriteLine($"Field {field.Name}, type {field.Type}, length={field.Length}, nullable={field.Nullable}");
			}

			_output.WriteLine("");

			// Fields: Keyword, ParameterName, ConfigString (all type String)
			// Interesting: no OID!?!

			for (int oid = 1; oid <= table.MaxObjectID; oid++)
			{
				var row = table.ReadRow(oid);
				_output.WriteLine($"{oid,3:N0}  {row?[0]}  {row?[1]}  {row?[2]}");
			}
		}

		[Fact]
		public void DumpTableSpatialRefs()
		{
			var gdbPath = GetTempDataPath("Test1.gdb");
			using var gdb = FileGDB.Open(gdbPath);
			using var table = gdb.OpenTable(3);

			foreach (var field in table.Fields)
			{
				_output.WriteLine($"Field {field.Name}, type {field.Type}, length={field.Length}, nullable={field.Nullable}");
			}

			_output.WriteLine("");
			_output.WriteLine($"RowCount = {table.RowCount}");
			_output.WriteLine("");

			for (int oid = 1; oid <= table.MaxObjectID; oid++)
			{
				var row = table.ReadRow(oid);
				if (row is null) continue;

				_output.WriteLine($"{oid,3:N0}  {row[0]}  {row[1]}");
			}
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

			var rows = table.Search(null, null, null);

			Assert.True(rows.Step());

			Assert.True(rows.HasShape);

			var shapeBuffer = rows.Shape ?? throw new Exception("Shape is null");
			Assert.Equal(GeometryType.Point, shapeBuffer.GeometryType);
			Assert.False(shapeBuffer.HasZ);
			Assert.False(shapeBuffer.HasM);
			Assert.False(shapeBuffer.HasID);
			Assert.Equal(1, shapeBuffer.NumParts);
			Assert.Equal(1, shapeBuffer.NumPoints);

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
			Assert.IsType<ShapeBuffer>(value);

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

			var rows = table.Search(null, null, null);

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

		#region Move to TestUtils.cs or similar

		public static string GetTestDataPath(string fileName)
		{
			const string testDataFolder = "TestData";
			var assembly = Assembly.GetCallingAssembly();
			var location = assembly.Location;
			var parent = Directory.GetParent(location);
			var root = parent?.FullName ?? ".";

			return Path.Combine(root, testDataFolder, fileName);
		}

		public static string CreateTempFolder()
		{
			const int attempts = 20;

			var tempPath = Path.GetTempPath(); // e.g. AppData\Local\Temp

			for (int i = 0; i < attempts; i++)
			{
				var name = Path.GetRandomFileName();
				name = Path.ChangeExtension(name, null);
				var path = Path.Combine(tempPath, name);
				if (File.Exists(path)) continue;
				if (Directory.Exists(path)) continue;
				var info = Directory.CreateDirectory(path);
				return info.FullName;
			}

			throw new IOException("Failed to create a temporary folder");
		}

		#endregion
	}
}
