using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RegisteredD13PackageMergesWholeGrantsIntoRealStacksWithoutAllocatingAnyUid(bool claimAll) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var credited = StackCreditAll(before.Owner.PlayerItems.InventoryItems);
            var attachment = claimAll ? StackAttachmentBefore(before) : null;
            var expected = StackExpectedItems(before, attachment is null ? credited : [.. credited, attachment],
                attachment is null ? [] : [attachment.Uid]);
            var sends = 0;
            join.SendObserver = _ =>
            {
                AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], claimAll, 1031, newCount: 0);
                sends++;
            };
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll));
            join.SendObserver = null;
            Assert.True(sends > 0);
            AssertStackFrames(fixture, before, [new(fixture.AtomicMailSeed, StackOutputs(credited), 1031, attachment)],
                claimAll, started, DateTime.Now, before.FirstFrame);
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], claimAll, 1031, 0);
            await AssertStackReplayRetainsAsync(fixture, join, claimAll);
        });

    [Fact]
    public Task RegisteredD13PackageFullBagCanReceiveEveryWholeGrantInExistingStackCapacity() =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible(), fillTo: 40), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            Assert.Equal(40, before.Owner.PlayerItems.InventoryItems.Count);
            var credited = StackCreditAll(before.Owner.PlayerItems.InventoryItems);
            var expected = StackExpectedItems(before, credited);
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 0);
            AssertStackFrames(fixture, before, [new(fixture.AtomicMailSeed, StackOutputs(credited.Take(6)), 1031)],
                false, started, DateTime.Now, before.FirstFrame);
        });

    [Fact]
    public Task RegisteredD13PackageUsesActualFirstCompatibleInventoryRowAndPreservesEveryOtherCandidate() =>
        RunStackPackageAsync(fixture =>
        {
            var rows = StackAllCompatible();
            fixture.SetStackInventory([rows[1].WithStackSize(100), StackOwnedRow(8_000_050, rows[1].ItemId, 100),
                .. rows.Where(row => row.ItemId != rows[1].ItemId)]);
        }, async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            Assert.Equal(StackExistingFirstUid + 1, before.Owner.PlayerItems.InventoryItems[0].Uid);
            var target = StackExistingFirstUid + 1;
            var credited = before.Owner.PlayerItems.InventoryItems.Select(row =>
                row.ItemId == 4_050_001 && row.Uid != target ? row :
                row.WithStackSize(checked((ushort)(row.StackSize + RankOnePackageRows.Single(grant => grant.Id == row.ItemId).Quantity)))).ToArray();
            var rewards = RankOnePackageRows.Select(grant => credited.First(row => row.ItemId == grant.Id)).ToArray();
            var expected = StackExpectedItems(before, credited);
            var started = DateTime.Now;
            await join.FrameAsync(PackageJoinRequest(claimAll: false));
            AssertStackCommitted(fixture, join, before, expected, [fixture.AtomicMailSeed], false, 1031, 0);
            AssertStackFrames(fixture, before, [new(fixture.AtomicMailSeed, StackOutputs(rewards), 1031)],
                false, started, DateTime.Now, before.FirstFrame);
        });

    private static PlayerItemSnapshotItem[] StackCreditAll(IEnumerable<PlayerItemSnapshotItem> source, int claims = 1) =>
        source.Select(item => RankOnePackageRows.Any(grant => grant.Id == item.ItemId)
            ? item.WithStackSize(checked((ushort)(item.StackSize + claims * RankOnePackageRows.Single(grant => grant.Id == item.ItemId).Quantity)))
            : item).ToArray();

    private static PlayerItemSnapshotItem StackAttachmentBefore(PackageJoinBefore before) =>
        Assert.Single(before.Owner.PlayerItems.WarehouseItems, row => row.Uid == AtomicMailItemUid).WithWarehouseId(0, position: 0);

    private static PlayerItemSnapshot StackExpectedItems(PackageJoinBefore before,
        IReadOnlyList<PlayerItemSnapshotItem> inventory, uint[]? moved = null) => before.Owner.PlayerItems with
    {
        InventoryItems = inventory,
        WarehouseItems = before.Owner.PlayerItems.WarehouseItems.Where(row => !(moved ?? []).Contains(row.Uid)).ToArray(),
    };

    private static async Task AssertStackReplayRetainsAsync(Fixture fixture, PackageJoin join, bool claimAll)
    {
        var files = fixture.PackageFiles();
        var live = StackLiveBusiness(join);
        var first = fixture.Sent.Count;
        await join.FrameAsync(PackageJoinRequest(claimAll));
        AssertPackageJoinFiles(fixture, files);
        Assert.Equal(live, StackLiveBusiness(join));
        AssertPackageJoinReplay(fixture, claimAll, first);
        var previous = join.Current;
        var savedCandidate = LoadDebtRecoveryCandidate(fixture, previous);
        await join.RestartAsync();
        AssertStackColdBusinessState(join, previous, savedCandidate);
        AssertPackageJoinFiles(fixture, files);
        live = StackLiveBusiness(join);
        Assert.Equal(JsonSerializer.Serialize(join.Current.PlayerItems), JsonSerializer.Serialize(
            new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid)));
        first = fixture.Sent.Count;
        await join.FrameAsync(PackageJoinRequest(claimAll));
        AssertPackageJoinFiles(fixture, files);
        Assert.Equal(live, StackLiveBusiness(join));
        AssertPackageJoinReplay(fixture, claimAll, first);
    }

    private static void AssertStackColdBusinessState(PackageJoin join, WorldSession previous, WorldSession savedCandidate)
    {
        // These fixtures have no active steed equipment or saved vigor. Normal
        // equipment startup publishes Vigor=0; pending recovery skipped that phase.
        Assert.DoesNotContain(savedCandidate.PlayerItems.EquipmentItems,
            item => item.Durability != 0 && item.Position % 50 is 12 or 18);
        var attributes = new Dictionary<string, string>(savedCandidate.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        Assert.True(!attributes.TryGetValue("Vigor", out var vigor) || vigor == "0");
        attributes["Vigor"] = "0";
        savedCandidate = savedCandidate with { Character = savedCandidate.Character with { Attributes = attributes } };
        Assert.Equal(StackLiveBusiness(savedCandidate), StackLiveBusiness(join));
        AssertDebtPinnedResourcesEqual(previous, join.Current);
    }

    private static string StackLiveBusiness(PackageJoin join) => StackLiveBusiness(join.Current);

    private static string StackLiveBusiness(WorldSession session) => JsonSerializer.Serialize(new
    { session.Character, session.PlayerItems, session.CombatGear });
}
