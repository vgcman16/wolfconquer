using WolfConquer.Core.World;
using Job = WolfConquer.Core.World.LegacyBossHarmRankPackageJob;

namespace WolfConquer.Tests;

public sealed partial class LegacyBossHarmRankPackageTargetTests
{
    private static readonly (long First, long Last, Job Job)[] TargetRewardFamilies =
    [
        (1000, 1057, Job.Trojan), (2000, 2057, Job.Warrior),
        (4000, 4057, Job.Archer), (5000, 5057, Job.Ninja), (7000, 7057, Job.Pirate),
    ];

    public static IEnumerable<object[]> ExtendedRewardStages()
    {
        foreach (var family in TargetRewardFamilies)
        for (long job = family.Last - 5; job <= family.Last; job++) yield return [job, family.Job];
    }

    public static IEnumerable<object[]> UniversalOnlyClasses()
    {
        foreach (long value in new long[] { long.MinValue, -1, 0, uint.MaxValue, long.MaxValue, 10000, 10001, 10002 }
                     .Concat(TargetRewardFamilies.SelectMany(family => new[] { family.First - 1, family.Last + 1 }))
                     .Concat(PrestigeTargetClassFixture.Families.Where(family => family.Category is 5 or 7 or 8 or 9 or 10 or 11)
                         .SelectMany(family => new long[] { family.First, family.Last }))
                     .Concat(PrestigeTargetClassFixture.ObservedUnmodeled.Select(value => (long)value)).Distinct())
            yield return [value];
    }

    private static LegacyBossHarmRankPackageItem Item(uint id, uint quantity) => new(id, 0, quantity, false, 0);

    private static LegacyBossHarmRankPackageItem[] RankOneUniversal() =>
    [
        Item(3_324_226, 1), Item(4_050_001, 200), Item(4_040_001, 200),
        Item(3_009_003, 5), Item(3_316_793, 10), Item(3_330_770, 10),
    ];

    private static LegacyBossHarmRankPackageItem[] RankOneExtra(Job job) => job switch
    {
        Job.Trojan => [Item(3_321_098, 200), Item(3_321_107, 200)],
        Job.Warrior => [Item(3_333_403, 5)],
        Job.Archer => [Item(3_335_128, 10)],
        Job.Ninja or Job.Pirate or Job.Other => [],
        _ => throw new ArgumentOutOfRangeException(nameof(job)),
    };

    private static (uint Ninja, uint PiratePoints, uint PirateExp) RankOnePoints(Job job) =>
        (job == Job.Ninja ? 50u : 0u, job == Job.Pirate ? 50u : 0u, job == Job.Pirate ? 50u : 0u);
}
