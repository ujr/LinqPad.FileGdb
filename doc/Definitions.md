# File GDB Definitions

## Constants

From *FileGDBCore.h* of Esri's File Geodatabase API:

**FieldType** constants:

```C
fieldTypeSmallInteger =  0
fieldTypeInteger      =  1
fieldTypeSingle       =  2
fieldTypeDouble       =  3
fieldTypeString       =  4
fieldTypeDate         =  5
fieldTypeOID          =  6
fieldTypeGeometry     =  7
fieldTypeBlob         =  8
fieldTypeRaster       =  9
fieldTypeGUID         = 10
fieldTypeGlobalID     = 11
fieldTypeXML          = 12
```

**ShapeType** constants:

```C
shapeNull               =  0
shapePoint              =  1
shapePointM             = 21
shapePointZM            = 11
shapePointZ             =  9
shapeMultipoint         =  8
shapeMultipointM        = 28
shapeMultipointZM       = 18
shapeMultipointZ        = 20
shapePolyline           =  3
shapePolylineM          = 23
shapePolylineZM         = 13
shapePolylineZ          = 10
shapePolygon            =  5
shapePolygonM           = 25
shapePolygonZM          = 15
shapePolygonZ           = 19
shapeMultiPatchM        = 31
shapeMultiPatch         = 32
shapeGeneralPolyline    = 50
shapeGeneralPolygon     = 51
shapeGeneralPoint       = 52
shapeGeneralMultipoint  = 53
shapeGeneralMultiPatch  = 54
```

**ShapeModifiers** constants:

```C
shapeHasZs                  = (-2147483647 - 1),  // 80000000
shapeHasMs                  = 1073741824,         // 40000000
shapeHasCurves              = 536870912,          // 20000000
shapeHasIDs                 = 268435456,          // 10000000
shapeHasNormals             = 134217728,          //  8000000
shapeHasTextures            = 67108864,           //  4000000
shapeHasPartIDs             = 33554432,           //  2000000
shapeHasMaterials           = 16777216,           //  1000000
shapeIsCompressed           = 8388608,            //   800000
shapeModifierMask           = -16777216,          // FF000000
shapeMultiPatchModifierMask = 15728640,           //   F00000
shapeBasicTypeMask          = 255,                //       FF
shapeBasicModifierMask      = -1073741824,        // C0000000
shapeNonBasicModifierMask   = 1056964608,         // 3F000000
shapeExtendedModifierMask   = -587202560          // DD000000
```

**GeometryType** constants:

```C
geometryNull       = 0
geometryPoint      = 1
geometryMultipoint = 2
geometryPolyline   = 3
geometryPolygon    = 4
geometryMultiPatch = 9
```

**CurveType** constants:

```C
curveTypeCircularArc = 1
curveTypeBezier      = 4
curveTypeEllipticArc = 5
```
