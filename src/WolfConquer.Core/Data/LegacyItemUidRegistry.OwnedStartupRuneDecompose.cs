namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal sealed partial class OwnedStartupCustody
    {
        internal void RequireRuneDecomposeAllocation(LegacyItemUidReservationRequest request,
            LegacyItemUidReservationReceipt receipt)
        {
            RequireCurrent();
            LegacyItemUidRegistry.RequireRuneDecomposeAllocation(retained, request, receipt);
            RequireCurrent();
        }
    }
}
