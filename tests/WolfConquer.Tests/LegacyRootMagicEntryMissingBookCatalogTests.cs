using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootMagicEntryManual18220CatalogFallbackCannotReplaceLearnedBookBeforeStamp(bool mounted)
    {
        await using var fixture = new RootMagicEntryFixture(selectedId: 18_220, coldTime: 1_500,
            catalogArrowWithoutLearnedBook: true);
        Assert.Equal(4_005, fixture.Owner.Character.Class);
        Assert.DoesNotContain(fixture.Owner.PlayerSpells.Spells, spell => spell.Id == 18_220);
        var equipped = Assert.Single(fixture.Owner.CombatGear!.Items);
        Assert.Equal((401U, 1U, 1U), (equipped.ItemID, equipped.Level, equipped.Equip));
        var sourceGear = Assert.IsType<CombatGear>(fixture.SourceArrowCatalog);
        Assert.Equal((401, 1), (sourceGear.GearTypeId, sourceGear.Level));
        Assert.Equal(new[] { 18_220_000 }, sourceGear.MagicTypeIds);
        Assert.Null(fixture.SelectedEffect); // A passive denial must not hide the missing-book boundary.
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        if (mounted) await fixture.MountAndWaitAsync(); // Real7001, not a seeded Ride bit or stamp.
        var hadStamp = fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var beforeStamp);
        var peerHp = fixture.Peer.Character.MinHitPoints;
        var start = fixture.Order.Count;

        await fixture.CastAsync(18_220);

        Assert.Equal(hadStamp, fixture.Owner.Character.Attributes.TryGetValue("LegacyAttackStampUtc", out var afterStamp));
        Assert.Equal(beforeStamp, afterStamp); // Preserve the actual mount-produced stamp, if any.
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.Equal(1_000, fixture.Owner.Character.MinMana);
        Assert.Equal(5_000, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(peerHp, fixture.Peer.Character.MinHitPoints);
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 0x32));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 332));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 333));
        Assert.True(fixture.Owner.MagicCooldowns is null || !fixture.Owner.MagicCooldowns.ContainsKey(18_220));
        foreach (var recipient in new[] { PlayerUid, TargetPlayerUid })
        {
            var frames = fixture.FramesSince(recipient, start);
            Assert.Empty(RootMagicEntryCosts(frames));
            Assert.Empty(ParseSpellAnimations(frames));
            Assert.Empty(ParseMagicColdTimePackets(frames));
            var vectors = ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).ToArray();
            Assert.Equal(mounted ? 1 : 0, vectors.Length);
            if (mounted)
                Assert.Contains(Assert.Single(vectors).Updates, entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } bits &&
                    !IsLegacyStatusFlagSet(bits, 0x32)); // Native Ride removal precedes the learned-book lookup.
        }
    }

    private static CombatGear CreateRootMissingArrowSourceGear() => new(
        Id: 401, GearTypeId: 401, Level: 1, UpExperience: 0,
        Life: 0, Attack: 0, Defense: 0, UnknownStat0: 0, MagicDefense: 0,
        MagicTypeIds: [18_220_000], Score: 0, Unknown0: 0, Unknown1: 0, Unknown2: 0,
        SourceFile: "fixture://root-missing-book/combat_gear", LineNumber: 1, RawLine: "", Fields: []);
}
