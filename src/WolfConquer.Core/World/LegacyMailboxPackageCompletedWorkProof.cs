using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// A completed immutable operation may retire active UID metadata. This proof
// cannot allocate, materialize rewards, write game files, or publish a session.
internal sealed class LegacyMailboxPackageCompletedWorkProof
{
    private readonly LegacyItemUidProducerIntentSnapshot intent;

    private LegacyMailboxPackageCompletedWorkProof(LegacyItemUidProducerIntentSnapshot intent)
    {
        this.intent = new(intent.DomainId, intent.Request, intent.PreparedGeneration,
            intent.CopyPayload(), intent.Allocation);
    }

    internal static bool TryCapture(LegacyMailboxPackageProducer owner, Guid operation,
        out LegacyMailboxPackageCompletedWorkProof? proof, out string reason)
    {
        proof = null;
        if (!owner.TryReadCompletedIntent(operation, out var actual, out reason)) return false;
        proof = new(actual!);
        return true;
    }

    internal bool MatchesProducerIntent(Guid domain, LegacyItemUidProducerIntentSnapshot actual) =>
        domain == intent.DomainId && actual.DomainId == intent.DomainId &&
        actual.Request == intent.Request && actual.PreparedGeneration == intent.PreparedGeneration &&
        actual.Allocation is not null && actual.Allocation == intent.Allocation &&
        actual.CopyPayload().AsSpan().SequenceEqual(intent.CopyPayload());
}
