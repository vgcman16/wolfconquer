using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task CurrentOwnedGetLevelKeepsAlternatePriorityAndPairedFallback() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var ordinary = scope.Item(100, 100, 100, 1);
        var alternate = scope.Item(200, 200, 5, 9);
        var fallback = scope.Item(300, 300, 4, 2);
        scope.Client.Equipment!.Alternante = true;
        scope.Ninja.GetLevel();
        Assert.Equal((byte)1, scope.Client.Player.NiniaP0); // Selected5 + fallback4, not ordinary100.
        scope.Sent(LevelFrame(17, 1));
        Assert.Same(ordinary, scope.Ninja.Items[100]);
        Assert.Same(alternate, scope.Ninja.Items[200]);
        Assert.Same(fallback, scope.Ninja.Items[300]);
    });

    [Fact]
    public Task ExplicitRealZeroMagicRetainsZeroWithoutInventingMetadata() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Item(100, 100, 9, 1);
        scope.Meta(17, 100, 9, magic1: 0, magic2: 0);
        var oldMap = new LegacyNativeSpellMutationWitness(scope.Spells);
        scope.Ninja.Alternate();
        oldMap.RequireEscapedMapUnchanged();
        var row = Assert.Single(scope.Spells.ClientSpells);
        Assert.Equal((ushort)0, row.Key); Assert.Equal((ushort)0, row.Value.ID);
        Assert.Equal((ushort)9, row.Value.Level);
        scope.Sent(SpellFrame(0, 9));
    });

    [Fact]
    public Task ExistingSkillsKeepTheirPhysicalRowsEvenWhenSelectedItemLevelNeedsNoNewByteCast() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Item(100, 100, 258, 1);
        scope.Meta(17, 100, 258, magic1: 16310, magic2: 16320);
        var first = scope.Spell(16310, 99);
        var second = scope.Spell(16320, 88);
        var oldMap = new LegacyNativeSpellMutationWitness(scope.Spells);
        scope.Ninja.Alternate();
        oldMap.RequireEscapedMapUnchanged();
        Assert.Same(first, scope.Spells.ClientSpells[16310]);
        Assert.Same(second, scope.Spells.ClientSpells[16320]);
        Assert.Equal((ushort)99, first.Level); Assert.Equal((ushort)88, second.Level);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task HighestRepresentableNewSkillLevelKeepsItsIndependentFrame() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Item(100, 100, 255, 1);
        scope.Meta(17, 100, 255, magic1: 16310, magic2: 16320);
        scope.Ninja.Alternate();
        scope.Sent(SpellFrame(16310, 255), SpellFrame(16320, 255));
        Assert.Equal((ushort)255, scope.Spells.ClientSpells[16310].Level);
        Assert.Equal((ushort)255, scope.Spells.ClientSpells[16320].Level);
    });

    [Fact]
    public Task BadgeOnlyPreparationDoesNotRequireUnusedSpellMetadataOrRanks() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Item(100, 100, 9, 1);
        scope.Client.MySpells = null;
        LegacyNativeNinjaFile.gouyu_type = null!;
        LegacyNativeNinjaRank.Ranks = null!;
        scope.Ninja.GetLevel();
        Assert.Equal((byte)1, scope.Client.Player.NiniaP0);
        scope.Sent(LevelFrame(17, 1));
    });
}
