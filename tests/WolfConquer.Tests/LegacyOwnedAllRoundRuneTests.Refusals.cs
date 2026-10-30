using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAllRoundRuneTests
{
    [Fact]
    public Task ActualSavedNonstackableAllRoundWithQuantityTwoCannotPublishThePassive() => Run(async f =>
    {
        var saved = Saved(f); AssertOwner(f);
        Assert.Equal((ushort)2, Assert.Single(f.Current().PlayerItems.RuneItems).StackSize);
        Assert.Equal((ushort)2, Assert.Single(f.Current().NativeGameClient!.OrdinarySpellBinding!.Items.Rune.Objects, row => row.Uid == RuneUid).StackSize);
        f.ActorFrames.Clear();
        await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
        Assert.Empty(f.ActorFrames); AssertSaved(f, saved);
    }, stack: 2);

    [Fact]
    public Task ActualDuplicateEquippedSlotWithAnotherRuneCannotBypassTheCompleteNativeEquippedOwner() => Run(async f =>
    {
        var saved = Saved(f); AssertOwner(f);
        Assert.Empty(f.Current().NativeGameClient!.Rune!.WornObjects);
        Assert.Equal(2, f.Current().PlayerItems.RuneItems.Count(row => row.Position == 102));
        f.ActorFrames.Clear();
        await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
        Assert.Empty(f.ActorFrames); AssertSaved(f, saved);
    }, duplicateBlue: true);

    [Theory]
    [InlineData(0, "4020202")]
    [InlineData(ItemType.RequiredClassFieldIndex, "6000")]
    [InlineData(ItemType.RequiredLevelFieldIndex, "1")]
    [InlineData(ItemType.RuneSpellIdFieldIndex, "0")]
    [InlineData(ItemType.StackSizeFieldIndex, "1")]
    [InlineData(ItemType.RequiredClassFieldIndex, "bad")]
    public Task ActualAdoptedMetadataDriftCannotPublishAStatusReply(int index, string replacement) => Run(async f =>
    {
        var saved = Saved(f); var session = f.Current(); var native = session.NativeGameClient!;
        var catalog = native.OrdinarySpellBinding!.ItemCatalog!;
        var row = Assert.Single(catalog.ItemsById[4_020_201]); var fields = Assert.IsType<string[]>(row.Fields);
        var old = fields[index]; f.ActorFrames.Clear();
        try
        {
            fields[index] = replacement;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
            Assert.Empty(f.ActorFrames); Assert.Same(session, f.Current()); Assert.Same(native, f.Current().NativeGameClient);
            foreach (var pair in saved) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }
        finally { fields[index] = old; }
        AssertSaved(f, saved); AssertDelta(f, 300, 3000, 500);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualMissingOrAmbiguousAdoptedIndexCannotPublishThePassive(bool duplicate) => Run(async f =>
    {
        var saved = Saved(f); var catalog = f.Current().NativeGameClient!.OrdinarySpellBinding!.ItemCatalog!;
        var index = Assert.IsType<Dictionary<int, IReadOnlyList<ItemType>>>(catalog.ItemsById);
        var old = index[4_020_201]; f.ActorFrames.Clear();
        try
        {
            if (duplicate) index[4_020_201] = new[] { old[0], old[0] }; else Assert.True(index.Remove(4_020_201));
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
            Assert.Empty(f.ActorFrames);
            foreach (var pair in saved) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        }
        finally { index[4_020_201] = old; }
        AssertSaved(f, saved);
    });

    [Fact]
    public Task OmittedOrOtherPureProjectionProfilesRetainExistingResultsAndCannotIssueAnOwner() => Run(f =>
    {
        var current = f.Current(); var catalog = current.NativeGameClient!.OrdinarySpellBinding!.ItemCatalog;
        var baseline = LegacyPlayerStatusCalculator.CalculateFields(current.Character, current.PlayerItems, catalog, 1, 0,
            current.CombatGear, current.AlternateEquipmentActive, current.PlayerSpells);
        var other = LegacyPlayerStatusCalculator.CalculateFields(current.Character, current.PlayerItems, catalog, 1, 0,
            current.CombatGear, current.AlternateEquipmentActive, current.PlayerSpells, ConquerProtocolProfile.RootD11_7899);
        Assert.Equal(baseline, other); AssertDelta(f, 300, 3000, 500); AssertOwner(f);
        return Task.CompletedTask;
    });
}
