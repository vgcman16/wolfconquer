using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Private historical item image and ordered protocol data, not a commit receipt.
internal sealed partial class LegacyRelicDecomposeProjection
{
    private readonly byte[] afterImage;
    private readonly byte[][] frames;
    private LegacyRelicDecomposeProjection(byte[] afterImage, byte[][] frames)
    { this.afterImage = afterImage; this.frames = frames; }

    internal PlayerItemSnapshot Items => PlayerItemSnapshotLoader.Parse(afterImage);
    internal byte[] CopyAfterImage() => afterImage.ToArray();
    internal byte[][] CopyFrames() => frames.Select(frame => frame.ToArray()).ToArray();

    internal static bool TryCreate(LegacyRelicDecomposeRecipe recipe, PlayerItemSnapshot before,
        uint firstUid, int allocatedCount, out LegacyRelicDecomposeProjection? projection, out string reason)
    {
        projection = null; reason = "relicDecomposeUidRangeInvalid";
        if (recipe is null || firstUid == 0 || allocatedCount != recipe.NewUidCount ||
            (ulong)firstUid + (uint)allocatedCount - 1 > uint.MaxValue) return false;
        if (!LegacyRelicDecomposeInputs.TryCapture(before, recipe.Request, out var inputs, out reason) || inputs is null) return false;
        if (!inputs.Selections.SequenceEqual(recipe.Selections))
        { reason = "relicDecomposeRecordedInputsMismatch"; return false; }
        // Necessary local freshness only. The actual shared issuer, retained
        // receipt and owned journal publication are required outside this data.
        if (firstUid <= inputs.MaximumUid)
        { reason = "relicDecomposeUidRangeCollision"; return false; }
        try
        {
            var owned = PlayerItemSnapshotLoader.Parse(inputs.CopyBeforeImage());
            var selectedUids = recipe.Request.SelectedUids.ToArray().ToHashSet();
            var rewards = CreateRewards(recipe, firstUid);
            var after = owned with
            {
                InventoryItems = owned.InventoryItems.Where(row => !selectedUids.Contains(row.Uid)).Concat(rewards).ToArray(),
            };
            if (!LegacyItemSaveGraph.TryCapture(after, out var graph))
            { reason = "relicDecomposeAfterImageInvalid"; return false; }
            var image = PlayerItemSnapshotLoader.ToLegacyBytes(after);
            if (!graph.HasBytes(image) || !graph.IsCurrent(after))
            { reason = "relicDecomposeAfterImageInvalid"; return false; }
            var frames = CreateFrames(recipe, rewards);
            if (frames.Length != recipe.FrameCount || !inputs.IsCurrent(before))
            { reason = "relicDecomposeInputsChanged"; return false; }
            projection = new(image, frames); reason = string.Empty; return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or IndexOutOfRangeException or
            InvalidOperationException or OverflowException)
        { reason = "relicDecomposeProjectionInvalid"; return false; }
    }

    private static PlayerItemSnapshotItem[] CreateRewards(LegacyRelicDecomposeRecipe recipe, uint firstUid)
    {
        var rewards = new PlayerItemSnapshotItem[recipe.NewUidCount];
        for (var index = 0; index < rewards.Length; index++)
            rewards[index] = new()
            {
                Uid = checked(firstUid + (uint)index), ItemId = LegacyRelicDecomposeRecipe.RewardItemId,
                Bound = recipe.Selections[index / LegacyRelicDecomposeRecipe.RewardsPerInput].Bound,
                Durability = recipe.RewardDurability, MaximumDurability = recipe.RewardDurability,
                StackSize = 1, Position = 0,
            };
        return rewards;
    }
}
