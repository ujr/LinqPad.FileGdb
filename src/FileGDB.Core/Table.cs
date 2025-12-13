using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace FileGDB.Core;

/// <summary>
/// Represents a single FGDB table. Each FGDB table has at least two files:
/// - aXXXXXXXX.gdbtable - the data file (field descriptions and row data)
/// - aXXXXXXXX.gdbtablx - the index file (row offsets by OID)
/// and may have additional files for spatial and/or attribute indexes:
/// - aXXXXXXXX.NAME.atx - attribute index
/// - aXXXXXXXX.NAME.spx - spatial index
/// </summary>
public sealed class Table : IDisposable
{
	private DataReader? _dataReader;
	private DataReader? _indexReader;
	private int _indexFormatVersion; // format of .gdbtablx file: 3 or 4
	private int _offsetSize; // bytes per offset in .gdbtablx file: 4 or 5 or 6
	private BitArray? _blockMap;
	private int _indexHeaderUnknown1;
	private int _tableFormatVersion; // format of .gdbtable file: 3 or 4
	private long _tableFileSize; // size of .gdbtable file in bytes
	private int _maxEntrySize; // max of all row size and field desc size (bytes)
	private int _fieldsHeaderSize; // size of fields section in bytes
	private uint _fieldsHeaderFlags;
	private bool _useUtf8; // bit 8 in _fieldsHeaderFlags
	private int _fieldCount; // including the implicit Object ID field
	private int _dataHeaderUnknown1;
	private int _dataHeaderUnknown2;
	private int _dataHeaderUnknown3;
	private int _dataHeaderUnknown4;
	private IReadOnlyList<IndexInfo>? _indexes;

	/// <summary>The File GDB epoch for date values: 1899-12-30 00:00:00</summary>
	private static readonly DateTime Epoch = new(1899, 12, 30, 0, 0, 0);

	/// <summary>The base name of the files for this table, always
	/// of the form aXXXXXXXX where XXXXXXXX is the zero-padded
	/// hexadecimal number of the table.</summary>
	public string BaseName { get; }

	/// <summary>The folder that contains all files of this file
	/// geodatabase, conventionally with the extension ".gdb"</summary>
	public string FolderPath { get; }

	/// <summary>The version of this table: 3 if it was created
	/// with ArcGIS 9.x, 4 if created with ArcGIS 10.x, and may
	/// be 6 if using new ArcGIS Pro 3.2 features</summary>
	public int Version { get; private set; }

	/// <summary>Number of rows in this table (not counting deleted rows)</summary>
	public long RowCount { get; private set; }

	/// <summary>The geometry type, or GeometryType.Null
	/// if this table has no geometry</summary>
	public GeometryType GeometryType { get; private set; }

	public bool HasZ { get; private set; }
	public bool HasM { get; private set; }

	/// <summary>The highest Object ID ever used</summary>
	/// <remarks>This value is stored in the .gdbtablx file and
	/// equals the number of rows including deleted rows</remarks>
	public long MaxObjectID { get; private set; }

	/// <summary>The fields on this table (name, type, nullable, etc.)</summary>
	/// <remarks>Values for the fields will be returned in the same order</remarks>
	public IReadOnlyList<FieldInfo> Fields { get; private set; }

	/// <summary>Info on the indexes associated with this table</summary>
	public IReadOnlyList<IndexInfo> Indexes => _indexes ??= LoadIndexes();

	public InternalInfo Internals { get; }

