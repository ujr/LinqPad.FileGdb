using System;

namespace FileGDB.Core.Shapes;

[Flags]
public enum ShapeFlags
{
	None = 0,
	HasZ = 1,
	HasM = 2,
	HasID = 4
}