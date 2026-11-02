namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private readonly object relicFuseRuleLeaseIssuer = new();
    // Only stabilizes this real catalog's rule generation. Inventory ownership,
    // UID reservation and durable/native publication require their own owners.
    internal bool TryAcquireLegacyRelicFuseRuleLease(LegacyRelicFuseRuleGeneration expected,
        out LegacyRelicFuseRuleLease? lease)
    {
        lease = null;
        Monitor.Enter(relicFuseRulesPublicationSync);
        try
        {
            if (expected is null || !expected.IsCurrent(this) || activeRelicFuseRuleLeases == int.MaxValue) return false;
            lease = LegacyRelicFuseRuleLease.Issue(this, expected, relicFuseRuleLeaseIssuer);
            activeRelicFuseRuleLeases++;
            return true;
        }
        finally { if (lease is null) Monitor.Exit(relicFuseRulesPublicationSync); }
    }

    // Synchronous thread-affine lease: never hold it across an await.
    internal sealed class LegacyRelicFuseRuleLease : IDisposable
    {
        private readonly WorldDataCatalog owner;
        private readonly LegacyRelicFuseRuleGeneration generation;
        private readonly int threadId = Environment.CurrentManagedThreadId;
        private bool disposed;
        private LegacyRelicFuseRuleLease(WorldDataCatalog owner, LegacyRelicFuseRuleGeneration generation)
        { this.owner = owner; this.generation = generation; }
        internal static LegacyRelicFuseRuleLease Issue(WorldDataCatalog owner, LegacyRelicFuseRuleGeneration generation,
            object actualPublisher)
        {
            if (!ReferenceEquals(actualPublisher, owner.relicFuseRuleLeaseIssuer) ||
                !Monitor.IsEntered(owner.relicFuseRulesPublicationSync) || !generation.IsCurrent(owner))
                throw new InvalidOperationException("Actual catalog publication owner required for a rule lease.");
            return new(owner, generation);
        }
        internal bool IsCurrent => !disposed && Environment.CurrentManagedThreadId == threadId &&
            Monitor.IsEntered(owner.relicFuseRulesPublicationSync) && owner.activeRelicFuseRuleLeases > 0 && generation.IsCurrent(owner);
        public void Dispose()
        {
            if (disposed) return;
            if (Environment.CurrentManagedThreadId != threadId || !Monitor.IsEntered(owner.relicFuseRulesPublicationSync))
                throw new InvalidOperationException("Relic rule lease must be released by its synchronous acquiring owner.");
            disposed = true;
            owner.activeRelicFuseRuleLeases--;
            Monitor.Exit(owner.relicFuseRulesPublicationSync);
        }
    }
}
