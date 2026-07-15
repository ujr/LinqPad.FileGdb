using FileGDB.Core.Geometry;
using Xunit;

namespace FileGDB.Core.Test;

public class BoundingBoxTest
{
	[Fact]
	public void CanEmpty()
	{
		var box = new BoundingBox();

		Assert.True(box.IsEmpty);

		Assert.Equal(0.0, box.Width);
		Assert.Equal(0.0, box.Height);

		Assert.False(box.Contains(0, 0));
		Assert.False(box.Contains(double.NaN, double.NaN));
	}
	
	[Fact]
	public void CanAdd()
	{
		var box = new BoundingBox();

		box.Add(double.NaN, double.NaN);
		Assert.True(box.IsEmpty);

		box.Add(0, 0);
		Assert.False(box.IsEmpty);
		Assert.True(box.Contains(0, 0));
		Assert.False(box.Contains(1, 1));
		Assert.Equal(0.0, box.Width);
		Assert.Equal(0.0, box.Height);

		box.Add(2, -1);
		Assert.True(box.Contains(0, 0));
		Assert.True(box.Contains(2, -1));
		Assert.False(box.Contains(2.0, -1.000000001));
		Assert.False(box.Contains(2.000000001, 0));
		Assert.Equal(2.0, box.Width);
		Assert.Equal(1.0, box.Height);

		// Adding NaN (an empty point) shall not change the bounding box:
		box.Add(double.NaN, double.NaN);
		Assert.Equal(0.0, box.XMin);
		Assert.Equal(-1.0, box.YMin);
		Assert.Equal(2.0, box.XMax);
		Assert.Equal(0.0, box.YMax);
	}

	[Fact]
	public void CanContains()
	{
		var empty = new BoundingBox();
		Assert.True(empty.IsEmpty);
		Assert.False(empty.Contains(10, 10));

		var pt = new BoundingBox(10, 10);
		Assert.True(pt.Contains(10, 10));

		var box = new BoundingBox(10, 10, 20, 20);
		Assert.True(box.Contains(15, 15)); // inside
		Assert.True(box.Contains(10, 15)); // left boundary
		Assert.True(box.Contains(15, 10)); // bottom boundary
		Assert.True(box.Contains(20, 20)); // top-right boundary
		Assert.False(box.Contains(9.999999999, 9.999999999)); // just outside bottom-left
		Assert.False(box.Contains(20.000000001, 20.000000001)); // just outside top-right

		// The empty set is a subset of any set, therefore
		// any bounding box contains an empty bounding box:
		Assert.True(pt.Contains(empty));
		Assert.True(box.Contains(empty));
		Assert.True(empty.Contains(empty));

		// But the overload taking just two coordinates does
		// not consider NaN to be contained in any bounding box:
		Assert.False(pt.Contains(double.NaN, double.NaN));
		Assert.False(box.Contains(double.NaN, double.NaN));
		Assert.False(empty.Contains(double.NaN, double.NaN));
	}

	[Fact]
	public void CanIntersects()
	{
		var a = new BoundingBox(10, 10, 20, 20);
		var b = new BoundingBox(15, 15, 25, 25);
		var c = new BoundingBox(0, 0, 5, 5);
		var empty = new BoundingBox();

		Assert.True(a.Intersects(b));
		Assert.True(b.Intersects(a));
		Assert.False(b.Intersects(c));
		Assert.False(c.Intersects(b));

		Assert.False(a.Intersects(empty));
		Assert.False(empty.Intersects(a));
	}

	[Fact]
	public void CanZ()
	{
		var box = new BoundingBox();

		Assert.True(double.IsNaN(box.ZMin));
		Assert.True(double.IsNaN(box.ZMax));

		box.AddZ(1.0);
		box.AddZ(0.8);
		box.AddZ(1.3);
		box.AddZ(double.NaN);

		Assert.Equal(0.8, box.ZMin);
		Assert.Equal(1.3, box.ZMax);

		// Z does not affect emptiness:
		Assert.True(box.IsEmpty);
	}
}
