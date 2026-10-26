namespace WolfConquer.Core.Data;

// Durable work description and range evidence, not a materialization token,
// gameplay authority, completed reward, or permission to publish items.
// A future real reward transaction must rebuild and CAS its identical images.
internal sealed class LegacyItemUidProducerIntentSnapshot
{
    private readonly byte[] payload;
    internal Guid DomainId { get; }
    internal LegacyItemUidReservationRequest Request { get; }
    internal ulong PreparedGeneration { get; }
    internal LegacyItemUidReservationReceipt? Allocation { get; }
    internal byte[] CopyPayload() => payload.ToArray();

    internal LegacyItemUidProducerIntentSnapshot(Guid domain, LegacyItemUidReservationRequest request,
        ulong preparedGeneration, byte[] payload, LegacyItemUidReservationReceipt? allocation)
    {
        DomainId = domain; Request = request; PreparedGeneration = preparedGeneration;
        this.payload = payload.ToArray(); Allocation = allocation;
    }
}

internal sealed partial class LegacyItemUidRegistry
{
    internal const int MaximumProducerIntents = 128;
    internal const int MaximumProducerPayloadBytes = 8192;
    internal const int MaximumTotalProducerPayloadBytes = 256 * 1024;

    private sealed record ProducerIntent(string OperationId, string Writer, uint AccountId,
        uint CharacterUid, string PayloadSha256, int Count, string PayloadBase64, ulong PreparedGeneration);

    internal bool TryPrepareProducerIntent(Guid domain, LegacyItemUidReservationRequest request, byte[] payload,
        out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason) =>
        TryPrepareProducerIntent(domain, request, payload, LegacyItemUidInterruption.None, out snapshot, out reason);

    internal bool TryPrepareProducerIntent(Guid domain, LegacyItemUidReservationRequest request, byte[] payload,
        LegacyItemUidInterruption interruption, out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason)
    {
        lock (gate)
        {
            snapshot = null;
            if (!TryAdmitProducerIntent(domain, request, payload, interruption, out var copied, out reason)) return false;
            var operationId = request.OperationId.ToString("N");
            var existing = FindProducerIntent(operationId);
            if (existing is not null)
                return TryReplayProducerIntent(existing, request, copied, out snapshot, out reason);
            if (!TryReadArchivedIntent(operationId, out var archived, out reason)) return false;
            if (archived is not null)
                return ReplayArchivedIntent(archived, request, copied, out snapshot, out reason);
            if (state.Operations?.Any(row => row.OperationId == operationId) == true)
            { reason = "itemUidProducerIntentAfterReservation"; return false; }
            var intents = state.ProducerIntents ?? [];
            if (intents.Length >= MaximumProducerIntents ||
                intents.Sum(row => Convert.FromBase64String(row.PayloadBase64).Length) + copied.Length > MaximumTotalProducerPayloadBytes)
            { reason = "itemUidProducerIntentLedgerFull"; return false; }
            if (state.Generation == ulong.MaxValue) { reason = "itemUidNamespaceExhausted"; return false; }
            var row = new ProducerIntent(operationId, request.Writer, request.AccountId, request.CharacterUid,
                request.PayloadSha256, request.Count, Convert.ToBase64String(copied), state.Generation + 1);
            var next = state with { Version = Math.Max(3, state.Version), Generation = row.PreparedGeneration,
                Operations = state.Operations ?? [], ProducerIntents = [.. intents, row] };
            var prepared = ProducerSnapshot(row, null);
            return TryPublishProducerState(next, prepared, interruption, out snapshot, out reason);
        }
    }

    internal bool TryReserveProducerIntent(Guid domain, LegacyItemUidReservationRequest request, byte[] payload,
        out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason) =>
        TryReserveProducerIntent(domain, request, payload, LegacyItemUidInterruption.None, out snapshot, out reason);

    internal bool TryReserveProducerIntent(Guid domain, LegacyItemUidReservationRequest request, byte[] payload,
        LegacyItemUidInterruption interruption, out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason)
    {
        lock (gate)
        {
            snapshot = null;
            if (!TryAdmitProducerIntent(domain, request, payload, interruption, out var copied, out reason)) return false;
            var intent = FindProducerIntent(request.OperationId.ToString("N"));
            if (intent is null)
            {
                if (!TryReadArchivedIntent(request.OperationId.ToString("N"), out var archived, out reason)) return false;
                if (archived is not null) return ReplayArchivedIntent(archived, request, copied, out snapshot, out reason);
                reason = "itemUidProducerIntentMissing"; return false;
            }
            if (!MatchesProducerRequest(intent, request, copied)) { reason = "itemUidProducerIntentConflict"; return false; }
            var operations = state.Operations!;
            var existing = operations.FirstOrDefault(row => row.OperationId == intent.OperationId);
            if (existing is not null)
            { snapshot = ProducerSnapshot(intent, existing); reason = string.Empty; return true; }
            if (operations.Length >= MaximumOperationReceipts) { reason = "itemUidOperationLedgerFull"; return false; }
            if (state.Generation == ulong.MaxValue || (ulong)state.HighWater + (uint)request.Count > uint.MaxValue)
            { reason = "itemUidNamespaceExhausted"; return false; }
            var allocation = new OperationReservation(intent.OperationId, intent.Writer, intent.AccountId,
                intent.CharacterUid, intent.PayloadSha256, state.Generation + 1, state.HighWater + 1, intent.Count);
            var next = state with { Generation = allocation.Generation,
                HighWater = state.HighWater + (uint)intent.Count, Operations = [.. operations, allocation] };
            var prepared = ProducerSnapshot(intent, allocation);
            return TryPublishProducerState(next, prepared, interruption, out snapshot, out reason);
        }
    }

    internal bool TryReadProducerIntent(Guid domain, Guid operationId,
        out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason)
    {
        lock (gate)
        {
            snapshot = null;
            reason = "itemUidProducerIntentInvalid";
            if (operationId == Guid.Empty || !TryAdmitProducerOwner(domain, out reason)) return false;
            var intent = FindProducerIntent(operationId.ToString("N"));
            if (intent is null)
            {
                if (!TryReadArchivedIntent(operationId.ToString("N"), out snapshot, out reason)) return false;
                reason = snapshot is null ? "itemUidProducerIntentMissing" : string.Empty;
                return snapshot is not null;
            }
            snapshot = ProducerSnapshot(intent, state.Operations!.FirstOrDefault(row => row.OperationId == intent.OperationId));
            reason = string.Empty;
            return true;
        }
    }

    // Fresh trusted reconciliation owners can discover IDs lost with process
    // memory. Enumeration conveys data only; it never automatically pays a reward.
    internal bool TryReadProducerIntents(Guid domain,
        out LegacyItemUidProducerIntentSnapshot[] snapshots, out string reason)
    {
        lock (gate)
        {
            snapshots = [];
            if (!TryAdmitProducerOwner(domain, out reason)) return false;
            var prepared = (state.ProducerIntents ?? []).Select(intent => ProducerSnapshot(intent,
                state.Operations?.FirstOrDefault(row => row.OperationId == intent.OperationId))).ToList();
            foreach (var key in CurrentArchives.Entries.Keys)
            {
                if (!TryReadArchivedIntent(key, out var archived, out reason)) return false;
                prepared.Add(archived!);
            }
            snapshots = prepared.OrderBy(row => row.PreparedGeneration).ToArray();
            return true;
        }
    }
}
