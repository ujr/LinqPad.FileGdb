# FileGDB Driver for LINQPad

Based on reverse-engineered spec by Even Rouault:  
<https://github.com/rouault/dump_gdbtable/wiki/FGDB-Spec>

## Notes

- a File GDB is a flat list of tables (.gdbtable files, no hierarchy)
- the first such table contains the catalog (list of tables)
- ObjectIDs in the File GDB begin with 1
- max ObjectID is 2.147.483.648 (by Esri docs, i.e. `2**31`,
  but I'd guess it's rather `2**31-1`)
- three known versions: 9.x (very old), 10.x (current), and
  10.x with 64bit ObjectIDs (optional since ArcGIS Pro 3.2)

## Versions

There is not much documentation about Geodatabase versioning.
The communicated version seems to be the version of the client
software that created or upgraded the Geodatabase; within the
data file for a File GDB table is a version field that reads
3 for 9.x and 4 for 10.x File GDBs.

Quoting from ArcGIS Pro documentation at
<https://pro.arcgis.com/en/pro-app/latest/help/data/geodatabases/overview/client-geodatabase-compatibility.htm>

> The version for file geodatabases has not changed since \[ArcGIS] 10.1.
> The version for mobile geodatabases \[SQLite] has not changed since ArcGIS Pro 2.7.

Since ArcGIS Pro 3.2 the Object IDs can optionally be 64bit
integers (before only 32bit). This does not constitute a new
version of the File GDB.

## Limits

Size and name limits (from Esri's ArcGIS Pro documentation at
<https://pro.arcgis.com/en/pro-app/latest/help/data/geodatabases/manage-file-gdb/file-geodatabase-size-and-name-limits.htm>)

- File geodatabase size: No limit
- Table or feature class size: 1 TB (default), 4 GB or 256 TB with keyword
- Number of feature classes and tables: 2,147,483,647
- Number of fields in a feature class or table: 65,534
- Number of rows in a feature class or table: 2,147,483,647
- Geodatabase name length: The number of characters the operating system allows in a folder name
- Feature dataset name length: 160 characters
- Feature class or table name length: 160 characters
- Field name length: 64 characters
- Text field width: 2,147,483,647 characters

## Configuration Keywords

List of keywords and how they affect data storage.
It is for ArcGIS 10.0 and cannot be customized.
Additional keywords may be added in later releases.

- DEFAULTS:  
  Stores data up to 1 TB in size. Text is stored in UTF8 format.

- TEXT_UTF16:  
  Stores data up to 1 TB in size. Text is stored in UTF16 format.

- MAX_FILE_SIZE_4GB:  
  Limits data size to 4 GB. Text is stored in UTF8 format.

- MAX_FILE_SIZE_256TB:
  Stores data up to 256 TB in size. Text is stored in UTF8 format

- GEOMETRY_OUTOFLINE:
  Stores data up to 1 TB in size. Text is stored in UTF8 format.
  Stores the geometry attribute in a file separate from the nonspatial attributes

- BLOB_OUTOFLINE:
  Stores data up to 1 TB in size. Text is stored in UTF8 format.
  Stores BLOB attributes in a file separate from the rest of the attributes.

- GEOMETRY_AND_BLOB_OUTOFLINE:
  Stores data up to 1 TB in size. Text is stored in UTF8 format.
  Stores both geometry and BLOB attributes in files separate from the rest of the attributes.

## Resources

- Specification (reverse-engineered) by Even Rouault:  
  <https://github.com/rouault/dump_gdbtable/wiki/FGDB-Spec>

- Esri's File Geodatabase API on GitHub:  
  <https://github.com/Esri/file-geodatabase-api>

- Esri File Geodatabase API topic in GeoNet:  
  <https://community.esri.com/t5/file-geodatabase-api/ct-p/file-geodatabase-api>
