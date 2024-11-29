using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace FileGDB.Core;

// Flat list of tables (.gdbtable files)
// First such table contains catalog (list of tables)
// 3 known versions: 9.x (very old), 10.x (current), 10.x with 64bit OIDs (since Pro 3.2)

public sealed class FileGDB : IDisposable
{
	private readonly object _syncLock = new();
	private readonly IList<Table> _openTables;
	private IReadOnlyList<CatalogEntry>? _catalog;

	static FileGDB()
	{
		SystemTableDescriptions = GetSystemTableDescriptions();
	}

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

	public static IReadOnlyDictionary<string, string> SystemTableDescriptions { get; }

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

	public IReadOnlyList<CatalogEntry> Catalog => GetCatalog();

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
		if (entry.ID <= 0)
			throw Error($"No such table: {tableName}");
		return OpenTable(entry.ID);
	}

	#region Private methods

	private void LoadCatalog()
	{
		var list = new List<CatalogEntry>();

		var baseName = GetTableBaseName(1); // "a00000001"

		using (var table = Table.Open(baseName, FolderPath))
		{
			var limit = (int) Math.Min(int.MaxValue, table.MaxObjectID);
			for (int oid = 1; oid <= limit; oid++)
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

		if (entry.ID <= 0)
		{
			const StringComparison ignoreCase = StringComparison.OrdinalIgnoreCase;
			entry = catalog.FirstOrDefault(e => string.Equals(e.Name, tableName, ignoreCase));
		}

		return entry;
	}

	private IReadOnlyList<CatalogEntry> GetCatalog()
	{
		lock (_syncLock)
		{
			return _catalog ?? Array.Empty<CatalogEntry>();
		}
	}

	private void SetCatalog(IList<CatalogEntry> list)
	{
		if (list is null)
			throw new ArgumentNullException(nameof(list));

		lock (_syncLock)
		{
			_catalog = new ReadOnlyCollection<CatalogEntry>(list);
		}
	}

	private static string GetTableBaseName(int tableID)
	{
		return string.Format("a{0:x8}", tableID);
	}

	private static Exception Error(string? message)
	{
		return new FileGDBException(message ?? "File GDB error");
	}

	private static IReadOnlyDictionary<string, string> GetSystemTableDescriptions()
	{
		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			{ "GDB_SystemCatalog", "Catalog system table (list of all tables)" },
			{ "GDB_DBTune", "DBTune system table (config keyword parameters)" },
			{ "GDB_SpatialRefs", "Spatial references used by tables in this File GDB" },
			{ "GDB_Items", "The GDB_Items system table" },
			{ "GDB_ItemTypes", "The GDB_ItemTypes system table" },
			{ "GDB_ItemRelationships", "The GDB_ItemRelationships system table" },
			{ "GDB_ItemRelationshipTypes", "The GDB_ItemRelationshipTypes system table" },
			{ "GDB_ReplicaLog", "The ReplicaLog system table (may not exist)" },
			{ "GDB_EditingTemplates", "new with Pro 3.2" },
			{ "GDB_EditingTemplateRelationships", "new with Pro 3.2" }
		};

		// TODO Version 9.2 File GDBs had many more system tables

		return new ReadOnlyDictionary<string, string>(result);
	}

	#endregion

	public readonly struct CatalogEntry
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

		//public bool Missing => ID <= 0 || Name == null;

		public override string ToString()
		{
			return $"ID={ID} Name={Name} Format={Format}";
		}
	}
}

/// <summary>
/// Represents a single FGDB table. Each FGDB table has at least two files:
/// - aXXXXXXXX.gdbtable - the data file (field descriptions and row data)
/// - aXXXXXXXX.gdbtablx - the index file (row offsets)
/// and may have additional files for spatial and/or attribute indexes:
/// - aXXXXXXXX.NAME.atx - attribute index
/// - aXXXXXXXX.NAME.spx - spatial index
/// </summary>
public sealed class Table : IDisposable
{
	private DataReader? _dataReader;
	private DataReader? _indexReader;
	private BitArray? _blockMap; // pabyTablXBlockMap chez Even Rouault
	private IReadOnlyList<IndexInfo>? _indexes;

	public string BaseName { get; } // "aXXXXXXXX"
	public string FolderPath { get; } // "Path\To\MyFile.gdb"

