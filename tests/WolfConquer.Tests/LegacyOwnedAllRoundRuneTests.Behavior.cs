using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAllRoundRuneTests
{
    [Theory]
    [InlineData(1, 300U, 3000U, 500U, 0)]
    [InlineData(16, 1100U, 4500U, 1300U, 1)]
    [InlineData(27, 3000U, 10000U, 5000U, 1)]
    public Task ActualOwnedStatusAndCombatProjectionUseExactNonlinearAllRoundValues(int level, uint attack, uint defence, uint magicDefence, ushort stack) =>
        Run(async f =>
        {
            Assert.Equal(stack, Assert.Single(f.Current().PlayerItems.RuneItems).StackSize);
            Assert.Equal(stack, Assert.Single(f.Current().NativeGameClient!.OrdinarySpellBinding!.Items.Rune.Objects, row => row.Uid == RuneUid).StackSize);
            var saved = Saved(f); AssertDelta(f, attack, defence, magicDefence);
            var runtime = CombatFields(f); var first = await Query(f); var second = await Query(f);
            Assert.Equal(first.Fields, second.Fields);
            Assert.Equal(runtime[LegacyPlayerStatusPacket.MinAttackIndex], first.MinAttack);
            Assert.Equal(runtime[LegacyPlayerStatusPacket.MaxAttackIndex], first.MaxAttack);
            Assert.Equal(runtime[LegacyPlayerStatusPacket.DefenceIndex], first.Defence);
            Assert.Equal(runtime[LegacyPlayerStatusPacket.MagicDefenceIndex], first.MagicDefence);
            Assert.Equal(100U + attack, DirectDamage(f, OwnedEquipmentBehaviorFixture.Actor, OwnedEquipmentBehaviorFixture.Peer));
            Assert.Equal(1U, DirectDamage(f, OwnedEquipmentBehaviorFixture.Peer, OwnedEquipmentBehaviorFixture.Actor));
            Assert.Equal(0U, first.MDefence); AssertSaved(f, saved);
        }, level: level, peer: true, stack: stack);

    [Fact]
    public Task ActualEquipAndUnequipPublishOnlyTheExistingSixAndFiveFrameSequencesWithoutAPhantomSpell() => Run(async f =>
    {
        var before = Saved(f); AssertDelta(f, 0, 0, 0);
        var equip = new MsgItemUsagePacket(RuneUid, 102, MsgItemUsageAction.EquipRune, 112233, 44, 55, 66);
        f.ActorFrames.Clear(); await f.DispatchAsync(equip);
        Assert.Equal(6, f.ActorFrames.Count);
        Assert.Equal(new MsgItemUsagePacket(RuneUid, 212, MsgItemUsageAction.UnequipRune, 0, 0, 0, 0).ToFrameBytes(), f.ActorFrames[0]);
        Assert.Equal(equip.ToFrameBytes(), f.ActorFrames[1]);
        Assert.Equal((ushort)102, Assert.Single(f.Current().PlayerItems.RuneItems).Position);
        AssertDelta(f, 300, 3000, 500);
        Assert.Equal(before.Single(pair => pair.Key.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) && pair.Key.Contains(f.Data.Spells)).Value,
            File.ReadAllBytes(Path.Combine(f.Data.Spells, OwnedEquipmentBehaviorFixture.Actor + ".bin")));
        var afterEquip = Saved(f);
        var unequip = new MsgItemUsagePacket(RuneUid, 102, MsgItemUsageAction.UnequipRune, 112244, 77, 88, 99);
        f.ActorFrames.Clear(); await f.DispatchAsync(unequip);
        Assert.Equal(5, f.ActorFrames.Count); Assert.Equal(unequip.ToFrameBytes(), f.ActorFrames[0]);
        AssertDelta(f, 0, 0, 0); Assert.Equal((ushort)212, Assert.Single(f.Current().PlayerItems.RuneItems).Position);
        Assert.Equal(before[f.ItemPath()], File.ReadAllBytes(f.ItemPath()));
        Assert.Equal(before[f.Data.State], File.ReadAllBytes(f.Data.State));
        Assert.Equal(afterEquip[f.CharacterPath()], File.ReadAllBytes(f.CharacterPath()));
        AssertOwner(f);
    }, position: 212);

    [Fact]
    public Task ActualAlternatePublicationSelectsOneRuneSetAndFallsBackToTheNormalShield() => Run(async f =>
    {
        var before = Saved(f); AssertDelta(f, 300, 3000, 500);
        await f.ToggleAsync(); Assert.True(f.Current().AlternateEquipmentActive);
        Assert.True(f.Current().NativeGameClient!.Equipment!.Alternante);
        AssertDelta(f, 3000, 10000, 5000);
        var packet = await Query(f); Assert.Equal(3100U, packet.MinAttack);
        await f.DispatchAsync(new MsgItemUsagePacket(0, 0, MsgItemUsageAction.UnAlternante, 0, 0, 0, 0));
        Assert.False(f.Current().AlternateEquipmentActive);
        AssertDelta(f, 300, 3000, 500); AssertSaved(f, before);
    }, alternateRune: true);

    [Fact]
    public Task ActualAlternateShieldAndRuneBecomeEligibleTogetherAndNormalRuneFallsBack() => Run(async f =>
    {
        var before = Saved(f); AssertDelta(f, 0, 0, 0);
        await f.ToggleAsync(); Assert.True(f.Current().AlternateEquipmentActive);
        AssertDelta(f, 300, 3000, 500); _ = await Query(f); AssertSaved(f, before);
    }, shield: "alternate");

    [Fact]
    public Task ActualScalarDeathRemovesThePassiveWithoutChangingItemsSpellsOrUidLedger() => Run(async f =>
    {
        AssertDelta(f, 300, 3000, 500); var before = Saved(f); var current = f.Current();
        var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["MinHitPoints"] = "0" };
        f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), current with
        { Character = current.Character with { MinHitPoints = 0, Attributes = attributes } });
        Assert.Equal(0L, f.Current().Character.MinHitPoints); Assert.Equal(0, f.Current().NativeGameClient!.Player!.HitPoints);
        AssertDelta(f, 0, 0, 0); _ = await Query(f);
        foreach (var pair in before.Where(pair => pair.Key != f.CharacterPath())) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        AssertOwner(f);
    });

    [Fact]
    public Task ActualOwnedShieldUnequipRetiresTheBonusThroughTheExistingItemWriterAndQuery() => Run(async f =>
    {
        var before = Saved(f); var shield = f.Current().PlayerItems.EquipmentItems.Single(row => row.Uid == ShieldUid);
        AssertDelta(f, 300, 3000, 500); f.ActorFrames.Clear();
        await f.DispatchAsync(new MsgItemUsagePacket(ShieldUid, 5, MsgItemUsageAction.Unequip, 0, 0, 0, 0));
        Assert.True(f.ActorFrames.Count >= 2, f.Log.ToString());
        Assert.Equal(new MsgItemUsagePacket(ShieldUid, 5, MsgItemUsageAction.Unequip, 0, 0, 0, 0).ToFrameBytes(), f.ActorFrames[0]);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(shield.WithPosition(0)).ToFrameBytes(), f.ActorFrames[1]);
        Assert.Contains(f.Current().PlayerItems.InventoryItems, row => row.Uid == ShieldUid && row.Position == 0);
        Assert.DoesNotContain(f.Current().PlayerItems.EquipmentItems, row => row.Uid == ShieldUid);
        AssertDelta(f, 0, 0, 0); _ = await Query(f);
        Assert.Equal(before[f.Data.State], File.ReadAllBytes(f.Data.State));
        foreach (var pair in before.Where(pair => pair.Key.Contains(f.Data.Spells))) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        AssertOwner(f);
    });

    [Theory]
    [InlineData("missing", 2000, 2, 140)]
    [InlineData("broken", 2000, 2, 140)]
    [InlineData("normal", 6000, 2, 140)]
    [InlineData("normal", 2000, 1, 140)]
    [InlineData("normal", 2000, 2, 14)]
    public Task ActualOwnedIneligibleStateCannotReceiveThePassive(string shield, long job, long reborn, long level) => Run(async f =>
    { var saved = Saved(f); AssertDelta(f, 0, 0, 0); _ = await Query(f); AssertSaved(f, saved); },
        job: job, reborn: reborn, characterLevel: level, shield: shield);

    [Theory]
    [InlineData(211)]
    [InlineData(212)]
    [InlineData(122)]
    public Task ActualOwnedStoredOrInactiveRuneCannotReceiveThePassive(ushort position) => Run(async f =>
    { var saved = Saved(f); AssertDelta(f, 0, 0, 0); _ = await Query(f); AssertSaved(f, saved); }, position: position);
}
