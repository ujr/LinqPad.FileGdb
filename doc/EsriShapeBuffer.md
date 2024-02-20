# Esri Shape Buffer

Geometries are internally serialized into a byte array.
The format is an extension of what has been used for
Shapefiles since the 1990ies.

Can we read/write this byte array ourselves?
Why would we?

## Support in the Pro SDK

- `Geometry.ToEsriShape()` returns the byte array serialization for any geometry
- `GeometryEngine.GetEsriShapeSize(flags, geometry)` required byte array size
- `GeometryEngine.ImportFromEsriShape(flags, buffer, sref)`
- `GeometryEngine.ExportToEsriShape(flags, geometry)`
- `FooBuilder.FromEsriShape(buffer, sref)` hydrate geometry from byte array

## General Structure

Basic types

- Integer: signed 32 bit integer (2's complement)
- Double: IEEE double precision (64 bit) floating point
- Point: two Double coordinates X and Y
- BoundingBox: four Doubles XMin, YMin, XMax, YMax

Polygon and Polyline shapes are represented as indicated below,
and have been so since Shapefile times:

```text
Integer      type
BoundingBox  bbox
Integer      numParts
Integer      numPoints
Integer      parts[numParts]
Point        points[numPoints]
```

## References

*ESRI Shapefile Technical Description.*
An ESRI White Paper--July 1998.
Available online: <https://support.esri.com/en/white-paper/279>

*Extended Shape Buffer Format.* June 20, 2012.
Available as part of the File Geodatabase API:
<http://www.esri.com/apps/products/download/>

(ev my old shpdump on github)

## Snippets

```C#
public static class ShapeBuffer
{
    private enum Endian { Big, Little };

    private static double ReadDouble(byte[] buffer, int offset, Endian endian) { ... }
    private static void WriteDouble(byte[] buffer, int offset, Endian endian, double value) { ... }
    // etc for Int32, ...

    public static ShapeType GetShapeType(byte[] buffer, int offset = 0) { ... } // usu 1st word
    public static bool IsZAware(byte[] buffer, int offset = 0) { ... }
    // etc for IsMAware, IsIDAware, HasCurves, ...

    public long GetBufferSize(...) { ... } // compute, to alloc proper array
    // overloads for Point/Multipoint/Polyline/...
    public Envelope GetExtent(buffer|Shape) {...}

    public void WriteShape(Shape shape, byte[] buffer, int offset = 0) {...}
    public Shape ReadShape(byte[] buffer, int offset = 0) {...}
}
```
