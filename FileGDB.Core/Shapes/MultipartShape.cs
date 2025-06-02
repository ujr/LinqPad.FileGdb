using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FileGDB.Core.Shapes;

public abstract class MultipartShape : PointListShape
{
	private readonly IReadOnlyList<int>? _partStarts;
	private readonly IReadOnlyList<SegmentModifier>? _curves;

	protected MultipartShape(uint shapeType,
		IReadOnlyList<XY> xys, IReadOnlyList<double>? zs = null,
		IReadOnlyList<double>? ms = null, IReadOnlyList<int>? ids = null,
		IReadOnlyList<int>? partVertexCounts = null,
		IReadOnlyList<SegmentModifier>? curves = null)
		: base(shapeType, xys, zs, ms, ids)
	{
		_partStarts = GetPartStarts(partVertexCounts, NumPoints);
		_curves = ValidateCurves(curves, NumPoints);
	}

	private static int[]? GetPartStarts(IReadOnlyList<int>? partVertexCounts, int totalVertexCount)
	{
		if (partVertexCounts is null)
		{
			return null;
		}

		int startIndex = 0;
		int numParts = partVertexCounts.Count;

		var starts = new int[numParts];

		for (int i = 0; i < numParts; i++)
		{
			starts[i] = startIndex;
			startIndex += partVertexCounts[i];
		}

		if (totalVertexCount >= 0 && startIndex != totalVertexCount)
		{
			throw new ArgumentException("Given part vertex counts don't sum up to NumPoints");
		}

		return starts;
	}

	private static IReadOnlyList<SegmentModifier>? ValidateCurves(IReadOnlyList<SegmentModifier>? curves, int numPoints)
	{
		if (curves is null || curves.Count < 1)
		{
			return null;
		}

		for (int i = 0; i < curves.Count; i++)
		{
			int segmentIndex = curves[i].SegmentIndex;

			if (segmentIndex < 0 || segmentIndex >= numPoints)
			{
				throw new ArgumentException($"Curve segment index ({segmentIndex}) is out of range");
			}

			if (i > 0 && segmentIndex <= curves[i - 1].SegmentIndex)
			{
				throw new ArgumentException("Curved segments must be ordered by increasing segment index");
			}
		}

		return curves;
	}

	protected internal List<int> GetPartCounts(List<int>? result = null)
	{
		result ??= new List<int>(_partStarts?.Count ?? 1);
		return GetPartCounts(result, _partStarts, NumPoints);
	}

	private static List<int> GetPartCounts(List<int> result, IReadOnlyList<int>? partStarts, int numPoints)
	{
		result.Clear();

		if (partStarts is null || partStarts.Count < 1)
		{
			result.Add(numPoints);
			return result;
		}

		int i = partStarts[0]; // always 0
		int n = partStarts.Count;

		for (int j = 1; j < n; j++)
		{
			int k = partStarts[j];
			result.Add(k - i);
			i = k;
		}

		result.Add(numPoints - i);

		return result;
	}

	public int NumParts => _partStarts?.Count ?? 1;

	public int NumCurves => _curves?.Count ?? 0;

	public IReadOnlyList<SegmentModifier> Curves => _curves ?? Array.Empty<SegmentModifier>();

	/// <returns>Index into coordinate lists where part <paramref name="partIndex"/> starts</returns>
	/// <remarks>Returns 0 or NumPoints if <paramref name="partIndex"/> is out of range </remarks>
	public int GetPartStart(int partIndex)
	{
		if (partIndex <= 0) return 0;
		if (_partStarts is null) return NumPoints;
		if (partIndex >= _partStarts.Count) return NumPoints;
		return _partStarts[partIndex];
	}

	/// <summary>
	/// Get start index and point count for part <paramref name="partIndex"/>.
	/// Throw an exception if <paramref name="partIndex"/> is out of range.
	/// </summary>
	protected void GetPartStartAndCount(int partIndex, out int start, out int count)
	{
		if (partIndex < 0 || partIndex >= NumParts)
			throw new ArgumentOutOfRangeException(nameof(partIndex), partIndex, null);

		start = _partStarts?[partIndex] ?? 0;

		int nextStart = partIndex < NumParts - 1
			? _partStarts![partIndex + 1]
			: NumPoints;

		count = nextStart - start;
	}

