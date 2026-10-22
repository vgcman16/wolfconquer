using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Native monster ActionHandler17..24 birth and Rate only. Monster AI is not ported here.
internal partial class LegacyNativeMonsterActionHandler
{
    public LegacyNativeMSRandom Random = new LegacyNativeMSRandom();

    public Random SystemRandom = new Random();

    public bool Rate(int value)
    {
        return value > Random.Next() % 100;
    }
}
