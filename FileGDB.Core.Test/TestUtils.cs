using System.IO;
using System.Reflection;

namespace FileGDB.Core.Test;

public static class TestUtils
{
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
}
