using System;

namespace FileGDB.Core;

public class CompensatedSummation
{
	private double _sum;
	private double _compensation;

	public CompensatedSummation() => Reset();

	public double Result => _sum + _compensation;

	public void Add(double value)
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

	public void Reset(double startValue = 0.0)
	{
		_sum = startValue;
		_compensation = 0.0;
	}
}
