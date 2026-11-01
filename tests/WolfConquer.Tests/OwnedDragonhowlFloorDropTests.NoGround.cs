using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFloorDropTests
{
    [Theory]
    [InlineData((ushort)17_490, false, false)]
    [InlineData((ushort)17_490, true, false)]
    [InlineData((ushort)17_650, false, false)]
    [InlineData((ushort)17_650, true, false)]
    [InlineData((ushort)17_490, false, true)]
    [InlineData((ushort)17_490, true, true)]
    [InlineData((ushort)17_650, false, true)]
    [InlineData((ushort)17_650, true, true)]
    public Task RealKilledPhaseNoGroundConsumesNoMoneyUidButOneSelectedItemUid(
        ushort spellId, bool money, bool pinnedCapacity) => Run(spellId, money, walkable: true, async f =>
    {
        var pins = new List<FloorItemRuntimeStateStore.PickupReservation>();
        var seeds = new List<FloorItemRuntimeEntry>();
        try
        {
            if (pinnedCapacity)
            {
                // Fill the actual Frame-owned default4096 store through its
                // existing APIs, then hold real pickups. No replaced store,
                // invented owner, private capacity change or fake reservation.
                uint seedStart = unchecked(FloorUid() + 65_536U);
                for (int i = 0; i < FloorItemRuntimeStateStore.DefaultCapacity; i++)
                {
                    var entry = f.Seed(unchecked(seedStart + (uint)i), (ushort)(i % 64), (ushort)(i / 64), f.Clock.GetUtcNow());
                    Assert.Equal(FloorItemRuntimePickupStatus.Available, f.Floor.TryReservePickup(entry, Actor,
                        false, f.Clock.GetUtcNow(), out var pin));
                    pins.Add(Assert.IsType<FloorItemRuntimeStateStore.PickupReservation>(pin)); seeds.Add(entry);
                }
                Assert.Equal(4_096, pins.Count);
            }
            else
            {
                seeds.AddRange(f.SeedNormalDropNeighborhood());
                Assert.Equal(9, seeds.Count);
            }
            var files = ExpectedFiles(f, reward: true);
            uint beforeUid = FloorUid(); var beforeStamp = f.Binding.Player.AttackStamp;
            await f.Attack();
            AssertCompleted(f);
            Assert.NotEqual(beforeStamp, f.Binding.Player.AttackStamp);
            Assert.Equal(unchecked(beforeUid + (money ? 0U : 1U)), FloorUid());
            Assert.Empty(FloorPackets(f));
            var retained = f.Floor.SnapshotVisible(f.Clock.GetUtcNow());
            Assert.Equal(seeds.Count, retained.Count);
            foreach (var seed in seeds)
            {
                Assert.Contains(retained, actual => ReferenceEquals(seed, actual));
                Assert.Equal(pinnedCapacity ? FloorItemRuntimePickupStatus.Busy : FloorItemRuntimePickupStatus.Available,
                    f.Floor.GetPickupStatus(seed.Packet.Uid, Actor, f.Clock.GetUtcNow(), out var actual));
                Assert.Same(seed, actual);
            }
            if (!pinnedCapacity)
            {
                Assert.DoesNotContain(retained, entry => entry.Packet.X == 1 && entry.Packet.Y == 1);
                Assert.Equal(9, retained.Count);
            }
            f.Owner.Data.AssertFiles(files);
        }
        finally { foreach (var pin in pins) pin.Dispose(); }
    });

    [Theory]
    [InlineData((ushort)17_490)]
    [InlineData((ushort)17_650)]
    public Task RealGroundedMoneyPublishesFourExactEntriesBeforeCpsAndFinalAnimation(ushort spellId) =>
        Run(spellId, money: true, walkable: true, async f =>
    {
        var files = ExpectedFiles(f, reward: true);
        uint beforeUid = FloorUid();
        await f.Attack();
        AssertCompleted(f);
        Assert.Equal(unchecked(beforeUid + 4U), FloorUid());
        var frames = f.Owner.ActorFrames.Where(bytes => LegacyFloorItemPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)).ToArray();
        Assert.Equal(4, frames.Length);
        // Each actual attempt starts at its own predecessor's placed cell.
        (ushort X, ushort Y)[] expected = [(11, 10), (10, 9), (9, 8), (8, 7)];
        for (int i = 0; i < expected.Length; i++)
        {
            var (x, y) = expected[i];
            uint uid = unchecked(beforeUid + (uint)i + 1U);
            Assert.Equal(FloorGolden(uid, Money, x, y), frames[i]);
            Assert.True(f.Floor.TryGetVisibleEntry(uid, f.Clock.GetUtcNow(), out var actual));
            Assert.Equal(FloorItemRuntimePickupKind.Money, actual.Kind); Assert.Equal(50L, actual.Amount);
            Assert.Equal(Map, actual.MapId); Assert.Equal(77U, actual.DynamicId);
            Assert.True(actual.OwnerOnly); Assert.Null(actual.Item); Assert.Null(actual.Effect);
        }
        Assert.Equal(4, f.Floor.SnapshotVisible(f.Clock.GetUtcNow()).Count);
        int death = f.Owner.ActorFrames.FindIndex(bytes => MsgAttackPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Query.AttackType == MsgAttackType.Death);
        int reward = f.Owner.ActorFrames.FindIndex(bytes => IsCps(bytes, 639_778_909));
        Assert.True(f.Owner.ActorFrames.IndexOf(frames[0]) > death);
        Assert.True(f.Owner.ActorFrames.IndexOf(frames[^1]) < reward);
        f.Owner.Data.AssertFiles(files);
    });
}
