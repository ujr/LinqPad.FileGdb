namespace FileGDB.Core;

/// <summary>
/// Geodatabase field types
/// </summary>
/// <remarks>
/// Each table has exactly one field of type ObjectID; its value
/// is the index in the .gdbtablx file and not explicitly stored.
/// </remarks>
public enum FieldType
{
	Int16 = 0, // esriFieldTypeSmallInteger
	Int32 = 1, // esriFieldTypeInteger
	Single = 2, // esriFieldTypeSingle (32bit IEEE754)
	Double = 3, // esriFieldTypeDouble (64bit IEEE754)
	String = 4, // esriFieldTypeString
	DateTime = 5, // esriFieldTypeDate
	ObjectID = 6, // esriFieldTypeOID
	Geometry = 7, // esriFieldTypeGeometry
	Blob = 8, // esriFieldTypeBlob
	Raster = 9, // esriFieldTypeRaster
	GUID = 10, // esriFieldTypeGUID
	GlobalID = 11, // esriFieldTypeGlobalID
	XML = 12, // esriFieldTypeXML
	Int64 = 13, // added with ArcGIS Pro 3.2
	DateOnly = 14, // added with ArcGIS Pro 3.2
	TimeOnly = 15, // added with ArcGIS Pro 3.2
	DateTimeOffset = 16 // added with ArcGIS Pro 3.2
}
