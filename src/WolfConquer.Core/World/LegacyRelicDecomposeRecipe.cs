using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Immutable journal recipe DATA. Its recorded facts never authorize allocation.
internal sealed class LegacyRelicDecomposeRecipe
{
    internal const uint RewardItemId = 3_306_885;
    internal const int RewardsPerInput = 9;
    private readonly LegacyRelicDecomposeSelection[] selections;

    private LegacyRelicDecomposeRecipe(LegacyRelicDecomposeRequest request,
        LegacyRelicDecomposeSelection[] selections, ushort durability)
    { Request = request; this.selections = selections; RewardDurability = durability; }

    internal LegacyRelicDecomposeRequest Request { get; }
    internal ReadOnlySpan<LegacyRelicDecomposeSelection> Selections => selections;
    internal ushort RewardDurability { get; }
    internal int NewUidCount => checked(selections.Length * RewardsPerInput);
    internal int FrameCount => checked(selections.Length * (RewardsPerInput + 1) + 1);

    internal static bool TryFromRecorded(LegacyRelicDecomposeRequest request,
        ReadOnlySpan<LegacyRelicDecomposeSelection> selections, ushort rewardDurability,
        out LegacyRelicDecomposeRecipe? recipe, out string reason)
    {
        recipe = null; reason = "relicDecomposeRecordedRecipeInvalid";
        if (request is null || selections.Length != request.SelectedUids.Length ||
            selections.Length is <= 0 or > LegacyRelicDecomposeRequest.MaximumSelectedCount) return false;
        var owned = selections.ToArray();
        for (var index = 0; index < owned.Length; index++)
            if (owned[index].Uid != request.SelectedUids[index] || owned[index].ItemId is < 4_100_001 or > 4_100_005 ||
                owned[index].Bound > 1) return false;
        recipe = new(request, owned, rewardDurability); reason = string.Empty; return true;
    }

    internal bool TryProject(PlayerItemSnapshot before, uint firstUid, int allocatedCount,
        out LegacyRelicDecomposeProjection? projection, out string reason) =>
        LegacyRelicDecomposeProjection.TryCreate(this, before, firstUid, allocatedCount, out projection, out reason);
}
