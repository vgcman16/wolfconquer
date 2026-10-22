using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Fact]
    public Task RankingUsesLiveScoresNarrowedPlayerLevelAndRetainsStaleRowsForLowWeaponLevel() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = scope.Ready(Weapon.Blade); scope.Ready(Weapon.Sword, 3, 12001);
        scope.Owner.Unlocked = true; scope.SetLevel(300); scope.Owner.UpdateRank();
        var subtype = LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Dominator].RankingList[17];
        var main = LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList[17];
        Assert.Equal(4000u, subtype.TotalPoints); Assert.Equal(7000u, main.TotalPoints);
        Assert.Equal((byte)44, main.Level); Assert.Equal(LegacyNativeHWRank.Type.Dominator, main.Type);
        Assert.NotSame(subtype, main); Assert.Same(scope.Client.Player.Name, main.Name);
        Assert.Empty(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Judge].RankingList);
        first.Level = 3; scope.Owner.UpdateRank(); Assert.Same(main, LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList[17]);
        scope.Owner.Unlocked = false; scope.Owner.UpdateRank();
        Assert.All(LegacyNativeHWRank.Ranks.Values, rank => Assert.Empty(rank.RankingList)); Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public Task ClearMatchesObservedFrameworkInvalidationAfterFirstSuccessfulRemovalEvenWhenLast(int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Ready(Weapon.Blade);
        scope.Spells.ClientSpells.TryAdd(12000, new LegacyNativeMsgSpell { ID = 12000, Level = 5 });
        if (count == 2)
        {
            scope.Ready(Weapon.Sword, 4, 12001);
            scope.Spells.ClientSpells.TryAdd(12001, new LegacyNativeMsgSpell { ID = 12001, Level = 5 });
        }
        scope.Rank(LegacyNativeHWRank.Type.Dominator); scope.Rank(LegacyNativeHWRank.Type.Main); scope.Rank(LegacyNativeHWRank.Type.Faith);
        Assert.Throws<InvalidOperationException>(() => scope.Owner.ClearWeapons(true));
        Assert.False(scope.Owner.Objects.ContainsKey(Weapon.Blade)); Assert.Equal(count - 1, scope.Owner.Objects.Count);
        Assert.Empty(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Dominator].RankingList);
        Assert.Empty(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList);
        Assert.Single(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Faith].RankingList); // Final global Remove was not reached.
        Assert.Empty(scope.Queue); Assert.Equal(16, scope.Available); // Optional Info was not reached either.
    });

    [Fact]
    public Task EmptyClearCompletesWhileMismatchedPhysicalKeyDoesNotRemoveTheEnumeratedEntry() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Rank(LegacyNativeHWRank.Type.Faith);
        Assert.True(scope.Owner.ClearWeapons(true)); Assert.Equal(Hex("0800300908021011"), Assert.Single(scope.Queue));
        Assert.Empty(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Faith].RankingList);
        var row = scope.Item(Weapon.Blade, 1, Weapon.Sword); scope.Rank(LegacyNativeHWRank.Type.Faith);
        Assert.True(scope.Owner.ClearWeapons()); Assert.Same(row, Assert.Single(scope.Owner.Objects).Value);
        Assert.Empty(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Faith].RankingList); Assert.Single(scope.Queue);
    });

    [Fact]
    public Task RemoveMetadataFaultWinsBeforeClearEnumeratorInvalidationOrAnyDeletion() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = scope.Item(Weapon.Blade, 4); scope.Rank(LegacyNativeHWRank.Type.Main);
        Assert.Throws<NullReferenceException>(() => scope.Owner.RemoveWeapon(Weapon.Blade));
        Assert.Same(row, Assert.Single(scope.Owner.Objects).Value);
        Assert.Throws<NullReferenceException>(() => scope.Owner.ClearWeapons(true));
        Assert.Same(row, Assert.Single(scope.Owner.Objects).Value);
        Assert.Single(LegacyNativeHWRank.Ranks[LegacyNativeHWRank.Type.Main].RankingList); Assert.Empty(scope.Queue);
    });
}
