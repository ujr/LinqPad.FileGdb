# FileGDB Driver for LINQPad

Based on reverse-engineered spec by Even Rouault:  
<https://github.com/rouault/dump_gdbtable/wiki/FGDB-Spec>

## Notes

- a File GDB is a flat list of tables (no hierarchy)
- the first such table contains the catalog (list of tables)
- Object IDs in the File GDB begin with 1
- max Object ID is 2.147.483.648 (by Esri docs, i.e. `2**31`,
  but I'd guess it's rather `2**31-1`) (but see next)
- three known versions: 9.x (very old), 10.x (current), and
  10.x with 64bit Object IDs (optional since ArcGIS Pro 3.2)

## Versions

There is not much documentation about Geodatabase versioning.
The communicated version seems to be the version of the client
software that created or upgraded the Geodatabase; within the
data file for a File Geodatabase table is a **version** field
that reads **3** for 9.x and **4** for 10.x File GDBs.

Quoting from ArcGIS Pro documentation at
<https://pro.arcgis.com/en/pro-app/latest/help/data/geodatabases/overview/client-geodatabase-compatibility.htm>

> The version for file geodatabases has not changed since \[ArcGIS] 10.1.
> The version for mobile geodatabases \[SQLite] has not changed since ArcGIS Pro 2.7.

Since ArcGIS Pro 3.2 the Object IDs can optionally be 64bit
integers (before only 32bit). Such tables report **6** as
their version.

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

## System Tables

- GDB_SystemCatalog: the catalog: list of all tables
- GDB_DBTune: config keyword parameters
- GDB_SpatialRefs: spatial references used by tables in this File GDB
- GDB_Items: the GDB_Items system table
- GDB_ItemTypes: the GDB_ItemTypes system table
- GDB_ItemRelationships: the GDB_ItemRelationships system table
- GDB_ItemRelationshipTypes: the GDB_ItemRelationshipTypes system table
- GDB_ReplicaLog: the ReplicaLog system table (may not exist)
- GDB_ReplicaChanges: replica changes, only exists if this GDB is a replica

Additionally with Pro 3.2

- GDB_EditingTemplates
- GDB_EditingTemplateRelationships

Version 9.x File GDBs had many more system tables

## Item Types

The system table `GDB_ItemTypes` defines a class hierarchy
with `Item` at its root.

- Item
  - Folder
  - Resource
    - Dataset
      - Extension Dataset
        - Representation Class, Catalog Dataset, Trace
          Network, Mosaic Dataset, Network Dataset
          Parcel Dataset, Terrain, Parcel Fabric
          Utility Network, Location Referencing Dataset
      - Topology
      - Diagram Dataset
      - Tin
      - Relationship Class
      - Geometric Network
      - Domain
        - Coded Value Domain and Range Domain
      - Replica Dataset
      - Feature Dataset
      - AbstractTable
        - Table, Feature Class, Raster Dataset, Raster Class
      - Workspace Extension
      - Workspace
      - Sync Dataset
      - Sync Replica
      - Toolbox
      - Historical Marker
      - Replica
      - Survey Dataset
      - Network Diagram

The system table `GDB_RelationshipTypes` defines the
possible relationships between the item types. Examples:

- DatasetInFeatureDataset (origin: FeatureDataset, dest: Dataset)
- FeatureClassInTopology (origin: Topology, dest: Feature Class)
- RepresentationOfFeatureClass (origin: Feature Class, dest: Representation Class)
- DatasetOfReplicaDataset (origin: Replica Dataset, dest: Dataset)
- and some 20 more

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
