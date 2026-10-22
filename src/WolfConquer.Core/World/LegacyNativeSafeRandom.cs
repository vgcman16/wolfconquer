namespace WolfConquer.Core.World;

// Native default Random() on net48 means Random(Environment.TickCount).
// Explicit-seeded base Random on the reviewed net9 BCL selects the legacy-compatible algorithm.
public class LegacyNativeSafeRandom
{
    private Random _random;
    private readonly Func<int> _nativeDefaultTickCount;
    public object SyncRoot;

    public LegacyNativeSafeRandom(int seed = 0)
        : this(seed, static () => Environment.TickCount)
    {
    }

    // Deterministic clock seam ONLY; production always uses the public Environment.TickCount path.
    // This supplies time, not a forced random result, stored seed or private PRNG-state oracle.
    internal LegacyNativeSafeRandom(int seed, Func<int> nativeDefaultTickCount)
    {
        ArgumentNullException.ThrowIfNull(nativeDefaultTickCount);
        SyncRoot = new object();
        _nativeDefaultTickCount = nativeDefaultTickCount;
        if (seed != 0)
            _random = new Random(seed);
        else
            // TickCount==0 is an EXPLICIT Random seed0, not another SafeRandom-default request.
            _random = new Random(_nativeDefaultTickCount());
    }

    public int Next(int minValue, int maxValue)
    {
        lock (SyncRoot)
            return _random.Next(minValue, maxValue);
    }

    public int Next(int maxValue)
    {
        lock (SyncRoot)
            return _random.Next(maxValue);
    }

    public int Next()
    {
        lock (SyncRoot)
            return _random.Next();
    }

    // Ignore caller seed exactly as native. Sample the actual default-clock seed INSIDE this lock,
    // at replacement construction, never at method entry/before a blocked lock.
    public void SetSeed(int seed)
    {
        lock (SyncRoot)
            _random = new Random(_nativeDefaultTickCount());
    }
}
