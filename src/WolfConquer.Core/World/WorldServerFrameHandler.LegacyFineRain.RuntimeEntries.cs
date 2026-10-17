namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private readonly record struct LegacyFineRainRuntimeEntry(
        int Flag, LegacyFineRainExpiryEntry? Expiry, LegacyFineRain212Invocation? Invocation);

    private static IReadOnlyList<LegacyFineRainRuntimeEntry> CaptureLegacyFineRainRuntimeEntries(
        WorldSession session, IReadOnlyList<LegacyFineRainExpiryEntry> expirations)
    {
        var entries = new List<LegacyFineRainRuntimeEntry>();
        foreach (var flag in new[] { LegacyFineRain1StatusFlag, LegacyFineRain2StatusFlag })
        {
            LegacyFineRainExpiryEntry? expiry = null;
            foreach (var candidate in expirations)
                if (candidate.Flag == flag) expiry = candidate;
            LegacyFineRain212Invocation? invocation = null;
            if (flag == LegacyFineRain1StatusFlag &&
                LegacyFineRain212Invocation.TryRead(session.Character, out var captured))
                invocation = captured;
            if (expiry is not null || invocation is not null)
                entries.Add(new(flag, expiry, invocation));
        }
        return entries;
    }
}
