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
