using System.Text;

namespace FileGDB.Core;

/// <summary>
/// Wrapper around a <see cref="Stream"/> that can read
/// basic data types found in the FileGDB spec.
/// </summary>
/// <remarks>NOT thread-safe!</remarks>
internal class DataReader : IDisposable
{
	private readonly Stream _stream;
	private readonly BinaryReader _reader;

	public DataReader(Stream stream)
	{
		_stream = stream ?? throw new ArgumentNullException(nameof(stream));
		// .NET's BinaryReader uses little endian, as does the FileGDB.
		_reader = new BinaryReader(stream, Encoding.GetEncoding("utf-16"));
	}

	public long Position => _stream.Position;

	public long Length => _stream.Length;

	public void Seek(long offset, SeekOrigin origin = SeekOrigin.Begin)
	{
		_stream.Seek(offset, origin);
	}

	public byte ReadByte()
	{
		return _reader.ReadByte();
	}

	public void ReadBytes(int count, byte[] buffer, int offset = 0)
	{
		if (buffer is null)
			throw new ArgumentNullException(nameof(buffer));
		if (offset > buffer.Length)
			throw new ArgumentOutOfRangeException(nameof(offset));
		if (buffer.Length - offset < count)
			throw new ArgumentOutOfRangeException(nameof(count));

		var read = _stream.Read(buffer, offset, count);
		if (read < count)
			throw new IOException("Reading past end of stream");
	}

	public byte[] ReadBytes(int count)
	{
		var result = new byte[count];
		var read = _stream.Read(result, 0, count);
		if (read < count)
			throw new IOException("Reading past end of stream");
		return result;
	}

	public void SkipBytes(ulong count)
	{
		if (count > long.MaxValue)
		{
			_stream.Seek(long.MaxValue, SeekOrigin.Current);
			count -= long.MaxValue;
		}

		_stream.Seek((long) count, SeekOrigin.Current);
	}

	public short ReadInt16()
	{
		return _reader.ReadInt16();
	}

	public int ReadInt32()
	{
		return _reader.ReadInt32();
	}

	public long ReadInt64()
	{
		return _reader.ReadInt64();
	}

	public uint ReadUInt32()
	{
		return _reader.ReadUInt32();
	}

	public double ReadFloat64()
	{
		return _reader.ReadDouble();
	}

	public string ReadUtf16(int numChars)
	{
		if (numChars < 0)
			throw new ArgumentOutOfRangeException(nameof(numChars));

		var chars = new char[numChars];

		for (int i = 0; i < numChars; i++)
		{
			var c = (char) _reader.ReadUInt16();
			chars[i] = c;
		}

		return new string(chars);
	}

	public long ReadUInt40()
	{
		long lo = _reader.ReadByte();
		long hi = _reader.ReadUInt32();
		return (hi << 8) | (lo & 255);
	}

	public long ReadUInt48()
	{
		long lo = _reader.ReadUInt16();
		long hi = _reader.ReadUInt32();
		return (hi << 32) | (lo & 65535);

		// Even Rouault:
		// lo = read_uint8(f)
		// hi = read_uint40(f)
		// return lo | (hi << 8)
	}

	public ulong ReadVarUInt()
	{
		ulong result = 0;
		int shift = 0;

		// TODO check for overflow

		while (true)
		{
			byte b = _reader.ReadByte();
			result |= (ulong)(b & 127) << shift;
			if ((b & 128) == 0) break;
			shift += 7;
		}

		return result;
	}

	public long ReadVarInt()
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
		_stream.Dispose();
	}
}
