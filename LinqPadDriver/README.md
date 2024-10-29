# FileGDB Driver for LINQPad

A driver for LINQPad that allows read-only access
to the Esri File Geodatabase (File GDB).

## About

Esri File Geodatabases (File GDB) store spatial data
as relational tables on the local disk.
This NuGet contains a driver for LINQPad that allows
it to read File Geodatabases. The driver makes use of
a small .NET library to read the File GDB; it does not
use Esri's File Geodatabase API.

## How to use in LINQPad

Start LINQPad and click on *Add connection*. This opens
the *Choose Data Context* dialog. At the bottom, click
on *View more driver* to open the LINQPad NuGet Manager.
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
LINQ queries against the table.

## How to use the .NET File GDB library

The FileGDB.Core assembly that ships as part of this NuGet
contains a pure .NET Core library to read a 10.x File GDB
(the old 9.x File GDBs are not supported).

TODO

## References

- LINQPad:
  <https://www.linqpad.net/>
- Even Rouault's File GDB specification:
  <https://github.com/rouault/dump_gdbtable/wiki/FGDB-Spec>
- Esri's File Geodatabase API on GitHub:
  <https://github.com/Esri/file-geodatabase-api>
