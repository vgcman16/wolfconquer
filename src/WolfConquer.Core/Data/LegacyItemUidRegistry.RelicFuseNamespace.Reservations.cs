using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal LegacyItemUidReservationOutcome ReserveRelicFuseNamespace(
        LegacyOwnedAdmissionRuntime.RelicFuseUidAuthority authority, LegacyItemUidReservationRequest request,
        out Reservation? reservation, out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        lock (gate)
        {
            reservation = null; receipt = null; reason = "relicFuseNamespaceAuthorityRequired";
            if (authority is null || !authority.IsCurrentPlain(this, request) || !IsValidRequest(request) ||
                request.Writer != RelicFuseWriter || request.Count != 1) return LegacyItemUidReservationOutcome.Rejected;
            if (!TryAdmitProducerOwner(authority.Domain, out reason)) return LegacyItemUidReservationOutcome.Rejected;
            var operation = request.OperationId.ToString("N");
            if (CurrentArchives.Entries.ContainsKey(operation) || state.ProducerIntents?.Any(row => row.OperationId == operation) == true)
            { reason = "itemUidProducerIntentOwnsOperation"; return LegacyItemUidReservationOutcome.Rejected; }
            var existing = state.Operations?.FirstOrDefault(row => row.OperationId == operation);
            if (existing is not null)
            {
                if (!MatchesRelicFuseRequest(existing, request)) { reason = "itemUidOperationConflict"; return LegacyItemUidReservationOutcome.Rejected; }
                receipt = CreateReceipt(existing); reason = string.Empty; return LegacyItemUidReservationOutcome.AlreadyRecorded;
            }
            RequireRelicFuseCapacity(authority.Configuration);
            if (!authority.TryPrepareNextUid(this, request, checked(state.HighWater + 1), out reason))
                return LegacyItemUidReservationOutcome.Rejected;
            var row = new OperationReservation(operation, RelicFuseWriter, request.AccountId, request.CharacterUid,
                request.PayloadSha256, state.Generation + 1, state.HighWater + 1, 1, new(1, RelicFuseNamespaceKind));
            var operations = new OperationReservation[(state.Operations?.Length ?? 0) + 1];
            state.Operations?.CopyTo(operations, 0); operations[^1] = row;
            var next = state with { Version = Math.Max(7, state.Version), Generation = row.Generation, HighWater = state.HighWater + 1,
                Operations = operations, ProducerIntents = state.ProducerIntents ?? [] };
            var prepared = Reservation.Prepare(this, 1); var recorded = CreateReceipt(row);
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

    internal void RequireRelicFuseAllocation(LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        lock (gate) { RequireRuneDecomposeStateCurrent(); _ = RequireRelicFuseAllocation(state, request, receipt); }
    }

    private static OperationReservation RequireRelicFuseAllocation(RegistryState current,
        LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        if (current.Version is not (7 or 8) || !IsValidRequest(request) || request.Writer != RelicFuseWriter || request.Count != 1 ||
            receipt is null || receipt.DomainId != Guid.ParseExact(current.Domain, "N") || request.OperationId != receipt.OperationId)
            throw new InvalidDataException("relicFuseAllocationDomainMismatch");
        var row = current.Operations?.SingleOrDefault(value => value.OperationId == request.OperationId.ToString("N"));
        if (row is null || !MatchesRelicFuseRequest(row, request) || receipt.Writer != row.Writer ||
            receipt.AccountId != row.AccountId || receipt.CharacterUid != row.CharacterUid || receipt.PayloadSha256 != row.PayloadSha256 ||
            receipt.Generation != row.Generation || receipt.First != row.First || receipt.Count != row.Count)
            throw new InvalidDataException("relicFuseTypedAllocationChanged");
        return row;
    }

    private static bool MatchesRelicFuseRequest(OperationReservation row, LegacyItemUidReservationRequest request) =>
        IsRelicFuseNamespace(row) && row.OperationId == request.OperationId.ToString("N") && row.Writer == request.Writer &&
        row.AccountId == request.AccountId && row.CharacterUid == request.CharacterUid &&
        row.PayloadSha256 == request.PayloadSha256 && row.Count == request.Count;
}
