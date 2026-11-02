using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// A private historical after image. Neither this recipe nor its caller-supplied
// UID establishes allocation, native publication or a durable commit.
internal sealed class LegacyRelicFuseProjection
{
    private readonly byte[] afterImage;
    private LegacyRelicFuseProjection(byte[] afterImage, uint resultUid)
    { this.afterImage = afterImage; ResultUid = resultUid; }

    internal uint ResultUid { get; }
    internal PlayerItemSnapshot Items => PlayerItemSnapshotLoader.Parse(afterImage);
    internal PlayerItemSnapshotItem ResultItem => Items.RelicFuseItems.Single(row => row.Uid == ResultUid);
    internal byte[] CopyAfterImage() => afterImage.ToArray();

    internal static bool TryCreate(PlayerItemSnapshot before, LegacyRelicFuseRollData retained,
        uint issuerUid, out LegacyRelicFuseProjection? projection, out string reason)
    {
        projection = null; reason = "relicFuseRecordedProjectionInvalid";
        if (retained is null || issuerUid == 0) return false;
        if (!LegacyRelicFuseInputs.TryCapture(before, retained.FirstUid, retained.SecondUid, out var inputs, out reason)) return false;
        if (inputs!.MaximumDepth > 1 || inputs.First.ItemId != retained.ItemId || inputs.ResultBound != retained.Bound)
        { reason = "relicFuseRecordedInputMismatch"; return false; }
        if (!LegacyRelicFuseImages.TryCapture(before, addsResult: true, out var beforeImage, out reason)) return false;
        var owned = PlayerItemSnapshotLoader.Parse(beforeImage);
        var rows = WorldPacketHandlers.IndexRuneBagPhysicalItems(owned);
        // This is only a local collision check. Actual reservation authority
        // belongs to the shared registered issuer, outside this data recipe.
        if (rows.ContainsKey(issuerUid)) { reason = "relicFuseResultUidCollision"; return false; }
        var first = owned.InventoryItems.Single(row => row.Uid == retained.FirstUid);
        var second = owned.InventoryItems.Single(row => row.Uid == retained.SecondUid);
        var result = new PlayerItemSnapshotItem
        {
            Uid = issuerUid, ItemId = retained.ItemId, Bound = retained.Bound,
            Durability = retained.Durability, MaximumDurability = retained.Durability,
            Position = 215, StackSize = 1,
            Attribute1 = retained.Attribute1, Attribute2 = retained.Attribute2,
            Attribute3 = retained.Attribute3, Attribute4 = retained.Attribute4, Attribute5 = retained.Attribute5,
        };
        var after = owned with
        {
            InventoryItems = owned.InventoryItems.Where(row => row.Uid != retained.FirstUid && row.Uid != retained.SecondUid).ToArray(),
            RelicFuseItems = [first.WithPosition(213).WithStackSize(1), second.WithPosition(214).WithStackSize(1), result],
        };
        if (!LegacyRelicFuseImages.TryCapture(after, addsResult: false, out var image, out reason)) return false;
        if (!inputs.IsCurrent(before)) { reason = "relicFusePreparedOwnerChanged"; return false; }
        projection = new(image, issuerUid);
        reason = string.Empty;
        return true;
    }
}
