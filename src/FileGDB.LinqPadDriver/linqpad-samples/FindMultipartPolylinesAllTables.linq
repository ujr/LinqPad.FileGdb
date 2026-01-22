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
 * Find all multipart polylines across all tables.
 * Connect to a File Geodatabase prior to running this.
 */

from entry in Catalog
where entry.IsUserTable()
let table = entry.OpenTable()
where table.GeometryType == GeometryType.Polyline
from row in table.Enumerable()
where row.Shape.ShapeBuffer.NumParts > 1
select new
{
    TableName = entry.Name, row.OID,
    Shape = Util.OnDemand(row.Shape?.ShapeType.ToString(), () => row.Shape),
    row.Shape.ShapeBuffer.NumParts,
    Operator = row.GetValue("OPERATOR")
}