	public long RowCount { get; private set; }
	public int FieldCount { get; private set; }
	public int Version { get; private set; }
	public int OffsetSize { get; private set; } // bytes per offset in .gdbtablx file, 4 or 5 or 6
	public GeometryType GeometryType { get; private set; }
	public bool HasZ { get; private set; }
	public bool HasM { get; private set; }
	public bool HasID { get; private set; }
	public bool UseUtf8 { get; private set; }
	public IReadOnlyList<FieldInfo> Fields { get; private set; }
	public int MaxEntrySize { get; private set; } // max(row sizes and field description section)
	public long MaxObjectID { get; private set; } // TODO unsure (experiment with deleting rows)
	public long FileSizeBytes { get; private set; }
	public IReadOnlyList<IndexInfo> Indexes => _indexes ??= LoadIndexes();

	private Table(string baseName, string folderPath)
	{
		if (baseName is null)
			throw new ArgumentNullException(nameof(baseName));
		if (baseName.Length != 9 || (baseName[0] != 'a' && baseName[0] != 'A'))
			throw new ArgumentException("Malformed table file name", nameof(baseName));

		FolderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
		BaseName = baseName;

		Fields = new ReadOnlyCollection<FieldInfo>(Array.Empty<FieldInfo>());
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

	public RowsResult Search(string? fields, string? whereClause, Envelope? extent)
	{
		fields = fields is null ? "*" : fields.Trim();
		whereClause = Canonical(whereClause);

		if (fields != "*")
			throw new NotImplementedException("Sub fields not yet implemented");
		if (whereClause != null)
			throw new NotImplementedException("Where Clause filtering not yet implemented");
		if (extent != null)
			throw new NotImplementedException("Spatial extent restriction not yet implemented");

		return new TableScanResult(this);
	}

	/// <returns>Size in bytes, or -1 if no such row</returns>
	public long GetRowSize(int oid)
	{
		if (_dataReader is null)
			throw new ObjectDisposedException(GetType().Name);

		var offset = GetRowOffset(oid);
		if (offset <= 0) return -1; // no such oid (or deleted)

		_dataReader.Seek(offset);

		long rowDataSize = _dataReader.ReadUInt32();

		return rowDataSize;
	}

	///// <returns>Row data bytes, or null if no such row</returns>
	//public byte[]? ReadRowBytes(int oid)
	//{
	//	if (_dataReader is null)
	//		throw new ObjectDisposedException(GetType().Name);

	//	var offset = GetRowOffset(oid);
	//	if (offset <= 0) return null;

	//	_dataReader.Seek(offset);

	//	byte[]? buffer = null;
	//	uint size = ReadRowBlob(_dataReader, offset, ref buffer);

	//	return buffer;
	//}

	public object?[]? ReadRow(long oid, object?[]? values = null)
	{
		if (_dataReader is null)
			throw new ObjectDisposedException(GetType().Name);

		var offset = GetRowOffset(oid);
		if (offset <= 0) return null;

		_dataReader.Seek(offset);

		var startPosition = _dataReader.Position;
		var rowBlobSize = _dataReader.ReadUInt32();
		var nullFlags = ReadNullFlags(_dataReader, Fields);

		var fieldCount = Fields.Count;

		values ??= new object?[fieldCount];
		int maxField = Math.Min(values.Length, fieldCount);

		for (int i = 0, j = 0; i < maxField; i++)
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
					values[i] = oid;
					break;
				case FieldType.Geometry:
					var geometryDef = field.GeometryDef ??
					                  throw Error($"Field \"{field.Name}\" is of type {FieldType.Geometry} but has no GeometryDef");
					values[i] = ReadGeometryBlob(_dataReader, geometryDef);
					break;
				case FieldType.Blob:
					values[i] = ReadBlobField(_dataReader);
					break;
				case FieldType.Raster:
					// depends on RasterType in field definition
					throw new NotImplementedException("Raster fields not yet implemented");
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
					throw new NotSupportedException($"Unknown field type: {field.Type}");
			}
		}

		var currentPosition = _dataReader.Position;
		var expectedPosition = startPosition + 4 + rowBlobSize;
		var delta = expectedPosition - currentPosition;
		Debug.Assert(delta >= 0, $"Oops, read {delta} byte(s) beyond end of row blob");
		if (delta > 0)
		{
			var excess = _dataReader.ReadBytes((int)delta);
			// Some test data gets me here with 4 bytes excess:
			// LINESZM and MULTIPOINTSZM in TestSLDLM.gdb
			//Debug.Assert(false, $"Unread data in row blob: {delta} byte(s)");
		}

