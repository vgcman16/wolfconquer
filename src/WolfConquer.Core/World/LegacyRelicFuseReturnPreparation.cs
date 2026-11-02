using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Private copied data only; this object grants no native publication or persistence authority.
internal sealed record LegacyRelicFuseReturnProjection(PlayerItemSnapshot Items, PlayerItemSnapshotItem Returned);

/// <summary>Pure choose-one recipe over an actual held triple and a private validated saved image.</summary>
internal sealed class LegacyRelicFuseReturnPreparation
{
    private readonly LegacyRelicFuseInputs.ReturnWitness witness;
    private readonly byte[] beforeImage;

    private LegacyRelicFuseReturnPreparation(LegacyRelicFuseInputs.ReturnWitness witness, byte[] ownedImage)
    { this.witness = witness; beforeImage = ownedImage; }

    internal PlayerItemSnapshotItem Selected => witness.Selected;

    internal static bool TryPrepare(PlayerItemSnapshot owner, uint selectedUid,
        out LegacyRelicFuseReturnPreparation? preparation, out string reason)
    {
        preparation = null;
        if (!LegacyRelicFuseInputs.TryCaptureReturn(owner, selectedUid, out var witness, out reason)) return false;
        if (!LegacyRelicFuseImages.TryCapture(owner, addsResult: false, out var image, out reason)) return false;
        if (!witness!.IsCurrent(owner))
        { reason = "relicFuseReturnOwnerChanged"; return false; }
        preparation = new(witness, image);
        reason = string.Empty;
        return true;
    }

    internal bool IsCurrent(PlayerItemSnapshot owner) => witness.IsCurrent(owner) &&
        LegacyRelicFuseImages.TryCapture(owner, addsResult: false, out var currentImage, out _) &&
        beforeImage.AsSpan().SequenceEqual(currentImage);

    internal bool TryProject(PlayerItemSnapshot owner, out LegacyRelicFuseReturnProjection? projection, out string reason)
    {
        projection = null; reason = "relicFuseReturnOwnerChanged";
        if (!IsCurrent(owner)) return false;
        try
        {
            // Reparse the private image on every projection. Mutable DTOs from
            // one result cannot change the owner, recipe or another result.
            var copied = PlayerItemSnapshotLoader.Parse(beforeImage);
            var selected = copied.RelicFuseItems.Single(row => row.Uid == Selected.Uid);
            var returned = selected.WithPosition(0);
            var after = copied with
            {
                InventoryItems = copied.InventoryItems.Append(returned).ToArray(),
                RelicFuseItems = [],
            };
            if (!LegacyRelicFuseImages.TryCapture(after, addsResult: false, out _, out reason)) return false;
            if (!witness.IsCurrent(owner))
            { reason = "relicFuseReturnOwnerChanged"; return false; }
            projection = new(after, returned);
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or
            IndexOutOfRangeException or InvalidOperationException or OverflowException)
        { reason = "relicFuseReturnProjectionInvalid"; return false; }
    }
}
