using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCommitLegacyFreeSpells(WorldPacketHandlerContext context, WorldSession expected,
        PlayerSpellSnapshot proposed, CancellationToken cancellationToken, out WorldSession final, out string reason)
        => TryCommitLegacyFreeSpellsForCapturedIdentity(context, expected, proposed, null, cancellationToken, out final, out reason);

    private static bool TryCommitLegacyFreeSpellsForCapturedIdentity(WorldPacketHandlerContext context, WorldSession expected,
        PlayerSpellSnapshot proposed, object? expectedIdentity, CancellationToken cancellationToken, out WorldSession final, out string reason)
        => TryCommitLegacyFreeSpellsCore(context, expected, proposed, expectedIdentity, null, cancellationToken, out final, out reason);

    private static bool TryCommitLegacyFreeSpellsCore(WorldPacketHandlerContext context, WorldSession expected,
        PlayerSpellSnapshot proposed, object? expectedIdentity, LegacyShockCasterOwner? expectedCaster,
        CancellationToken cancellationToken, out WorldSession final, out string reason)
    {
        final = expected; reason = "spellMutationOwnerUnavailable";
        var result = expected; var failure = reason;
        LegacySpellFileOwner? capturedOwner = null;
        bool Apply()
        {
            if (!LegacySpellFileOwner.TryCapture(context, expected, paid: false, out var owner, out failure)) return false;
            if (expectedIdentity is not null && !owner!.MatchesIdentity(expectedIdentity))
            { failure = "spellMutationOriginalIssuanceChanged"; return false; }
            if (expectedCaster is not null && !expectedCaster.MatchesCapturedFileOwner(owner!))
            { failure = "spellMutationOriginalPhysicalOwnerChanged"; return false; }
            capturedOwner = owner;
            var inputs = new LegacySpellMutationInputs(expected, captureItems: false);
            var source = inputs.Spells;
            var after = new PlayerSpellSnapshot(new LegacyPerformerSpellRows(new LegacyPerformerSpellSource(proposed.Spells).Rows));
            var spellWrite = context.Sessions!.PrepareSpellFileWrite(context.Connection, expected,
                owner!.Recovery, owner.UsersRoot, owner.SpellsRoot);
            spellWrite.PrepareImage(after);
            if (!owner.IsCurrent() || !source.IsCurrent()) { failure = "spellMutationOwnerChanged"; return false; }
            inputs.ArmPlainProof();
            var changed = expected with { PlayerSpells = after };
            var committed = context.Sessions!.TryCommitSession(context.Connection, expected, changed, () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!owner.IsCurrent() || !source.IsCurrent() || !inputs.ConsumeCurrentPlain())
                { failure = "spellMutationInputsChanged"; return false; }
                return spellWrite.TryReplaceSpellsAtomically(out failure);
            }, out var published);
            if (committed)
            {
                if (published is null)
                {
                    owner.Recovery.Block("ordinarySpellRecoveryRequired:spellSessionPublicationUnproven");
                    throw new LegacyCommittedSpellPublicationException();
                }
                result = published;
            }
            return committed;
        }
        try
        {
            bool success;
            if (context.Trades is { } trades) { lock (trades.SyncRoot) success = Apply(); }
            else success = Apply();
            final = result; reason = failure; return success;
        }
        catch (WorldSessionPublicationException published)
        {
            FenceSpellPublishedOutcome(capturedOwner, published);
            throw;
        }
        catch (LegacyCommittedSpellPublicationException)
        {
            capturedOwner?.Recovery.Block("ordinarySpellRecoveryRequired:spellPublicationUnproven");
            throw;
        }
        catch (Exception error) when (IsSpellMutationPreparationFailure(error))
        { reason = error is LegacySpellMutationRefusalException ? error.Message : "spellMutationPreparation:" + error.GetType().Name; return false; }
    }

    private static void FenceSpellPublishedOutcome(LegacySpellFileOwner? owner, WorldSessionPublicationException published)
    {
        try { owner?.Recovery.Block("ordinarySpellRecoveryRequired:spellPublishedOutcomeUnproven"); }
        catch (Exception secondary) { published.RecordFencingFailure(secondary); }
    }

    private static bool IsSpellMutationPreparationFailure(Exception error) => error is not WorldSessionPublicationException &&
        error is (IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or
            InvalidOperationException or FormatException or OverflowException or System.Security.SecurityException);
}
