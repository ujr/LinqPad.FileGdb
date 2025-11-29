using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using FileGDB.Core.Shapes;

namespace FileGDB.Core;

/// <summary>
/// The original bytes stored in the File Geodatabase that represent
/// a geometry (available through the <see cref="Bytes"/> property).
/// Can translate the same geometry to an Esri Shape Buffer and to
/// an "unpacked" Shape instance (easy access to coordinates).
/// </summary>
public class GeometryBlob
{
	private readonly GeometryDef _geomDef;
	private readonly byte[] _blob;
	private IReadOnlyList<byte>? _wrapper; // cache
	private ShapeBuffer? _buffer; // cache
	private Shape? _shape; // cache

	public GeometryBlob(GeometryDef geomDef, byte[] blob)
	{
		_geomDef = geomDef ?? throw new ArgumentNullException(nameof(geomDef));
		_blob = blob ?? throw new ArgumentNullException(nameof(blob));
	}

	public ShapeType ShapeType => GetShapeType();

	/// <summary>
	/// The number of bytes in this geometry blob.
	/// </summary>
	public int Length => _blob.Length;

	/// <summary>
	/// The bytes stored in the File GDB in the geometry blob.
	/// </summary>
	public IReadOnlyList<byte> Bytes => _wrapper ??= new ReadOnlyCollection<byte>(_blob);

	/// <summary>
	/// This File Geodatabase geometry blob decoded into an Esri Shape
	/// buffer. Throws a <see cref="FileGDBException"/> if we cannot
	/// parse the geometry blob.
	/// </summary>
	/// <remarks>This is a convenience wrapper around <see cref="Read"/></remarks>
	public ShapeBuffer ShapeBuffer => _buffer ??= GetShapeBuffer(null);

	/// <summary>
	/// This File Geodatabase geometry blob decoded into a Shape object.
	/// Throws a <see cref="FileGDBException"/> if we cannot parse the
	/// geometry blob.
	/// </summary>
	/// <remarks>This is a convenience wrapper around <see cref="Read"/></remarks>
	public Shape Shape => _shape ??= GetShape(null);

	private ShapeBuffer GetShapeBuffer(ShapeBuilder? factory, bool validate = true)
	{
		factory ??= new ShapeBuilder();
		Read(factory, validate);
		return factory.ToShapeBuffer();
	}

	private Shape GetShape(ShapeBuilder? factory, bool validate = true)
	{
		factory ??= new ShapeBuilder();
		Read(factory, validate);
		return factory.ToShape();
	}

	/// <summary>
	/// Read this geometry blob into the given shape builder.
	/// </summary>
	/// <remarks>When reading many geometry blobs, prefer this method
	/// (reusing a <see cref="ShapeBuilder"/> instance) over reading the
	/// <see cref="Shape"/> and <see cref="ShapeBuffer"/> properties.</remarks>
	public void Read(ShapeBuilder builder, bool validate = true)
	{
		if (builder is null)
			throw new ArgumentNullException(nameof(builder));

		var reader = new GeometryBlobReader(_geomDef, _blob);

		reader.Read(builder);

		if (validate)
		{
			if (!builder.Validate(out string message))
			{
				throw new FileGDBException(message);
			}

			if (!reader.EntireBlobConsumed(out int bytesConsumed))
			{
				throw new FileGDBException(
					$"Geometry BLOB reader consumed only {bytesConsumed} bytes " +
					$"of a total of {_blob.Length} bytes in the BLOB");
			}
		}
	}

	private ShapeType GetShapeType()
	{
		if (_blob.Length > 0)
		{
			return (ShapeType)(_blob[0] & 0x7F);
		}

		return ShapeType.Null;
	}

	public override string ToString()
	{
		try
		{
			var buffer = ShapeBuffer;
			var sb = new StringBuilder();

			sb.Append(ShapeBuffer.GeometryType);
			if (buffer.IsEmpty)
			{
				sb.Append(" empty");
			}
			else
			{
				var hasZ = buffer.HasZ;
				var hasM = buffer.HasM;
				if (hasZ && hasM) sb.Append(" ZM");
				else if (hasZ) sb.Append(" Z");
				else if (hasM) sb.Append(" M");
			}

			return sb.ToString();
		}
		catch (Exception ex)
		{
			return $"Error: {ex.Message}";
		}
	}
}
