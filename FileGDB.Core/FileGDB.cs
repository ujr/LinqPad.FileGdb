namespace FileGDB.Core;

public class FileGDB
{
	public FileGDB(string gdbFolderPath)
	{
		FolderPath = gdbFolderPath ?? throw new ArgumentNullException(nameof(gdbFolderPath));

	}

	public string FolderPath { get; }

}