# FileGDB Driver for LINQPad

A driver for LINQPad that allows read-only access
to the Esri File Geodatabase (also known as File GDB,
FileGDB, or FGDB.

## About

Esri File Geodatabases store spatial data as relational
tables persisted in a bunch of files in a folder with
the extension `.gdb`.

This package contains a driver for LINQPad that allows
it to read File Geodatabases. The driver makes use of
a small .NET library to read the File GDB; it does not
use Esri's File Geodatabase API nor any other library.

## How to use in LINQPad

1. Install the driver
2. Add a connection to a File Geodatabase
3. Write queries (or any other .NET code)

Start LINQPad and click on *Add connection*. This opens
the *Choose Data Context* dialog. At the bottom, click
on *View more drivers* to open the LINQPad NuGet Manager.
Click on *Settings* and make sure that the location of
this NuGet is a package source; if not, add a new package
source (see LINQPad documentation for details).

Once the driver is available to LINQPad, in the *Choose
Data Context* dialog select "FileGDB Driver" and click
*Next*. The *File GDB Connection Details* dialog appears,
where you enter the path to the File GDB .gdb folder.

A new data connection entry will appear and list all
the tables in the File GDB. You can drag those table
entries to a Query pane and use Intellisense to write
LINQ queries against the table. For example:

```cs
Tables.MyPointTable.Dump();
Tables.MyPointTable.Select(row => row.Shape.ShapeBuffer).Dump();
Tables.MyPointTable.Select(row => row.Shape.Bytes).Dump();
Tables.MyPointTable.GetRow(5).Dump(); // get row by Object ID
```

## Limitations

- this is **experimental** code and comes with **no warranty**
- only a small subset of the File Geodatabase is supported
- field types DateOnly, TimeOnly, DateTimeOffset are not implemented
- MultiPatch geometries are not supported
- only full table scans are supported
- indices are not used and not accessible
- no concurrency control (no locking)
- strictly read-only (no updates)

## References

- LINQPad:
  <https://www.linqpad.net/>
- Even Rouault's FGDB specification:
  <https://github.com/rouault/dump_gdbtable/wiki/FGDB-Spec>
- Esri's File Geodatabase API on GitHub:
  <https://github.com/Esri/file-geodatabase-api>

This project would not have been possible without
Even Rouault's detailed FGDB specification.
