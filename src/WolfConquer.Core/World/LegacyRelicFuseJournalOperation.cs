using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Immutable recipe data, never authority to persist or publish. A future
// journal owner must bind this exact real allocation to its own file images.
internal sealed class LegacyRelicFuseJournalOperation
{
    private readonly ReadOnlyCollection<uint> words;
    public LegacyItemUidReservationRequest Request { get; }
    public LegacyItemUidReservationReceipt Allocation { get; }
    public uint FirstUid { get; }
    public uint SecondUid { get; }
    public uint ResultItemId { get; }
    public byte Bound { get; }
    public ushort Durability { get; }
    public IReadOnlyList<uint> Words => words;

    internal LegacyRelicFuseJournalOperation(LegacyItemUidReservationRequest request,
        LegacyItemUidReservationReceipt allocation, uint firstUid, uint secondUid,
        uint resultItemId, byte bound, ushort durability, uint[] values)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(values);
        if (request.Writer != LegacyItemUidRegistry.RelicFuseWriter || request.Count != 1 ||
            request.OperationId == Guid.Empty || request.AccountId == 0 || request.CharacterUid == 0 ||
            request.PayloadSha256 is not { Length: 64 } ||
            request.PayloadSha256.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            allocation.DomainId == Guid.Empty || allocation.Generation == 0 ||
            allocation.OperationId != request.OperationId || allocation.Writer != request.Writer ||
            allocation.AccountId != request.AccountId || allocation.CharacterUid != request.CharacterUid ||
            allocation.PayloadSha256 != request.PayloadSha256 || allocation.Count != 1 || allocation.First == 0 ||
            firstUid == 0 || secondUid == 0 || firstUid == secondUid || allocation.First == firstUid || allocation.First == secondUid ||
            resultItemId is < 4_100_001 or > 4_100_005 || bound > 1 ||
            durability is < 3000 or > 10000 || durability % 100 != 0 || values.Length != 5)
            throw new InvalidDataException("relicFuseJournalRecipeInvalid");
        Request = request; Allocation = allocation; FirstUid = firstUid; SecondUid = secondUid;
        ResultItemId = resultItemId; Bound = bound; Durability = durability;
        words = Array.AsReadOnly(values.ToArray());
    }
}
