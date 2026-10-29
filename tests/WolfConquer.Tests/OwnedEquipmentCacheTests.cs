using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedEquipmentCacheTests
{
    [Fact]
    public Task HandlerEquipmentQueryOwnsEffectCachesAndRetainsAbsentNecklaceEffect() => Run(async fixture =>
    {
        await QueryAsync(fixture);
        var earned = LegacyPhysicalStatusOverlay.ForOwner(fixture.Current());
        Assert.Equal(202U, earned.RightWeaponItemEffect);
        Assert.Equal(201U, earned.RingItemEffect);
        Assert.Equal(203U, earned.NecklaceItemEffect);
        Assert.Equal(450U, earned.MaxMana);
        await UnequipAsync(fixture, 4);
        Assert.Equal(0U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Current()).RightWeaponItemEffect);
        Assert.Equal(420U, (await ReadStatusAsync(fixture)).MaxMana);
        await UnequipAsync(fixture, 6);
        Assert.Equal(0U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Current()).RingItemEffect);
        Assert.Equal(380U, (await ReadStatusAsync(fixture)).MaxMana);
        await UnequipAsync(fixture, 2);
        Assert.Equal(203U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Current()).NecklaceItemEffect);
        Assert.Equal(330U, (await ReadStatusAsync(fixture)).MaxMana);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(fixture.Current()));
        var serializedOverlay = json.RootElement.GetProperty("PhysicalStatusOverlay");
        Assert.False(serializedOverlay.TryGetProperty("RightWeaponItemEffect", out _));
        Assert.False(serializedOverlay.TryGetProperty("RingItemEffect", out _));
        Assert.False(serializedOverlay.TryGetProperty("NecklaceItemEffect", out _));
        Assert.False(serializedOverlay.TryGetProperty("MaxMana", out _));
        var foreign = fixture.Current() with { Character = fixture.Current().Character with { Uid = OwnedEquipmentBehaviorFixture.Actor + 1 } };
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(foreign).MaxMana);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(foreign).NecklaceItemEffect);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HandlerEquipmentQueryManaAndEffectsResetOnlyWhenFineRainAdmitsUnequipQuery(bool rejected) => Run(async fixture =>
    {
        await QueryAsync(fixture);
        if (rejected)
        {
            await fixture.SendAsync(OwnedEquipmentBehaviorFixture.Actor,
                new MsgAttackPacket(new MsgAttackQuery(Uid: OwnedEquipmentBehaviorFixture.Actor,
                    OpponentUid: OwnedEquipmentBehaviorFixture.Actor, X: 830, Y: 584, AttackType: MsgAttackType.Magic,
                    Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellId: 14_500)).ToFrameBytes());
            Assert.True(fixture.Current().Character.Attributes.TryGetValue("StatusFlags", out var flags), fixture.Log.ToString());
            var words = flags.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.True(words.Length > 0xD4 / 32 && (ulong.Parse(words[0xD4 / 32], CultureInfo.InvariantCulture) & (1UL << (0xD4 % 32))) != 0,
                fixture.Log.ToString());
        }
        await UnequipAsync(fixture, 4);
        Assert.DoesNotContain(fixture.Current().PlayerItems.EquipmentItems, item => item.Position == 4);
        var current = LegacyPhysicalStatusOverlay.ForOwner(fixture.Current());
        Assert.Equal(rejected ? 202U : 0U, current.RightWeaponItemEffect);
        Assert.Equal(rejected ? 450U : 420U, current.MaxMana);
        Assert.Equal(rejected ? 450U : 420U, (await ReadStatusAsync(fixture)).MaxMana);
    });

    private static Task Run(Func<OwnedEquipmentBehaviorFixture, Task> exercise)
    {
        var inputs = LegacyMagicAttackRuntimeTests.CreateOwnedEquipmentCacheInputs();
        var clock = new FrozenCacheTimeProvider();
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(inputs.Input, inputs.World,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
                LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000)), exercise);
    }

    private sealed class FrozenCacheTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private static Task QueryAsync(OwnedEquipmentBehaviorFixture fixture) => fixture.SendAsync(OwnedEquipmentBehaviorFixture.Actor,
        new MsgActionPacket { ObjId = OwnedEquipmentBehaviorFixture.Actor, Type = (MsgActionType)251 }.ToFrameBytes());

    private static async Task<LegacyPlayerStatusPacket> ReadStatusAsync(OwnedEquipmentBehaviorFixture fixture)
    {
        var start = fixture.ActorFrames.Count;
        await fixture.SendAsync(OwnedEquipmentBehaviorFixture.Actor,
            new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
        var bytes = Assert.Single(fixture.ActorFrames.Skip(start),
            frame => LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(frame), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var status));
        return status;
    }

    private static async Task UnequipAsync(OwnedEquipmentBehaviorFixture fixture, ushort slot)
    {
        var row = Assert.Single(fixture.Current().PlayerItems.EquipmentItems, item => item.Position == slot);
        var before = File.ReadAllBytes(fixture.ItemPath());
        var expected = before.ToArray();
        var count = BinaryPrimitives.ReadInt32LittleEndian(expected);
        var matches = 0;
        for (var i = 0; i < count; i++)
        {
            var offset = 4 + i * PlayerItemSnapshotLoader.LegacyDbItemSize;
            Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset + 136, 4)));
            if (BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset, 4)) != row.Uid) continue;
            matches++;
            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset + 12, 2), 0);
        }
        Assert.Equal(1, matches);
        var registry = File.ReadAllBytes(fixture.Data.State);
        var observed = false;
        var start = fixture.ActorFrames.Count;
        fixture.BeforeSend = (uid, _) =>
        {
            if (observed || uid != OwnedEquipmentBehaviorFixture.Actor) return;
            observed = true;
            Assert.Equal(expected, File.ReadAllBytes(fixture.ItemPath()));
            Assert.False(LegacyTradeJournal.HasPending(fixture.Data.Users));
            Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            Assert.False(Monitor.IsEntered(LegacySecondaryPasswordRuntime.SyncRoot));
            Assert.DoesNotContain(fixture.Current().PlayerItems.EquipmentItems, item => item.Uid == row.Uid);
            Assert.Contains(fixture.Current().PlayerItems.InventoryItems, item => item.Uid == row.Uid && item.Position == 0);
        };
        await fixture.DispatchAsync(new MsgItemUsagePacket(row.Uid, slot, MsgItemUsageAction.Unequip, 0, 0, 0, 0));
        fixture.BeforeSend = null;
        Assert.True(observed, fixture.Log.ToString());
        Assert.Equal(new MsgItemUsagePacket(row.Uid, slot, MsgItemUsageAction.Unequip, 0, 0, 0, 0).ToFrameBytes(), fixture.ActorFrames[start]);
        Assert.Equal(LegacyGameItemPacket.FromSnapshotItem(row.WithPosition(0)).ToFrameBytes(), fixture.ActorFrames[start + 1]);
        Assert.Equal(expected, File.ReadAllBytes(fixture.ItemPath()));
        Assert.Equal(registry, File.ReadAllBytes(fixture.Data.State));
    }
}
