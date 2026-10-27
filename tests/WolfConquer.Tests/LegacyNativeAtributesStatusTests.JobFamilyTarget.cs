using Table = WolfConquer.Core.World.LegacyNativeAtributesStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAtributesStatusTests
{
    public static IEnumerable<object[]> TargetStatFamilyStages()
    {
        foreach (var family in PrestigeTargetClassFixture.Families)
        {
            uint rowKey = family.First switch
            {
                13002 or 14002 => 10000,
                _ => family.First + 5,
            };
            for (uint job = family.Last - 5; job <= family.Last; job++) yield return [job, rowKey];
        }
    }

    [Theory]
    [MemberData(nameof(TargetStatFamilyStages))]
    public void TargetStages52Through57UseTheActualFamilyRowForResetCheckAndGetStatus(uint job, uint rowKey)
    {
        var selected = Row(20);
        var fallback = Row(80);
        var table = new Table { [rowKey] = new() { [7] = selected }, [0] = new() { [7] = fallback } };
        var player = Player(job);
        var owner = player.Owner;
        Sentinel(player);

        table.ResetStatsNonReborn(player);

        AssertStats(selected, player);
        Assert.True(table.CheckStatus(player));
        player.Strength--;
        var below = Stats(player);
        Assert.False(table.CheckStatus(player));
        Assert.Equal(below, Stats(player)); // A query may not repair the insufficient stat.
        Sentinel(player);
        table.GetStatus(player);
        AssertStats(selected, player);
        Assert.Equal(job, player.Class);
        Assert.Equal((ushort)7, player.Level);
        Assert.Same(owner, player.Owner);
        Assert.Same(player, owner.Player);
        Assert.Same(selected, table[rowKey][7]);
        Assert.Same(fallback, table[0][7]);
    }

    [Theory]
    [InlineData(1058u)] [InlineData(2058u)] [InlineData(4058u)] [InlineData(5058u)]
    [InlineData(6058u)] [InlineData(7058u)] [InlineData(8058u)] [InlineData(9058u)]
    [InlineData(14058u)] [InlineData(16058u)] [InlineData(0u)] [InlineData(uint.MaxValue)]
    public void OutsideTargetFamiliesKeepTheExistingKeyZeroStatFallback(uint job)
    {
        var fallback = Row(80);
        var table = new Table { [0] = new() { [7] = fallback } };
        var player = Player(job);

        table.ResetStatsNonReborn(player);

        AssertStats(fallback, player);
        Assert.True(table.CheckStatus(player));
        Sentinel(player);
        table.GetStatus(player);
        AssertStats(fallback, player);
        Assert.Equal(job, player.Class);
    }
}
