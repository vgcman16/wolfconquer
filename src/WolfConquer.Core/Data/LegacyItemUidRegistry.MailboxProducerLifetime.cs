using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private LegacyMailboxPackageProducer? mailboxPackageProducer;

    internal bool TryClaimMailboxPackageProducer(LegacyMailboxPackageProducer producer, out string reason)
    {
        lock (gate)
        {
            reason = "mailboxClaimRecoveryRequired:packageProducerLifetime";
            if (!ReferenceEquals(producer.Registry, this) || !TryAdmitProducerOwner(producer.Domain, out reason)) return false;
            if (mailboxPackageProducer is not null)
            { reason = "mailboxClaimRecoveryRequired:packageProducerAlreadyOwned"; return false; }
            mailboxPackageProducer = producer;
            reason = string.Empty;
            return true;
        }
    }

    internal bool IsMailboxPackageProducer(LegacyMailboxPackageProducer producer)
    { lock (gate) return !disposed && ReferenceEquals(mailboxPackageProducer, producer); }
}
