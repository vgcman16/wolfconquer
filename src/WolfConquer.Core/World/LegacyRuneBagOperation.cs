using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Recorded recipe data is not a live allocation or publication capability.
internal sealed record LegacyRuneBagOperation(bool IntoBag, uint SelectedUid, LegacyRuneBagMetadata Metadata,
    LegacyItemUidReservationRequest? Request, LegacyItemUidReservationReceipt? Allocation)
{
    internal void Validate()
    {
        if (SelectedUid == 0 || Metadata is null || Metadata.ItemId == 0 || (Request is null) != (Allocation is null))
            throw new InvalidDataException("Invalid recorded direct RuneBag recipe.");
        Metadata.Validate();
        if (Request is null) return;
        var receipt = Allocation!;
        if (IntoBag || Request.OperationId == Guid.Empty || Request.Writer != LegacyItemUidRegistry.RuneBagWriter ||
            Request.AccountId == 0 || Request.CharacterUid == 0 || Request.Count != 1 ||
            Request.PayloadSha256 is not { Length: 64 } || Request.PayloadSha256.Any(value => value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            receipt.DomainId == Guid.Empty || receipt.OperationId != Request.OperationId || receipt.Writer != Request.Writer ||
            receipt.AccountId != Request.AccountId || receipt.CharacterUid != Request.CharacterUid ||
            receipt.PayloadSha256 != Request.PayloadSha256 || receipt.Count != 1 || receipt.First == 0 || receipt.Generation == 0)
            throw new InvalidDataException("Invalid recorded RuneBag namespace receipt.");
    }
}
