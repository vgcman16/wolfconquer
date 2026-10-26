using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private LegacyOrdinaryAdmissionUidOwner? ordinaryAdmissionOwner;
    internal bool TryClaimOrdinaryAdmissionOwner(LegacyOrdinaryAdmissionUidOwner owner, out string reason)
    {
        lock (gate)
        {
            reason = "ordinaryAdmissionUidOwnerAlreadyClaimed";
            if (owner is null || ordinaryAdmissionOwner is not null) return false;
            if (!ReferenceEquals(owner.Registry, this) || owner.Roots.Items is null ||
                !TryValidateProducerBinding(owner.Domain, owner.Roots.Users, owner.Roots.Items, owner.MailboxRoot, out reason)) return false;
            ordinaryAdmissionOwner = owner; reason = string.Empty; return true;
        }
    }
    internal bool IsOrdinaryAdmissionOwner(LegacyOrdinaryAdmissionUidOwner owner)
    { lock (gate) return !disposed && blocked is null && ReferenceEquals(ordinaryAdmissionOwner, owner); }
}
