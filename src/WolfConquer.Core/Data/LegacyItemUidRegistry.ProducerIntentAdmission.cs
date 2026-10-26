using System.Security.Cryptography;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private bool TryAdmitProducerIntent(Guid domain, LegacyItemUidReservationRequest request, byte[] payload,
        LegacyItemUidInterruption interruption, out byte[] copied, out string reason)
    {
        copied = [];
        reason = "itemUidProducerIntentInvalid";
        if (!IsValidRequest(request) || !Enum.IsDefined(interruption) ||
            payload is null || payload.Length is < 1 or > MaximumProducerPayloadBytes) return false;
        copied = payload.ToArray();
        if (Convert.ToHexString(SHA256.HashData(copied)).ToLowerInvariant() != request.PayloadSha256) return false;
        return TryAdmitProducerOwner(domain, out reason);
    }

    private bool TryAdmitProducerOwner(Guid domain, out string reason)
    {
        reason = "itemUidProducerIntentInvalid";
        if (disposed) { reason = "itemUidRegistryDisposed"; return false; }
        if (blocked is not null) { reason = blocked; return false; }
        if (domain == Guid.Empty || domain != Guid.ParseExact(state.Domain, "N"))
        { reason = "itemUidDomainMismatch"; return false; }
        try
        {
            if (!TryValidateDirectory(paths, out reason) || File.Exists(paths.Pending) ||
                LegacyTradeJournal.HasPending(paths.Users) || !SameState(ReadState(paths.State), state))
            {
                reason = RecoveryRequired + ":producerStateUnproven";
                blocked = reason;
                return false;
            }
        }
        catch (Exception error) when (IsFileFailure(error))
        {
            reason = RecoveryRequired + ":producerRead:" + error.GetType().Name;
            blocked = reason;
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private ProducerIntent? FindProducerIntent(string operationId) =>
        state.ProducerIntents?.FirstOrDefault(row => row.OperationId == operationId);

    private static bool MatchesProducerRequest(ProducerIntent intent,
        LegacyItemUidReservationRequest request, byte[] payload) => intent.Writer == request.Writer &&
        intent.AccountId == request.AccountId && intent.CharacterUid == request.CharacterUid &&
        intent.PayloadSha256 == request.PayloadSha256 && intent.Count == request.Count &&
        intent.PayloadBase64 == Convert.ToBase64String(payload);

    private bool TryReplayProducerIntent(ProducerIntent intent, LegacyItemUidReservationRequest request, byte[] payload,
        out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason)
    {
        snapshot = null;
        reason = "itemUidProducerIntentConflict";
        if (!MatchesProducerRequest(intent, request, payload)) return false;
        snapshot = ProducerSnapshot(intent, state.Operations!.FirstOrDefault(row => row.OperationId == intent.OperationId));
        reason = string.Empty;
        return true;
    }

    private LegacyItemUidProducerIntentSnapshot ProducerSnapshot(ProducerIntent intent, OperationReservation? allocation) =>
        new(Guid.ParseExact(state.Domain, "N"), new(Guid.ParseExact(intent.OperationId, "N"), intent.Writer,
            intent.AccountId, intent.CharacterUid, intent.PayloadSha256, intent.Count), intent.PreparedGeneration,
            Convert.FromBase64String(intent.PayloadBase64), allocation is null ? null : CreateReceipt(allocation));

    private bool TryPublishProducerState(RegistryState next, LegacyItemUidProducerIntentSnapshot prepared,
        LegacyItemUidInterruption interruption, out LegacyItemUidProducerIntentSnapshot? snapshot, out string reason)
    {
        snapshot = null;
        if (!TryAdvance(paths, state, next, interruption, out reason))
        {
            if (reason.StartsWith(RecoveryRequired, StringComparison.Ordinal)) blocked = reason;
            return false;
        }
        state = next;
        snapshot = prepared;
        return true;
    }
}
