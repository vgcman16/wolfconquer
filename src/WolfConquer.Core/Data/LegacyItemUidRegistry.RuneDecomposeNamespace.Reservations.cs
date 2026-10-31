using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal LegacyItemUidReservationOutcome ReserveRuneDecomposeNamespace(
        LegacyOwnedAdmissionRuntime.RuneDecomposeNamespaceAuthority authority,
        LegacyItemUidReservationRequest request, out Reservation? reservation,
        out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        lock (gate)
        {
            reservation = null; receipt = null; reason = "runeDecomposeNamespaceAuthorityRequired";
            if (authority is null || !authority.IsCurrentPlain(this, request) || !IsValidRequest(request) ||
                request.Writer != RuneDecomposeWriter) return LegacyItemUidReservationOutcome.Rejected;
            if (!TryAdmitProducerOwner(authority.Domain, out reason)) return LegacyItemUidReservationOutcome.Rejected;
            var operation = request.OperationId.ToString("N");
            if (CurrentArchives.Entries.ContainsKey(operation) ||
                state.ProducerIntents?.Any(row => row.OperationId == operation) == true)
            { reason = "itemUidProducerIntentOwnsOperation"; return LegacyItemUidReservationOutcome.Rejected; }
            var existing = state.Operations?.FirstOrDefault(row => row.OperationId == operation);
            if (existing is not null)
            {
                if (!MatchesRuneDecomposeRequest(existing, request))
                { reason = "itemUidOperationConflict"; return LegacyItemUidReservationOutcome.Rejected; }
                receipt = CreateReceipt(existing); reason = string.Empty;
                return LegacyItemUidReservationOutcome.AlreadyRecorded;
            }
            try { RequireRuneDecomposeReservationCapacityPlain(authority.Configuration, request.Count); }
            catch (InvalidDataException error)
            { reason = "runeDecomposeNamespacePreflight:" + error.Message; return LegacyItemUidReservationOutcome.Rejected; }
            var row = new OperationReservation(operation, RuneDecomposeWriter, request.AccountId,
                request.CharacterUid, request.PayloadSha256, state.Generation + 1, state.HighWater + 1,
                request.Count, new(1, RuneDecomposeNamespaceKind));
            var rows = new OperationReservation[(state.Operations?.Length ?? 0) + 1];
            state.Operations?.CopyTo(rows, 0); rows[^1] = row;
            var next = state with { Version = Math.Max(5, state.Version), Generation = row.Generation,
                HighWater = state.HighWater + (uint)row.Count, Operations = rows,
                ProducerIntents = state.ProducerIntents ?? [] };
            var prepared = Reservation.Prepare(this, row.Count);
            var recorded = CreateReceipt(row);
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

    // Read-only actual-domain preflight before private Color/request creation.
    // This method issues no UID, token, receipt or gameplay permission.
    internal void RequireRuneDecomposeReservationCapacity(LegacyOwnedAdmissionOptions configuration, int count)
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            RequireRuneDecomposeReservationCapacityPlain(configuration, count);
        }
    }

    private void RequireRuneDecomposeReservationCapacityPlain(LegacyOwnedAdmissionOptions configuration, int count)
    {
        if (!Monitor.IsEntered(gate)) throw new InvalidOperationException("Decompose preflight requires the actual Registry gate.");
        if (count is < 1 or > 101) throw new InvalidDataException("Invalid Decompose registered root count.");
        RequireCompletedOrdinaryHistory(configuration);
        if ((state.Operations?.Length ?? 0) >= MaximumOperationReceipts)
            throw new InvalidDataException("Decompose namespace receipt table is full.");
        // Allocation and its normal retirement each need one durable generation.
        if ((ulong)state.HighWater + (uint)count > uint.MaxValue || state.Generation > ulong.MaxValue - 2)
            throw new InvalidDataException("Decompose namespace lacks its complete allocation and retirement capacity.");
    }

    internal void RequireRuneDecomposeAllocation(LegacyItemUidReservationRequest request,
        LegacyItemUidReservationReceipt receipt)
    {
        lock (gate)
        {
            RequireRuneDecomposeStateCurrent();
            RequireRuneDecomposeAllocation(state, request, receipt);
        }
    }

    private static OperationReservation RequireRuneDecomposeAllocation(RegistryState current,
        LegacyItemUidReservationRequest request, LegacyItemUidReservationReceipt receipt)
    {
        if (!IsValidRequest(request) || receipt is null || receipt.DomainId == Guid.Empty ||
            receipt.DomainId != Guid.ParseExact(current.Domain, "N") || request.OperationId != receipt.OperationId)
            throw new InvalidDataException("Decompose allocation request or domain differs from its namespace.");
        var row = current.Operations?.SingleOrDefault(row => row.OperationId == request.OperationId.ToString("N"));
        if (row is null || !MatchesRuneDecomposeRequest(row, request) ||
            receipt.Writer != row.Writer || receipt.AccountId != row.AccountId ||
            receipt.CharacterUid != row.CharacterUid || receipt.PayloadSha256 != row.PayloadSha256 ||
            receipt.Generation != row.Generation || receipt.First != row.First || receipt.Count != row.Count)
            throw new InvalidDataException("Decompose allocation differs from its exact typed namespace row.");
        return row;
    }

    private static bool MatchesRuneDecomposeRequest(OperationReservation row, LegacyItemUidReservationRequest request) =>
        IsRuneDecomposeNamespace(row) && row.OperationId == request.OperationId.ToString("N") &&
        row.Writer == request.Writer && row.AccountId == request.AccountId && row.CharacterUid == request.CharacterUid &&
        row.PayloadSha256 == request.PayloadSha256 && row.Count == request.Count;
}
