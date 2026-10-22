using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Fact]
    public Task OrdinaryAlternateUsesSlotsOneThroughEightKeepsExistingLevelsNarrowsNewSpellsAndRemovesStaleSkills() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var existing = scope.Spell(16310, 99);
            scope.Spell(16330);
            var unrelated = scope.Spell(16600);
            var first = scope.Item(999, 100, 258, 1);
            scope.Meta(7, 100, 258, magic1: 16310, magic2: 70000);
            scope.Item(1, 200, 3, 8);
            scope.Meta(8, 200, 3, magic1: 16320, magic2: 16310);
            scope.Item(2, 300, 1, 0); scope.Item(3, 400, 1, 9); scope.Item(4, 500, 1, 17);
            scope.Ninja.Alternate();
            scope.Sent(SpellFrame(4464, 2), SpellFrame(16320, 3), RemoveFrame(17, 16330));
            Assert.Same(existing, scope.Spells.ClientSpells[16310]);
            Assert.Equal((ushort)99, existing.Level); Assert.Equal(123, existing.Experience);
            Assert.Same(unrelated, scope.Spells.ClientSpells[16600]);
            Assert.False(scope.Spells.ClientSpells.ContainsKey(16330));
            Assert.Equal((ushort)2, scope.Spells.ClientSpells[4464].Level);
            Assert.Equal(0, scope.Spells.ClientSpells[4464].Experience);
            Assert.Equal(4, scope.Spells.ClientSpells.Count);
            Assert.Equal(new uint[] { 100, 258, 1, 0, 0 }, new[] { first.ItemID, first.Level, first.Position, first.Exp, first.UK });
            scope.Serialized(0, RemoveFrame(17, 16330), untouchedTail: false);
            Assert.Equal(11, scope.Available); Assert.Empty(scope.Log);
        });

    [Fact]
    public Task AlternateEquipmentPrioritizesNineThroughSixteenThenPairedFallbackAndDeduplicatesActualSkills() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Equipment!.Alternante = true;
            scope.Item(1, 100, 1, 1); scope.Meta(1, 100, 1, magic1: 16550, magic2: 16540);
            scope.Item(2, 200, 4, 9); scope.Meta(2, 200, 4, magic1: 16310, magic2: 16320);
            scope.Item(3, 300, 5, 2); scope.Meta(3, 300, 5, magic1: 16330, magic2: 16340);
            scope.Item(4, 400, 1, 8); scope.Meta(4, 400, 1, magic1: 16530, magic2: 16520);
            scope.Item(5, 500, 6, 16); scope.Meta(5, 500, 6, magic1: 16310, magic2: 16350);
            scope.Ninja.Alternate();
            scope.Sent(SpellFrame(16310, 4), SpellFrame(16320, 4), SpellFrame(16330, 5),
                SpellFrame(16340, 5), SpellFrame(16350, 6));
            Assert.Equal(new ushort[] { 16310, 16320, 16330, 16340, 16350 }, scope.Spells.ClientSpells.Keys.Order());
            Assert.Equal((ushort)4, scope.Spells.ClientSpells[16310].Level);
            scope.Serialized(0, SpellFrame(16350, 6)); Assert.Equal(11, scope.Available);
        });

    [Fact]
    public Task EmptyEquipmentRemovesAllTwentyFiveNativeSkillIdsInListOrderAndKeepsOtherSpells() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            foreach (ushort id in NinjaSkills) scope.Spell(id);
            var unrelated = scope.Spell(16560);
            scope.Ninja.Alternate();
            scope.Sent(NinjaSkills.Select(id => RemoveFrame(17, id)).ToArray());
            Assert.Same(unrelated, Assert.Single(scope.Spells.ClientSpells).Value);
            scope.Serialized(0, RemoveFrame(17, 16550)); Assert.Equal(11, scope.Available);
        });

    [Fact]
    public Task UnmatchedMetadataStillAddsTheNativeZeroIdSpellWithNarrowedLevelOnce() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Item(1, 100, 300, 1);
            scope.Ninja.Alternate();
            scope.Sent(SpellFrame(0, 44));
            Assert.Equal((ushort)0, Assert.Single(scope.Spells.ClientSpells).Key);
            Assert.Equal((ushort)44, scope.Spells.ClientSpells[0].Level);
            scope.Serialized(0, SpellFrame(0, 44)); Assert.Equal(11, scope.Available);
        });

    [Fact]
    public Task LaterMetadataFaultKeepsEarlierRealSpellAddsAndDisposesTheSameReusedPacket() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Item(1, 100, 2, 1); scope.Meta(1, 100, 2, magic1: 16310, magic2: 16320);
            scope.Item(2, 200, 3, 2);
            scope.Metadata.Add(2, null!); // First DBItem matches before null; later lookup reaches it.
            Assert.Throws<NullReferenceException>(() => scope.Ninja.Alternate());
            scope.Sent(SpellFrame(16310, 2), SpellFrame(16320, 2));
            Assert.Equal(2, scope.Spells.ClientSpells.Count);
            scope.Serialized(0, SpellFrame(16320, 2)); Assert.Equal(11, scope.Available);
        });

    [Theory]
    [InlineData("equipment")]
    [InlineData("items")]
    [InlineData("spells")]
    public Task AlternateTakesThenDisposesBeforeEachNativeMissingOwnerFault(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            if (missing == "equipment") scope.Client.Equipment = null;
            if (missing == "items") scope.Ninja.Items = null!;
            if (missing == "spells") scope.Client.MySpells = null;
            Assert.Throws<NullReferenceException>(() => scope.Ninja.Alternate());
            Assert.Empty(scope.Queue); Assert.Empty(scope.Spells.ClientSpells);
            scope.UnserializedDisposed(0); Assert.Equal(11, scope.Available);
        });
}
