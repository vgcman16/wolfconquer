using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryRetireRuneDecomposeNamespace(LegacyPreparedSpellFileWrite.RuneCharacterPublication proof,
        LegacyItemUidReservationReceipt receipt, LegacyItemUidInterruption interruption, out string reason)
    {
        // This concrete proof checks its actual committed writer and complete
        // saved tuple before the Registry gate; it must not acquire Store here.
        try { proof.RequireDecomposeNamespaceRetirement(this, receipt); }
        catch (Exception error) when (IsOwnedStartupFailure(error))
        {
            reason = RecoveryRequired + ":decomposeNamespaceCommitProof:" + error.GetType().Name;
            lock (gate) blocked = reason;
            return false;
        }
        lock (gate)
        {
            try
            {
                RequireRuneDecomposeStateCurrent();
                if (LegacyTradeJournal.HasPending(paths.Users))
                    throw new InvalidDataException("Decompose namespace retirement cannot precede its game commit.");
                var request = RequestFromReceipt(receipt);
                var row = RequireRuneDecomposeAllocation(state, request, receipt);
                if (TryRetireRuneDecomposeNamespaceRow(row, out reason, interruption)) return true;
                // Gameplay is already durable. Even a known clean UID-write
                // refusal cannot return this live producer to ordinary work.
                blocked = RecoveryRequired + ":decomposeNamespaceRetirementIncomplete:" + reason;
                reason = blocked; return false;
            }
            catch (Exception error) when (IsOwnedStartupFailure(error))
            {
                reason = RecoveryRequired + ":decomposeNamespaceRetirement:" + error.GetType().Name;
                blocked = reason; return false;
            }
        }
    }

    internal void BlockRuneDecomposeNamespace()
    { lock (gate) blocked = RecoveryRequired + ":decomposeAfterNamespaceReservation"; }

    private bool TryRetireRuneDecomposeNamespaceRow(OperationReservation row, out string reason,
        LegacyItemUidInterruption interruption = LegacyItemUidInterruption.None)
    {
        reason = "itemUidNamespaceExhausted";
        if (!Enum.IsDefined(interruption)) throw new InvalidDataException("Unknown Decompose namespace interruption.");
        if (state.Generation == ulong.MaxValue) return false;
        var operations = state.Operations ?? throw new InvalidDataException("Decompose namespace rows are missing.");
        if (!IsRuneDecomposeNamespace(row) || operations.Count(candidate => candidate == row) != 1)
            throw new InvalidDataException("Decompose namespace retirement requires its one exact row.");
        var next = state with { Generation = state.Generation + 1,
            Operations = operations.Where(candidate => candidate != row).ToArray() };
        if (!IsNext(state, next)) throw new InvalidDataException("Invalid Decompose namespace retirement transition.");
        if (!TryAdvance(paths, state, next, interruption, out reason))
        {
            if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
            return false;
        }
        state = next; reason = string.Empty; return true;
    }

    private static LegacyItemUidReservationRequest RequestFromReceipt(LegacyItemUidReservationReceipt receipt) =>
        new(receipt.OperationId, receipt.Writer, receipt.AccountId, receipt.CharacterUid, receipt.PayloadSha256, receipt.Count);

    private void RequireRuneDecomposeStateCurrent()
    {
        if (!Monitor.IsEntered(gate)) throw new InvalidOperationException("Decompose namespace requires the actual Registry gate.");
        try
        {
            if (disposed || blocked is not null || processLease.SafeFileHandle.IsClosed ||
                processLease.SafeFileHandle.IsInvalid || !processLease.CanRead || !processLease.CanWrite)
                throw new InvalidDataException("Decompose namespace owner is not current.");
            _ = processLease.Length;
            EnsureOrdinaryPath(paths.Lease); EnsureOrdinaryPath(paths.Pending); EnsureOrdinaryPath(paths.ArchivePending);
            if (!File.Exists(paths.Lease) || !TryValidateDirectory(paths, out _) ||
                File.Exists(paths.Pending) || Directory.Exists(paths.Pending) ||
                File.Exists(paths.ArchivePending) || Directory.Exists(paths.ArchivePending) ||
                !SameState(ReadState(paths.State), state))
                throw new InvalidDataException("Decompose namespace state or transition changed.");
            _ = CurrentArchives;
        }
        catch (Exception error) when (IsOwnedStartupFailure(error))
        {
            blocked = RecoveryRequired + ":decomposeNamespaceState:" + error.GetType().Name;
            throw;
        }
    }
}
