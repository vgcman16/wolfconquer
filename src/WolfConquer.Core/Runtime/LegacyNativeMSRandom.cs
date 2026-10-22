namespace WolfConquer.Core.Runtime;

// Complete native System.MSRandom: its seed is independent of Pool.GetRandom.
internal class LegacyNativeMSRandom
{
    public const int RAND_MAX = 32767;

    private uint Seed = 1u;

    public LegacyNativeMSRandom()
    {
        Seed = 1u;
    }

    public LegacyNativeMSRandom(uint seed)
    {
        Seed = seed;
    }

    public int Next()
    {
        return (int)(((Seed = unchecked(Seed * 214013 + 2531011)) >> 16) & 0x7FFF);
    }
}
