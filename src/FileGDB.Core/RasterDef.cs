namespace FileGDB.Core;

/// <summary>
/// Very unsure and not yet investigated. Should be related
/// to ArcObjects IRasterDef (and IRasterDef2 and IRasterDef3),
/// which have properties: Description (string), SpatialReference,
/// IsManaged, IsRasterDataset, IFunction, and IsInline.
/// </summary>
public class RasterDef
{
	public string? RasterColumn { get; set; } // or Description?
	public string? SpatialReference { get; set; } // WKT

	public byte Unknown { get; set; } // Flags?
	public bool RasterHasZ => Unknown is 5 or 7;
	public bool RasterHasM => Unknown is 7;

	public double XOrigin { get; set; } = double.NaN;
	public double YOrigin { get; set; } = double.NaN;
	public double XYScale { get; set; } = double.NaN;
	public double MOrigin { get; set; } = double.NaN;
	public double MScale { get; set; } = double.NaN;
	public double ZOrigin { get; set; } = double.NaN;
	public double ZScale { get; set; } = double.NaN;
	public double XYTolerance { get; set; } = double.NaN;
	public double MTolerance { get; set; } = double.NaN;
	public double ZTolerance { get; set; } = double.NaN;

	/// <summary>0 = external, 1 = managed, 2 = inline</summary>
	public byte RasterType { get; set; }
}
