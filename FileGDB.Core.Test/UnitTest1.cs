using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Xunit;

namespace FileGDB.Core.Test
{
	public class UnitTest1 : IDisposable
	{
		private readonly string _myTempPath;

		public UnitTest1()
		{
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
		public void Test1()
		{
			var gdbPath = GetTempDataPath("Test1.gdb");

			using var gdb = FileGDB.Open(gdbPath);
			Assert.Equal(gdbPath, gdb.FolderPath);

			using var table = gdb.OpenTable("a00000001");
			Assert.Equal(gdbPath, table.FolderPath);
			Assert.Equal("a00000001", table.BaseName);
			// inspect table, especially table.Fields
			long size = table.GetRecordSize(1);
			var bytes = table.ReadRecordBytes(1);
		}


		private string GetTestDataPath(string fileName)
		{
			const string testDataFolder = "TestData";

			var assembly = Assembly.GetExecutingAssembly();
			var location = assembly.Location;
			var parent = Directory.GetParent(location);
			var root = parent?.FullName ?? ".";

			return Path.Combine(root, testDataFolder, fileName);
		}

		private string GetTempDataPath(string fileName)
		{
			return Path.Combine(_myTempPath, fileName);
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
	}
}
