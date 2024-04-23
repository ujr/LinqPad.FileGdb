using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

namespace FileGDB.Core;

// Flat list of tables (.gdbtable files)
// First such table contains catalog (list of tables)

public sealed class FileGDB : IDisposable
{
	private readonly object _syncLock = new();
	private readonly IList<Table> _openTables;
	private IList<CatalogEntry>? _catalog;

	private FileGDB(string gdbFolderPath)
	{
		FolderPath = gdbFolderPath ?? throw new ArgumentNullException(nameof(gdbFolderPath));

		_openTables = new List<Table>();
	}

	public string FolderPath { get; }

	public static FileGDB Open(string gdbFolderPath)
	{
		if (gdbFolderPath is null)
			throw new ArgumentNullException(nameof(gdbFolderPath));

		var gdb = new FileGDB(gdbFolderPath);
		gdb.LoadCatalog();
		return gdb;
	}

	public void Dispose()
	{
		Table[] copy;

		lock (_syncLock)
		{
			copy = _openTables.ToArray();
			_openTables.Clear();
		}

		foreach (var table in copy)
		{
			table.Dispose();
		}
	}

	public IEnumerable<string> TableNames
	{
		get
		{
			IList<CatalogEntry>? catalog;

			lock (_syncLock)
			{
				catalog = _catalog;
			}

			return catalog?.Select(e => e.Name) ?? Enumerable.Empty<string>();
		}
	}

	public Table OpenTable(int tableID)
	{
		var baseName = GetTableBaseName(tableID);
		var table = Table.Open(baseName, FolderPath);

		lock (_syncLock)
		{
			_openTables.Add(table);
		}

		return table;
	}

	public Table OpenTable(string tableName)
	{
		var entry = GetCatalogEntry(tableName);
		if (entry.Missing)
			throw Error($"No such table: {tableName}");
		return OpenTable(entry.ID);
	}

	private void LoadCatalog()
	{
		var list = new List<CatalogEntry>();

		var baseName = GetTableBaseName(1); // "a00000001"

		using (var table = Table.Open(baseName, FolderPath))
		{
			for (int oid = 1; oid <= table.MaxObjectID; oid++)
			{
				var row = table.ReadRow(oid);
				if (row is null) continue;
				var name = Convert.ToString(row[1]);
				if (name is null)
					throw Error("Catalog contains NULL name");
				var format = Convert.ToInt32(row[2] ?? 0);
				list.Add(new CatalogEntry(oid, name, format));
			}
		}

		SetCatalog(list);
	}

	private CatalogEntry GetCatalogEntry(string tableName)
	{
		var catalog = GetCatalog();

		var entry = catalog.FirstOrDefault(entry => entry.Name == tableName);

		if (entry.Missing)
		{
			const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;
			entry = catalog.FirstOrDefault(e => string.Equals(e.Name, tableName, ignoreCase));
		}

		return entry;
	}

	private IList<CatalogEntry> GetCatalog()
	{
		lock (_syncLock)
		{
			return _catalog ??= new List<CatalogEntry>();
		}
	}

	private void SetCatalog(IList<CatalogEntry> list)
	{
		lock (_syncLock)
		{
			_catalog?.Clear();
			_catalog = list;
		}
	}

	private static string GetTableBaseName(int tableID)
	{
		return string.Format("a{0:x8}", tableID);
	}

	private static Exception Error(string message)
	{
		return new IOException(message ?? "File GDB error"); // TODO Custom exception
	}

	private readonly struct CatalogEntry
	{
		public int ID { get; }
		public string Name { get; }
		public int Format { get; }

		public CatalogEntry(int id, string name, int format = 0)
		{
			ID = id;
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Format = format;
		}

		public bool Missing => ID <= 0 || Name == null;

		public override string ToString()
		{
			return $"ID={ID} Name={Name}";
		}
	}
}

/// <summary>
/// Represents a single FGDB table. Each FGDB table has at least two files:
/// - aXXXXXXXX.gdbtable - the data file (field descriptions and row data)
/// - aXXXXXXXX.gdbtable - the index file (row offsets)
/// The .gdbtable file structure is:
/// - Header (40 bytes)
/// - Field descriptions (fixed part, per-field part)
/// - Row data
/// The .gdbtablx file structure is:
/// - Header (16 bytes)
/// - Offset section
/// - Trailing section
/// </summary>
public sealed class Table : IDisposable
{
	private DataReader? _dataReader;
	private DataReader? _indexReader;
	private int _offsetSize;
	private BitArray? _blockMap; // pabyTablXBlockMap chez Even Rouault

