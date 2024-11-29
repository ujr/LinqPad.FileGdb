using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace FileGDB.Core;

/// <summary>
/// The original bytes stored in the File GDB that represent a geometry.
/// Can translate the same geometry to an Esri Shape Buffer.
/// </summary>
public class GeometryBlob
{
	private readonly GeometryDef _geomDef;
	private readonly byte[] _blob;
	private IReadOnlyList<byte>? _wrapper; // cache
	private ShapeBuffer? _buffer; // cache

	public GeometryBlob(GeometryDef geomDef, byte[] blob)
	{
		_geomDef = geomDef ?? throw new ArgumentNullException(nameof(geomDef));
		_blob = blob ?? throw new ArgumentNullException(nameof(blob));
	}

	/// <summary>
	/// The number of bytes in this geometry blob.
	/// </summary>
	public int Length => _blob.Length;

	/// <summary>
	/// The bytes stored in the File GDB in the geometry blob.
	/// </summary>
	public IReadOnlyList<byte> Bytes => _wrapper ??= new ReadOnlyCollection<byte>(_blob);

	/// <summary>
	/// The geometry translated to Esri Shape Buffer format.
	/// Throws a <see cref="FileGDBException"/> if we cannot
	/// parse the geometry blob.
	/// </summary>
	public ShapeBuffer ShapeBuffer
	{
		get
		{
			if (_buffer is null)
			{
				var reader = new GeometryBlobReader(_geomDef, _blob);
				_buffer = reader.ReadAsShapeBuffer();
				if (!reader.EntireBlobConsumed(out var bytesConsumed))
					throw new FileGDBException(
						$"Geometry BLOB reader consumed only {bytesConsumed} bytes " +
						$"of a total of {_blob.Length} bytes in the BLOB");
			}

			return _buffer;
		}
	}

	public Shape Shape => throw new NotImplementedException();

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
