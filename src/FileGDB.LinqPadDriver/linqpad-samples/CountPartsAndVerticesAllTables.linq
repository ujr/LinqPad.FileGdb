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
 * Count shapes, parts, and vertices per feature class.
 * Connect to a File Geodatabase prior to running this.
 *
 * Notice: OpenTable() returns a plain Table object,
 * which is not directly enumerable; the Enumerable()
 * extension method returns a wrapper that is enumerable,
 * has OID and Shape properties, and a GetValue(fieldName)
 * method, which returns null if there is no such field.
 */

from c in Catalog
where c.IsUserTable()
let n = c.Name
let t = c.OpenTable().Enumerable()
where t.GeometryType != GeometryType.Null
orderby n ascending
select new
{
    Name = n,
    Shapes = t.Count(r => r.Shape is not null),
    Parts = t.Sum(r => r.Shape?.ShapeBuffer.NumParts ?? 0),
    Points = t.Sum(r => r.Shape?.ShapeBuffer.NumPoints ?? 0)
}
