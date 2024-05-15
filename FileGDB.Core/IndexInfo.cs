namespace FileGDB.Core;

public class IndexInfo
{
	public string Name { get; }
	//public string FileName { get; } // aXXXXXXXX.Name.atx/.spx
	public string FieldName { get; } // or Expression? e.g. "LOWER(Name)"
	public bool IsUnique { get; }
	//public bool IsAscending { get; } // or IsDescending? not with FileGDB?

	public IndexInfo(string name, string fieldName, bool isUnique = false)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
		IsUnique = isUnique;
	}
}
