using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// The actual handler counter is shared by unrelated floor producers. Isolate
// these observations from other test collections; never reset or replace it.
[CollectionDefinition("DeferredMonsterMoneyFloorUid", DisableParallelization = true)]
public sealed class DeferredMonsterMoneyFloorUidCollection { }

[Collection("DeferredMonsterMoneyFloorUid")]
public sealed partial class LegacyMonsterDeathDeferredMoneyTests
{
    private static readonly FieldInfo FloorUidCounterField =
        typeof(WorldPacketHandlers).GetField("s_legacyFloorItemUid", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The actual shared floor UID counter is required.");

    [Fact]
    public async Task ActualMoneyHandlerBlockedTerrainConsumesNeitherAmountNorFloorUid()
    {
        using var fixture = new Fixture(49, [50_000], ground: static (_, _) => false);
        var before = ReadFloorUidCounter();

        await InvokeActualDropBodyAsync(fixture);

        AssertUidDelta(before, 0);
        Assert.Equal(0, fixture.Attributes.MoneyReads);
        Assert.True(fixture.Cells.Reads > 0);
        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ActualMoneyHandlerPinnedCapacityConsumesNeitherAmountNorFloorUid()
    {
        var floor = new FloorItemRuntimeStateStore(capacity: 1);
        using var fixture = new Fixture(49, [50_000], floor: floor);
        var packet = LegacyFloorItemPacket.FromMonsterDrop(1_090_010, 31, 31,
            fixture.Session.Character, 80_000_002);
        var seed = FloorItemRuntimeEntry.ForMoney(1002, packet, 125, ownerOnly: true);
        floor.AddVisible(seed);
        Assert.Equal(FloorItemRuntimePickupStatus.Available,
            floor.TryReservePickup(seed, PlayerUid, false, DateTimeOffset.UtcNow, out var reservation));
        using var pinned = Assert.IsType<FloorItemRuntimeStateStore.PickupReservation>(reservation);
        var before = ReadFloorUidCounter();

        await InvokeActualDropBodyAsync(fixture);

        AssertUidDelta(before, 0);
        Assert.Equal(0, fixture.Attributes.MoneyReads);
        Assert.Equal(0, fixture.Cells.Reads);
        Assert.Empty(FloorPackets(fixture));
        Assert.Same(seed, Assert.Single(floor.SnapshotVisible(DateTimeOffset.UtcNow)));
    }

    [Fact]
    public async Task ActualMoneyHandlerFailedInitialSearchConsumesUidOnlyForLaterPositiveTile()
    {
        using var fixture = new Fixture(49, [6_000], ground: static (x, y) => x == 1 && y == 1);
        var before = ReadFloorUidCounter();
        fixture.Attributes.BeforeMoneyRead = call =>
        {
            Assert.Equal(1, call);
            AssertUidDelta(before, 0); // This valid payload has not allocated its UID yet.
            Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        };

        await InvokeActualDropBodyAsync(fixture);

        AssertUidDelta(before, 1);
        Assert.Equal(1, fixture.Attributes.MoneyReads);
        AssertMoneyPiles(fixture, [6_000], [1_091_000u]);
        var packet = Assert.Single(FloorPackets(fixture));
        Assert.Equal(unchecked(before + 1u), packet.Uid);
        Assert.Equal((ushort)1, packet.X);
        Assert.Equal((ushort)1, packet.Y);
    }

    [Fact]
    public async Task ActualMoneyHandlerFourValidPayloadsAllocateFourSequentialUidsAfterEachAmountRead()
    {
        using var fixture = new Fixture(49, [50, 200, 6_000, 50_000]);
        var before = ReadFloorUidCounter();
        fixture.Attributes.BeforeMoneyRead = call =>
        {
            AssertUidDelta(before, (uint)(call - 1));
            Assert.Equal(call - 1, fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow).Count);
            Assert.Equal(call - 1, FloorPackets(fixture).Count);
        };

        await InvokeActualDropBodyAsync(fixture);

        AssertUidDelta(before, 4);
        Assert.Equal(4, fixture.Attributes.MoneyReads);
        AssertMoneyPiles(fixture, [50, 200, 6_000, 50_000],
            [1_090_000u, 1_090_010u, 1_091_000u, 1_091_020u]);
        Assert.Equal(Enumerable.Range(1, 4).Select(index => unchecked(before + (uint)index)).ToArray(),
            FloorPackets(fixture).Select(packet => packet.Uid).ToArray());
    }

    [Fact]
    public async Task ActualMoneyHandlerMissingCatalogConsumesFourAmountsButNoUidIncludingDiagnostic()
    {
        using var fixture = new Fixture(49, [50, 200, 6_000, 50_000]);
        var context = fixture.Context with
        {
            WorldData = LegacyItemWorldDataFixture.Create(new ItemType(1_001_000, "LifePotion", []))
        };
        var before = ReadFloorUidCounter();
        fixture.Attributes.BeforeMoneyRead = _ => AssertUidDelta(before, 0);

        await InvokeActualDropBodyAsync(fixture, context);

        AssertUidDelta(before, 0);
        Assert.Equal(4, fixture.Attributes.MoneyReads);
        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Contains("source money drop item id 1091020 is not present in the local itemtype catalog",
            fixture.Log.ToString());
    }

    [Fact]
    public async Task ActualNoDropBranchDoesNotAllocateAFloorUid()
    {
        using var fixture = new Fixture(40, [50_000]);
        var before = ReadFloorUidCounter();

        await InvokeActualDropBodyAsync(fixture);

        AssertUidDelta(before, 0);
        Assert.Equal(0, fixture.Attributes.MoneyReads);
        Assert.Empty(FloorPackets(fixture));
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ActualUntrackedMoneyCompatibilityPathStillAllocatesAfterEachValidPayload()
    {
        using var fixture = new Fixture(49, [50, 200, 6_000, 50_000]);
        var context = fixture.Context with { FloorItems = null };
        var before = ReadFloorUidCounter();
        fixture.Attributes.BeforeMoneyRead = call =>
        {
            AssertUidDelta(before, (uint)(call - 1));
            Assert.Equal(call - 1, FloorPackets(fixture).Count);
            Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        };

        await InvokeActualDropBodyAsync(fixture, context);

        AssertUidDelta(before, 4);
        Assert.Equal(4, fixture.Attributes.MoneyReads);
        var packets = FloorPackets(fixture);
        Assert.Equal(Enumerable.Range(1, 4).Select(index => unchecked(before + (uint)index)).ToArray(),
            packets.Select(packet => packet.Uid).ToArray());
        Assert.Equal(new[] { 1_090_000u, 1_090_010u, 1_091_000u, 1_091_020u },
            packets.Select(packet => packet.ItemId).ToArray());
        Assert.Empty(fixture.Floor.SnapshotVisible(DateTimeOffset.UtcNow));
        Assert.Contains("floor item runtime state is not loaded for pickup tracking", fixture.Log.ToString());
    }

    private static uint ReadFloorUidCounter() => unchecked((uint)Assert.IsType<int>(FloorUidCounterField.GetValue(null)));

    private static void AssertUidDelta(uint before, uint expectedDelta) =>
        Assert.Equal(unchecked(before + expectedDelta), ReadFloorUidCounter());
}
