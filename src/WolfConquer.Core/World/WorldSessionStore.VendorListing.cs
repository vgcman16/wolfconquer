using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldSessionStore
{
    // Fixed advertisement producer, not a caller-supplied persistence/owner callback.
    internal bool TryPublishVendorListing(WorldPacketHandlerContext context, WorldSession expected, object identity,
        LegacyItemSaveGraph graph, LegacyItemSaveGraph.Closure incoming, LegacyVendorListingCostMode mode, long amount,
        out WorldSession? final, out LegacyVendorListing? added, out string reason)
    {
        final = null; added = null; reason = "listing-owner-or-item-changed";
        if (!ReferenceEquals(context.Sessions, this) || expected.LegacyVendorListings is not { } book ||
            amount < 0 || mode is not (LegacyVendorListingCostMode.Gold or LegacyVendorListingCostMode.CPs) ||
            mode == LegacyVendorListingCostMode.CPs && amount > uint.MaxValue) return false;
        var connection = context.Connection;
        RefuseBoundSpellReentry(connection);
        object publishedIdentity;
        LegacyOrdinarySpellBinding? publishedBinding;
        using (LegacyMailboxPackageMutationFence.EnterStoreScope(MailboxPackages, expected))
        {
            if (!_connectionGates.TryGetValue(connection, out var gate)) return false;
            lock (gate)
            {
                using var nativeGate = EnterNativeConnectionGate();
                if (!_sessions.TryGetValue(connection, out var holder) || !ReferenceEquals(holder.Read(), expected) ||
                    !ReferenceEquals(holder.NativeClientIdentity, identity) ||
                    !LegacyMailboxPackageMutationFence.CanMutateSession(MailboxPackages, expected, expected)) return false;
                // Existing observation publication owns the real actor/storage gates;
                // it performs no publication. The issuer cannot run without them.
                using var observation = new PreparedSessionSpellPublication(this, connection, holder, expected, expected);
                if (!WorldPacketHandlers.IsLegacyVendorListingAdmissionCurrent(context, expected, identity, graph, incoming) ||
                    !WorldPacketHandlers.TryGetLegacyVendorMetadata(context, connection, expected, incoming, out var metadata) ||
                    !VendorItemLineage.TryCapture(this, connection, expected, incoming, out var lineage)) return false;
                var result = book.TryAdd(incoming, mode, amount, lineage, metadata);
                reason = "listing-rejected:" + result.Failure;
                if (!result.Added) return false;
                var candidate = expected with { LegacyVendorListings = result.Book };
                using var publication = new PreparedSessionSpellPublication(this, connection, holder, expected, candidate);
                reason = "listing-owner-or-item-changed";
                if (!WorldPacketHandlers.IsLegacyVendorListingAdmissionCurrent(context, expected, identity, graph, incoming) ||
                    !result.Book.TryGetCurrentListing(incoming.Source.Uid, incoming.Source, this, connection, expected, context.WorldData, out _) ||
                    !TryPublishFrameOwnedHitpoints(publication, holder, out _)) return false;
                final = publication.FinalSession;
                publishedIdentity = publication.PublishedIdentity;
                publishedBinding = publication.Binding;
                var currentRow = final.PlayerItems.InventoryItems.FirstOrDefault(row => row.Uid == incoming.Source.Uid);
                if (currentRow is null || !result.Book.TryGetCurrentListing(currentRow.Uid, currentRow, this, connection, final, context.WorldData, out _))
                    FailCommittedSpellPublication(holder, publishedBinding);
                publication.Dispose();
                observation.Dispose();
                PersistFrameOwnedSessionChange(connection, expected, final, publishedIdentity, publishedBinding, publication.HitpointsWrite);
                added = result.Listing;
            }
        }
        NotifyPublishedSessionChange(connection, expected, final, publishedIdentity, publishedBinding);
        reason = string.Empty;
        return true;
    }
}
