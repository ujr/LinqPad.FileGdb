using System.Linq;
using FileGDB.Core.Geometry;
using Xunit;

namespace FileGDB.Core.Test;

public class CompSumTest
{
	[Fact]
	public void CanCompSum()
	{
		// These values are from the "Kahan summation algorithm"
		// Wikipedia article; the correct sum is 2, but ordinary
		// double precision addition (in given order) yields zero:

		var values = new[] { 1.0, 1e100, 1.0, -1e100 };

		CompSum sum = 0.0;

		foreach (var value in values)
		{
			sum += value;
		}

		double result = sum;
		double plain = values.Sum();

		Assert.Equal(2.0, result); // mathematically correct
		Assert.Equal(0.0, plain); // wrong due to limited precision
	}
}
