using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Project is a local game transition, never a persistence/acceptance callback.
    // Chance may issue one private decision after saved-before preflight; its
    // same issued capability must make final reprojection deterministic.
    private sealed record ItemSpellProjection(WorldSession Updated, byte[][] Frames, byte[][]? Refresh = null,
        bool UpdateRank = false, LegacyRuneOperationEvidence? Rune = null, LegacyRuneContainerEvidence? Container = null,
        LegacyRuneComposeEvidence? Compose = null, LegacyRuneChanceEvidence? Chance = null, LegacyRuneDecomposeEvidence? Decompose = null, LegacyRuneBagEvidence? Bag = null);

    private static bool TryCommitLegacyItemSpell(WorldPacketHandlerContext context, WorldSession expected,
        Func<WorldSession, object, ItemSpellProjection> project, CancellationToken cancellationToken,
        out WorldSession final, out byte[][] frames, out object publicationIdentity, out string reason) =>
        TryCommitLegacyItemSpell(context, expected, (stable, identity, _) => project(stable, identity), cancellationToken,
            out final, out frames, out publicationIdentity, out reason);

    private static bool TryCommitLegacyItemSpell(WorldPacketHandlerContext context, WorldSession expected,
        Func<WorldSession, object, ItemSpellBeforeImages, ItemSpellProjection> project, CancellationToken cancellationToken,
        out WorldSession final, out byte[][] frames, out object publicationIdentity, out string reason)
    {
        final = expected; frames = []; publicationIdentity = null!; reason = "itemSpellOwnerUnavailable";
        if (context.Trades is not { } trades || context.Sessions is null ||
            string.IsNullOrWhiteSpace(context.PlayerItemsRoot) || string.IsNullOrWhiteSpace(context.PlayerSpellsRoot) ||
            !Path.IsPathFullyQualified(context.PlayerItemsRoot) || !Path.IsPathFullyQualified(context.PlayerSpellsRoot)) return false;
        var result = expected; byte[][] output = []; object issuedIdentity = null!; var failure = reason; var durable = false;
        LegacySpellFileOwner? capturedOwner = null;
        LegacyRuneRankingState.PreparedRuneRankLease? rankLease = null;
        try
        {
            lock (trades.SyncRoot)
            {
                if (!LegacySpellFileOwner.TryCapture(context, expected, paid: true, out var owner, out failure) || owner is null ||
                    !owner.Recovery.IsReadyForCharacterItemsSpells(owner.UsersRoot, context.PlayerItemsRoot, owner.SpellsRoot))
                { reason = failure; return false; }
                capturedOwner = owner;
                if (owner.Native is { } physical && physical.OrdinarySpellBinding is null)
                { reason = "itemSpellNativeBindingUnavailable"; return false; }
                var inputs = new LegacySpellMutationInputs(expected, captureItems: true);
                if (!LegacyTradeEscrowClaims.TryRead(inputs.Stable.Character.Attributes, out var claim, out failure) || claim is not null)
                { reason = "itemSpellEscrowUnresolved"; return false; }
                if (!LegacyNativeClientPublication.TryCaptureIdentity(context.Sessions!, context.Connection, expected.Character.Uid, out var identity))
                { reason = "itemSpellOwnerChanged"; return false; }
                var spellWrite = context.Sessions!.PrepareSpellFileWrite(context.Connection, expected,
                    owner.Recovery, owner.UsersRoot, owner.SpellsRoot);
                var itemRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(context.PlayerItemsRoot));
                var characterPath = Path.Combine(owner.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
                var itemPath = Path.Combine(itemRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
                var spellPath = Path.Combine(owner.SpellsRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
                var paths = new[] { characterPath, itemPath, spellPath };
                var beforeImages = context.Sessions.CaptureItemSpellBeforeImages(context, expected, inputs.Stable, spellWrite, paths);
                if (!owner.IsCurrent()) { reason = "itemSpellOwnerChanged"; return false; }
                cancellationToken.ThrowIfCancellationRequested();
                var prepared = project(inputs.Stable, identity, beforeImages);
                if ((prepared.Rune is not null ? 1 : 0) + (prepared.Container is not null ? 1 : 0) +
                    (prepared.Compose is not null ? 1 : 0) + (prepared.Chance is not null ? 1 : 0) +
                    (prepared.Decompose is not null ? 1 : 0) + (prepared.Bag is not null ? 1 : 0) > 1) throw InvalidSpellMutation();
                if (!FullAccountCharacterScalarsEqual(prepared.Updated.Character with { MinHitPoints = inputs.Stable.Character.MinHitPoints }, inputs.Stable.Character) ||
                    !LegacyItemSaveGraph.TryCapture(prepared.Updated.PlayerItems, out var afterGraph)) throw InvalidSpellMutation();
                var candidate = afterGraph.WithOwnedItemsForPersistence(prepared.Updated);
                spellWrite.PrepareImage(candidate.PlayerSpells);
                var afterItems = PlayerItemSnapshotLoader.ToLegacyBytes(candidate.PlayerItems);
                LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(afterItems);
                LegacyTradeJournalChange[]? changes = null;
                SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                {
                    changes = PrepareItemSpellImages(inputs, candidate, beforeImages, spellWrite,
                        prepared.Rune?.Operation, prepared.Container?.Operation, prepared.Compose?.Operation, prepared.Chance?.Operation,
                        prepared.Decompose?.Operation, prepared.Bag?.Operation);
                    return true;
                });
                var runePublication = prepared.Rune is not null || prepared.Container is not null || prepared.Compose is not null ||
                    prepared.Chance is not null || prepared.Decompose is not null || prepared.Bag is not null
                    ? spellWrite.PrepareRuneCharacterPublication(candidate, changes!, prepared.Rune, prepared.Container, prepared.Compose, prepared.Chance,
                        prepared.Decompose, prepared.Bag) : null;
                if (!owner.IsCurrent()) { reason = "itemSpellOwnerChanged"; return false; }
                inputs.ArmPlainProof();
                bool Writer()
                {
                    bool Locked()
                    {
                        lock (LegacySecondaryPasswordRuntime.SyncRoot)
                        return SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!owner.IsCurrent() || !owner.Recovery.IsReadyForCharacterItemsSpells(owner.UsersRoot, itemRoot, owner.SpellsRoot) ||
                                !owner.Recovery.TryRequireLiveJournalClear(out failure) || context.PlayerItemsRoot != itemRoot ||
                                prepared.Rune is { } rune && !rune.IsCurrent() ||
                                prepared.Container is { } container && !container.IsCurrent() ||
                                prepared.Compose is { } compose && !compose.IsCurrent() ||
                                prepared.Chance is { } chance && !chance.IsCurrent() ||
                                prepared.Decompose is { } decompose && !decompose.IsCurrent() ||
                                prepared.Bag is { } bag && !bag.IsCurrent() ||
                                prepared.Refresh is not null && !ItemSpellFramesEqual(prepared.Refresh, BuildItemSpellRefresh(context, candidate, identity)) ||
                                !ItemSpellBeforeImagesMatch(paths, changes!) ||
                                !ItemSpellProjectionMatches(prepared, project(inputs.Stable, identity, beforeImages)))
                            { failure = "itemSpellBeforeImageChanged"; return false; }
                            if (prepared.UpdateRank)
                            {
                                if (context.RuneRankings is not { } ranks) { failure = "itemSpellRankingOwnerChanged"; return false; }
                                rankLease = ranks.PrepareUpdate(candidate.Character, candidate.PlayerItems);
                            }
                            if (rankLease is not null && !rankLease.IsCurrentPlain() || !inputs.ConsumeCurrentPlain() ||
                                prepared.Rune is { } finalRune && !finalRune.IsCurrent() ||
                                prepared.Container is { } finalContainer && !finalContainer.IsCurrent() ||
                                prepared.Compose is { } finalCompose && !finalCompose.IsCurrent() ||
                                prepared.Chance is { } finalChance && !finalChance.IsCurrent() ||
                                prepared.Decompose is { } finalDecompose && !finalDecompose.IsCurrent() ||
                                prepared.Bag is { } finalBag && !finalBag.IsCurrent())
                            { failure = "itemSpellBeforeImageChanged"; return false; }
                            if (!spellWrite.TryCommitCharacterItemsSpells(itemRoot, changes!, out failure,
                                    context.ItemSpellJournalInterruption, prepared.Rune?.Operation, prepared.Container?.Operation, runePublication,
                                    prepared.Compose?.Operation, prepared.Chance?.Operation, prepared.Decompose, prepared.Bag))
                            {
                                if (failure.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal)) owner.Recovery.Block(failure);
                                return false;
                            }
                            durable = true;
                            prepared.Decompose?.RetireCommittedNamespace(runePublication!);
                            prepared.Bag?.RetireCommittedNamespace(runePublication!);
                            return true;
                        });
                    }
                    if (owner.Native is not { } native) return Locked();
                    lock (native.SpellAdmissionSyncRoot)
                    lock (owner.Player!.SpellAdmissionSyncRoot) return Locked();
                }
                WorldSession? published;
                bool committed = runePublication is null
                    ? context.Sessions!.TryCommitSession(context.Connection, expected, candidate, Writer, out published)
                    : context.Sessions!.TryCommitRuneItemSpellSession(context.Connection, expected, candidate, runePublication, Writer, out published);
                if (!committed) { reason = failure; return false; }
                if (published is null) { owner.Recovery.Block("ordinarySpellRecoveryRequired:itemSpellPublicationUnproven"); throw new LegacyCommittedSpellPublicationException(); }
                rankLease?.Publish();
                result = published; output = prepared.Frames; issuedIdentity = identity;
            }
            final = result; frames = output; publicationIdentity = issuedIdentity; reason = string.Empty; return true;
        }
        catch (WorldSessionPublicationException published)
        {
            FenceSpellPublishedOutcome(capturedOwner, published);
            throw;
        }
        catch (LegacyCommittedSpellPublicationException)
        {
            context.TradeRecovery?.Block("ordinarySpellRecoveryRequired:itemSpellPublicationUnproven");
            reason = "ordinarySpellRecoveryRequired:itemSpellPublicationUnproven"; return false;
        }
        catch (Exception error) when (IsSpellMutationPreparationFailure(error))
        {
            if (durable) { context.TradeRecovery?.Block("ordinarySpellRecoveryRequired:itemSpellCommittedFault"); reason = "ordinarySpellRecoveryRequired:itemSpellCommittedFault"; }
            else reason = error is LegacySpellMutationRefusalException ? error.Message : "itemSpellPreparation:" + error.GetType().Name;
            return false;
        }
        finally { rankLease?.Dispose(); }
    }
}
