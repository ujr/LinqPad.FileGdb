using System;

namespace FileGDB.Core;

public class IndexInfo
{
	public string Name { get; }
	//public string FileName { get; } // aXXXXXXXX.Name.atx/.spx
	public string FieldName { get; } // or Expression? e.g. "LOWER(Name)"

	// Note: the File Geodatabase does not support IsUnique and IsAscending
	// for indexes (Esri documentation for the Add Attribute Index GP tool)

	public IndexInfo(string name, string fieldName)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
	}
}
