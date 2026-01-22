<Query Kind="Expression">
  <Connection>
    <ID>f8b8dad7-c7cd-4cb7-bad4-ece0224418f9</ID>
    <NamingServiceVersion>2</NamingServiceVersion>
    <Persist>true</Persist>
    <Driver Assembly="FileGDB.LinqPadDriver" PublicKeyToken="no-strong-name">FileGDB.LinqPadDriver.FileGdbDriver</Driver>
    <DriverData/>
  </Connection>
</Query>

/*
 * List the XY coordinate properties of all spatial tables.
 * Connect to a File Geodatabase prior to running this.
 */

from entry in Catalog
where entry.IsUserTable()
let table = entry.OpenTable()
where table.GeometryType != GeometryType.Null
let gdef = table.Fields.First(f => f.Type == FieldType.Geometry).GeometryDef
orderby entry.Name
select new
{
	TableName = entry.Name,
	gdef.XOrigin,
	gdef.YOrigin,
	gdef.XYScale, // stored in the File GDB
	XYResolution = 1.0 / gdef.XYScale, // reported by ArcGIS
	gdef.XYTolerance
}
