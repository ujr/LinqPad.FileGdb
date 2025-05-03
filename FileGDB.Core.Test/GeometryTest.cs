using Xunit;

namespace FileGDB.Core.Test;

public class GeometryTest
{
	[Fact]
	public void CanEmptyXY()
	{
		Assert.True(XY.Empty.IsEmpty);
		Assert.True(double.IsNaN(XY.Empty.Magnitude));
	}
}