	protected IReadOnlyList<SegmentModifier>? GetPartCurves(int partIndex)
	{
		if (_curves is null) return null;
		int start = GetPartStart(partIndex);
		int limit = GetPartStart(partIndex + 1);
		return _curves
			.Where(c => start <= c.SegmentIndex && c.SegmentIndex < limit)
			.ToList();
	}

	protected void WriteCoordinates(WKTWriter writer)
	{
		for (int i = 0, j = 0; i < NumPoints; i++)
		{
			int k = GetPartStart(j);
			if (i == k) // first vertex of new part
				//if (j < _partStarts.Count && i == _partStarts[j])
			{
				writer.NewPart();
				j += 1;
			}

			var xy = CoordsXY[i];
			var z = CoordsZ is null ? DefaultZ : CoordsZ[i];
			var m = CoordsM is null ? DefaultM : CoordsM[i];
			var id = CoordsID?[i] ?? DefaultID; // null unless HasID

			writer.AddVertex(xy.X, xy.Y, z, m, id);
		}
	}

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));

		var shapeType = GetShapeType();
		int length = ShapeBuffer.GetMultipartBufferSize(HasZ, HasM, HasID, NumParts, NumPoints);

		bool mayHaveCurves = ShapeBuffer.GetMayHaveCurves(shapeType);
		int numCurves = _curves?.Count ?? 0;

		if (mayHaveCurves || numCurves > 0) length += 4;

		foreach (var modifier in Curves)
		{
			length += 4 + 4; // startIndex and curveType

			switch (modifier)
			{
				case CircularArcModifier:
					length += 8 + 8 + 4;
					break;
				case CubicBezierModifier:
					length += 8 + 8 + 8 + 8;
					break;
				case EllipticArcModifier:
					length += 8 + 8 + 8 + 8 + 8 + 4;
					break;
				default:
					throw new NotSupportedException(
						$"Unknown segment modifier: {modifier.GetType().Name} " +
						$"(curve type {modifier.CurveType})");
			}
		}

		if (bytes is null || bytes.Length - offset < length)
			return length;

		// Empty Multipart is handled implicitly: zero for numPoints and numParts,
		// NaN for all min/max values (including Zmin/max if hasZ and Mmin/max if hasM)

		offset += ShapeBuffer.WriteShapeType(shapeType, bytes, offset);

		var box = Box;
		offset += ShapeBuffer.WriteDouble(box.XMin, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.YMin, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.XMax, bytes, offset);
		offset += ShapeBuffer.WriteDouble(box.YMax, bytes, offset);

		var numPoints = NumPoints;
		offset += ShapeBuffer.WriteInt32(numPoints, bytes, offset);

		var xys = CoordsXY;
		for (int i = 0; i < numPoints; i++)
		{
			offset += ShapeBuffer.WriteDouble(xys[i].X, bytes, offset);
			offset += ShapeBuffer.WriteDouble(xys[i].Y, bytes, offset);
		}

		if (HasZ)
		{
			offset += ShapeBuffer.WriteDouble(box.ZMin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(box.ZMax, bytes, offset);

			var zs = CoordsZ ?? throw Bug("HasZ is true but CoordsZ is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteDouble(zs[i], bytes, offset);
			}
		}

		if (HasM)
		{
			offset += ShapeBuffer.WriteDouble(box.MMin, bytes, offset);
			offset += ShapeBuffer.WriteDouble(box.MMax, bytes, offset);

			var ms = CoordsM ?? throw Bug("HasM is true but CoordsM is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteDouble(ms[i], bytes, offset);
			}
		}

		if (mayHaveCurves || numCurves > 0)
		{
			offset += ShapeBuffer.WriteInt32(numCurves, bytes, offset);

			Debug.Assert(_curves is not null);

			for (int i = 0; i < numCurves; i++)
			{
				var modifier = _curves[i];
				offset += modifier.WriteShapeBuffer(bytes, offset);
			}
		}

		if (HasID)
		{
			var ids = CoordsID ?? throw Bug("HasID is true but CoordsID is null");
			for (int i = 0; i < numPoints; i++)
			{
				offset += ShapeBuffer.WriteInt32(ids[i], bytes, offset);
			}
		}

		Debug.Assert(bytes.Length == offset);

		return length;
	}
}
