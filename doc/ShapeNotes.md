# Shape Notes

Documents:

- Esri: Extended Shape Buffer Format, June 20, 2012,
  comes with the File Geodatabase API
- Rouault: FGDB Spec (reverse engineered)

## GeometryType vs ShapeType

The shape buffer contains a ShapeType; the feature class
contains a GeometryType.

**GeometryType** is the type of geometry that can be stored
in the database. It's also referred to as the high-level
geometry type:

- 0 = geometryNull
- 1 = geometryPoint
- 2 = geometryMultipoint
- 3 = geometryPolyline
- 4 = geometryPolygon
- 9 = geometryMultiPatch

**ShapeType** *also* comprises the type of constituent
geometry types and attributes such as having Z coordinates.

```text
  Ordered by Code                        Ordered by Type

  shapeNull               =  0           shapeNull               =  0
  shapePoint              =  1           
  shapePolyline           =  3           shapePoint              =  1
  shapePolygon            =  5           shapePointM             = 21
  shapeMultipoint         =  8           shapePointZM            = 11
  shapePointZ             =  9           shapePointZ             =  9
  shapePolylineZ          = 10           
  shapePointZM            = 11           shapeMultipoint         =  8
  shapePolylineZM         = 13           shapeMultipointM        = 28
  shapePolygonZM          = 15           shapeMultipointZM       = 18
  shapeMultipointZM       = 18           shapeMultipointZ        = 20
  shapePolygonZ           = 19           
  shapeMultipointZ        = 20           shapePolyline           =  3
  shapePointM             = 21           shapePolylineZ          = 10
  shapePolylineM          = 23           shapePolylineZM         = 13
  shapePolygonM           = 25           shapePolylineM          = 23
  shapeMultipointM        = 28           
  shapeMultiPatchM        = 31           shapePolygon            =  5
  shapeMultiPatch         = 32           shapePolygonZ           = 19
                                         shapePolygonZM          = 15
  New types having flags elsewhere       shapePolygonM           = 25
                                         
  shapeGeneralPolyline    = 50           shapeMultiPatch         = 32
  shapeGeneralPolygon     = 51           shapeMultiPatchM        = 31
  shapeGeneralPoint       = 52           
  shapeGeneralMultipoint  = 53           and the general shape types
  shapeGeneralMultiPatch  = 54           
```
