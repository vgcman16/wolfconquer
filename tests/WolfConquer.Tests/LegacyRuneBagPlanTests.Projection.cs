using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuneBagPlanTests
{
    [Fact]
    public void IngressPreservesTheCompleteRichRootChildAndPerfectionBytesWithOnlyPositionChanged()
    {
        var target = RichRoot().WithPosition(0); var before = Items(inventory: [target]);
        var original = PlayerItemSnapshotLoader.ToLegacyBytes(before); var expected = original.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 12), 211);
        var recipe = LegacyRuneBagRecipe.Prepare(before, TargetUid, true, new(target.ItemId, 0));
        var projected = recipe.Project(null);
        Assert.Equal(expected, PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items));
        Assert.Equal(TargetUid, projected.ResultItem.Uid); Assert.Equal((ushort)211, projected.ResultItem.Position);
        Assert.Equal(77U, Assert.Single(projected.ResultItem.DepositedItems).Uid);
        Assert.Equal(original, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Fact]
    public void ReturnRekeysOnlyRootDbUidPositionAndItsPerfectionUidWhileRetainingEveryOtherByte()
    {
        const uint issued = 5000;
        var target = RichRoot(); var before = Items(runes: [target]);
        var original = PlayerItemSnapshotLoader.ToLegacyBytes(before); var expected = original.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4), issued);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 12), 0);
        int perfection = 4 + 2 * PlayerItemSnapshotLoader.LegacyDbItemSize + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(perfection), issued);
        var recipe = LegacyRuneBagRecipe.Prepare(before, TargetUid, false, new(target.ItemId, 0));
        Assert.Equal(1, recipe.Plan.NewUidCount);
        var projected = recipe.Project(issued);
        Assert.Equal(expected, PlayerItemSnapshotLoader.ToLegacyBytes(projected.Items));
        Assert.Empty(projected.Items.RuneItems); Assert.Equal(issued, Assert.Single(projected.Items.InventoryItems).Uid);
        Assert.Equal(77U, Assert.Single(projected.ResultItem.DepositedItems).Uid);
        Assert.Equal(original, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData((ushort)0)] [InlineData((ushort)1)]
    public void WholeMergeRetiresOnlyTheSelectedRootAndChangesOnlyReceiverQuantity(ushort rawStack)
    {
        var selected = Row(TargetUid, stack: rawStack);
        var receiver = Row(50, position: 0, stack: rawStack);
        var before = Items([receiver], [selected]); var original = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var expected = PlayerItemSnapshotLoader.ToLegacyBytes(Items([receiver]));
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 52), 2);
        var recipe = LegacyRuneBagRecipe.Prepare(before, TargetUid, false, new(FragmentType, 10));
        var result = recipe.Project(null);
        Assert.Equal(expected, PlayerItemSnapshotLoader.ToLegacyBytes(result.Items));
        Assert.Equal(50U, result.ResultItem.Uid); Assert.Equal((byte)1, result.ResultItem.Locked);
        Assert.Equal((ushort)2, result.ResultItem.StackSize); Assert.Empty(result.Items.RuneItems);
        Assert.Equal(original, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(null)] [InlineData(0U)] [InlineData(TargetUid)] [InlineData(77U)]
    public void FreshProjectionRequiresOneNonzeroUidDisjointFromAllExistingPhysicalRootsAndChildren(uint? issued)
    {
        var target = RichRoot(); var before = Items(runes: [target]);
        var image = PlayerItemSnapshotLoader.ToLegacyBytes(before);
        var recipe = LegacyRuneBagRecipe.Prepare(before, TargetUid, false, new(target.ItemId, 0));
        Assert.Throws<InvalidDataException>(() => recipe.Project(issued));
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(before));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ZeroBirthBranchesCannotConsumeAnUnneededUid(bool ingress)
    {
        var target = Row(TargetUid, position: ingress ? (ushort)0 : (ushort)211);
        var before = ingress ? Items(inventory: [target]) : Items([Row(50, position: 0)], [target]);
        var recipe = LegacyRuneBagRecipe.Prepare(before, TargetUid, ingress, new(FragmentType, 10));
        Assert.Equal(0, recipe.Plan.NewUidCount);
        Assert.Throws<InvalidDataException>(() => recipe.Project(5000));
    }

    [Fact]
    public void RecipeCopiesInputsAndReturnedMutablePerfectionRowsCannotRewriteLaterProjections()
    {
        var target = RichRoot(); var before = Items(runes: [target]);
        var recipe = LegacyRuneBagRecipe.Prepare(before, TargetUid, false, new(target.ItemId, 0));
        var expected = PlayerItemSnapshotLoader.ToLegacyBytes(recipe.Project(5000).Items);
        target.PerfectionLevel = 999; target.DepositedItems[0].OwnerName = "changed input";
        recipe.Plan.Target.PerfectionProgress = 444;
        var first = recipe.Project(5000); first.ResultItem.Signature = "changed output";
        first.ResultItem.DepositedItems[0].PerfectionLevel = 555;
        Assert.Equal(expected, PlayerItemSnapshotLoader.ToLegacyBytes(recipe.Project(5000).Items));
    }

    [Fact]
    public void WithUidIsACompleteDataCopyAndRejectsZeroWithoutSupplyingAnIssuer()
    {
        var original = RichRoot(); var image = PlayerItemSnapshotLoader.ToLegacyBytes(Items(runes: [original]));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.WithUid(0));
        var clone = original.WithUid(5000);
        Assert.Same(original.DepositedItems, clone.DepositedItems);
        var expected = image.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(4), 5000);
        int perfection = 4 + 2 * PlayerItemSnapshotLoader.LegacyDbItemSize + 4;
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(perfection), 5000);
        Assert.Equal(expected, PlayerItemSnapshotLoader.ToLegacyBytes(Items(runes: [clone])));
        Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(Items(runes: [original])));
        // A serializer-truncated input is not a full-row preservation success.
        var unrepresentable = RichRoot(); unrepresentable.OwnerName = new string('x', 33);
        Assert.Throws<InvalidDataException>(() => LegacyRuneBagRecipe.Prepare(
            Items(runes: [unrepresentable]), TargetUid, false, new(unrepresentable.ItemId, 0)));
    }

    private static PlayerItemSnapshotItem RichRoot() => new()
    {
        Uid = TargetUid, ItemId = 4_020_517, Position = 211, StackSize = 1,
        Durability = 321, MaximumDurability = 999, SocketProgress = 12345,
        SocketOne = 1, SocketTwo = 2, Effect = 3, Plus = 4, Bless = 5, Bound = 1,
        Enchant = 6, Suspicious = 7, Locked = 1, PlusProgress = 8, Inscribed = 9,
        Activate = 1, TimeLeftInMinutes = 0, Color = 6, UnlockTimer = 10, ItemPoints = 11,
        PurificationItemId = 12, PurificationLevel = 13, PurificationDuration = 14,
        PurificationAddedOn = 15, RefineryEffectId = 16, RefineryEffectLevel = 17,
        RefineryEffectPercent = 18, RefineryEffectPercent2 = 19, RefineryEffectDuration = 20,
        RefineryEffectAddedOn = 21, AnimaItemId = 22, RuneExperience = 7500,
        Attribute1 = 23, Attribute2 = 24, Attribute3 = 25, Attribute4 = 26, Attribute5 = 27,
        Expiration = 638_000_000_000_000_000, MythSoul = 28, MythSoulPoints = 29,
        PerfectionLevel = 30, PerfectionProgress = 31, OwnerUid = 32,
        OwnerName = "full root", Signature = "opaque retained",
        DepositedItems = [Row(77, 1_000_000, 0)],
    };
}
