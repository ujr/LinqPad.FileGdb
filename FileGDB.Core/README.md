# Reading a File Geodatabase

Limited read-only access to the Esri File Geodatabase
(also known as File GDB, FileGDB, or FGDB).

## About

Esri File Geodatabases store spatial data as relational
tables persisted in a bunch of files in a folder with
the extension `.gdb`.

This package contains a pure .NET assembly for reading
from a File Geodatabase. The assembly has no dependencies
beyond .NET; it does not use Esri's File Geodatabase API
nor any other library.

## Usage

To start, you provide the path to the `.gdb` folder that
contains the File Geodatabase files to the `FileGDB.Open()`
method. The resulting `FileGDB` object provides a catalog
(list of all tables in the File Geodatabase) and a method
to open any table. Example usage:

```cs
var gdb = FileGDB.Core.FileGDB.Open(@"path\to\my.gdb");
var allTables = gdb.Catalog; // list of all tables in File GDB
using var table = gdb.OpenTable("TableName");
var geometryType = table.GeometryType;
int tableVersion = table.Version; // 3 for 9.x, 4 for 10.x geodatabase
string fieldName = table.Fields.First().Name;
var fieldType = table.Fields.First().Type;
for (long oid = 1; oid < table.MaxObjectID; oid++)
{
    object[] values = table.ReadRow(oid);
}
```

Geometries (usually in the SHAPE column) are returned as
`GeometryBlob` objects. `GeometryBlob.Bytes` gives access
to the geometry as stored in the File GDB.
`GeometryBlob.ShapeBuffer` gives access to the geometry
as an Esri Shape Buffer byte array and information about
the geometry (type, attributes, number of points, access
coordinates, etc). Note that these two byte arrays are
different encodings of the geometry: the former is part
of the File Geodatabase and stores coordinates as variable
length integers, the latter is documented in an Esri white
paper that comes with the File Geodatabase API and stores
coordinates as `double` (IEEE 754) values.

## Limitations

- this is **experimental** code and comes with **no warranty**
- only a small subset of the File Geodatabase is supported
- only full table scans are supported
- MultiPatch geometries are not supported
- indices are not used and not accessible
- no concurrency control (no locking)
- strictly read-only (no updates)

## References

- Even Rouault's FGDB specification:
  <https://github.com/rouault/dump_gdbtable/wiki/FGDB-Spec>
- Esri's File Geodatabase API on GitHub:
  <https://github.com/Esri/file-geodatabase-api>

This project would not have been possible without
Even Rouault's detailed FGDB specification.
