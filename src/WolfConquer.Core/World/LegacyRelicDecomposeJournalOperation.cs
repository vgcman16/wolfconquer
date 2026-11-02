using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Strict immutable recorded DATA, not a reservation or publication capability.
internal sealed class LegacyRelicDecomposeJournalOperation
{
    private readonly byte[] originalFrame;
    private readonly ReadOnlyCollection<LegacyRelicDecomposeSelection> selections;
    public LegacyItemUidReservationRequest Request { get; }
    public LegacyItemUidReservationReceipt Allocation { get; }
    public byte[] OriginalFrame => originalFrame.ToArray();
    public IReadOnlyList<LegacyRelicDecomposeSelection> Selections => selections;
    public ushort RewardDurability { get; }

    internal LegacyRelicDecomposeJournalOperation(LegacyItemUidReservationRequest request,
        LegacyItemUidReservationReceipt allocation, byte[] originalFrame,
        LegacyRelicDecomposeSelection[] selections, ushort rewardDurability)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(originalFrame); ArgumentNullException.ThrowIfNull(selections);
        if (request.Writer != LegacyItemUidRegistry.RelicDecomposeWriter || request.OperationId == Guid.Empty ||
            request.AccountId == 0 || request.CharacterUid == 0 || request.PayloadSha256 is not { Length: 64 } ||
            request.PayloadSha256.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            selections.Length is < 1 or > 49 || request.Count != checked(selections.Length * 9) ||
            originalFrame.Length is < 4 or > ushort.MaxValue || allocation.DomainId == Guid.Empty || allocation.Generation == 0 ||
            allocation.OperationId != request.OperationId || allocation.Writer != request.Writer ||
            allocation.AccountId != request.AccountId || allocation.CharacterUid != request.CharacterUid ||
            allocation.PayloadSha256 != request.PayloadSha256 || allocation.Count != request.Count || allocation.First == 0 ||
            (ulong)allocation.First + (uint)allocation.Count - 1 > uint.MaxValue ||
            selections.Any(row => row.Uid == 0 || row.Bound > 1) || selections.Select(row => row.Uid).Distinct().Count() != selections.Length)
            throw new InvalidDataException("relicDecomposeJournalRecipeInvalid");
        Request = request; Allocation = allocation; this.originalFrame = originalFrame.ToArray();
        this.selections = Array.AsReadOnly(selections.ToArray()); RewardDurability = rewardDurability;
    }
}
