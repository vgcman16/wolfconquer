using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneDecomposeRewardMetadata(uint ItemId, ushort MaximumStackSize,
    ushort? Durability, uint TimeMinutes);

// Captured recipe input, never a live issuer, allocation token or completion.
internal sealed record LegacyRuneDecomposeOperation(uint SelectedUid, LegacyRuneDecomposeProgressRow Progress,
    LegacyRuneMetadata[] RuneMetadata, LegacyRuneDecomposeRewardMetadata[] RewardMetadata,
    LegacyItemUidReservationRequest Request, LegacyItemUidReservationReceipt Allocation, byte? FragmentColor)
{
    internal void Validate()
    {
        if (SelectedUid == 0 || Progress is null || Progress.Family is < 401 or > 403 || Progress.Level is 0 or > 27 ||
            RuneMetadata is null || RuneMetadata.Length > ushort.MaxValue || RewardMetadata is null || RewardMetadata.Length != 2 ||
            FragmentColor is not null && FragmentColor is < 3 or > 8 || Request is null || Allocation is null ||
            Request.OperationId == Guid.Empty || Request.Writer != LegacyItemUidRegistry.RuneDecomposeWriter ||
            Request.AccountId == 0 || Request.CharacterUid == 0 || Request.Count is < 1 or > 101 ||
            Request.PayloadSha256 is not { Length: 64 } || Request.PayloadSha256.Any(value => value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            Allocation.DomainId == Guid.Empty || Allocation.OperationId != Request.OperationId || Allocation.Writer != Request.Writer ||
            Allocation.AccountId != Request.AccountId || Allocation.CharacterUid != Request.CharacterUid ||
            Allocation.PayloadSha256 != Request.PayloadSha256 || Allocation.Count != Request.Count || Allocation.First == 0 ||
            Allocation.Generation == 0 || (ulong)Allocation.First + (uint)Allocation.Count - 1 > uint.MaxValue)
            throw new InvalidDataException("Invalid recorded Rune decomposition inputs.");
        if (RuneMetadata.Length != 0) LegacyRuneOperation.ValidateMetadata(RuneMetadata);
        uint previous = 0;
        foreach (var row in RewardMetadata)
        {
            bool essence = row is not null && row.ItemId is 4_040_001 or 4_050_001 or 4_060_001;
            bool fragment = row is not null && row.ItemId is >= 3_306_366 and <= 3_306_371;
            if (row is null || row.ItemId <= previous || !essence && !fragment || row.TimeMinutes != 0 ||
                essence != (row.Durability is null))
                throw new InvalidDataException("Invalid, timed, duplicate or unordered Rune reward metadata.");
            previous = row.ItemId;
        }
    }
}
