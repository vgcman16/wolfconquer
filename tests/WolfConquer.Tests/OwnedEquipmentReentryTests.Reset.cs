using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
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
        var clock = new ReentryClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearOptionsAsync(inputs.Actor, inputs.World, options, async f =>
        {
            AssertAdmittedDragonhowlInputs(f, Actor, 17_490);
            Assert.Equal(100_000, f.Current(Actor).Character.MinHitPoints);
            Assert.Equal(100_000, f.Current(Target).Character.MinHitPoints);
            f.Log.WriteLine("Original scenario: " + scenario);
            var operationStart = f.ActorFrames.Count;
            var start = operationStart;
            await f.SendAsync(Actor, Attack(Actor, Target));
            var first = Assert.Single(Animation(f.ActorFrames.Skip(start), 17_490, f.Log.ToString()).Targets);
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
            // The real owner must reject the still-early request without a hit or debit.
            var targetBeforeEarly = f.Current(Target).Character.MinHitPoints;
            var manaBeforeEarly = f.Current(Actor).Character.MinMana;
            var staminaBeforeEarly = f.Current(Actor).Character.Attributes["Stamina"];
            var actorFileBeforeEarly = File.ReadAllBytes(f.CharacterPath(Actor));
            var targetFileBeforeEarly = File.ReadAllBytes(f.CharacterPath(Target));
            var earlyStart = f.ActorFrames.Count;
            var priorLog = f.Log.ToString();
            await f.SendAsync(Actor, Attack(Actor, Target));
            var earlyLog = f.Log.ToString();
            Assert.StartsWith(priorLog, earlyLog, StringComparison.Ordinal);
            Assert.DoesNotContain("runtime-exception", earlyLog[priorLog.Length..], StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WorldServer refused owned Dragonhowl physical reason=cooldown.", earlyLog[priorLog.Length..], StringComparison.Ordinal);
            Assert.DoesNotContain(f.ActorFrames.Skip(earlyStart), bytes =>
                LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) &&
                animation.SpellId == 17_490);
            Assert.Equal(targetBeforeEarly, f.Current(Target).Character.MinHitPoints);
            Assert.Equal(manaBeforeEarly, f.Current(Actor).Character.MinMana);
            Assert.Equal(staminaBeforeEarly, f.Current(Actor).Character.Attributes["Stamina"]);
            Assert.Equal(actorFileBeforeEarly, File.ReadAllBytes(f.CharacterPath(Actor)));
            Assert.Equal(targetFileBeforeEarly, File.ReadAllBytes(f.CharacterPath(Target)));

            // The genuine prior physical hit owns a 300–700 ms attack interval.
            // Advance only after the real equip and its saved-item assertions.
            clock.Advance(TimeSpan.FromMilliseconds(700));
            start = f.ActorFrames.Count;
            await f.SendAsync(Actor, Attack(Actor, Target));
            var second = Assert.Single(Animation(f.ActorFrames.Skip(start), 17_490, f.Log.ToString()).Targets);
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
    private sealed class ReentryClock : TimeProvider
    {
        private long ticks;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(Interlocked.Read(ref ticks));
        internal void Advance(TimeSpan amount) => Interlocked.Add(ref ticks, amount.Ticks);
    }
}