	public string BaseName { get; } // "aXXXXXXXX"
	public string FolderPath { get; } // "Path\To\MyFile.gdb"

	public int RowCount { get; private set; }
	public int FieldCount { get; private set; }
	public int Version { get; private set; }
	public GeometryType GeometryType { get; private set; }
	public bool HasZ { get; private set; }
	public bool HasM { get; private set; }
	public bool UseUtf8 { get; private set; }
	public IReadOnlyList<Field> Fields { get; private set; }
	public int MaxEntrySize { get; private set; }
	public int MaxObjectID { get; private set; } // TODO unsure (experiment with deleting rows)
	public IReadOnlyList<object> Indexes => throw new NotImplementedException();

	private Table(string baseName, string folderPath)
	{
		if (baseName is null)
			throw new ArgumentNullException(nameof(baseName));
		if (baseName.Length != 9 || (baseName[0] != 'a' && baseName[0] != 'A'))
			throw new ArgumentException("Malformed table file name", nameof(baseName));

		FolderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
		BaseName = baseName;

		Fields = new ReadOnlyCollection<Field>(Array.Empty<Field>());
	}

	public static Table Open(string baseName, string folderPath)
	{
		var table = new Table(baseName, folderPath);

		var dataFileName = Path.ChangeExtension(baseName, ".gdbtable");
		var dataFilePath = Path.Combine(folderPath, dataFileName);
		var dataStream = new FileStream(dataFilePath, FileMode.Open, FileAccess.Read);

		var indexFileName = Path.ChangeExtension(baseName, ".gdbtablx");
		var indexFilePath = Path.Combine(folderPath, indexFileName);
		var indexStream = new FileStream(indexFilePath, FileMode.Open, FileAccess.Read);

		table.Open(dataStream, indexStream);

		return table;
	}

	private void Open(Stream dataStream, Stream indexStream)
	{
		if (dataStream is null)
			throw new ArgumentNullException(nameof(dataStream));
		if (!dataStream.CanRead)
			throw new ArgumentException("Stream is not readable", nameof(dataStream));
		if (!dataStream.CanSeek)
			throw new ArgumentException("Stream is not seekable", nameof(dataStream));

		if (indexStream is null)
			throw new ArgumentNullException(nameof(indexStream));
		if (!indexStream.CanRead)
			throw new ArgumentException("Stream is not readable", nameof(indexStream));
		if (!indexStream.CanSeek)
			throw new ArgumentException("Stream is not seekable", nameof(indexStream));

		var dataReader = new DataReader(dataStream);
		var indexReader = new DataReader(indexStream);

		Dispose();

		_dataReader = dataReader;
		_indexReader = indexReader;

		ReadIndexHeader(_indexReader);
		ReadDataHeader(_dataReader);
	}

	public void Dispose()
	{
		if (_dataReader is not null)
		{
			_dataReader.Dispose();
			_dataReader = null;
		}

		if (_indexReader is not null)
		{
			_indexReader.Dispose();
			_indexReader = null;
		}
	}

	/// <returns>Size in bytes, or -1 if no such row</returns>
	public long GetRowSize(int fid)
	{
		if (_dataReader is null)
			throw new ObjectDisposedException(GetType().Name);

		var offset = GetRowOffset(fid);
		if (offset <= 0) return -1; // no such fid (or deleted)

		_dataReader.Seek(offset);

		long rowDataSize = _dataReader.ReadUInt32();

		return rowDataSize;
	}

	/// <returns>Row data bytes, or null if no such row</returns>
	public byte[]? ReadRowBytes(int fid)
	{
		if (_dataReader is null)
			throw new ObjectDisposedException(GetType().Name);

		var offset = GetRowOffset(fid);
		if (offset <= 0) return null;

		_dataReader.Seek(offset);

		byte[]? buffer = null;
		uint size = ReadRowBlob(_dataReader, offset, ref buffer);

		return buffer;
	}

