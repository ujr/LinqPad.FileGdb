using System;

namespace FileGDB.Core;

public class FieldInfo
{
	public string Name { get; }
	public string? Alias { get; }
	public FieldType Type { get; }
	public bool Nullable { get; set; }
	public bool Required { get; set; } // true means: can't delete
	public bool Editable { get; set; }
	public int Length { get; set; }
	// Precision and Scale are always zero for File GDBs
	// Domain? DomainFixed? (seem not stored in FGDB)
	public object? DefaultValue { get; set; }
	public GeometryDef? GeometryDef { get; set; }
	public int? RasterType { get; set; } // TODO eventually "RasterDef" à la GeometryDef
	public int Size { get; set; } // TODO EXPERIMENTAL
	public int Flags { get; set; } // TODO EXPERIMENTAL

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
