using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal LegacyItemUidReservationOutcome ReserveRuneBagNamespace(
        LegacyOwnedAdmissionRuntime.RuneBagNamespaceAuthority authority,
        LegacyItemUidReservationRequest request, out Reservation? reservation,
        out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        lock (gate)
        {
            reservation = null; receipt = null; reason = "runeBagNamespaceAuthorityRequired";
            if (authority is null || !authority.IsCurrentPlain(this, request) || !IsValidRequest(request) ||
                request.Writer != RuneBagWriter || request.Count != 1) return LegacyItemUidReservationOutcome.Rejected;
            if (!TryAdmitProducerOwner(authority.Domain, out reason)) return LegacyItemUidReservationOutcome.Rejected;
            var operation = request.OperationId.ToString("N");
            if (CurrentArchives.Entries.ContainsKey(operation) ||
                state.ProducerIntents?.Any(row => row.OperationId == operation) == true)
            { reason = "itemUidProducerIntentOwnsOperation"; return LegacyItemUidReservationOutcome.Rejected; }
            var existing = state.Operations?.FirstOrDefault(row => row.OperationId == operation);
            if (existing is not null)
            {
                if (!MatchesRuneBagRequest(existing, request))
                { reason = "itemUidOperationConflict"; return LegacyItemUidReservationOutcome.Rejected; }
                receipt = CreateReceipt(existing); reason = string.Empty;
                return LegacyItemUidReservationOutcome.AlreadyRecorded;
            }
            try { RequireRuneBagReservationCapacityPlain(authority.Configuration, 1); }
            catch (InvalidDataException error)
            { reason = "runeBagNamespacePreflight:" + error.Message; return LegacyItemUidReservationOutcome.Rejected; }
            var row = new OperationReservation(operation, RuneBagWriter, request.AccountId, request.CharacterUid,
                request.PayloadSha256, state.Generation + 1, state.HighWater + 1, 1, new(1, RuneBagNamespaceKind));
            var operations = new OperationReservation[(state.Operations?.Length ?? 0) + 1];
            state.Operations?.CopyTo(operations, 0); operations[^1] = row;
            var next = state with { Version = 6, Generation = row.Generation, HighWater = state.HighWater + 1,
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

    // Count0 observes the exact current domain/history only. It does not need
    // receipt-table, generation or UID room and cannot write a namespace row.
    internal void RequireRuneBagReservationCapacity(LegacyOwnedAdmissionOptions configuration, int count)
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            RequireRuneBagReservationCapacityPlain(configuration, count);
        }
    }

    private void RequireRuneBagReservationCapacityPlain(LegacyOwnedAdmissionOptions configuration, int count)
    {
        if (!Monitor.IsEntered(gate)) throw new InvalidOperationException("Rune bag preflight requires the actual Registry gate.");
        if (count is < 0 or > 1) throw new InvalidDataException("Invalid Rune bag registered root count.");
        _ = RequireCompletedOrdinaryHistory(configuration);
        if (count == 0) return;
        if ((state.Operations?.Length ?? 0) >= MaximumOperationReceipts ||
            state.HighWater == uint.MaxValue || state.Generation > ulong.MaxValue - 2)
            throw new InvalidDataException("Rune bag lacks complete allocation/retirement capacity.");
    }

    // The actual Evidence performs the bounded raw scan outside these gates.
    // This typed current-state observation can deny; it never grants an ID or
    // exposes the allocator's high-water as a replacement ownership proof.
    internal void RequireRuneBagObservedMaximum(LegacyRuneBagEvidence evidence, uint observed)
    {
        lock (gate)
        {
            evidence.RequireRegistry(this); // Exact reference proof; no Store acquisition.
            RequireRuneDecomposeStateCurrent();
            if (observed > state.HighWater)
                throw new InvalidDataException("Rune bag physical namespace exceeds its registered range.");
        }
    }

    internal void RequireRuneBagAllocation(LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            _ = RequireRuneBagAllocation(state, request, receipt);
        }
    }

    private static OperationReservation RequireRuneBagAllocation(RegistryState current,
        LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        if (current.Version != 6 || !IsValidRequest(request) || request.Writer != RuneBagWriter || request.Count != 1 || receipt is null ||
            receipt.DomainId == Guid.Empty || receipt.DomainId != Guid.ParseExact(current.Domain, "N") ||
            request.OperationId != receipt.OperationId)
            throw new InvalidDataException("Rune bag allocation request or domain differs from its namespace.");
        var row = current.Operations?.SingleOrDefault(value => value.OperationId == request.OperationId.ToString("N"));
        if (row is null || !MatchesRuneBagRequest(row, request) || receipt.Writer != row.Writer ||
            receipt.AccountId != row.AccountId || receipt.CharacterUid != row.CharacterUid ||
            receipt.PayloadSha256 != row.PayloadSha256 || receipt.Generation != row.Generation ||
            receipt.First != row.First || receipt.Count != row.Count)
            throw new InvalidDataException("Rune bag allocation differs from its exact typed namespace row.");
        return row;
    }

    private static bool MatchesRuneBagRequest(OperationReservation row, LegacyItemUidReservationRequest request) =>
        IsRuneBagNamespace(row) && row.OperationId == request.OperationId.ToString("N") &&
        row.Writer == request.Writer && row.AccountId == request.AccountId && row.CharacterUid == request.CharacterUid &&
        row.PayloadSha256 == request.PayloadSha256 && row.Count == request.Count;
}
