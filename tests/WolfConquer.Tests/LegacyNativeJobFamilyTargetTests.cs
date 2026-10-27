using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeJobFamilyTargetTests
{
    private static readonly (uint Category, Func<uint, bool> Contains)[] SpecificFamilies =
    [
        (1, LegacyNativeAtributesStatus.IsTrojan), (2, LegacyNativeAtributesStatus.IsWarrior),
        (3, LegacyNativeAtributesStatus.IsArcher), (4, LegacyNativeAtributesStatus.IsNinja),
        (5, LegacyNativeAtributesStatus.IsMonk), (6, LegacyNativeAtributesStatus.IsPirate),
        (7, LegacyNativeAtributesStatus.IsLee), (8, LegacyNativeAtributesStatus.IsWater),
        (9, LegacyNativeAtributesStatus.IsFire), (10, LegacyNativeAtributesStatus.IsWindWalker),
        (11, LegacyNativeAtributesStatus.IsThunderStriker),
    ];

    [Fact]
    public void EveryKnownTargetIdKeepsItsObservedSpecificFamilyWithoutCrossFamilyMembership()
    {
        // Independently observed numeric target fixture:634 IDs in11 existing
        // families. No localized labels/assets or newer suffix58 are embedded.
        foreach (var family in PrestigeTargetClassFixture.Families)
        for (uint job = family.First; job <= family.Last; job++)
        foreach (var predicate in SpecificFamilies)
            Assert.Equal(predicate.Category == family.Category, predicate.Contains(job));
    }

    [Fact]
    public void Adjacent58AndObservedUnmodeledJobsDoNotAcquireASpecificKnownFamily()
    {
        var jobs = PrestigeTargetClassFixture.Families.Select(family => family.Last + 1)
            .Concat(PrestigeTargetClassFixture.ObservedUnmodeled)
            .Concat(PrestigeTargetClassFixture.WorldOnly).Append(0u).Append(uint.MaxValue).Distinct();
        foreach (uint job in jobs)
        foreach (var predicate in SpecificFamilies)
            Assert.False(predicate.Contains(job));
    }

    [Fact]
    public void BroadTaoistCompatibilityDoesNotCreateWaterOrFireMembershipForIntermediateJobs()
    {
        foreach (uint job in new uint[] { 10000, 10001, 10002, 11002, 11005, 12002, 12005, 13000, 13001, 13500, 14000, 14001 })
        {
            Assert.True(LegacyNativeAtributesStatus.IsTaoist(job));
            Assert.False(LegacyNativeAtributesStatus.IsWater(job));
            Assert.False(LegacyNativeAtributesStatus.IsFire(job));
        }
        Assert.False(LegacyNativeAtributesStatus.IsTaoist(14058));
        Assert.False(LegacyNativeAtributesStatus.IsTaoist(15002));
        Assert.False(LegacyNativeAtributesStatus.IsTaoist(19000));
    }
}
