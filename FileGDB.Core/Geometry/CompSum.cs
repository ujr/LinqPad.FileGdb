using System;
using System.Globalization;

namespace FileGDB.Core.Geometry;

public struct CompSum
{
	private double _sum;
	private double _compensation;

	private CompSum(double value = 0.0)
	{
		_sum = value;
		_compensation = 0.0;
	}

	private double Result => _sum + _compensation;

	private void Add(double value)
	{
		// Kahan summation, improved version by Neumaier, see
		// https://en.wikipedia.org/wiki/Kahan_summation_algorithm
		// (mathematically, _compensation is always zero, but due
		// to limited precision, it preserves lost low-order bits)

		double t = _sum + value;

		if (Math.Abs(_sum) >= Math.Abs(value))
		{
			// sum > value, compensate for lost low-order bits of current input:
			_compensation += _sum - t + value;
		}
		else
		{
			// value > sum, compensate for lost low-order bits of sum:
			_compensation += value - t + _sum;
		}

		_sum = t;
	}

	public static implicit operator double(CompSum s) => s.Result;
	public static implicit operator CompSum(double v) => new(v);

	public static CompSum operator +(CompSum s, double v)
	{
		s.Add(v);
		return s;
	}

	public static CompSum operator -(CompSum s, double v)
	{
		s.Add(-v);
		return s;
	}

	public override string ToString()
	{
		return Result.ToString(CultureInfo.InvariantCulture);
	}
}
