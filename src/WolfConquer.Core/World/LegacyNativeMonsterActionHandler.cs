using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Native ActionHandler17..24 birth/Rate. Whole targeting/movement lives in partials;
// damage dispatch, HP/death and scheduler adoption remain held on their real graph.
internal partial class LegacyNativeMonsterActionHandler
{
    public LegacyNativeMSRandom Random = new LegacyNativeMSRandom();

    public Random SystemRandom = new Random();

    public bool Rate(int value)
    {
        return value > Random.Next() % 100;
    }
}
