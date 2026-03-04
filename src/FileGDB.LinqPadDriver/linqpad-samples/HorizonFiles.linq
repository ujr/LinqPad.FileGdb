<Query Kind="Expression">
  <Connection>
    <ID>296472f5-beff-4ac6-9f76-da4b60488e13</ID>
    <NamingServiceVersion>2</NamingServiceVersion>
    <Persist>true</Persist>
    <Driver Assembly="FileGDB.LinqPadDriver" PublicKeyToken="no-strong-name">FileGDB.LinqPadDriver.FileGdbDriver</Driver>
    <DriverData/>
  </Connection>
</Query>

// What are the .horizon files in a File Geodatabase?
// They are NOT documented, automatically created and recreated
// by ArcGIS, and seem to contain four double values (4*8 bytes)
// that represent some "horizon" that is DIFFERENT from the XY
// domain stored in the FGDB. Probably some cache?
//
// Connect to a File Geodatabase prior to running this query.

from c in Catalog where c.IsUserTable()
let horizonPath = Path.Combine(FolderPath, FileGDB.Core.FileGDB.GetTableBaseName(c.ID)) + ".horizon"
where File.Exists(horizonPath)
let bytes = File.ReadAllBytes(horizonPath)
select new {
    HorizonPath = horizonPath, TableName = c.Name, Length = new FileInfo(horizonPath).Length,
	d0 = BitConverter.ToDouble(bytes, 0), d1 = BitConverter.ToDouble(bytes, 8), d2 = BitConverter.ToDouble(bytes, 16), d3 = BitConverter.ToDouble(bytes, 24) }
