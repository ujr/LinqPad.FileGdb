namespace FileGDB.Core.Shapes;

public class NullShape : Shape
{
	private NullShape() : base((uint)ShapeType.Null) { }

	public override bool IsEmpty => true;

	public override int ToShapeBuffer(byte[]? bytes, int offset = 0)
	{
		const int length = 4;
		if (bytes is null || bytes.Length - offset < length) return length;
		return ShapeBuffer.WriteShapeType((uint)ShapeType.Null, bytes, offset);
	}

	protected override BoxShape GetBox()
	{
		return NullBox;
	}

	public static readonly NullShape Instance = new();
	private static readonly BoxShape NullBox = new(ShapeFlags.None, double.NaN, double.NaN, double.NaN, double.NaN);
}
