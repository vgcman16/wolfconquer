namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal sealed partial class OwnedStartupCustody
    {
        internal void RequireRuneBagAllocation(LegacyItemUidReservationRequest request,
            LegacyItemUidReservationReceipt receipt)
        {
            RequireCurrent();
            _ = LegacyItemUidRegistry.RequireRuneBagAllocation(retained, request, receipt);
            RequireCurrent();
        }
    }
}
