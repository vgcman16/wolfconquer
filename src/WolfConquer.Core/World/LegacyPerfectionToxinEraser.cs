namespace WolfConquer.Core.World;

internal static class LegacyPerfectionToxinEraser
{
    internal static double FromPrestigeLevel(uint value)
    {
        // Native CreatePrestigePoints passes unchecked (int)PrestigeLevel to PerfectionEffect.Update.
        var level = unchecked((int)value);
        return level switch
        {
            > 600 => 200D, > 299 => 100D, > 259 => 90D, > 229 => 80D,
            > 199 => 70D, > 184 => 60D, > 144 => 50D, > 109 => 45D,
            > 84 => 40D, > 44 => 35D, > 0 => 30D, _ => 0D,
        };
    }
}
