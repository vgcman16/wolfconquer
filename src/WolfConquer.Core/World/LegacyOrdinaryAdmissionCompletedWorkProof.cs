using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Retirement evidence only, never a materialization token or gameplay callback.
internal sealed class LegacyOrdinaryAdmissionCompletedWorkProof
{
    private readonly LegacyItemUidProducerIntentSnapshot intent;
    private LegacyOrdinaryAdmissionCompletedWorkProof(LegacyItemUidProducerIntentSnapshot actual) =>
        intent = new(actual.DomainId, actual.Request, actual.PreparedGeneration, actual.CopyPayload(), actual.Allocation);

    internal static bool TryCapture(LegacyOrdinaryAdmissionUidOwner owner, Guid operation,
        out LegacyOrdinaryAdmissionCompletedWorkProof? proof, out string reason)
    {
        proof = null;
        if (!owner.TryReadCompletedIntent(operation, out var actual, out reason) || actual is null) return false;
        proof = new(actual); return true;
    }

    internal bool MatchesProducerIntent(Guid domain, LegacyItemUidProducerIntentSnapshot actual) =>
        domain == intent.DomainId && actual.DomainId == intent.DomainId && actual.Request == intent.Request &&
        actual.PreparedGeneration == intent.PreparedGeneration && actual.Allocation is not null &&
        actual.Allocation == intent.Allocation && actual.CopyPayload().AsSpan().SequenceEqual(intent.CopyPayload());
}
