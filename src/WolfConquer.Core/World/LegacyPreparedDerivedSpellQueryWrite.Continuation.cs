using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedDerivedSpellQueryWrite
{
    private LegacyExperienceContinuation? pendingContinuation;
    private bool continuationImagesPrepared;

    private void PrepareContinuationParticipation()
    {
        pendingContinuation = LegacyDerivedSpellContinuationImages.RequirePreserved(binding.AccountId,
            checked((uint)binding.Uid), characterBefore, characterAfter);
        if (pendingContinuation is not null) preparation.BindPendingWriter(this, pendingContinuation);
        else if (!preparation.PendingSourceIsCurrentPlain(binding)) throw Invalid();
        continuationImagesPrepared = true;
    }

    internal bool CapturesPendingParticipant(WorldPacketHandlers.OwnedDerivedSpellQueryPreparation actual,
        LegacyExperienceContinuation pending) => ReferenceEquals(preparation, actual) &&
        Equals(pending, LegacyDerivedSpellContinuationImages.RequirePreserved(binding.AccountId,
            checked((uint)binding.Uid), characterBefore, characterAfter)) &&
        Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(preparation.Expected.Character, binding.AccountId)) &&
        Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(Proposed.Character, binding.AccountId));

    private bool PendingBeforeIsCurrent() => !continuationImagesPrepared ? preparation.PendingSourceIsCurrentPlain(binding) :
        pendingContinuation is null ? preparation.PendingSourceIsCurrentPlain(binding) :
            preparation.PendingWriterIsCurrentPlain(binding, this, pendingContinuation);

    private bool ConsumePendingJournalParticipation() => pendingContinuation is null ||
        preparation.TryConsumePendingWriter(binding, this, pendingContinuation);

    private bool PendingJournalIsCurrent() => pendingContinuation is null ||
        preparation.PendingWriterIsCurrentPlain(binding, this, pendingContinuation, requireConsumed: true);

    private bool PendingPublishedIsCurrent(WorldSession actual) => pendingContinuation is null ||
        preparation.PendingPublicationIsCurrentPlain(binding, this, actual, pendingContinuation);

    internal sealed partial class Publication
    {
        internal bool PermitsPendingContinuation(WorldSessionStore store, ConquerTcpConnection connection,
            WorldSession? expected, WorldSession proposed) => writer.pendingContinuation is not null && expected is not null &&
            ReferenceEquals(writer.store, store) && ReferenceEquals(writer.binding.Connection, connection) &&
            ReferenceEquals(writer.preparation.Expected, expected) && writer.BeforeOwnerIsCurrent() &&
            writer.ProposalMatches(proposed) && writer.PendingBeforeIsCurrent();
    }
}
