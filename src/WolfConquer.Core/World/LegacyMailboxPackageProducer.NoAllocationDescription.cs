using System.Security.Cryptography;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    internal bool TryValidateNoAllocationRecord(LegacyMailboxPackageWorkStore.Record row,
        out LegacyMailboxPackageRecipe.WorkDescription? description)
    {
        description = null;
        if (row.Version != 2 || row.Domain != Domain.ToString("N") || row.AllocationKind != "NoAllocation" ||
            row.First != 0 || row.Count != 0 || row.Users != UsersRoot || row.Items != ItemsRoot ||
            row.Mailbox != MailboxRoot || row.CombatGear != CombatGearRoot || row.AccountId == 0 || row.CharacterUid == 0 ||
            row.Before is not { Length: 4 } || row.After is not { Length: 4 } ||
            row.PayloadBase64 is not { Length: >= 4 and <= 10924 }) return false;
        byte[] payload;
        try { payload = Convert.FromBase64String(row.PayloadBase64); }
        catch (FormatException) { return false; }
        if (payload.Length is < 1 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes ||
            Convert.ToBase64String(payload) != row.PayloadBase64 ||
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant() != row.PayloadSha256 ||
            !LegacyMailboxPackageRecipe.TryReadOperationIdentity(payload, Domain, row.AccountId, row.CharacterUid, out var operation) ||
            operation.ToString("N") != row.Operation ||
            !LegacyMailboxPackageRecipe.TryReadWorkDescription(payload, Domain, row.AccountId, row.CharacterUid, out description) ||
            description!.Count != 0 || description.ItemsAfterSha256 is null ||
            !row.Before.SequenceEqual(description.Before, StringComparer.Ordinal) ||
            row.After[0] != description.After[0] || row.After[1] != description.ItemsAfterSha256 ||
            row.After[2] != description.After[2] || row.After[3] != description.After[1]) return false;
        return true;
    }
}
