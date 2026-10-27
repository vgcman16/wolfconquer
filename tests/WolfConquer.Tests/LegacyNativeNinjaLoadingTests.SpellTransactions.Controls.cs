using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData("remove")]
    [InlineData("reborn")]
    [InlineData("clear")]
    public Task ValidNoFrameSpellMutationsDoNotRequireCallerStorage(string operation) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (operation != "clear") scope.Spell(1000, 9);
        var before = new LoadingTransactionWitness(scope);
        if (operation == "remove") scope.Spells.Remove(2000, null!);
        if (operation == "reborn") Assert.False(scope.Spells.RebornSpell(null!, 2000));
        if (operation == "clear") scope.Spells.ClearSpells([], null!);
        before.RequireUnchanged();
    });

    [Theory]
    [InlineData((ushort)1105)]
    [InlineData((ushort)4000)]
    [InlineData((ushort)4070)]
    public Task ExcludedAddRequiresNoUnusedOwnerMapOrFrame(ushort id) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Spells.ClientSpells = null!;
        scope.Client.MySpells = null;
        scope.Client.Player.UID = 0;
        var before = new LoadingTransactionWitness(scope);
        scope.Spells.Add(null!, id, 300);
        before.RequireUnchanged();
    });

    [Fact]
    public Task ClearPreparesTheWholeRealSequenceAndPreservesEscapedRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Spell(1000, 9).SoulLevel = 2;
        scope.Spell(1001, 7);
        var old = scope.Spells.ClientSpells;
        var escaped = new LegacyNativeSpellMutationWitness(scope.Spells);
        var order = old.ToArray().Select(pair => pair.Key).ToArray();
        scope.Spells.ClearSpells([1000], scope.Packet);
        Assert.NotSame(old, scope.Spells.ClientSpells);
        escaped.RequireEscapedMapUnchanged();
        var retained = Assert.Single(scope.Spells.ClientSpells);
        Assert.Equal((ushort)1000, retained.Key);
        Assert.NotSame(old[1000], retained.Value);
        Assert.Equal((byte)9, retained.Value.PreviousLevel);
        Assert.Equal((byte)2, retained.Value.SoulLevel);
        Assert.Equal((ushort)0, retained.Value.Level);
        Assert.Equal(0, retained.Value.Experience);
        // Header / exp / ID+unknown / level+type / UseSoul / Soul / hash.
        var added = Convert.FromHexString("19003508" + "00000000" + "E8030000" +
            "00000000" + "00000000" + "00" + "12000000");
        scope.Sent(order.Select(id => id == 1000 ? added : RemoveFrame(17, id)).ToArray());
    });

    [Fact]
    public Task RebornExcludedExistingSkillRemovesItWithoutAnUnusedLevelCastOrFrame() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Spell(1105, 300);
        var oldMap = scope.Spells.ClientSpells;
        var escaped = new LegacyNativeSpellMutationWitness(scope.Spells);
        var packet = new LegacyNativePacketMutationWitness(scope.Packet);
        Assert.True(scope.Spells.RebornSpell(null!, 1105));
        Assert.NotSame(oldMap, scope.Spells.ClientSpells);
        Assert.Empty(scope.Spells.ClientSpells);
        escaped.RequireEscapedMapUnchanged();
        packet.RequireUnchanged();
        Assert.Empty(scope.Queue);
    });
}
