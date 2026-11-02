using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal sealed partial class OwnedStartupCustody
    {
        // Real exclusive startup custody, not an accepting replay callback.
        // This validates one exact generic global operation row; it does not
        // issue another UID or declare a relic journal committed.
        internal void RequireRelicFuseAllocation(LegacyItemUidReservationRequest request,
            LegacyItemUidReservationReceipt receipt)
        {
            RequireCurrent();
            ArgumentNullException.ThrowIfNull(receipt);
            if (!IsValidRequest(request) || request.Writer != RelicFuseWriter || request.Count != 1 ||
                receipt.DomainId != Configuration.ExpectedDomain || receipt.OperationId != request.OperationId ||
                receipt.Writer != request.Writer || receipt.AccountId != request.AccountId ||
                receipt.CharacterUid != request.CharacterUid || receipt.PayloadSha256 != request.PayloadSha256 ||
                receipt.Count != request.Count) throw new InvalidDataException("relicFuseStartupAllocationMismatch");
            _ = LegacyItemUidRegistry.RequireRelicFuseAllocation(retained, request, receipt);
            RequireCurrent();
        }
    }
}
