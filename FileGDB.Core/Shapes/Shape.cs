using System;
using System.Collections.Generic;
using FileGDB.Core.Geometry;

namespace FileGDB.Core.Shapes;

public abstract class Shape
{
	private readonly uint _shapeType;
	private BoxShape? _box;

	public GeometryType GeometryType => ShapeBuffer.GetGeometryType(_shapeType);
	public ShapeFlags Flags => GetShapeFlags(_shapeType);
	public bool HasZ => ShapeBuffer.GetHasZ(_shapeType);
	public bool HasM => ShapeBuffer.GetHasM(_shapeType);
	public bool HasID => ShapeBuffer.GetHasID(_shapeType);

	public const double DefaultZ = ShapeBuffer.DefaultZ;
	public const double DefaultM = ShapeBuffer.DefaultM;
	public const int DefaultID = ShapeBuffer.DefaultID;

	protected Shape(uint shapeType)
	{
		_shapeType = shapeType;
	}

	public uint GetShapeType() => _shapeType;

	public BoxShape Box => _box ??= GetBox();

	public abstract bool IsEmpty { get; }

	public byte[] ToShapeBuffer()
	{
		int length = ToShapeBuffer(null);
		var bytes = new byte[length];
		ToShapeBuffer(bytes);
		return bytes;
	}

	public abstract int ToShapeBuffer(byte[]? bytes, int offset = 0);

	public static Shape FromShapeBuffer(byte[] bytes)
	{
		if (bytes is null)
			throw new ArgumentNullException(nameof(bytes));

		throw new NotImplementedException();
	}

	/// <summary>
	/// Set the box to be reported by the <see cref="Box"/> property.
	/// Set <c>null</c> to force the box to be computed from coordinates.
	/// </summary>
	public Shape SetBox(BoxShape? box)
	{
		_box = box; // null is ok (box will be lazily computed)
		return this;
	}

	public static NullShape Null => NullShape.Singleton;

	protected abstract BoxShape GetBox();

	protected static uint GetShapeType(GeometryType type, ShapeFlags flags)
	{
		uint result;

		switch (type)
		{
			case GeometryType.Null:
				result = (uint)ShapeType.Null;
				break;
			case GeometryType.Point:
				result = (uint)ShapeType.GeneralPoint;
				break;
			case GeometryType.Multipoint:
				result = (uint)ShapeType.GeneralMultipoint;
				break;
			case GeometryType.Polyline:
				result = (uint)ShapeType.GeneralPolyline;
				break;
			case GeometryType.Polygon:
				result = (uint)ShapeType.GeneralPolygon;
				break;
			case GeometryType.Envelope:
				result = (uint)ShapeType.Box;
				break;
			case GeometryType.MultiPatch:
				result = (uint)ShapeType.GeneralMultiPatch;
				break;
			case GeometryType.Any:
			case GeometryType.Bag:
				throw new ArgumentOutOfRangeException(nameof(type), type, "Any and Bag are not supported");
			default:
				throw new ArgumentOutOfRangeException(nameof(type), type, null);
		}

		if ((flags & ShapeFlags.HasZ) != 0)
			result |= (uint)ShapeBuffer.Flags.HasZ;
		if ((flags & ShapeFlags.HasM) != 0)
			result |= (uint)ShapeBuffer.Flags.HasM;
		if ((flags & ShapeFlags.HasID) != 0)
			result |= (uint)ShapeBuffer.Flags.HasID;
		// MultiPatch flags: Materials, Normals, Textures, PartIDs

		if (type == GeometryType.Polyline || type == GeometryType.Polygon)
		{
			result |= (uint)ShapeBuffer.Flags.HasCurves; // TODO unsure
		}

		return result;
	}

	protected BoxShape GetBox(IEnumerable<XY>? xys, IEnumerable<double>? zs, IEnumerable<double>? ms)
	{
		var xmin = double.NaN;
		var xmax = double.NaN;
		var ymin = double.NaN;
		var ymax = double.NaN;

		if (xys is not null)
		{
			foreach (var xy in xys)
			{
				if (double.IsNaN(xmin) || xy.X < xmin) xmin = xy.X;
				if (double.IsNaN(xmax) || xy.X > xmax) xmax = xy.X;

				if (double.IsNaN(ymin) || xy.Y < ymin) ymin = xy.Y;
				if (double.IsNaN(ymax) || xy.Y > ymax) ymax = xy.Y;
			}
		}

		var zmin = double.NaN;
		var zmax = double.NaN;

		if (HasZ && zs is not null)
		{
			foreach (var z in zs)
			{
				if (double.IsNaN(zmin) || z < zmin) zmin = z;
				if (double.IsNaN(zmax) || z > zmax) zmax = z;
			}
		}

		var mmin = double.NaN;
		var mmax = double.NaN;

		if (HasM && ms is not null)
		{
			foreach (var m in ms)
			{
				if (double.IsNaN(mmin) || m < mmin) mmin = m;
				if (double.IsNaN(mmax) || m > mmax) mmax = m;
			}
		}

		return new BoxShape(Flags, xmin, ymin, xmax, ymax, zmin, zmax, mmin, mmax);
	}

	private static ShapeFlags GetShapeFlags(uint shapeType)
	{
		var flags = ShapeFlags.None;
		if (ShapeBuffer.GetHasZ(shapeType)) flags |= ShapeFlags.HasZ;
		if (ShapeBuffer.GetHasM(shapeType)) flags |= ShapeFlags.HasM;
		if (ShapeBuffer.GetHasID(shapeType)) flags |= ShapeFlags.HasID;
		return flags;
	}

	protected static Exception Bug(string message)
	{
		return new InvalidOperationException($"Bug: {message}");
	}
}
