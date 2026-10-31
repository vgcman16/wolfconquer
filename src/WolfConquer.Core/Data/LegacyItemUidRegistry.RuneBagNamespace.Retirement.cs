using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryRetireRuneBagNamespace(LegacyPreparedSpellFileWrite.RuneCharacterPublication proof,
        LegacyItemUidReservationReceipt receipt, LegacyItemUidInterruption interruption, out string reason)
    {
        // The concrete committed-writer proof must not acquire Store while
        // Runtime is held. Its complete saved tuple precedes the Registry gate.
        try { proof.RequireRuneBagNamespaceRetirement(this, receipt); }
        catch (Exception error) when (IsOwnedStartupFailure(error))
        {
            reason = RecoveryRequired + ":runeBagNamespaceCommitProof:" + error.GetType().Name;
            lock (gate) blocked = reason;
            return false;
        }
        lock (gate)
        {
            try
            {
                RequireRuneDecomposeStateCurrent();
                if (LegacyTradeJournal.HasPending(paths.Users))
                    throw new InvalidDataException("Rune bag namespace retirement cannot precede its game commit.");
                var row = RequireRuneBagAllocation(state, RequestFromReceipt(receipt), receipt);
                if (TryRetireRuneBagNamespaceRow(row, out reason, interruption)) return true;
                blocked = RecoveryRequired + ":runeBagNamespaceRetirementIncomplete:" + reason;
                reason = blocked; return false;
            }
            catch (Exception error) when (IsOwnedStartupFailure(error))
            {
                reason = RecoveryRequired + ":runeBagNamespaceRetirement:" + error.GetType().Name;
                blocked = reason; return false;
            }
        }
    }

    internal void BlockRuneBagNamespace()
    { lock (gate) blocked = RecoveryRequired + ":runeBagAfterNamespaceReservation"; }

    private bool TryRetireRuneBagNamespaceRow(OperationReservation row, out string reason,
        LegacyItemUidInterruption interruption = LegacyItemUidInterruption.None)
    {
        reason = "itemUidNamespaceExhausted";
        if (!Enum.IsDefined(interruption)) throw new InvalidDataException("Unknown Rune bag namespace interruption.");
        if (state.Generation == ulong.MaxValue) return false;
        var operations = state.Operations ?? throw new InvalidDataException("Rune bag namespace rows are missing.");
        if (!IsRuneBagNamespace(row) || operations.Count(candidate => candidate == row) != 1)
            throw new InvalidDataException("Rune bag namespace retirement requires its one exact row.");
        var next = state with { Generation = state.Generation + 1,
            Operations = operations.Where(candidate => candidate != row).ToArray() };
        if (!IsNext(state, next)) throw new InvalidDataException("Invalid Rune bag namespace retirement transition.");
        if (!TryAdvance(paths, state, next, interruption, out reason))
        {
            if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
            return false;
        }
        state = next; reason = string.Empty; return true;
    }
}
