using System;

namespace FileGDB.Core;

/// <summary>
/// Flags if the shape is of one of the "General" types
/// </summary>
/// <remarks>>
/// Values from Extended Shape Buffer Format white paper
/// and FileGDBCore.h of the File Geodatabase API.
/// </remarks>
[Flags]
public enum ShapeModifiers : uint
{
	HasZs                = 0x80000000,
	HasMs                = 0x40000000,
	HasCurves            = 0x20000000,
	HasIDs               = 0x10000000,
	HasNormals           = 0x08000000,
	HasTextures          = 0x04000000,
	HasPartIDs           = 0x02000000,
	HasMaterials         = 0x01000000,
	ModifierMask         = 0xFF000000, // masks any of the modifiers above
	BasicTypeMask        = 0x000000FF, // bits 0..7 contain the basic shape type
	BasicModifierMask    = 0xC0000000, // HasZs and HasMs
	NonBasicModifierMask = 0x3F000000, // all the other flags besides Z and M
	ExtendedModifierMask = 0xDD000000, // from FileGDBCore.h of FileGDB API, not in Ext Shp Buf Fmt white paper
	MultiPatchModifierMask = 0xF00000, // in FileGDBCore.h of FileGDB API (as decimal 15728640)
	IsCompressed = 0x00800000 // from FileGDBCore.h of FileGDB API, not in Ext Shp Buf Fmt white paper
}
