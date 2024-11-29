using System;

namespace FileGDB.Core;

public class FileGDBException : Exception
{
	public FileGDBException() { }

	public FileGDBException(string message)
		: base(message) { }

	public FileGDBException(string message, Exception? inner)
		: base(message, inner) { }
}
