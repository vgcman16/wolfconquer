using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed partial class ManagedStartupPersistenceState
    {
        private byte[]? itemImage;
        private bool itemsCaptured;

        // Called only by the actual configured item loader's locked raw read,
        // before its DTO is returned. It does not accept a saver/validation hook.
        internal void CaptureItemsLoad(byte[]? bytes)
        {
            if (itemsCaptured) throw Refused("duplicateItemLoad");
            itemsCaptured = true; itemImage = bytes?.ToArray();
        }

        internal CharacterSnapshot CommitStarter(CharacterSnapshot expected, CharacterSnapshot proposed,
            PlayerItemSnapshot beforeItems, PlayerItemSnapshot afterItems)
        {
            if (!itemsCaptured || string.IsNullOrWhiteSpace(loader.PlayerItemsRoot) ||
                !Path.IsPathFullyQualified(loader.PlayerItemsRoot)) throw Refused("itemLoadUnavailable");
            string itemPath = Path.Combine(loader.PlayerItemsRoot, expected.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var parsedItems = itemImage is null ? PlayerItemSnapshot.Empty : PlayerItemSnapshotLoader.Parse(itemImage);
            if (!PlayerItemSnapshotLoader.ToLegacyBytes(parsedItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(beforeItems)))
                throw Refused("itemLoadProjectionChanged");
            var afterItemImage = PlayerItemSnapshotLoader.ToLegacyBytes(afterItems);
            if (afterItemImage.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes) throw Refused("itemImageBounds");
            lock (frame._tradeStates.SyncRoot)
                return CharacterSnapshotFileCoordinator.ExecuteLocked(new[] { path, itemPath }, () =>
                {
                    RequireCurrent(expected);
                    RequireItemBefore(itemPath);
                    // Complete projection/serialization preflight before either
                    // real per-file writer can commit. This is not a multi-file
                    // crash-atomic journal; partial completion is fenced below.
                    var candidate = LegacyManagedCharacterProjection.ProjectAfterImage(image, proposed);
                    _ = NormalizeCommittedSnapshot(expected, proposed, candidate);
                    var receipt = CommitCharacterLocked(expected, proposed);
                    try
                    {
                        RequireItemBefore(itemPath);
                        bool saved, uncertain; string reason; Exception? failure = null;
                        try
                        {
                            saved = itemImage is null
                                ? CharacterSnapshotFileCoordinator.TryCreateCapturedImage(itemPath, afterItemImage, out uncertain, out reason)
                                : CharacterSnapshotFileCoordinator.TryReplaceCapturedImage(itemPath, itemImage, afterItemImage, out uncertain, out reason);
                        }
                        catch (Exception error) when (IsStartupIoFailure(error))
                        { saved = false; uncertain = true; reason = error.GetType().Name; failure = error; }
                        if (!saved) throw Refused((uncertain ? "itemOutcomeUnproven:" : "itemWriteRefused:") + reason, failure);
                        if (!CharacterSnapshotFileBaseline.TryReadBytes(itemPath, "managedStartupItemsCommitted", out var actual, out _) ||
                            !actual.AsSpan().SequenceEqual(afterItemImage)) throw Refused("itemCommittedImageChanged");
                        var result = Accept(receipt);
                        itemImage = afterItemImage;
                        return result;
                    }
                    catch
                    {
                        frame._tradeRecovery.Block("managedStartupStarterPartialOrUnproven");
                        throw;
                    }
                });
        }

        private void RequireItemBefore(string itemPath)
        {
            if (itemImage is not null)
            {
                if (!CharacterSnapshotFileBaseline.TryReadBytes(itemPath, "managedStartupItems", out var actual, out _) ||
                    !actual.AsSpan().SequenceEqual(itemImage)) throw Refused("itemBeforeImageChanged");
                return;
            }
            try { _ = File.GetAttributes(itemPath); }
            catch (FileNotFoundException) { return; }
            throw Refused("itemAbsentLeafChanged");
        }
    }
}
