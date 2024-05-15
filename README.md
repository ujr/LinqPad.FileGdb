# FileGDB Driver for LINQPad

Based on reverse-engineered spec by Even Rouault:  
<https://github.com/rouault/dump_gdbtable/wiki/FGDB-Spec>

## Notes

- ObjectIDs in the FileGDB begin with 1
- max ObjectID is 2.147.483.648 (by Esri docs, i.e. `2**31`,
  but I'd guess it's rather `2**31-1`)

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
  Stores data up to 1 TB in size. Text is stored in UTF8 format. Stores the geometry attribute in a file separate from the nonspatial attributes

- BLOB_OUTOFLINE:
  Stores data up to 1 TB in size. Text is stored in UTF8 format. Stores BLOB attributes in a file separate from the rest of the attributes.

- GEOMETRY_AND_BLOB_OUTOFLINE:
  Stores data up to 1 TB in size. Text is stored in UTF8 format. Stores both geometry and BLOB attributes in files separate from the rest of the attributes.

## Resources

- Specification (reverse-engineered) by Even Rouault:  
  <https://github.com/rouault/dump_gdbtable/wiki/FGDB-Spec>

- Esri's File Geodatabase API on GitHub:  
  <https://github.com/Esri/file-geodatabase-api>

- Esri File Geodatabase API topic in GeoNet:  
  <https://community.esri.com/t5/file-geodatabase-api/ct-p/file-geodatabase-api>
