using System.Collections.Generic;

namespace FileGDB.Core;

public class GeometryDef
{
	public GeometryDef(GeometryType type, bool hasZ = false, bool hasM = false, bool hasID = false)
	{
		GeometryType = type;
		HasZ = hasZ;
		HasM = hasM;
		HasID = hasID;
		Grid = new GridIndex();
		Extent = new Envelope();
	}

	private GeometryDef()
	{
		GeometryType = GeometryType.Null;
		Grid = new GridIndex();
		Extent = new Envelope();
	}

	public GeometryType GeometryType { get; }
	public string? SpatialReference { get; set; }

	public double XOrigin { get; set; }
	public double YOrigin { get; set; }
	public double XYScale { get; set; }
	public double XYTolerance { get; set; }

	public bool HasZ { get; set; }
	public double ZOrigin { get; set; }
	public double ZScale { get; set; }
	public double ZTolerance { get; set; }

	public bool HasM { get; set; }
	public double MOrigin { get; set; }
	public double MScale { get; set; }
	public double MTolerance { get; set; }

	public bool HasID { get; set; }

	public Envelope Extent { get; }

	public GridIndex Grid { get; }

	public static GeometryDef None { get; } = new();

	public class GridIndex
	{
		private Dictionary<int, double>? _gridSizes = new();

		public int Count { get; set; }

		public double this[int index]
		{
			get => GridSizes.TryGetValue(index, out var value) ? value : 0;
			set => GridSizes[index] = value;
		}

		private IDictionary<int, double> GridSizes => _gridSizes ??= new();
	}
}
