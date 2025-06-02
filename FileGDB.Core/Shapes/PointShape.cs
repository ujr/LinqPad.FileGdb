using System;
using System.Diagnostics;

namespace FileGDB.Core.Shapes;

public class PointShape : Shape
{
	public double X { get; }
	public double Y { get; }
	public double Z { get; }
	public double M { get; }
	public int ID { get; }

	public static PointShape Empty { get; } = new(double.NaN, double.NaN);

	public PointShape(ShapeFlags flags, double x, double y, double z, double m, int id)
		: base(GetShapeType(GeometryType.Point, flags))
	{
		X = x;
		Y = y;
		Z = z;
		M = m;
		ID = id;
	}

	public PointShape(double x, double y, double z = DefaultZ, double m = DefaultM, int id = -1)
		: this(GuessFlags(z, m, id), x, y, z, m, id) { }

	private static ShapeFlags GuessFlags(double z, double m, int id)
	{
		var flags = ShapeFlags.None;
		if (!double.IsNaN(z)) flags |= ShapeFlags.HasZ;
		if (!double.IsNaN(m)) flags |= ShapeFlags.HasM;
		if (id >= 0) flags |= ShapeFlags.HasID;
		return flags;
	}

	protected override BoxShape GetBox()
	{
		return new BoxShape(Flags, X, Y, X, Y, Z, Z, M, M);
	}

	public override bool IsEmpty => double.IsNaN(X) || double.IsNaN(Y);

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		if (offset < 0)
			throw new ArgumentOutOfRangeException(nameof(offset));

		int length = ShapeBuffer.GetPointBufferSize(HasZ, HasM, HasID);
		if (bytes is null || bytes.Length - offset < length)
			return length;

		var shapeType = GetShapeType();
		offset += ShapeBuffer.WriteShapeType(shapeType, bytes, offset);

		if (IsEmpty)
		{
			offset += ShapeBuffer.WriteDouble(double.NaN, bytes, offset);
			offset += ShapeBuffer.WriteDouble(double.NaN, bytes, offset);
			if (HasZ) offset += ShapeBuffer.WriteDouble(DefaultZ, bytes, offset);
			if (HasM) offset += ShapeBuffer.WriteDouble(DefaultM, bytes, offset);
			if (HasID) offset += ShapeBuffer.WriteInt32(DefaultID, bytes, offset);
		}
		else
		{
			offset += ShapeBuffer.WriteDouble(X, bytes, offset);
			offset += ShapeBuffer.WriteDouble(Y, bytes, offset);
			if (HasZ) offset += ShapeBuffer.WriteDouble(Z, bytes, offset);
			if (HasM) offset += ShapeBuffer.WriteDouble(M, bytes, offset);
			if (HasID) offset += ShapeBuffer.WriteInt32(ID, bytes, offset);
		}

		Debug.Assert(bytes.Length == offset);

		return length;
	}
}
