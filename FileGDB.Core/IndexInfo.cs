using System;

namespace FileGDB.Core;

public enum IndexType
{
	PrimaryIndex,
	AttributeIndex,
	SpatialIndex
}

public class IndexInfo
{
	public string Name { get; }
	//public string FileName { get; } // aXXXXXXXX.Name.atx/.spx
	public string FieldName { get; } // or Expression? e.g. "LOWER(Name)"
	public IndexType IndexType { get; }
	private string BaseName { get; }

	// Note: the File Geodatabase does not support IsUnique and IsAscending
	// for indexes (Esri documentation for the Add Attribute Index GP tool)

	public IndexInfo(string name, string fieldName, IndexType indexType, string baseName)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
		IndexType = indexType;
		BaseName = baseName ?? throw new ArgumentNullException(nameof(baseName));
	}

	public string FileName => GetFileName();

	private string GetFileName()
	{
		// index name is suffix of index file name:
		// if index name is "foo", file name is "aXXXXXXXX.foo.atx"
		// unless the field is the shape, then it's "aXXXXXXXX.foo.spx"
		// or the field is the implicit object id, then it's "aXXXXXXXX.gdbtablx"

		return IndexType switch
		{
			IndexType.PrimaryIndex => $"{BaseName}.gdbtablx",
			IndexType.SpatialIndex => $"{BaseName}.{Name}.spx",
			IndexType.AttributeIndex => $"{BaseName}.{Name}.atx",
			_ => throw new InvalidOperationException($"Unknown index type: {IndexType}")
		};
	}
}
