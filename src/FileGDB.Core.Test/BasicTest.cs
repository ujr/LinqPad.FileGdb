using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace FileGDB.Core.Test;

public sealed class BasicTest : IDisposable
{
	private readonly string _myTempPath;
	private readonly ITestOutputHelper _output;

	public BasicTest(ITestOutputHelper output)
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
	public void CanOpenFileGDB()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");

		using var gdb = FileGDB.Open(gdbPath);

		Assert.NotNull(gdb);
		Assert.Equal(gdbPath, gdb.FolderPath);
	}

	[Fact]
	public void CanReadCatalog()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");

		using var gdb = FileGDB.Open(gdbPath);

		var tableNames = gdb.Catalog.Select(e => e.Name).ToArray();
		Assert.Contains("GDB_SystemCatalog", tableNames);
		Assert.Contains("GDB_DBTune", tableNames);
		Assert.Contains("GDB_SpatialRefs", tableNames);

		// System Catalog is always first entry (ID 1):
		var systemEntry = gdb.Catalog.Single(entry => entry.ID == 1);
		Assert.Equal("GDB_SystemCatalog", systemEntry.Name, StringComparer.OrdinalIgnoreCase);
		Assert.Equal(0, systemEntry.Format); // empirical
		Assert.True(systemEntry.IsSystemTable());
		Assert.False(systemEntry.IsUserTable());
		Assert.True(systemEntry.TableExists());

		// Test GDB contains a table "TABLE1":
		var userEntry = gdb.Catalog.Single(entry => entry.Name == "TABLE1");
		Assert.Equal(0, userEntry.Format); // empirical
		Assert.False(userEntry.IsSystemTable());
		Assert.True(userEntry.IsUserTable());
		Assert.True(userEntry.TableExists());
	}

	[Fact]
	public void CanOpenTable()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");

		using var gdb = FileGDB.Open(gdbPath);

		foreach (var entry in gdb.Catalog)
		{
			if (entry.TableExists(out string reason))
			{
				using var table = entry.OpenTable();

				Assert.NotNull(table);
			}
			else
			{
				_output.WriteLine($"Table {entry.Name} (id={entry.ID}) does not exist: {reason}");
			}
		}
	}

	[Fact]
	public void DumpCatalog()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");

		using var gdb = FileGDB.Open(gdbPath);

		foreach (var entry in gdb.Catalog)
		{
			var exists = entry.TableExists(out string reason);
			reason = exists ? string.Empty : $" ({reason})";
			_output.WriteLine($"{entry.ID,5:N0} {entry.Name} format={entry.Format} exists={exists}{reason}");
		}
	}

	[Fact]
	public void DumpCatalogTable()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");

		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable(1); // table ID=1 is system catalog

		Assert.Equal(gdbPath, table.FolderPath);
		Assert.Equal("a00000001", table.BaseName);
		// inspect table, especially table.Fields
		//long size = table.GetRowSize(1);
		//var bytes = table.ReadRowBytes(1);

		DumpFields(table);

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

		DumpIndexes(table);
	}

	[Fact]
	public void DumpDbTuneTable()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");

		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable(2); // table ID=2 is DBTune table

		DumpFields(table);

		_output.WriteLine("");

		// Fields: Keyword, ParameterName, ConfigString (all type String)
		// Interesting: no OID!?!

		for (int oid = 1; oid <= table.MaxObjectID; oid++)
		{
			var row = table.ReadRow(oid);
			_output.WriteLine($"{oid,3:N0}  {row?[0]}  {row?[1]}  {row?[2]}");
		}

		_output.WriteLine("");

		DumpIndexes(table);
	}

	[Fact]
	public void DumpSpatialRefsTable()
	{
		var gdbPath = GetTempDataPath("Test1.gdb");

		using var gdb = FileGDB.Open(gdbPath);
		using var table = gdb.OpenTable(3); // table ID=3 is SpatialRefs table

		DumpFields(table);

		_output.WriteLine("");
		_output.WriteLine($"RowCount = {table.RowCount}");
		_output.WriteLine("");

		for (int oid = 1; oid <= table.MaxObjectID; oid++)
		{
			var row = table.ReadRow(oid);
			if (row is null) continue;

			_output.WriteLine($"{oid,3:N0}  {row[0]}  {row[1]}");
		}

		_output.WriteLine("");

		DumpIndexes(table);
	}

	private void DumpFields(Table table)
	{
		if (table.Fields.Count > 0)
		{
			foreach (var field in table.Fields)
			{
				_output.WriteLine($"Field {field.Name}, type {field.Type}, length={field.Length}, nullable={field.Nullable}, alias \"{field.Alias}\"");
			}
		}
		else
		{
			_output.WriteLine("No fields");
		}
	}

	private void DumpIndexes(Table table)
	{
		if (table.Indexes.Count > 0)
		{
			foreach (var index in table.Indexes)
			{
				_output.WriteLine($"Index {index.Name}, type {index.IndexType}, field {index.FieldName}, file {index.FileName}");
			}
		}
		else
		{
			_output.WriteLine("No indexes");
		}
	}

	private string GetTempDataPath(string fileName)
	{
		return Path.Combine(_myTempPath, fileName);
	}
}
