using System;

namespace FileGDB.Core;

/// <remarks>Coordinate storage information such as False Origin and
/// resolution are stored in <see cref="CoordinateStorageInfo"/></remarks>
public class SpatialReferenceInfo
{
	// FileGDB has WKT, the other info must be parsed from WKT
	public string Name => throw new NotImplementedException();
	public string WKT { get; }
	public int WKID => throw new NotImplementedException();
	public string Authority => throw new NotImplementedException();

	public SpatialReferenceInfo(string wkt)
	{
		WKT = wkt ?? throw new ArgumentNullException(nameof(wkt));
	}
}

/// <remarks>Esri software has this info as part of the spatial reference</remarks>
public class CoordinateStorageInfo
{
	public double XOrigin { get; }
	public double YOrigin { get; }
	public double XYScale { get; }
	public double XYTolerance { get; }

	public double ZFalseOrigin { get; }
	public double ZResolution { get; }
	public double ZTolerance { get; }

	public double MFalseOrigin { get; }
	public double MResolution { get; }
	public double MTolerance { get; }
}
