namespace FileGDB.Core;

public class GeometryDef
{
	public GeometryDef(GeometryType type, bool hasZ = false, bool hasM = false)
	{
		GeometryType = type;
		HasZ = hasZ;
		HasM = hasM;
		Extent = new Envelope();
	}

	public GeometryType GeometryType { get; }
	public string? SpatialReference { get; set; }

	public double XOrigin { get; set; }
	public double YOrigin { get; set; }
	public double XYScale { get; set; }
	public double XYTolerance { get; set; }

	public bool HasZ { get; }
	public double ZOrigin { get; set; }
	public double ZScale { get; set; }
	public double ZTolerance { get; set; }

	public bool HasM { get; }
	public double MOrigin { get; set; }
	public double MScale { get; set; }
	public double MTolerance { get; set; }

	public Envelope Extent { get; }

	public double GridSize0 { get; set; } = double.NaN;
	public double GridSize1 { get; set; } = double.NaN;
	public double GridSize2 { get; set; } = double.NaN;
}
