using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Arithmetic controls only. Real admission, catalog custody and journal recovery
// are exercised separately by LegacyOwnedRuneComposeTests.
public sealed class LegacyRuneComposePlanMaximumTests
{
    [Fact]
    public void ReachingMaximumDiscardsExcessProgressAndRetainsLaterResolvedMaterials()
    {
        var target = Rune(4_020_526);
        var essence = new PlayerItemSnapshotItem
        { Uid = 802, ItemId = 4_040_001, Position = 0, StackSize = 5000 };
        // Source resolves this UID first, but stops before evaluating/consuming
        // its material family once the first material reaches the target cap.
        var untouched = new PlayerItemSnapshotItem
        { Uid = 803, ItemId = 1_000_000, Position = 0, StackSize = 2, Locked = 1 };
        var before = new PlayerItemSnapshot([essence, untouched], [], [], [], [target]);
        var beforeBytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);

        var plan = WorldPacketHandlers.PlanRecordedRuneCompose(before, target.Uid,
            [essence.Uid, untouched.Uid], Curves(), Caps());

        Assert.Equal(4_020_527U, plan.Target.ItemId);
        Assert.Equal(0U, plan.Target.RuneExperience);
        Assert.Equal(essence.Uid, Assert.Single(plan.Consumed).Uid);
        Assert.Same(untouched, Assert.Single(plan.Items.InventoryItems));
        Assert.Equal(target.Uid, Assert.Single(plan.Items.RuneItems).Uid);
        Assert.True(plan.UsedProgress.SetEquals(new[] { (402U, (byte)26), (402U, (byte)27), (404U, (byte)1) }));
        Assert.Equal(beforeBytes, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Fact]
    public void AnAlreadyMaximumTargetDoesNotConsumeMaterialsOrRewriteItsExistingProgress()
    {
        var target = Rune(4_020_527).WithRuneExperience(7);
        var material = new PlayerItemSnapshotItem
        { Uid = 802, ItemId = 4_040_001, Position = 0, StackSize = 5000 };
        var before = new PlayerItemSnapshot([material], [], [], [], [target]);
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);

        var plan = WorldPacketHandlers.PlanRecordedRuneCompose(before, target.Uid,
            [material.Uid], new Dictionary<(uint Family, byte Level), LegacyRuneComposeProgressRow>(),
            new Dictionary<uint, LegacyRuneComposeMaterialMetadata>());

        Assert.Empty(plan.Consumed);
        Assert.Empty(plan.UsedProgress);
        Assert.Same(target, plan.Target);
        Assert.Equal(7U, plan.Target.RuneExperience);
        Assert.Same(material, Assert.Single(plan.Items.InventoryItems));
        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(plan.Items));
    }

    [Fact]
    public void MissingLaterUidStillRefusesBeforeTheSourceMaximumShortCircuit()
    {
        var target = Rune(4_020_526);
        var essence = new PlayerItemSnapshotItem
        { Uid = 802, ItemId = 4_040_001, Position = 0, StackSize = 5000 };
        var before = new PlayerItemSnapshot([essence], [], [], [], [target]);
        var bytes = PlayerItemSnapshotLoader.ToLegacyBytes(before);

        Assert.Throws<InvalidOperationException>(() => WorldPacketHandlers.PlanRecordedRuneCompose(
            before, target.Uid, [essence.Uid, 899], Curves(), Caps()));

        Assert.Equal(bytes, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    private static PlayerItemSnapshotItem Rune(uint itemId) => new()
    { Uid = 801, ItemId = itemId, Position = 212, StackSize = 1 };

    private static Dictionary<(uint Family, byte Level), LegacyRuneComposeProgressRow> Curves() => new()
    {
        [(402, 26)] = new(402, 26, 311500),
        [(402, 27)] = new(402, 27, 341500),
        [(404, 1)] = new(404, 1, 10),
    };

    private static Dictionary<uint, LegacyRuneComposeMaterialMetadata> Caps() => new()
    { [4_040_001] = new(4_040_001, 10000) };
}
