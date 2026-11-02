using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryRetireRelicDecomposeNamespace(LegacyPreparedSpellFileWrite writer,
        LegacyRelicDecomposeOwnedEvidence evidence, LegacyItemUidReservationReceipt receipt,
        LegacyItemUidInterruption interruption, out string reason)
    {
        reason = "relicDecomposeRetirementOwnerUnavailable";
        writer.RequireRelicDecomposeNamespaceRetirement(this, evidence, receipt);
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            var request = new LegacyItemUidReservationRequest(receipt.OperationId, receipt.Writer, receipt.AccountId,
                receipt.CharacterUid, receipt.PayloadSha256, receipt.Count);
            var row = RequireRelicDecomposeAllocation(state, request, receipt);
            writer.RequireRelicDecomposeNamespaceRetirement(this, evidence, receipt);
            return TryRetireRelicDecomposeNamespaceRow(row, out reason, interruption);
        }
    }

    private bool TryRetireRelicDecomposeNamespaceRow(OperationReservation row, out string reason,
        LegacyItemUidInterruption interruption = LegacyItemUidInterruption.None)
    {
        reason = "relicDecomposeNamespaceExhausted";
        if (state.Generation == ulong.MaxValue) return false;
        var operations = state.Operations ?? throw new InvalidDataException("relicDecomposeNamespaceMissing");
        if (!IsRelicDecomposeNamespace(row) || operations.Count(candidate => candidate == row) != 1)
            throw new InvalidDataException("relicDecomposeNamespaceRetirementMismatch");
        var next = state with { Generation = state.Generation + 1, Operations = operations.Where(candidate => candidate != row).ToArray() };
        if (!IsNext(state, next)) throw new InvalidDataException("relicDecomposeNamespaceRetirementTransitionInvalid");
        if (!Enum.IsDefined(interruption)) throw new InvalidDataException("relicDecomposeNamespaceInterruptionInvalid");
        if (!TryAdvance(paths, state, next, interruption, out reason))
        {
            if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
            return false;
        }
        state = next; reason = string.Empty; return true;
    }
}
