using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Dormant until the registered UID completion/startup retirement and all
    // before-entropy frame proofs are joined. This is the fixed real source
    // path: no caller supplies an allocator, saver, validator or result.
    private static bool TryCommitOwnedRelicFuse(WorldPacketHandlerContext context, WorldSession expected,
        LegacyRelicFusePacket packet, CancellationToken cancellationToken,
        out WorldSession final, out WorldSessionStore.RelicWireOutput? wire, out string reason)
    {
        final = expected; wire = null; reason = "relicFuseOwnerUnavailable";
        if (!context.Connection.HasRegisteredRelicWire) return false;
        if (context.Trades is not { } trades || context.Sessions is not { } store ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot) || string.IsNullOrWhiteSpace(context.PlayerSpellsRoot) ||
            !Path.IsPathFullyQualified(context.PlayerItemsRoot) || !Path.IsPathFullyQualified(context.PlayerSpellsRoot)) return false;
        LegacyRelicFuseOwnedEvidence? issued = null; LegacySpellFileOwner? capturedOwner = null;
        bool durable = false; Exception? primaryFailure = null;
        try
        {
            lock (trades.SyncRoot)
            {
                if (!LegacySpellFileOwner.TryCapture(context, expected, paid: true, out var owner, out reason) || owner is null ||
                    !owner.Recovery.IsReadyForCharacterItemsSpells(owner.UsersRoot, context.PlayerItemsRoot, owner.SpellsRoot)) return false;
                capturedOwner = owner;
                if (owner.Native is not { } native || owner.Player is null || native.OrdinarySpellBinding is null) return false;
                var inputs = new LegacySpellMutationInputs(expected, captureItems: true);
                if (!LegacyTradeEscrowClaims.TryRead(inputs.Stable.Character.Attributes, out var claim, out reason) || claim is not null ||
                    !LegacyNativeClientPublication.TryCaptureIdentity(store, context.Connection, expected.Character.Uid, out var identity)) return false;
                var spellWrite = store.PrepareSpellFileWrite(context.Connection, expected, owner.Recovery, owner.UsersRoot, owner.SpellsRoot);
                var itemsRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.PlayerItemsRoot));
                var paths = new[] { Path.Combine(owner.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
                    Path.Combine(itemsRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin"),
                    Path.Combine(owner.SpellsRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin") };
                var before = store.CaptureItemSpellBeforeImages(context, expected, inputs.Stable, spellWrite, paths);
                if (!owner.IsCurrent()) return false;
                inputs.ArmPlainProof(); cancellationToken.ThrowIfCancellationRequested();
                issued = LegacyRelicFuseOwnedEvidence.Capture(context, expected, inputs.Stable, packet, before, inputs);
                if (!issued.TryAcquireRuleLease(out var ruleLease) || ruleLease is null)
                { reason = "relicFuseRuleGenerationChanged"; return false; }
                using var heldRuleLease = ruleLease;
                if (!issued.TryReserveOnce(out var uid, out reason) ||
                    !issued.TryCreateRetainedProjection(out var projection, out reason) || projection is null) return false;
                var proposed = inputs.Stable with { PlayerItems = projection.Items };
                if (!LegacyItemSaveGraph.TryCapture(proposed.PlayerItems, out var graph) || graph is null) return false;
                var candidate = graph.WithOwnedItemsForPersistence(proposed);
                spellWrite.PrepareImage(candidate.PlayerSpells);
                _ = issued.CopyPreparedFrames(uid);
                LegacyTradeJournalChange[]? changes = null;
                SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                {
                    changes = PrepareItemSpellImages(inputs, candidate, before, spellWrite);
                    return true;
                });
                if (!owner.IsCurrent()) return false;
                cancellationToken.ThrowIfCancellationRequested();
                if (!store.TryCommitOwnedRelicFuse(context, expected, candidate, spellWrite, issued, changes!,
                        context.ItemSpellJournalInterruption, out var published, out var committedWire, out reason)) return false;
                durable = true;
                if (published is null || committedWire is null) throw new LegacyCommittedSpellPublicationException();
                store.CompleteManagedRelicOutput(context, expected, published, spellWrite, committedWire);
                final = published; wire = committedWire; reason = string.Empty; return true;
            }
        }
        catch (WorldSessionPublicationException published)
        {
            primaryFailure = published;
            try { FenceSpellPublishedOutcome(capturedOwner, published); }
            catch (Exception fenceFailure)
            {
                primaryFailure = new LegacyCommittedSpellPublicationException(new AggregateException(published, fenceFailure));
                throw primaryFailure;
            }
            throw;
        }
        catch (LegacyCommittedSpellPublicationException failure)
        {
            primaryFailure = failure;
            try { context.TradeRecovery?.Block("ordinarySpellRecoveryRequired:relicCommittedPublicationUnproven"); }
            catch (Exception fenceFailure)
            {
                primaryFailure = new LegacyCommittedSpellPublicationException(new AggregateException(failure, fenceFailure));
                throw primaryFailure;
            }
            throw;
        }
        catch (Exception error) when (IsSpellMutationPreparationFailure(error))
        {
            primaryFailure = error;
            if (durable)
            {
                try { context.TradeRecovery?.Block("ordinarySpellRecoveryRequired:relicFuseCommittedFault"); }
                catch (Exception fenceFailure)
                {
                    primaryFailure = new LegacyCommittedSpellPublicationException(new AggregateException(error, fenceFailure));
                    throw primaryFailure;
                }
                primaryFailure = new LegacyCommittedSpellPublicationException(error);
                throw primaryFailure;
            }
            else reason = "relicFusePreparation:" + error.GetType().Name;
            return false;
        }
        catch (Exception failure)
        { primaryFailure = failure; throw; }
        finally
        {
            try { issued?.FenceUnlessPublished(); }
            catch (Exception fenceFailure) when (primaryFailure is not null)
            { throw new LegacyCommittedSpellPublicationException(new AggregateException(primaryFailure, fenceFailure)); }
        }
    }
}
