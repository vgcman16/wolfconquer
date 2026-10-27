namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    // Call under SynRoot. This admits actual present owners without requiring a
    // canonical twelve-bucket graph, so global removal can share the same scan.
    private static PrestigeRankSource[] CapturePrestigeRefreshRegistry()
    {
        var ranks = Ranks;
        if (ranks is null)
        {
            throw new InvalidOperationException("Prestige refresh requires a ranking graph.");
        }

        var sources = new List<PrestigeRankSource>(ranks.Count);
        var categories = new HashSet<Type>();
        var classUids = new HashSet<uint>();
        // Never GetValues: its cached array can omit actual current owners.
        foreach (var pair in ranks)
        {
            if ((uint)pair.Key > (uint)Type.Thunderstriker || !categories.Add(pair.Key))
            {
                throw new InvalidOperationException("Prestige refresh found an invalid ranking bucket key.");
            }

            var source = CapturePrestigeRankSource(pair.Value);
            if (source.Category != pair.Key)
            {
                throw new InvalidOperationException("Prestige refresh found a mismatched ranking owner.");
            }

            if (source.Category != Type.World)
            {
                foreach (var row in source.Rows)
                {
                    if (!classUids.Add(row.Key))
                    {
                        throw new InvalidOperationException("Prestige refresh found a duplicate UID across class rankings.");
                    }
                }
            }

            sources.Add(source);
        }

        return sources.ToArray();
    }
}
