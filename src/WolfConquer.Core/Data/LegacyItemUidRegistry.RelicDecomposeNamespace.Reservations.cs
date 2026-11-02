using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal LegacyItemUidReservationOutcome ReserveRelicDecomposeNamespace(
        LegacyOwnedAdmissionRuntime.RelicDecomposeUidAuthority authority, LegacyItemUidReservationRequest request,
        out Reservation? reservation, out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        lock (gate)
        {
            reservation = null; receipt = null; reason = "relicDecomposeNamespaceAuthorityRequired";
            if (authority is null || !authority.IsCurrentPlain(this, request) || !IsValidRequest(request) ||
                request.Writer != RelicDecomposeWriter || !ValidRelicDecomposeCount(request.Count)) return LegacyItemUidReservationOutcome.Rejected;
            if (!TryAdmitProducerOwner(authority.Domain, out reason)) return LegacyItemUidReservationOutcome.Rejected;
            var operation = request.OperationId.ToString("N");
            if (CurrentArchives.Entries.ContainsKey(operation) || state.ProducerIntents?.Any(row => row.OperationId == operation) == true)
            { reason = "itemUidProducerIntentOwnsOperation"; return LegacyItemUidReservationOutcome.Rejected; }
            var existing = state.Operations?.FirstOrDefault(row => row.OperationId == operation);
            if (existing is not null)
            {
                if (!MatchesRelicDecomposeRequest(existing, request)) { reason = "itemUidOperationConflict"; return LegacyItemUidReservationOutcome.Rejected; }
                receipt = CreateReceipt(existing); reason = string.Empty; return LegacyItemUidReservationOutcome.AlreadyRecorded;
            }
            try { RequireRelicDecomposeCapacityPlain(authority.Configuration, request.Count); }
            catch (InvalidDataException error)
            { reason = "relicDecomposeNamespacePreflight:" + error.Message; return LegacyItemUidReservationOutcome.Rejected; }
            if (!authority.TryPrepareNextUids(this, request, checked(state.HighWater + 1), request.Count, out reason))
                return LegacyItemUidReservationOutcome.Rejected;
            var row = new OperationReservation(operation, RelicDecomposeWriter, request.AccountId, request.CharacterUid,
                request.PayloadSha256, state.Generation + 1, state.HighWater + 1, request.Count, new(1, RelicDecomposeNamespaceKind));
            var operations = new OperationReservation[(state.Operations?.Length ?? 0) + 1];
            state.Operations?.CopyTo(operations, 0); operations[^1] = row;
            var next = state with { Version = 8, Generation = row.Generation, HighWater = checked(state.HighWater + (uint)request.Count),
                Operations = operations, ProducerIntents = state.ProducerIntents ?? [] };
            var prepared = Reservation.Prepare(this, request.Count); var recorded = CreateReceipt(row);
            if (!authority.IsCurrentPlain(this, request) ||
                !TryAdvance(paths, state, next, LegacyItemUidInterruption.None, out reason))
            {
                if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
                return LegacyItemUidReservationOutcome.Rejected;
            }
            state = next; reservation = prepared; receipt = recorded; reason = string.Empty;
            return LegacyItemUidReservationOutcome.Reserved;
        }
    }

    internal void RequireRelicDecomposeAllocation(LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        lock (gate) { RequireRuneDecomposeStateCurrent(); _ = RequireRelicDecomposeAllocation(state, request, receipt); }
    }

    private static OperationReservation RequireRelicDecomposeAllocation(RegistryState current,
        LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        if (current.Version != 8 || !IsValidRequest(request) || request.Writer != RelicDecomposeWriter || !ValidRelicDecomposeCount(request.Count) ||
            receipt is null || receipt.DomainId != Guid.ParseExact(current.Domain, "N") || request.OperationId != receipt.OperationId)
            throw new InvalidDataException("relicDecomposeAllocationDomainMismatch");
        var row = current.Operations?.SingleOrDefault(value => value.OperationId == request.OperationId.ToString("N"));
        if (row is null || !MatchesRelicDecomposeRequest(row, request) || receipt.Writer != row.Writer ||
            receipt.AccountId != row.AccountId || receipt.CharacterUid != row.CharacterUid || receipt.PayloadSha256 != row.PayloadSha256 ||
            receipt.Generation != row.Generation || receipt.First != row.First || receipt.Count != row.Count)
            throw new InvalidDataException("relicDecomposeTypedAllocationChanged");
        return row;
    }

    private static bool MatchesRelicDecomposeRequest(OperationReservation row, LegacyItemUidReservationRequest request) =>
        IsRelicDecomposeNamespace(row) && row.OperationId == request.OperationId.ToString("N") && row.Writer == request.Writer &&
        row.AccountId == request.AccountId && row.CharacterUid == request.CharacterUid &&
        row.PayloadSha256 == request.PayloadSha256 && row.Count == request.Count;
}