	public object?[]? ReadRow(int fid)
	{
		if (_dataReader is null)
			throw new ObjectDisposedException(GetType().Name);

		var offset = GetRowOffset(fid);
		if (offset <= 0) return null;

		_dataReader.Seek(offset);

		var startPosition = _dataReader.Position;
		var rowBlobSize = _dataReader.ReadUInt32();
		var nullFlags = ReadNullFlags(_dataReader, Fields);

		var fieldCount = Fields.Count;
		var values = new object?[fieldCount];

		for (int i = 0, j = 0; i < fieldCount; i++)
		{
			var field = Fields[i];

			if (field.Nullable && nullFlags[j++])
			{
				values[i] = null;
				continue;
			}

			switch (field.Type)
			{
				case FieldType.Int16:
					values[i] = _dataReader.ReadInt16();
					break;
				case FieldType.Int32:
					values[i] = _dataReader.ReadInt32();
					break;
				case FieldType.Single:
					values[i] = _dataReader.ReadSingle();
					break;
				case FieldType.Double:
					values[i] = _dataReader.ReadDouble();
					break;
				case FieldType.String:
				case FieldType.XML:
					values[i] = ReadTextField(_dataReader, UseUtf8);
					break;
				case FieldType.DateTime:
					values[i] = ReadDateTimeField(_dataReader);
					break;
				case FieldType.ObjectID:
					values[i] = fid;
					break;
				case FieldType.Geometry:
					values[i] = ReadGeometryBlob(_dataReader);
					break;
				case FieldType.Blob:
					values[i] = ReadBlobField(_dataReader);
					break;
				case FieldType.Raster:
					// depends on RasterType in field definition
					throw new NotImplementedException();
					break;
				case FieldType.GUID:
				case FieldType.GlobalID:
					values[i] = ReadGuidField(_dataReader);
					break;
				case FieldType.Int64:
					values[i] = _dataReader.ReadInt64();
					break;
				case FieldType.DateOnly:
				case FieldType.TimeOnly:
				case FieldType.DateTimeOffset:
					throw new NotImplementedException($"Field of type {field.Type} not yet implemented");
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		var currentPosition = _dataReader.Position;
		Debug.Assert(currentPosition == startPosition + 4 + rowBlobSize);

		return values;
	}

	private long GetRowOffset(int fid)
	{
		if (_indexReader is null)
			throw new ObjectDisposedException(GetType().Name);

		fid -= 1; // from external 1-based to internal 0-based
		if (fid < 0) return -1;

		/*
		   if pabyTablXBlockMap:
		       iBlock = fid // 1024
		       # Check if the block is not empty
		       if TEST_BIT(pabyTablXBlockMap, iBlock) == 0:
		           continue

		       # As we do sequential reading, optimization to avoid recomputing
		       # the number of blocks since the beginning of the map
		       assert iBlock >= nCountBlocksBeforeIBlockIdx
		       nCountBlocksBefore = nCountBlocksBeforeIBlockValue
		       for i in range(nCountBlocksBeforeIBlockIdx, iBlock):
		           nCountBlocksBefore += TEST_BIT(pabyTablXBlockMap, i)

		       nCountBlocksBeforeIBlockIdx = iBlock
		       nCountBlocksBeforeIBlockValue = nCountBlocksBefore
		       iCorrectedRow = nCountBlocksBefore * 1024 + (fid % 1024)
		       fx.seek(16 + size_tablx_offsets * iCorrectedRow)
		   else:
		       fx.seek(16 + fid * size_tablx_offsets, 0)

		   if size_tablx_offsets == 4:
		       feature_offset = read_uint32(fx)
		   elif size_tablx_offsets == 5:
		       feature_offset = read_uint40(fx)
		   elif size_tablx_offsets == 6:
		       feature_offset = read_uint48(fx)
		   else:
		       assert False
		 */

		if (_blockMap != null)
		{
			int iBlock = fid / 1024;
			if (!_blockMap[iBlock])
				return -1; // no such fid

			int nCountBlocksBefore = 0; // TODO optimize for sequential reading
			for (int i = 0; i < iBlock; i++)
				nCountBlocksBefore += _blockMap[i] ? 1 : 0;

			var iCorrectedRow = nCountBlocksBefore * 1024 + fid % 1024;

			_indexReader.Seek(16 + _offsetSize * iCorrectedRow);
		}
		else
		{
			_indexReader.Seek(16 + fid * _offsetSize);
		}

		long result = _offsetSize switch
		{
			4 => _indexReader.ReadUInt32(),
			5 => _indexReader.ReadUInt40(),
			6 => _indexReader.ReadUInt48(),
			_ => throw new InvalidOperationException($"Offset size {_offsetSize} is out of range")
		};

		return result;
	}

	private static uint ReadRowBlob(DataReader dataReader, long rowDataOffset, ref byte[]? buffer)
	{
		dataReader.Seek(rowDataOffset);

		var rowBlobSize = dataReader.ReadUInt32();

		if (buffer is null || buffer.LongLength < rowBlobSize)
		{
			buffer = new byte[rowBlobSize];
		}

		dataReader.ReadBytes((int) rowBlobSize, buffer);

		return rowBlobSize;
	}

	private static BitArray ReadNullFlags(DataReader dataReader, IReadOnlyList<Field> fields)
	{
		// assume reader positioned at row's null flags
		// if all fields are non-nullable, there are no null flags stored

		int count = fields.Count;

		var bytes = new List<byte>((count + 7) / 8);
		int nullable = fields.Count(f => f.Nullable);
		while (nullable > 0)
		{
			bytes.Add(dataReader.ReadByte());
			nullable -= 8;
		}

		return new BitArray(bytes.ToArray());
	}

	private static byte[] ReadGeometryBlob(DataReader reader)
	{
		// assume reader positioned at start of field data
		var size = reader.ReadVarUInt();
		if (size > int.MaxValue)
			throw Error("Geometry field too large for this API");
		var bytes = reader.ReadBytes((int)size);
		return bytes;
	}

	private static byte[] ReadBlobField(DataReader reader)
	{
		// assume reader positioned at start of field data
		var size = reader.ReadVarUInt();
		if (size > int.MaxValue)
			throw Error("Blob field too large for this API");
		var bytes = reader.ReadBytes((int)size);
		return bytes;
	}

	private static string ReadTextField(DataReader reader, bool isUtf8)
	{
		// assume reader positioned at start of field data
		var size = reader.ReadVarUInt();
		if (size > int.MaxValue)
			throw Error("String field too large for this API");
		var bytes = reader.ReadBytes((int)size);
		var text = isUtf8
			? Encoding.UTF8.GetString(bytes)
			: throw new NotImplementedException(); // what's the encoding?
		return text;
	}

	private static Guid ReadGuidField(DataReader reader)
	{
		var bytes = reader.ReadBytes(16);
		// TODO Test if bytes from FGDB are in the order expected by Guid()
		// FGDB: b3 b2 b1 b0   b5 b4   b7 b6   b8 b9 b10 b11 b12 b13 b14 b15 b16
		return new Guid(bytes);
	}

	private static DateTime ReadDateTimeField(DataReader reader)
	{
		double days = reader.ReadDouble();
		var epoch = new DateTime(1899, 12, 30, 0, 0, 0); // 1899-12-30 00:00:00
		return epoch.AddDays(days);
	}

	private void ReadIndexHeader(DataReader indexReader)
	{
		// - Header (16 bytes)
		// - Offsets (one entry per record)
		// - Trailer (16 bytes + bitmap)

		/*
		   magic1 = read_uint32(fx)
		   n1024Blocks = read_uint32(fx)
		   nfeaturesx = read_uint32(fx)
		   size_tablx_offsets = read_uint32(fx)
		 */
		// assume reader is positioned at start of header
		_ = indexReader.ReadBytes(4); // unknown magic
		var n1024Blocks = indexReader.ReadInt32(); // TODO rename num1KBlocks
		var nfeaturesx = indexReader.ReadInt32(); // including deleted rows! TODO rename numRows
		_offsetSize = indexReader.ReadInt32(); // 4, 5, or 6 (bytes per offset) TODO rename offsetSize

		MaxObjectID = nfeaturesx;

		if (n1024Blocks == 0)
			Debug.Assert(nfeaturesx == 0);
		else
			Debug.Assert(nfeaturesx > 0);

		Debug.Assert(_offsetSize is 4 or 5 or 6);

		if (n1024Blocks > 0)
		{
			// Seek to trailer section:
			indexReader.Seek(16 + 1024 * n1024Blocks * _offsetSize);

			var nBitmapInt32Words = indexReader.ReadUInt32();
			var nBitsForBlockMap = indexReader.ReadUInt32();
			var n1024BlocksBis = indexReader.ReadUInt32();
			var nLeadingNonZero32BitWords = indexReader.ReadUInt32();

			Debug.Assert(n1024Blocks == n1024BlocksBis);

			if (nBitmapInt32Words == 0)
			{
				Debug.Assert(nBitsForBlockMap == n1024Blocks);
				_blockMap = null;
			}
			else
			{
				Debug.Assert(nfeaturesx <= nBitsForBlockMap * 1024);
				var nSizeInBytes = (nBitsForBlockMap + 7) / 8; // bits to bytes (rounding up)
				var bytes = indexReader.ReadBytes((int)nSizeInBytes);
				_blockMap = new BitArray(bytes);
				int nCountBlocks = 0;
				for (int i = 0; i < nBitsForBlockMap; i++)
					nCountBlocks += _blockMap[i] ? 1 : 0;
				Debug.Assert(nCountBlocks == n1024Blocks);
			}
		}
		else
		{
			_blockMap = null;
		}

		/*
		   def TEST_BIT(ar, bit):
		       return 1 if (ar[(bit) // 8] & (1 << ((bit) % 8))) else 0
		   
		   def BIT_ARRAY_SIZE_IN_BYTES(bitsize):
		       return (((bitsize)+7)//8)
		   
		   pabyTablXBlockMap = None
		   if n1024Blocks != 0:
		       fx.seek(size_tablx_offsets * 1024 * n1024Blocks + 16, 0)
		       nBitmapInt32Words = read_uint32(fx)
		       nBitsForBlockMap = read_uint32(fx)
		       n1024BlocksBis = read_uint32(fx)
		       assert n1024Blocks == n1024BlocksBis
		       nLeadingNonZero32BitWords = read_uint32(fx)

		       if nBitmapInt32Words == 0:
		           assert nBitsForBlockMap == n1024Blocks
		       else:
		           assert nfeaturesx <= nBitsForBlockMap * 1024
		           #Allocate a bit mask array for blocks of 1024 features.
		           nSizeInBytes = BIT_ARRAY_SIZE_IN_BYTES(nBitsForBlockMap)
		           pabyTablXBlockMap = fx.read(nSizeInBytes)
		           pabyTablXBlockMap = struct.unpack('B' * nSizeInBytes, pabyTablXBlockMap)
		           nCountBlocks = 0
		           for i in range(nBitsForBlockMap):
		               nCountBlocks += TEST_BIT(pabyTablXBlockMap, i)
		           assert nCountBlocks == n1024Blocks, (nCountBlocks, n1024Blocks)
		 */
	}

	private void ReadDataHeader(DataReader reader)
	{
		// assume reader is positioned at start of header
		var magic1 = reader.ReadInt32();
		RowCount = reader.ReadInt32(); // actual (non-deleted) rows
		var maxEntrySize = reader.ReadInt32();
		var magic2 = reader.ReadInt32();
		var magic3 = reader.ReadBytes(4);
		var magic4 = reader.ReadBytes(4);
		var fileBytes = reader.ReadInt64();
		var fieldsOffset = reader.ReadInt64();

		// Fixed part of fields section:
		var headerBytes = reader.ReadInt32(); // excluding this field
		var version = reader.ReadInt32(); // 3 for FGDB at 9.x, 4 for FGDB at 10.x
		var flags = reader.ReadUInt32(); // see decoding below
		FieldCount = reader.ReadInt16(); // including the implicit OBJECTID field!

		// Decode known flag bits:
		UseUtf8 = (flags & (1 << 8)) != 0;
		GeometryType = (GeometryType)(flags & 255);
		HasZ = (flags & (1 << 31)) != 0;
		HasM = (flags & (1 << 30)) != 0;

		// Field descriptions:
		var fields = new List<Field>();
		for (int i = 0; i < FieldCount; i++)
		{
			var nameChars = reader.ReadByte();
			var name = reader.ReadUtf16(nameChars);

			var aliasChars = reader.ReadByte();
			var alias = reader.ReadUtf16(aliasChars);

			var type = (FieldType)reader.ReadByte();

			var field = ReadField(name, alias, type, reader, GeometryType, HasZ, HasM);

			fields.Add(field); // here we also add the OID field
		}

		Debug.Assert(FieldCount == fields.Count);

		Fields = new ReadOnlyCollection<Field>(fields);
	}

	private static Field ReadField(
		string name, string alias, FieldType type, DataReader reader,
		GeometryType geometryType, bool tableHasZ, bool tableHasM)
	{
		byte flag, size;
		var field = new Field(name, alias, type);

		switch (type)
		{
			case FieldType.ObjectID:
				_ = reader.ReadByte(); // always 2 (4)
				_ = reader.ReadByte(); // always 4 (2)
				field.Nullable = false;
				break;

			case FieldType.Geometry:
				var geomDef = new GeometryDef(geometryType);
				_ = reader.ReadByte(); // unknown (always 0?)
				flag = reader.ReadByte();
				field.Nullable = (flag & 1) != 0;
				var wktLen = reader.ReadInt16(); // in bytes
				var wkt = reader.ReadUtf16(wktLen / 2); // in chars
				geomDef.SpatialReference = wkt;
				var geomFlags = reader.ReadByte();
				// weird, but these are different from the table-level HasZ/M flags
				bool hasM = (geomFlags & 2) != 0; // and therefore also M scale, domain, tolerance
				bool hasZ = (geomFlags & 4) != 0; // and therefore also Z scale, domain, tolerance
				geomDef.XOrigin = reader.ReadDouble();
				geomDef.YOrigin = reader.ReadDouble();
				geomDef.XYScale = reader.ReadDouble();
				if (hasM)
				{
					geomDef.HasM = true; // TODO unsure, better use tableHasM?
					geomDef.MOrigin = reader.ReadDouble();
					geomDef.MScale = reader.ReadDouble();
				}
				if (hasZ)
				{
					geomDef.HasZ = true; // TODO unsure, ditto
					geomDef.ZOrigin = reader.ReadDouble();
					geomDef.ZScale = reader.ReadDouble();
				}
				geomDef.XYTolerance = reader.ReadDouble();
				if (hasM)
				{
					geomDef.MTolerance = reader.ReadDouble();
				}
				if (hasZ)
				{
					geomDef.ZTolerance = reader.ReadDouble();
				}

				geomDef.Extent.XMin = reader.ReadDouble();
				geomDef.Extent.YMin = reader.ReadDouble();
				geomDef.Extent.XMax = reader.ReadDouble();
				geomDef.Extent.YMax = reader.ReadDouble();
				if (tableHasZ)
				{
					geomDef.Extent.HasZ = true;
					geomDef.Extent.ZMin = reader.ReadDouble();
					geomDef.Extent.ZMax = reader.ReadDouble();
				}
				if (tableHasM)
				{
					geomDef.Extent.HasM = true;
					geomDef.Extent.MMin = reader.ReadDouble();
					geomDef.Extent.MMax = reader.ReadDouble();
				}

				_ = reader.ReadByte(); // always zero?

				geomDef.Grid.Count = reader.ReadInt32(); // 1 or 2 or 3
				for (int i = 0; i < geomDef.Grid.Count; i++)
				{
					geomDef.Grid[i] = reader.ReadDouble();
				}

				field.GeometryDef = geomDef;
				break;

			case FieldType.String:
				field.Length = reader.ReadInt32();
				flag = reader.ReadByte();
				field.Nullable = (flag & 1) != 0;
				var len = reader.ReadVarUInt();
				if (len > 0 && (flag & 4) != 0)
					reader.SkipBytes(len); // default value
				/* TODO
				   default_value_length = read_varuint(f)
				   print('default_value_length = %d' % default_value_length)
				   if (flag & 4) != 0 and default_value_length > 0:
				       print('default value: %s' % f.read(default_value_length))
				 */
				break;

			case FieldType.Blob:
				_ = reader.ReadByte(); // unknown
				flag = reader.ReadByte();
				field.Nullable = (flag & 1) != 0;
				break;

			case FieldType.GUID:
			case FieldType.GlobalID:
				// The size is 38, suggesting that the GUID is stored like
				// "{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}" (aka registry format)
				// but indeed it's stored as 16 bytes
				size = reader.ReadByte();
				flag = reader.ReadByte();
				field.Nullable = (flag & 1) != 0;
				break;

			case FieldType.XML:
				size = reader.ReadByte(); // 0?
				flag = reader.ReadByte();
				field.Nullable = (flag & 1) != 0;
				break;

			case FieldType.Int16:
			case FieldType.Int32:
			case FieldType.Int64:
			case FieldType.Single:
			case FieldType.Double:
			case FieldType.DateTime:
			case FieldType.DateOnly:
			case FieldType.TimeOnly:
			case FieldType.DateTimeOffset:
				size = reader.ReadByte(); // e.g. 2 for Int16
				flag = reader.ReadByte();
				field.Nullable = (flag & 1) != 0;
				var dvl = reader.ReadByte();
				if ((flag & 4) != 0)
				{
					reader.SkipBytes(dvl); // default value (skip for now)
				}
				break;

			case FieldType.Raster:
				throw new NotImplementedException();
			/*
		       elif type == TYPE_RASTER:
			       print('unknown_role = %d' % read_uint8(f))
			       flag = read_uint8(f)
			       if (flag & 1) == 0:
			           fd.nullable = False
			       
			       nbcar = read_uint8(f)
			       raster_column = read_utf16(f, nbcar)

			       wkt_len = read_uint8(f)
			       wkt_len += read_uint8(f) * 256
			       wkt = read_utf16(f, wkt_len // 2)
			       
			       #f.read(82)
			       
			       magic3 = read_uint8(f)
			       
			       if magic3 > 0:
			           raster_has_m = False
			           raster_has_z = False
			           if magic3 == 5:
			               raster_has_z = True
			           if magic3 == 7:
			               raster_has_m = True
			               raster_has_z = True

			           raster_xorig = read_float64(f)
			           raster_yorig = read_float64(f)
			           raster_xyscale = read_float64(f)
			           if raster_has_m:
			               raster_morig = read_float64(f)
			               raster_mscale = read_float64(f)
			           if raster_has_z:
			               raster_zorig = read_float64(f)
			               raster_zscale = read_float64(f)
			           raster_xytolerance = read_float64(f)
			           if raster_has_m:
			               raster_mtolerance = read_float64(f)
			           if raster_has_z:
			               raster_ztolerance = read_float64(f)

			       fd.raster_type = read_uint8(f)
			       if fd.raster_type == 0:
			           print('External raster')
			       elif fd.raster_type == 1:
			           print('Managed raster')
			       elif fd.raster_type == 2:
			           print('Inline binary content')
			       else:
			           print('Unknown raster_type: %d' % fd.raster_type)
			 */

			default:
				throw new NotSupportedException($"Unknown field type: {type}");
		}

		return field;
	}

	private static Exception Error(string message)
	{
		return new IOException(message);
	}
}

public class Field
{
	public string Name { get; }
	public string Alias { get; }
	public FieldType Type { get; }
	public bool Nullable { get; set; }
	public int Length { get; set; }
	// Precision and Scale are always zero for File GDBs
	// Required? Editable? Domain? DomainFixed? DefaultValue?
	public GeometryDef? GeometryDef { get; set; }

	public Field(string name, string? alias, FieldType type)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		Alias = alias ?? string.Empty;
		Type = type;
	}

	public override string ToString()
	{
		return $"{Name} Type={Type} Alias={Alias}";
	}
}

public class GeometryDef
{
	public GeometryDef(GeometryType type)
	{
		GeometryType = type;
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

public class Envelope
{
	public double XMin { get; set; }
	public double YMin { get; set; }
	public double XMax { get; set; }
	public double YMax { get; set; }

	public bool HasM { get; set; }
	public double MMin { get; set; }
	public double MMax { get; set; }

	public bool HasZ { get; set; }
	public double ZMin { get; set; }
	public double ZMax { get; set; }

	public Envelope()
	{
		XMin = XMax = double.NaN;
		YMin = YMax = double.NaN;

		MMin = MMax = double.NaN;
		ZMin = ZMax = double.NaN;
	}
}
