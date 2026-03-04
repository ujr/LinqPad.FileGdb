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
 * List all item definitions in the File GDB (essentially
 * the GDB_Items table).
 * Connect to a File Geodatabase prior to running this.
 */

from i in Tables.GDB_Items
join t in Tables.GDB_ItemTypes on i.Type equals t.UUID
select new {
  Type = t.Name, i.Name, i.Path,
  Definition = i.Definition is null
    ? null
    : Util.OnDemand("Definition", () => XElement.Parse(i.Definition))
}
