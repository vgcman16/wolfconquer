namespace WolfConquer.Core.Data;

internal enum LegacyItemUidReservationOutcome { Rejected, Reserved, AlreadyRecorded }

// These identifiers describe a request from a trusted cooperating producer.
// Neither this record nor its digest establishes gameplay/migration authority.
internal sealed record LegacyItemUidReservationRequest(Guid OperationId, string Writer,
    uint AccountId, uint CharacterUid, string PayloadSha256, int Count);

// Reconciliation evidence only. In particular, AlreadyRecorded never issues a
// second materialization capability, even if the original token was not used.
internal sealed record LegacyItemUidReservationReceipt(Guid DomainId, Guid OperationId,
    string Writer, uint AccountId, uint CharacterUid, string PayloadSha256,
    ulong Generation, uint First, int Count);

internal sealed partial class LegacyItemUidRegistry
{
    internal const int MaximumOperationReceipts = 2048;

    private sealed record OperationReservation(string OperationId, string Writer,
        uint AccountId, uint CharacterUid, string PayloadSha256, ulong Generation,
        uint First, int Count);

    internal LegacyItemUidReservationOutcome ReserveForOperation(Guid expectedDomain,
        LegacyItemUidReservationRequest request, out Reservation? reservation,
        out LegacyItemUidReservationReceipt? receipt, out string reason) =>
        ReserveForOperation(expectedDomain, request, LegacyItemUidInterruption.None,
            out reservation, out receipt, out reason);

    // The interruption is an owned test boundary, not a replacement writer.
    internal LegacyItemUidReservationOutcome ReserveForOperation(Guid expectedDomain,
        LegacyItemUidReservationRequest request, LegacyItemUidInterruption interruption,
        out Reservation? reservation, out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        lock (gate)
        {
            reservation = null;
            receipt = null;
            reason = "itemUidOperationInvalid";
            if (disposed) { reason = "itemUidRegistryDisposed"; return LegacyItemUidReservationOutcome.Rejected; }
            if (blocked is not null) { reason = blocked; return LegacyItemUidReservationOutcome.Rejected; }
            if (!IsValidRequest(request) || !Enum.IsDefined(interruption)) return LegacyItemUidReservationOutcome.Rejected;
            if (expectedDomain == Guid.Empty || expectedDomain != Guid.ParseExact(state.Domain, "N"))
            { reason = "itemUidDomainMismatch"; return LegacyItemUidReservationOutcome.Rejected; }

            var operationId = request.OperationId.ToString("N");
            try
            {
                var history = CurrentArchives;
                if (history.Entries.ContainsKey(operationId))
                {
                    if (!TryReadArchivedIntent(operationId, out _, out reason)) return LegacyItemUidReservationOutcome.Rejected;
                    reason = "itemUidProducerIntentOwnsOperation"; return LegacyItemUidReservationOutcome.Rejected;
                }
            }
            catch (Exception error) when (IsFileFailure(error))
            {
                reason = RecoveryRequired + ":archiveRead:" + error.GetType().Name;
                blocked = reason; return LegacyItemUidReservationOutcome.Rejected;
            }
            // Intent-owned operations never issue a legacy materialization token.
            if (state.ProducerIntents?.Any(row => row.OperationId == operationId) == true)
            { reason = "itemUidProducerIntentOwnsOperation"; return LegacyItemUidReservationOutcome.Rejected; }
            var existing = state.Operations?.FirstOrDefault(row => row.OperationId == operationId);
            if (existing is not null)
                return ReadRecordedOperation(request, existing, out receipt, out reason);
            if ((state.Operations?.Length ?? 0) >= MaximumOperationReceipts)
            { reason = "itemUidOperationLedgerFull"; return LegacyItemUidReservationOutcome.Rejected; }
            if ((ulong)state.HighWater + (uint)request.Count > uint.MaxValue || state.Generation == ulong.MaxValue)
            { reason = "itemUidNamespaceExhausted"; return LegacyItemUidReservationOutcome.Rejected; }

            var row = new OperationReservation(operationId, request.Writer,
                request.AccountId, request.CharacterUid, request.PayloadSha256,
                state.Generation + 1, state.HighWater + 1, request.Count);
            var operations = new OperationReservation[(state.Operations?.Length ?? 0) + 1];
            state.Operations?.CopyTo(operations, 0);
            operations[^1] = row;
            var next = state with { Version = Math.Max(2, state.Version), Generation = row.Generation,
                HighWater = state.HighWater + (uint)request.Count, Operations = operations };
            // Prepare all return objects before crossing the durable boundary.
            var prepared = Reservation.Prepare(this, request.Count);
            var preparedReceipt = CreateReceipt(row);
            if (!TryAdvance(paths, state, next, interruption, out reason))
            {
                if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
                return LegacyItemUidReservationOutcome.Rejected;
            }
            state = next;
            reservation = prepared;
            receipt = preparedReceipt;
            return LegacyItemUidReservationOutcome.Reserved;
        }
    }

    private LegacyItemUidReservationOutcome ReadRecordedOperation(LegacyItemUidReservationRequest request,
        OperationReservation existing, out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        receipt = null;
        reason = "itemUidOperationConflict";
        if (existing.Writer != request.Writer || existing.AccountId != request.AccountId ||
            existing.CharacterUid != request.CharacterUid || existing.PayloadSha256 != request.PayloadSha256 ||
            existing.Count != request.Count) return LegacyItemUidReservationOutcome.Rejected;
        // A replay issues no token, but its receipt must still describe the
        // actual current durable state, rather than a stale in-memory ledger.
        try
        {
            if (!TryValidateDirectory(paths, out reason) || File.Exists(paths.Pending) ||
                !SameState(ReadState(paths.State), state))
            {
                reason = RecoveryRequired + ":operationReplayStateUnproven";
                blocked = reason;
                return LegacyItemUidReservationOutcome.Rejected;
            }
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            reason = RecoveryRequired + ":operationReplayRead:" + exception.GetType().Name;
            blocked = reason;
            return LegacyItemUidReservationOutcome.Rejected;
        }
        receipt = CreateReceipt(existing);
        reason = string.Empty;
        return LegacyItemUidReservationOutcome.AlreadyRecorded;
    }

    private LegacyItemUidReservationReceipt CreateReceipt(OperationReservation row) =>
        new(Guid.ParseExact(state.Domain, "N"), Guid.ParseExact(row.OperationId, "N"),
            row.Writer, row.AccountId, row.CharacterUid, row.PayloadSha256,
            row.Generation, row.First, row.Count);

    private static bool IsValidRequest(LegacyItemUidReservationRequest? request) =>
        request is not null && request.OperationId != Guid.Empty && IsWriterName(request.Writer) &&
        request.AccountId != 0 && request.CharacterUid != 0 && IsDigest(request.PayloadSha256) &&
        request.Count is >= 1 and <= MaximumReservation;

    private static bool IsWriterName(string? value) => value is { Length: >= 1 and <= 64 } &&
        value.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= '0' and <= '9' or '.' or '_' or '-');

    private static bool IsDigest(string? value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
