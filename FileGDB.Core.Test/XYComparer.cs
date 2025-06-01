using System.Collections.Generic;

namespace FileGDB.Core.Test;

/// <summary>
/// Compare two <see cref="XY"/> for equality
/// within a given tolerance (Euclidean distance)
/// </summary>
public class XYComparer : IEqualityComparer<XY>
{
	private readonly double _toleranceSquared;

	public XYComparer(double tolerance)
	{
		_toleranceSquared = tolerance * tolerance;
	}

	public bool Equals(XY a, XY b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		var d = dx * dx + dy * dy;
		return d <= _toleranceSquared;
	}

	public int GetHashCode(XY pair)
	{
		return pair.GetHashCode();
	}
}
