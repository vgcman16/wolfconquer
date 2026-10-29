using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedEquipmentReentryTests
{
    [Theory]
    [InlineData("front-equipment-reset", false, 582u, 450u, 66u, 60u)]
    [InlineData("impregnability-equipment-reset", true, 291u, 291u, 75u, 75u)]
    public Task DirectOverlayPhysicalRunesRetainMutableStatusUntilActualEquipmentRebuild(
        string scenario, bool impregnation, uint firstDamage, uint secondDamage, uint firstStatus, uint secondStatus)
    {
        var inputs = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(impregnation, 17_490, callback: false);
        return OwnedEquipmentBehaviorFixture.RunAsync(inputs.Actor, inputs.World, async f =>
        {
            f.Log.WriteLine("Original scenario: " + scenario);
            var operationStart = f.ActorFrames.Count;
            var start = operationStart;
            await f.SendAsync(Actor, Attack(Actor, Target));
            var first = Assert.Single(Animation(f.ActorFrames.Skip(start), 17_490).Targets);
            Assert.Equal(Target, first.Uid); Assert.True(first.Hit); Assert.Equal(firstDamage, first.Damage);
            Assert.Equal(MsgAttackEffect.Penetration, first.Effect);
            Assert.Equal(firstStatus, impregnation ? (await Status(f, Target)).Counteraction : (await Status(f, Actor)).Breakthrough);
            var equipUid = impregnation ? Target : Actor;
            var gearUid = impregnation ? 91_002u : 90_002u;
            await f.DispatchAsync(new MsgItemUsagePacket(gearUid, 1, MsgItemUsageAction.Equip, 0, 0, 0, 0), equipUid);
            Assert.Equal(gearUid, Assert.Single(f.Current(equipUid).PlayerItems.EquipmentItems).Uid);
            Assert.Contains(f.Current(equipUid).PlayerItems.InventoryItems, row => row.Uid == gearUid - 1);
            Assert.Contains(new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(equipUid).EquipmentItems, row => row.Uid == gearUid && row.Position == 1);
            if (!impregnation) Assert.True(Flag(f.Current(Actor), 313));
            start = f.ActorFrames.Count;
            await f.SendAsync(Actor, Attack(Actor, Target));
            var second = Assert.Single(Animation(f.ActorFrames.Skip(start), 17_490).Targets);
            Assert.Equal(Target, second.Uid); Assert.True(second.Hit); Assert.Equal(secondDamage, second.Damage);
            Assert.Equal(impregnation ? MsgAttackEffect.Penetration : MsgAttackEffect.None, second.Effect);
            Assert.Equal(secondStatus, impregnation ? (await Status(f, Target)).Counteraction : (await Status(f, Actor)).Breakthrough);
            Assert.Equal(100_000 - (long)firstDamage - secondDamage, f.Current(Target).Character.MinHitPoints);
            Assert.Equal(500, f.Current(Actor).Character.MinMana);
            Assert.Equal("100", f.Current(Actor).Character.Attributes["Stamina"]);
            Assert.Equal(0, f.Current(Actor).Character.Experience);
            Assert.Equal(0, Assert.Single(f.Current(Actor).PlayerSpells.Spells).Experience);
            Assert.DoesNotContain(f.ActorFrames.Skip(operationStart), bytes => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var attack) && attack.Query.AttackType == MsgAttackType.Physical);
            Assert.DoesNotContain(f.ActorFrames.Skip(operationStart), bytes => LegacyMsgRefineEffectPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
        }, inputs.Target);
    }
}
