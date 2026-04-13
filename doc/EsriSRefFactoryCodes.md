# Esri Spatial Reference Factory Codes (WKID)

About the **FactoryCode** and **WKID** (well-known ID)
of `SpatialReference` objects in Esri software:

- Esri Spatial Reference objects have an integer ID
- in ArcObjects this was called the `FactoryCode`
- in ArcGIS Pro this is called `Wkid` and `LatestWkid`
- an **ID < 32767** corresponds to the EPSG ID
- an **ID ≥ 32767** is Esri-defined; if such an object is later added
  to the EPSG dataset, Esri will update the WKID to match the one
  assigned by EPSG, but the previous value will still work (my
  guess: the `Wkid` remains, `LatestWkid` becomes the new value).
- an **ID = 0** means that ArcGIS does not recognize the spatial
  reference (but it will still work)

This information is from a January 2012 answer on gis.stackexchange.com
by user [mkennedy](https://gis.stackexchange.com/users/963/mkennedy):
<https://gis.stackexchange.com/questions/18651/do-arcgis-spatialreference-object-factory-codes-correspond-with-epsg-numbers>

> If an Esri well-known ID is below 32767, it corresponds to the EPSG ID.
> WKIDs that are 32767 or above are Esri-defined. Either the object isn't
> in the EPSG Geodetic Parameter Dataset (<https://epsg.org/>) yet, or it
> probably won't be added. If an object is later added to the EPSG Dataset,
> Esri will update the WKID to match the EPSG one, but the previous value
> will still work.
>
> There are some limitations. Esri doesn't follow the axes directions that
> EPSG does, in ArcGIS Desktop at least, it's always longitude-latitude or
> easting-northing (xy), although we're picking up the axes order in Server
> now.
>
> I'm intimately familiar with this as I'm the product engineer that handles these for Esri.

Another answer to the same question cites from the ArcObjects reference,
and this text is still in the Enterprise SDK reference:
<https://developers.arcgis.com/enterprise-sdk/api-reference/net/IGeometryServer/>

> AuthorityName is usually "EPSG" or "ESRI", but can also be an
> arbitrary string. It can also be the empty string if you want the
> default authority name associated with the new spatial reference.
> Clients can associate their own authority names with factory codes
> that are currently associated with the EPSG or ESRI authority names,
> because only the WKID is used to create the spatial reference.
> Here are the current rules for mapping WKID ranges to default
> authority names:
>
> - A WKID in the EPSG code range (1000 – 32768) will result in an
>   AUTHORITY name of “EPSG”, and the version will be the current
>   EPSG version used (currently “6.12”).
> - A WKID in the ESRI code range (33000 – 199999) will result in
>   an AUTHORITY name of “ESRI”, and the version will be the current
>   PE library version (currently “9.3”).
> - A WKID in the user (objedit) range (200000 – 209199) will result
>   in an AUTHORITY name of “CUSTOM”, with no version associated with
>   it. This name is specified by the OGC.

Note that this is not entirely consistent with the first answer.
But the **upshot is** that ArcGIS **uses only the WKID** (aka
Factory Code) to identify a spatial reference, and the **Authority
is derived** from the WKID (and probably not really used within
ArcGIS). This differs from QGIS.
