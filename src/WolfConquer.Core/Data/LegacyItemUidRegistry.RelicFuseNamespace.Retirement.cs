using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal bool TryRetireRelicFuseNamespace(LegacyPreparedSpellFileWrite writer,
        LegacyRelicFuseOwnedEvidence evidence, LegacyItemUidReservationReceipt receipt,
        LegacyItemUidInterruption interruption, out string reason)
    {
        reason = "relicFuseRetirementOwnerUnavailable";
        writer.RequireRelicFuseNamespaceRetirement(this, evidence, receipt);
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            var request = new LegacyItemUidReservationRequest(receipt.OperationId, receipt.Writer, receipt.AccountId,
                receipt.CharacterUid, receipt.PayloadSha256, receipt.Count);
            var row = RequireRelicFuseAllocation(state, request, receipt);
            writer.RequireRelicFuseNamespaceRetirement(this, evidence, receipt);
            return TryRetireRelicFuseNamespaceRow(row, out reason, interruption);
        }
    }

    private bool TryRetireRelicFuseNamespaceRow(OperationReservation row, out string reason,
        LegacyItemUidInterruption interruption = LegacyItemUidInterruption.None)
    {
        reason = "relicFuseNamespaceExhausted";
        if (state.Generation == ulong.MaxValue) return false;
        var operations = state.Operations ?? throw new InvalidDataException("relicFuseNamespaceMissing");
        if (!IsRelicFuseNamespace(row) || operations.Count(candidate => candidate == row) != 1)
            throw new InvalidDataException("relicFuseNamespaceRetirementMismatch");
        var next = state with { Generation = state.Generation + 1, Operations = operations.Where(candidate => candidate != row).ToArray() };
        if (!IsNext(state, next)) throw new InvalidDataException("relicFuseNamespaceRetirementTransitionInvalid");
        if (!Enum.IsDefined(interruption)) throw new InvalidDataException("relicFuseNamespaceInterruptionInvalid");
        if (!TryAdvance(paths, state, next, interruption, out reason))
        {
            if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
            return false;
        }
        state = next; reason = string.Empty; return true;
    }
}
