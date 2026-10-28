using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyOwnedAdmissionRuntime
{
    internal const string OperatorGearMailWriter = "local-gear-mail-v1";

    internal bool TryGetOperatorGearMailConfiguration(WorldServerFrameHandler actualFrame, Guid domain,
        out LegacyOwnedAdmissionOptions routes, out string reason)
    {
        lock (gate)
        {
            routes = configured;
            reason = "operatorMailOwnedRuntimeUnavailable";
            return !closed && !stopping && ReferenceEquals(frame, actualFrame) &&
                domain != Guid.Empty && configured.ExpectedDomain == domain &&
                Ordinary.IsCurrent(configured.Roots) && Ordinary.IsReconciliationComplete &&
                Recovery.IsReadyForOrdinaryAdmission(configured.Roots) &&
                Recovery.IsReadyForMailbox(configured.UsersRoot, configured.ItemsRoot, configured.MailboxRoot) &&
                registry.TryValidateProducerBinding(domain, configured.UsersRoot, configured.ItemsRoot,
                    configured.MailboxRoot, out reason);
        }
    }

    internal LegacyItemUidReservationOutcome ReserveOperatorGearMail(WorldServerFrameHandler actualFrame,
        Guid domain, LegacyItemUidReservationRequest request, out LegacyItemUidRegistry.Reservation? reservation,
        out LegacyItemUidReservationReceipt? receipt, out string reason)
    {
        lock (gate)
        {
            reservation = null;
            receipt = null;
            if (request.Writer != OperatorGearMailWriter || request.Count is < 1 or > 32 ||
                !TryGetOperatorGearMailConfiguration(actualFrame, domain, out _, out reason))
            { reason = "operatorMailOwnedReservationUnavailable"; return LegacyItemUidReservationOutcome.Rejected; }
            return registry.ReserveForOperation(domain, request, out reservation, out receipt, out reason);
        }
    }
}
