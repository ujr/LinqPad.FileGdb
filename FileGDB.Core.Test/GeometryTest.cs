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

	[Fact]
	public void CanEnvelope()
	{
		var env = new Envelope();

		Assert.True(env.IsEmpty);

		Assert.False(env.Contains(0, 0));
		Assert.False(env.Contains(double.NaN, double.NaN));

		env.Expand(0, 0);
		Assert.False(env.IsEmpty);
		Assert.True(env.Contains(0, 0));

		env.Expand(2, -1);
		Assert.False(env.IsEmpty);
		Assert.True(env.Contains(2, -1));
		Assert.False(env.Contains(2, -1.0000001));
	}
}
