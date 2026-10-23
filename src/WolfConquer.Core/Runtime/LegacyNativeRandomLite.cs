using System;

namespace WolfConquer.Core.Runtime;

// Range arithmetic is widened; the underlying 31-bit draw domain and modulo
// distribution are unchanged. This is not a uniform full-width sampler.
internal class LegacyNativeRandomLite
{
	private const uint Y = 842502087u;

	private const uint Z = 3579807591u;

	private const uint W = 273326509u;

	private volatile uint x;

	private volatile uint y;

	private volatile uint z;

	private volatile uint w;

	public LegacyNativeRandomLite()
		: this(Environment.TickCount)
	{
	}

	public LegacyNativeRandomLite(int seed)
	{
		Reseed(seed);
	}

	public void Reseed(int seed)
	{
		x = (uint)seed;
		y = 842502087u;
		z = 3579807591u;
		w = 273326509u;
	}

	public int Next()
	{
		uint num = x ^ (x << 11);
		x = y;
		y = z;
		z = w;
		w = w ^ (w >> 19) ^ (num ^ (num >> 8));
		uint num2 = w & 0x7FFFFFFF;
		if (num2 == int.MaxValue)
		{
			return Next();
		}
		return (int)num2;
	}

	public int Next(int upperBound)
	{
		if (upperBound < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(upperBound), upperBound, "upperBound must be >=0");
		}
		if (upperBound == 0)
		{
			return 0;
		}
		// Widen before adding: Int32.MaxValue has inclusive width 2147483648.
		long width = (long)upperBound + 1;
		return (int)(Next() % width);
	}

	public int Next(int lowerBound, int upperBound)
	{
		if (lowerBound > upperBound)
		{
			throw new ArgumentOutOfRangeException(nameof(upperBound), upperBound, "upperBound must be >=lowerBound");
		}
		if (lowerBound == upperBound)
		{
			return lowerBound;
		}
		// Every ordered Int32 interval has width 1..4294967296. Keep both
		// width and offset arithmetic wide; this preserves the real Next() draw.
		long width = ((long)upperBound - lowerBound) + 1;
		return (int)(lowerBound + (Next() % width));
	}
}