	private Table(string baseName, string folderPath)
	{
		if (baseName is null)
			throw new ArgumentNullException(nameof(baseName));
		if (baseName.Length != 9 || (baseName[0] != 'a' && baseName[0] != 'A'))
			throw new ArgumentException("Malformed table file name", nameof(baseName));

		BaseName = baseName;
		FolderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));

		Fields = new ReadOnlyCollection<FieldInfo>(Array.Empty<FieldInfo>());
		Internals = new InternalInfo(this);
	}

	public class InternalInfo
	{
		private readonly Table _table;

		public InternalInfo(Table table)
		{
			_table = table ?? throw new ArgumentNullException(nameof(table));
		}

		/// <summary>Version in .gdbtablx file: 3 for 9.x, 4 for 10.x</summary>
		public int IndexFormatVersion => _table._indexFormatVersion;

		/// <summary>Bytes per offset in the .gdbtablx file: 4 or 5 or 6</summary>
		public int OffsetSize => _table._offsetSize;

		public int IndexHeaderUnknown1 => _table._indexHeaderUnknown1;

		/// <summary>Version in .gdbtable file: 3 for 9.x, 4 for 10.x</summary>
		public int TableFormatVersion => _table._tableFormatVersion;

		/// <summary>Size of the .gdbtable file in bytes</summary>
		/// <remarks>Should equal new FileInfo(GetDataFilePath()).Length</remarks>
		public long TableFileSize => _table._tableFileSize;

		/// <summary>Max of all row sizes and the field description section</summary>
		/// <remarks>Probably useful to allocate a buffer; not used by the code here</remarks>
		public int MaxEntrySize => _table._maxEntrySize;

		public int FieldsHeaderSize => _table._fieldsHeaderSize;

		public uint FieldsHeaderFlags => _table._fieldsHeaderFlags;

		/// <remarks>Bit 8 in <see cref="FieldsHeaderFlags"/></remarks>
		public bool UseUtf8 => _table._useUtf8;

		/// <summary>Number of fields, including the implicit Object ID field</summary>
		/// <remarks>Equals Table.Fields.Count</remarks>
		public int FieldCount => _table._fieldCount;

		public int DataHeaderUnknown1 => _table._dataHeaderUnknown1;
		public int DataHeaderUnknown2 => _table._dataHeaderUnknown2;
		public int DataHeaderUnknown3 => _table._dataHeaderUnknown3;
		public int DataHeaderUnknown4 => _table._dataHeaderUnknown4;
	}

	/// <returns>Full path to the data file (.gdbtable)</returns>
	public string GetDataFilePath()
	{
		var dataFileName = Path.ChangeExtension(BaseName, ".gdbtable");
		var dataFilePath = Path.Combine(FolderPath, dataFileName);
		return dataFilePath;
	}

	/// <returns>Full path to the index file (.gdbtablx)</returns>
	public string GetIndexFilePath()
	{
		var indexFileName = Path.ChangeExtension(BaseName, ".gdbtablx");
		var indexFilePath = Path.Combine(FolderPath, indexFileName);
		return indexFilePath;
	}

	public static bool Exists(string baseName, string folderPath, out string reason)
	{
		var table = new Table(baseName, folderPath);

		var dataPath = table.GetDataFilePath();
		if (!File.Exists(dataPath))
		{
			reason = $"Data file {dataPath} does not exist";
			return false;
		}

		var indexPath = table.GetIndexFilePath();
		if (!File.Exists(indexPath))
		{
			reason = $"Index file {indexPath} does not exist";
			return false;
		}

		reason = string.Empty;
		return true;
	}

	public static Table Open(string baseName, string folderPath)
	{
		var table = new Table(baseName, folderPath);

		var dataPath = table.GetDataFilePath();
		var dataStream = new FileStream(dataPath, FileMode.Open, FileAccess.Read);

		var indexPath = table.GetIndexFilePath();
		var indexStream = new FileStream(indexPath, FileMode.Open, FileAccess.Read);

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

	/// <returns>Field index of shape field, or -1 if no shape field</returns>
	/// <remarks>We define the shape field to be the first field of type Geometry</remarks>
	public int GetShapeIndex()
	{
		var fields = Fields;
		var fieldCount = fields.Count;

		for (int i = 0; i < fieldCount; i++)
		{
			if (fields[i].Type == FieldType.Geometry)
			{
				return i;
			}
		}

		return -1; // no shape field
	}

	/// <returns>Index of the named field, or -1 if no such field</returns>
	public int FindField(string fieldName)
	{
		for (int i = 0; i < Fields.Count; i++)
		{
			if (string.Equals(fieldName, Fields[i].Name, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return -1; // not found
	}

	/// <summary>
	/// Read raw bytes for the row with the given <paramref name="oid"/>.
	/// Never overflows the given <paramref name="bytes"/> array, but
	/// may not read all bytes of the row.
	/// </summary>
	/// <param name="oid">The OID of the row to read</param>
	/// <param name="bytes">Where to put the bytes read for the row;
	/// can be null or smaller than the size required to read all bytes</param>
	/// <param name="bytesOffset">Optional start offset into <paramref name="bytes"/></param>
	/// <returns>Size in bytes of this row</returns>
	public long ReadRowBytes(long oid, byte[]? bytes = null, int bytesOffset = 0)
	{
		if (_dataReader is null)
			throw new ObjectDisposedException(GetType().Name);

		var rowOffset = GetRowOffset(oid);
		if (rowOffset <= 0) return -1; // no such oid (or deleted)

		_dataReader.Seek(rowOffset);

		uint rowDataSize = _dataReader.ReadUInt32();
		if (rowDataSize > int.MaxValue)
			throw new NotSupportedException(
				$"Record with OID {oid} is {rowDataSize} bytes, which is too big for this API");

		if (bytes is not null)
		{
			_dataReader.ReadBytes((int)rowDataSize, bytes, bytesOffset);
		}

		return rowDataSize;
	}

	/// <summary>
	/// Read the values for the row with the given <paramref name="oid"/>.
	/// </summary>
	/// <param name="oid">The OID of the row to read</param>
	/// <param name="values">An optional array to receive the row's
	/// field values; if null, a new array will be allocated; if too
	/// small, not all fields will be read.</param>
	/// <returns>An array of field values read from the row,
	/// or null if there is no row with the given OID</returns>
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
					values[i] = ReadTextValue(_dataReader, _useUtf8);
					break;
				case FieldType.DateTime:
					values[i] = ReadDateTimeValue(_dataReader);
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
					values[i] = ReadBlobValue(_dataReader);
					break;
				case FieldType.Raster:
					// depends on RasterType in field definition
					throw new NotImplementedException("Raster fields not yet implemented");
				case FieldType.GUID:
				case FieldType.GlobalID:
					values[i] = ReadGuidValue(_dataReader);
					break;
				case FieldType.Int64:
					values[i] = _dataReader.ReadInt64();
					break;
				case FieldType.DateOnly:
					values[i] = ReadDateOnlyValue(_dataReader);
					break;
				case FieldType.TimeOnly:
					values[i] = ReadTimeOnlyValue(_dataReader);
					break;
				case FieldType.DateTimeOffset:
					values[i] = ReadDateTimeOffsetValue(_dataReader);
					break;
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
			// - LINESZM and MULTIPOINTSZM in TestSLDLM.gdb
			// but not if I copied these tables to a new file gdb
			//Debug.Assert(false, $"Unread data in row blob: {delta} byte(s)");
		}

		return values;
	}

	/// <summary>Read rows from this table</summary>
	/// <param name="whereClause">NOT YET IMPLEMENTED (pass null)</param>
	/// <param name="extent">NOT YET IMPLEMENTED (pass null)</param>
	/// <returns>A cursor object to iterate over the result set</returns>
	public RowsResult ReadRows(string? whereClause, Envelope? extent)
	{
		whereClause = Canonical(whereClause);

		if (whereClause != null)
			throw new NotImplementedException("Where clause filtering not yet implemented");
		if (extent != null)
			throw new NotImplementedException("Spatial extent filtering not yet implemented");

		return new TableScanResult(this);
	}

	/// <summary>
	/// Get the CLR data type used for the given field type.
	/// </summary>
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
				return typeof(DateOnly?);
			case FieldType.TimeOnly:
				return typeof(TimeOnly?);
			case FieldType.DateTimeOffset:
				return typeof(DateTimeOffset?);
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

		if (_blockMap != null)
		{
			long blockNum = oid / 1024;
			if (blockNum > int.MaxValue)
				throw Error($"OID {oid} is too big for this implementation");

			if (!_blockMap[(int)blockNum])
			{
				return -1; // no such oid
			}

			int blocksBefore = 0; // TODO optimize for sequential reading
			for (int i = 0; i < blockNum; i++)
			{
				blocksBefore += _blockMap[i] ? 1 : 0;
			}

			var correctedRow = blocksBefore * 1024 + oid % 1024;

			_indexReader.Seek(16 + _offsetSize * correctedRow);
		}
		else
		{
			long offset = 16 + oid * _offsetSize;
			if (offset >= _indexReader.Length)
			{
				return -1; // no such oid
			}
			_indexReader.Seek(offset);
		}

		long result = _offsetSize switch
		{
			4 => _indexReader.ReadUInt32(),
			5 => _indexReader.ReadUInt40(),
			6 => _indexReader.ReadUInt48(),
			_ => throw Error($"Offset size {_offsetSize} is out of range; expect 4 or 5 or 6")
		};

		return result;
	}

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

	private static byte[] ReadBlobValue(DataReader reader)
	{
		// assume reader positioned at start of field data
		var size = reader.ReadVarUInt();
		if (size > int.MaxValue)
			throw Error("Blob field too large for this API");
		var bytes = reader.ReadBytes((int)size);
		return bytes;
	}

	private static string ReadTextValue(DataReader reader, bool isUtf8)
	{
		// assume reader positioned at start of field data
		var size = reader.ReadVarUInt();
		if (size > int.MaxValue)
			throw Error("String field too large for this API");
		var bytes = reader.ReadBytes((int)size);
		var text = isUtf8
			? Encoding.UTF8.GetString(bytes)
			: throw new NotImplementedException("Non-UTF8 text fields not implemented"); // what's the encoding?
		return text;
	}

	private static Guid ReadGuidValue(DataReader reader)
	{
		var bytes = reader.ReadBytes(16);
		// FGDB: b3 b2 b1 b0   b5 b4   b7 b6   b8 b9 b10 b11 b12 b13 b14 b15 b16
		// Conveniently, the FGDB stores the GUID's bytes in the order expected
		// by the Guid class constructor:
		return new Guid(bytes);
	}

	private static DateTime ReadDateTimeValue(DataReader reader)
	{
		var days = reader.ReadDouble();
		return Epoch.AddDays(days);
	}

	private static DateOnly ReadDateOnlyValue(DataReader reader)
	{
		var days = reader.ReadDouble();
		return DateOnly.FromDateTime(Epoch.AddDays(days));
	}

	private static TimeOnly ReadTimeOnlyValue(DataReader reader)
	{
		var fraction = reader.ReadDouble();
		// 0 = 00:00:00, 1 = 23:59:59.9... clamp to range:
		if (fraction < 0) fraction = 0;
		if (fraction > 1) fraction = 1;
		var ts = TimeSpan.FromDays(1.0);
		ts *= fraction; // fails if NaN, but this should not occur
		return TimeOnly.FromTimeSpan(ts);
	}

	private static DateTimeOffset ReadDateTimeOffsetValue(DataReader reader)
	{
		var daysSinceEpoch = reader.ReadDouble();
		int utcOffsetMinutes = reader.ReadInt16();
		var dateTime = Epoch.AddDays(daysSinceEpoch);
		Debug.Assert(dateTime.Kind == DateTimeKind.Unspecified);
		var offset = TimeSpan.FromMinutes(utcOffsetMinutes);
		return new DateTimeOffset(dateTime, offset);
	}

	private void ReadIndexHeader(DataReader indexReader)
	{
		// - Header (16 bytes)
		// - Offsets (one entry per record)
		// - Trailer (16 bytes + bitmap)

		// assume reader is positioned at start of header

		_indexFormatVersion = indexReader.ReadInt32(); // 3 for 32-bit OIDs, 4 for 64-bit OIDs

		switch (_indexFormatVersion)
		{
			case 3:
				ReadIndexHeader3(indexReader);
				break;
			case 4:
				ReadIndexHeader4(indexReader);
				break;
			default:
				throw Error($"Unknown .gdbtablx version: {_indexFormatVersion}; expect 3 or 4");
		}
	}

	private void ReadIndexHeader3(DataReader indexReader)
	{
		// assume reader positioned after version field

		var num1KBlocks = indexReader.ReadInt32();
		var numRows = indexReader.ReadInt32(); // including deleted rows!

		_offsetSize = indexReader.ReadInt32(); // 4, 5, or 6 (bytes per offset)
		if (_offsetSize is not 4 and not 5 and not 6)
			throw Error($"Offset size {_offsetSize} is out of range; expect 4 or 5 or 6 (bytes per offset)");

		MaxObjectID = numRows;

		if (num1KBlocks == 0)
			Debug.Assert(numRows == 0);
		else
			Debug.Assert(numRows > 0);

		if (num1KBlocks > 0)
		{
			// Seek to trailer section:
			indexReader.Seek(16 + 1024 * num1KBlocks * _offsetSize);

			var nBitmapInt32Words = indexReader.ReadUInt32();
			var nBitsForBlockMap = indexReader.ReadUInt32();
			var num1KBlocksBis = indexReader.ReadUInt32();
			var nLeadingNonZero32BitWords = indexReader.ReadUInt32();

			Debug.Assert(num1KBlocks == num1KBlocksBis);

			if (nBitmapInt32Words == 0)
			{
				Debug.Assert(nBitsForBlockMap == num1KBlocks);
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
				Debug.Assert(nCountBlocks == num1KBlocks);
			}
		}
		else
		{
			_blockMap = null;
		}
	}

	private void ReadIndexHeader4(DataReader indexReader)
	{
		// assume reader positioned after version field

		var num1KBlocks = indexReader.ReadInt32();
		_indexHeaderUnknown1 = indexReader.ReadInt32(); // always 0? MSB of previous field?

		_offsetSize = indexReader.ReadInt32(); // 4, 5, or 6 (bytes per offset)
		if (_offsetSize is not 4 and not 5 and not 6)
			throw Error($"Offset size {_offsetSize} is out of range; expect 4 or 5 or 6 (bytes per offset)");

		if (num1KBlocks > 0)
		{
			// Seek to trailer section:
			indexReader.Seek(16 + 1024 * num1KBlocks * _offsetSize);

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

		_tableFormatVersion = reader.ReadInt32(); // 3 for 32-bit OID, 4 for 64-bit OID

		if (_tableFormatVersion == 3)
		{
			RowCount = reader.ReadInt32(); // actual (non-deleted) rows
			_maxEntrySize = reader.ReadInt32();
			_dataHeaderUnknown2 = reader.ReadInt32(); // always 5 (?)
			_dataHeaderUnknown3 = reader.ReadInt32();
			_dataHeaderUnknown4 = reader.ReadInt32();
		}
		else if (_tableFormatVersion == 4)
		{
			_dataHeaderUnknown1 = reader.ReadInt32(); // 1 if some features deleted, otherwise 0 (?)
			_maxEntrySize = reader.ReadInt32();
			_dataHeaderUnknown2 = reader.ReadInt32(); // always 5 (?)
			RowCount = reader.ReadInt64();
		}
		else
		{
			throw Error($"Unknown .gdbtable version: {_tableFormatVersion}; expect 3 or 4");
		}

		_tableFileSize = reader.ReadInt64();

		var fieldsOffset = reader.ReadInt64();
		reader.Seek(fieldsOffset);

		ReadFieldDescriptions(reader);
	}

	private void ReadFieldDescriptions(DataReader reader)
	{
		// assume reader is positioned at start of field descriptions section

		// Fixed part of fields section:
		_fieldsHeaderSize = reader.ReadInt32(); // excluding this field
		Version = reader.ReadInt32(); // 3 for FGDB at 9.x, 4 for FGDB at 10.x, 6 for FGDB using Pro 3.2 features (64bit OID, new field types)
		_fieldsHeaderFlags = reader.ReadUInt32(); // see decoding below
		_fieldCount = reader.ReadInt16(); // including the implicit Object ID field!

		// Decode known flag bits:
		_useUtf8 = (_fieldsHeaderFlags & (1 << 8)) != 0;
		GeometryType = (GeometryType)(_fieldsHeaderFlags & 255);
		HasZ = (_fieldsHeaderFlags & (1 << 31)) != 0;
		HasM = (_fieldsHeaderFlags & (1 << 30)) != 0;
		//HasID = (flags & (1 << 28)) != 0; // I think this is not on the table, only in the geom

		// Field descriptions:
		var fields = new List<FieldInfo>(Math.Max(0, _fieldCount));
		for (int i = 0; i < _fieldCount; i++)
		{
			var nameChars = reader.ReadByte();
			var name = reader.ReadUtf16(nameChars);

			var aliasChars = reader.ReadByte();
			var alias = reader.ReadUtf16(aliasChars);

			var type = (FieldType)reader.ReadByte();

			var field = ReadFieldInfo(name, alias, type, reader, GeometryType, HasZ, HasM);

			fields.Add(field); // here we also add the OID field
		}

		Debug.Assert(_fieldCount == fields.Count);

		Fields = new ReadOnlyCollection<FieldInfo>(fields);
	}

	private static FieldInfo ReadFieldInfo(
		string name, string alias, FieldType type, DataReader reader,
		GeometryType geometryType, bool tableHasZ, bool tableHasM)
	{
		int size;
		byte flag;
		var field = new FieldInfo(name, alias, type);

		switch (type)
		{
			case FieldType.ObjectID:
				size = reader.ReadByte(); // always 4 (or 8)
				flag = reader.ReadByte(); // always 2 (required, not nullable, not editable)
				break;

			case FieldType.Geometry:
				size = reader.ReadByte(); // unknown (always zero?)
				flag = reader.ReadByte();
				var geomDef = new GeometryDef(geometryType, tableHasZ, tableHasM);

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
					geomDef.MOrigin = reader.ReadDouble();
					geomDef.MScale = reader.ReadDouble();
				}

				if (hasInfoZ)
				{
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

				int gridCount = reader.ReadInt32(); // 1 or 2 or 3
				for (int i = 0; i < gridCount; i++)
				{
					switch (i)
					{
						case 0:
							geomDef.GridSize0 = reader.ReadDouble();
							break;
						case 1:
							geomDef.GridSize1 = reader.ReadDouble();
							break;
						case 2:
							geomDef.GridSize2 = reader.ReadDouble();
							break;
						default:
							throw Error($"Unexpected grid count: {gridCount}; expect 1 or 2 or 3");
					}
				}

				field.GeometryDef = geomDef;
				break;

			case FieldType.String:
				size = reader.ReadInt32();
				flag = reader.ReadByte();
				field.Length = size;
				var len = reader.ReadVarUInt();
				if (len > 0 && (flag & 4) != 0)
				{
					if (len > int.MaxValue)
						throw Error("Default value for String field too large for this API");
					field.DefaultValue = ReadDefaultValue(type, (uint)len, reader);
				}
				break;

			case FieldType.Blob:
				size = reader.ReadByte(); // always zero
				flag = reader.ReadByte();
				break;

			case FieldType.GUID:
			case FieldType.GlobalID:
				// The size is 38, suggesting that the GUID is stored like
				// "{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}" (aka registry format)
				// but indeed it's stored as 16 bytes
				size = reader.ReadByte(); // always 38
				flag = reader.ReadByte();
				break;

			case FieldType.XML:
				size = reader.ReadByte(); // 0?
				flag = reader.ReadByte();
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
				var dvl = reader.ReadByte();
				if (dvl > 0 && (flag & 4) != 0)
				{
					field.DefaultValue = ReadDefaultValue(type, dvl, reader);
					//reader.SkipBytes(dvl); // default value (skip for now)
				}
				break;

			case FieldType.Raster:
				size = reader.ReadByte();
				flag = reader.ReadByte();
				var rasterDef = new RasterDef();
				int numChars = reader.ReadByte();
				rasterDef.RasterColumn = reader.ReadUtf16(numChars);
				wktLen = reader.ReadInt16();
				rasterDef.SpatialReference = reader.ReadUtf16(wktLen / 2); // in chars
				var magic3 = reader.ReadByte();
				if (magic3 > 0)
				{
					bool rasterHasZ = false;
					bool rasterHasM = false;
					if (magic3 == 5) rasterHasZ = true;
					if (magic3 == 7) rasterHasM = rasterHasZ = true;
					rasterDef.XOrigin = reader.ReadDouble();
					rasterDef.YOrigin = reader.ReadDouble();
					rasterDef.XYScale = reader.ReadDouble();
					if (rasterHasM)
					{
						rasterDef.MOrigin = reader.ReadDouble();
						rasterDef.MScale = reader.ReadDouble();
					}
					if (rasterHasZ)
					{
						rasterDef.ZOrigin = reader.ReadDouble();
						rasterDef.ZScale = reader.ReadDouble();
					}
					rasterDef.XYTolerance = reader.ReadDouble();
					if (rasterHasM)
					{
						rasterDef.MTolerance = reader.ReadDouble();
					}
					if (rasterHasZ)
					{
						rasterDef.ZTolerance = reader.ReadDouble();
					}
				}
				rasterDef.RasterType = reader.ReadByte();
				// 0 = external raster, 1 = managed raster, 2 = inline binary raster

				field.RasterDef = rasterDef;
				break;

			default:
				throw new NotSupportedException($"Unknown field type: {type}");
		}

		field.Nullable = (flag & 1) != 0;
		field.Required = (flag & 2) != 0;
		field.Editable = (flag & 4) != 0;

		field.Size = size;
		field.Flags = flag;

		return field;
	}

	private static object? ReadDefaultValue(FieldType fieldType, uint size, DataReader reader)
	{
		object? result = null;
		bool read = false;

		switch (fieldType)
		{
			case FieldType.Int16:
				if (size == 2)
				{
					result = reader.ReadInt16();
					read = true;
				}
				break;

			case FieldType.Int32:
				if (size == 4)
				{
					result = reader.ReadInt32();
					read = true;
				}
				break;

			case FieldType.Int64:
				if (size == 8)
				{
					result = reader.ReadInt64();
					read = true;
				}
				break;

			case FieldType.Single:
				if (size == 4)
				{
					result = reader.ReadSingle();
					read = true;
				}
				break;

			case FieldType.Double:
				if (size == 8)
				{
					result = reader.ReadDouble();
					read = true;
				}
				break;

			case FieldType.DateTime:
				if (size == 8)
				{
					result = ReadDateTimeValue(reader);
					read = true;
				}
				break;

			case FieldType.DateOnly:
				if (size == 8)
				{
					result = ReadDateOnlyValue(reader);
					read = true;
				}
				break;

			case FieldType.TimeOnly:
				if (size == 8)
				{
					result = ReadTimeOnlyValue(reader);
					read = true;
				}
				break;

			case FieldType.DateTimeOffset:
				if (size == 8 + 2)
				{
					result = ReadDateTimeOffsetValue(reader);
					read = true;
				}
				break;

			case FieldType.String:
				// Empirical: UTF-8, but didn't test with old FGDBs -- TODO how about table's Utf8-flag?
				result = reader.ReadUtf8((int)size);
				read = true;
				break;
		}

		if (!read)
		{
			reader.SkipBytes(size);
		}

		return result;
	}

	private IReadOnlyList<IndexInfo> LoadIndexes()
	{
		var fileName = Path.ChangeExtension(BaseName, ".gdbindexes");
		var filePath = Path.Combine(FolderPath, fileName);
		// empirical: .gdbindexes may not exist (at least not for system tables)
		if (!File.Exists(filePath)) return Array.Empty<IndexInfo>();
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

			// IsUnique? IsAscending/Descending? Type (spatial/attribute)?
			// Esri documentation for the Add Attribute Index GP tool says
			// that IsUnique and IsAscending is not supported for FGDB and
			// the UI does not show these properties (ArcGIS Pro 3.3)

			_ = magic1 + magic2 + magic3 + magic4 + magic5; // silence the unused var warning

			int fieldIndex = FindField(fieldName);
			var fieldInfo = fieldIndex < 0 ? null : Fields[fieldIndex];
			var indexType = fieldInfo is null
				? IndexType.AttributeIndex // an expression like "LOWER(Name)"
				: fieldInfo.Type == FieldType.ObjectID
					? IndexType.PrimaryIndex
					: fieldInfo.Type == FieldType.Geometry
						? IndexType.SpatialIndex
						: IndexType.AttributeIndex;
			indexes.Add(new IndexInfo(indexName, fieldName, indexType, BaseName));
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
