namespace FileGDB.Core;

public class FieldInfo
{
	public string Name { get; }
	public string? Alias { get; }
	public FieldType Type { get; }
	public bool Nullable { get; set; }
	public int Length { get; set; }
	// Precision and Scale are always zero for File GDBs
	// Required? Editable? Domain? DomainFixed? DefaultValue?
	public GeometryDef? GeometryDef { get; set; }
	public int? RasterType { get; set; } // TODO eventually "RasterDef" à la GeometryDef

	public FieldInfo(string name, string? alias, FieldType type)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Alias = alias;
		Type = type;
	}

	public override string ToString()
	{
		return $"{Name} Type={Type} Alias={Alias}";
	}
}