		return values;
	}

	/// <summary>
	/// Get the CLR data type used for the given field type.
	/// </summary>
	// TODO shouldn't this be on FileGDB class? on the other hand, it corresponds with ReadRow above
	public static Type GetDataType(FieldType fieldType)
	{
		switch (fieldType)
		{
			case FieldType.Int16:
				return typeof(short?);
			case FieldType.Int32:
				return typeof(int?);
			case FieldType.Single:
				return typeof(float?);
			case FieldType.Double:
				return typeof(double?);
			case FieldType.String:
			case FieldType.XML:
				return typeof(string);
			case FieldType.DateTime:
				return typeof(DateTime?);
			case FieldType.ObjectID:
				// was 32bit until about year 2023; can be 64bit since ArcGIS Pro 3.2
				return typeof(long);
			case FieldType.Geometry:
				return typeof(GeometryBlob);
			case FieldType.Blob:
				return typeof(byte[]);
			case FieldType.Raster:
				return typeof(object); // TODO
			case FieldType.GUID:
			case FieldType.GlobalID:
				return typeof(Guid?);
			case FieldType.Int64:
				return typeof(long?);
			case FieldType.DateOnly:
			case FieldType.TimeOnly:
			case FieldType.DateTimeOffset:
				return typeof(object); // TODO
			default:
				return typeof(object);
		}
	}

	private long GetRowOffset(long oid)
	{
		if (_indexReader is null)
			throw new ObjectDisposedException(GetType().Name);

		oid -= 1; // from external 1-based to internal 0-based
		if (oid < 0) return -1;

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
		 */

		if (_blockMap != null)
		{
			long blockNum = oid / 1024;
			if (blockNum > int.MaxValue)
				throw Error($"OID {oid} is too big for this implementation");

			if (!_blockMap[(int) blockNum])
			{
				return -1; // no such oid
			}

			int blocksBefore = 0; // TODO optimize for sequential reading
			for (int i = 0; i < blockNum; i++)
			{
				blocksBefore += _blockMap[i] ? 1 : 0;
			}

			var correctedRow = blocksBefore * 1024 + oid % 1024;

			_indexReader.Seek(16 + OffsetSize * correctedRow);
		}
		else
		{
			long offset = 16 + oid * OffsetSize;
			if (offset >= _indexReader.Length)
			{
				return -1; // no such oid
			}
			_indexReader.Seek(offset);
		}

		long result = OffsetSize switch
		{
			4 => _indexReader.ReadUInt32(),
			5 => _indexReader.ReadUInt40(),
			6 => _indexReader.ReadUInt48(),
			_ => throw Error($"Offset size {OffsetSize} is out of range; expect 4 or 5 or 6")
		};

		return result;
	}

	//private static uint ReadRowBlob(DataReader dataReader, long rowDataOffset, ref byte[]? buffer)
	//{
	//	dataReader.Seek(rowDataOffset);

	//	var rowBlobSize = dataReader.ReadUInt32();

	//	if (buffer is null || buffer.LongLength < rowBlobSize)
	//	{
	//		buffer = new byte[rowBlobSize];
	//	}

	//	dataReader.ReadBytes((int) rowBlobSize, buffer);

	//	return rowBlobSize;
	//}

	private static BitArray ReadNullFlags(DataReader dataReader, IReadOnlyList<FieldInfo> fields)
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

	private static GeometryBlob ReadGeometryBlob(DataReader reader, GeometryDef geomDef)
	{
		// assume reader positioned at start of field data
		var size = reader.ReadVarUInt();
		if (size > int.MaxValue)
			throw Error("Geometry field too large for this API");
		var bytes = reader.ReadBytes((int)size);
		return new GeometryBlob(geomDef, bytes);
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

		// assume reader is positioned at start of header
		var formatVersion = indexReader.ReadInt32(); // 3 for 32-bit OIDs, 4 for 64-bit OIDs

		switch (formatVersion)
		{
			case 3:
				ReadIndexHeader3(indexReader);
				break;
			case 4:
				ReadIndexHeader4(indexReader);
				break;
			default:
				throw Error($"Version {formatVersion} of .gdbtablx is not supported; expect 3 or 4");
		}
	}

	private void ReadIndexHeader3(DataReader indexReader)
	{
		// assume reader positioned after version field

		var n1024Blocks = indexReader.ReadInt32(); // TODO rename num1KBlocks
		var numRows = indexReader.ReadInt32(); // including deleted rows!

		OffsetSize = indexReader.ReadInt32(); // 4, 5, or 6 (bytes per offset)
		if (OffsetSize is not 4 and not 5 and not 6)
			throw Error($"Offset size {OffsetSize} is out of range; expect 4 or 5 or 6 (bytes per offset)");

		MaxObjectID = numRows;

		if (n1024Blocks == 0)
			Debug.Assert(numRows == 0);
		else
			Debug.Assert(numRows > 0);

		if (n1024Blocks > 0)
		{
			// Seek to trailer section:
			indexReader.Seek(16 + 1024 * n1024Blocks * OffsetSize);

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
				Debug.Assert(numRows <= nBitsForBlockMap * 1024);
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

	private void ReadIndexHeader4(DataReader indexReader)
	{
		// assume reader positioned after version field

		var n1024Blocks = indexReader.ReadInt32(); // TODO rename num1KBlocks
		var unknown = indexReader.ReadInt32(); // always 0? MSB of previous field?

		OffsetSize = indexReader.ReadInt32(); // 4, 5, or 6 (bytes per offset)
		if (OffsetSize is not 4 and not 5 and not 6)
			throw Error($"Offset size {OffsetSize} is out of range; expect 4 or 5 or 6 (bytes per offset)");

		if (n1024Blocks > 0)
		{
			// Seek to trailer section:
			indexReader.Seek(16 + 1024 * n1024Blocks * OffsetSize);

			var numRows = indexReader.ReadInt64(); // including deleted rows!

			MaxObjectID = numRows;

			var sectionBytes = indexReader.ReadInt32(); // 0 for non-sparse files
			if (sectionBytes > 0)
				throw Error("Unsupported v4 (Pro 3.2) .gdbtablx file");
			// Cf with_holes files at https://github.com/qgis/QGIS/issues/57471
		}
		else
		{
			_blockMap = null;
		}
	}

	private void ReadDataHeader(DataReader reader)
	{
		// assume reader is positioned at start of header
		var formatVersion = reader.ReadInt32(); // 3 for 32-bit OID, 4 for 64-bit OID
		if (formatVersion == 3)
		{
			RowCount = reader.ReadInt32(); // actual (non-deleted) rows
			MaxEntrySize = reader.ReadInt32();
			var magic2 = reader.ReadInt32(); // always 5 (?)
			var magic3 = reader.ReadBytes(4);
			var magic4 = reader.ReadBytes(4);
		}
		else if (formatVersion == 4)
		{
			var magic1 = reader.ReadInt32(); // 1 if some features deleted, otherwise 0 (?)
			MaxEntrySize = reader.ReadInt32();
			var magic2 = reader.ReadInt32(); // always 5 (?)
			RowCount = reader.ReadInt64();
		}

		FileSizeBytes = reader.ReadInt64();

		var fieldsOffset = reader.ReadInt64();
		reader.Seek(fieldsOffset);

		ReadFieldDescriptions(reader);
	}

	private void ReadFieldDescriptions(DataReader reader)
	{
		// assume reader is positioned at start of field descriptions section
		// Fixed part of fields section:
		var headerBytes = reader.ReadInt32(); // excluding this field
		Version = reader.ReadInt32(); // 3 for FGDB at 9.x, 4 for FGDB at 10.x, 6 for FGDB using Pro 3.2 features (64bit OID, new field types)
		var flags = reader.ReadUInt32(); // see decoding below
		FieldCount = reader.ReadInt16(); // including the implicit OBJECTID field!

		// Decode known flag bits:
		UseUtf8 = (flags & (1 << 8)) != 0;
		GeometryType = (GeometryType)(flags & 255);
		HasZ = (flags & (1 << 31)) != 0;
		HasM = (flags & (1 << 30)) != 0;
		HasID = (flags & (1 << 28)) != 0; // TODO this is just a guess

		// Field descriptions:
		var fields = new List<FieldInfo>(Math.Max(0, FieldCount));
		for (int i = 0; i < FieldCount; i++)
		{
			var nameChars = reader.ReadByte();
			var name = reader.ReadUtf16(nameChars);

			var aliasChars = reader.ReadByte();
			var alias = reader.ReadUtf16(aliasChars);

			var type = (FieldType)reader.ReadByte();

			var field = ReadFieldInfo(name, alias, type, reader, GeometryType, HasZ, HasM, HasID);

			fields.Add(field); // here we also add the OID field
		}

		Debug.Assert(FieldCount == fields.Count);

		Fields = new ReadOnlyCollection<FieldInfo>(fields);
	}

	private static FieldInfo ReadFieldInfo(
		string name, string alias, FieldType type, DataReader reader,
		GeometryType geometryType, bool tableHasZ, bool tableHasM, bool tableHasID)
	{
		byte flag, size;
		var field = new FieldInfo(name, alias, type);

		switch (type)
		{
			case FieldType.ObjectID:
				_ = reader.ReadByte(); // always 2 (4)
				_ = reader.ReadByte(); // always 4 (2)
				field.Nullable = false;
				break;

			case FieldType.Geometry:
				var geomDef = new GeometryDef(geometryType, tableHasZ, tableHasM, tableHasID);
				_ = reader.ReadByte(); // unknown (always 0?)
				flag = reader.ReadByte();
				field.Nullable = (flag & 1) != 0;

				var wktLen = reader.ReadInt16(); // in bytes
				var wkt = reader.ReadUtf16(wktLen / 2); // in chars
				geomDef.SpatialReference = wkt;

				var geomFlags = reader.ReadByte();
				// weird, but these are different from the table-level HasZ/M flags:
				bool hasInfoM = (geomFlags & 2) != 0; // i.e., M scale, domain, tolerance
				bool hasInfoZ = (geomFlags & 4) != 0; // i.e., Z scale, domain, tolerance

				geomDef.XOrigin = reader.ReadDouble();
				geomDef.YOrigin = reader.ReadDouble();
				geomDef.XYScale = reader.ReadDouble();

				if (hasInfoM)
				{
					//geomDef.HasM = tableHasM;
					geomDef.MOrigin = reader.ReadDouble();
					geomDef.MScale = reader.ReadDouble();
				}

				if (hasInfoZ)
				{
					//geomDef.HasZ = tableHasZ;
					geomDef.ZOrigin = reader.ReadDouble();
					geomDef.ZScale = reader.ReadDouble();
				}

				geomDef.XYTolerance = reader.ReadDouble();

				if (hasInfoM)
				{
					geomDef.MTolerance = reader.ReadDouble();
				}

				if (hasInfoZ)
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
				_ = reader.ReadByte();
				flag = reader.ReadByte();
				field.Nullable = (flag & 1) != 0;
				var nbcar = reader.ReadByte();
				var rasterColumn = reader.ReadUtf16(nbcar);
				wktLen = reader.ReadInt16();
				wkt = reader.ReadUtf16(wktLen / 2); // in chars
				var magic3 = reader.ReadByte();
				if (magic3 > 0)
				{
					bool rasterHasZ = false;
					bool rasterHasM = false;
					if (magic3 == 5) rasterHasZ = true;
					if (magic3 == 7) rasterHasM = rasterHasZ = true;
					var xOrig = reader.ReadDouble();
					var yOrig = reader.ReadDouble();
					var xyScale = reader.ReadDouble();
					if (rasterHasM)
					{
						var mOrig = reader.ReadDouble();
						var mScale = reader.ReadDouble();
					}
					if (rasterHasZ)
					{
						var zOrig = reader.ReadDouble();
						var zScale = reader.ReadDouble();
					}
					var xyTolerance = reader.ReadDouble();
					if (rasterHasM)
					{
						var mTolerance = reader.ReadDouble();
					}
					if (rasterHasZ)
					{
						var zTolerance = reader.ReadDouble();
					}
				}
				field.RasterType = reader.ReadByte();
				// 0 = external raster, 1 = managed raster, 2 = inline binary raster
				break;
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

	private IReadOnlyList<IndexInfo> LoadIndexes()
	{
		var fileName = Path.ChangeExtension(BaseName, ".gdbindexes");
		var filePath = Path.Combine(FolderPath, fileName);
		using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
		using var reader = new DataReader(stream);

		var numIndexes = reader.ReadInt32();
		var indexes = new List<IndexInfo>();

		for (int i = 0; i < numIndexes; i++)
		{
			int numNameChars = reader.ReadInt32();
			string indexName = reader.ReadUtf16(numNameChars);

			var magic1 = reader.ReadInt16();
			var magic2 = reader.ReadInt32();
			var magic3 = reader.ReadInt16();
			var magic4 = reader.ReadInt32();

			int numFieldChars = reader.ReadInt32();
			string fieldName = reader.ReadUtf16(numFieldChars);

			var magic5 = reader.ReadInt16();

			// TODO IsUnique? IsAscending/Descending? Type (spatial/attribute)?

			indexes.Add(new IndexInfo(indexName, fieldName));
		}

		return indexes;
	}

	private static Exception Error(string message)
	{
		return new IOException(message);
	}

	private static string? Canonical(string? text)
	{
		if (text is null) return null;
		text = text.Trim();
		return text.Length < 1 ? null : text;
	}
}

public abstract class RowsResult //: IEnumerable<int>, IEnumerator<int>
{
	// concrete subclasses: e.g. FullScanResult, TableSubsetResult, ...?

	protected RowsResult(bool hasShape, IReadOnlyList<FieldInfo> fields)
	{
		HasShape = hasShape;
		Fields = fields ?? throw new ArgumentNullException(nameof(fields));
	}

	public IReadOnlyList<FieldInfo> Fields { get; }

	public bool HasShape { get; }

	/// <summary>Advance to next row (including the first row)</summary>
	public abstract bool Step();

	#region IEnumerable & IEnumerator

	//IEnumerator IEnumerable.GetEnumerator()
	//{
	//	return GetEnumerator();
	//}

	//public IEnumerator<int> GetEnumerator()
	//{
	//	return this;
	//}

	//bool IEnumerator.MoveNext()
	//{
	//	return Step();
	//}

	//void IEnumerator.Reset()
	//{
	//	throw new NotImplementedException();
	//}

	//object IEnumerator.Current => OID;

	//void IDisposable.Dispose()
	//{
	//	// nothing to dispose
	//}

	//int IEnumerator<int>.Current => OID;

	#endregion

	public abstract long OID { get; }

	public abstract GeometryBlob? Shape { get; } // null if no shape

	public abstract object? GetValue(string fieldName);
}

public class TableScanResult : RowsResult
{
	private long _oid;
	private readonly FieldInfo[] _fields;
	private readonly object?[] _values;
	private readonly long _maxOid;
	private readonly Table _table;
	private readonly IDictionary<string, int> _fieldIndices;
	private readonly int _shapeFieldIndex;

	public TableScanResult(Table table) : base(HasGeometry(table), GetFields(table))
	{
		_table = table ?? throw new ArgumentNullException(nameof(table));
		_oid = 0;
		_maxOid = table.MaxObjectID;
		_fields = table.Fields.ToArray();
		_values = new object[_fields.Length];
		_fieldIndices = new Dictionary<string, int>();
		_shapeFieldIndex = FindShapeField(table.Fields);
	}

	public override bool Step()
	{
		_oid += 1;

		while (_oid <= _maxOid)
		{
			var row = _table.ReadRow(_oid, _values);
			if (row is not null) return true;
			_oid += 1;
		}

		return false; // exhausted
	}

	public override long OID => _oid;

	public override GeometryBlob? Shape => GetShape();

	public override object? GetValue(string? fieldName)
	{
		if (fieldName is null) return null;

		if (!_fieldIndices.TryGetValue(fieldName, out int index))
		{
			index = FindField(fieldName, _fields);
			if (index < 0) throw new FileGDBException($"No such field: {fieldName}");
			_fieldIndices.Add(fieldName, index);
		}

		return _values[index];
	}

	private static int FindField(string fieldName, FieldInfo[] fields)
	{
		for (int i = 0; i < fields.Length; i++)
		{
			if (string.Equals(fieldName, fields[i].Name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return -1; // not found
	}

	/// <returns>index of first field of type Geometry, or -1 if no such field</returns>
	private static int FindShapeField(IReadOnlyList<FieldInfo> fields)
	{
		int count = fields.Count;

		for (int i = 0; i < count; i++)
		{
			if (fields[i].Type == FieldType.Geometry)
			{
				return i;
			}
		}

		return -1; // no shape field
	}

	private GeometryBlob? GetShape()
	{
		if (_shapeFieldIndex < 0)
		{
			return null; // table has no shape field
		}

		return _values[_shapeFieldIndex] as GeometryBlob;
	}

	private static bool HasGeometry(Table? table)
	{
		if (table is null) return false;
		return table.GeometryType != GeometryType.Null;
	}

	private static ImmutableArray<FieldInfo> GetFields(Table? table)
	{
		return table is null
			? ImmutableArray<FieldInfo>.Empty
			: table.Fields.ToImmutableArray();
	}
}
