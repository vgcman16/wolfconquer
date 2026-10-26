using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    internal bool TryReconcileRetainedWork(out string reason)
    {
        reason = "mailboxClaimRecoveryRequired:packageReconciliation";
        try
        {
            if (Volatile.Read(ref storeBound) != 0)
            { reason = "mailboxClaimRecoveryRequired:packageFreshOwnerRequired"; return false; }
            if (!IsCurrent(UsersRoot, ItemsRoot, MailboxRoot, Recovery) ||
                !Registry.TryReadActiveProducerIntents(Domain, out var intents, out reason)) return false;
            foreach (var intent in intents.Where(row => row.Request.Writer == Writer))
            {
                if (!TryDescribe(intent, out var description))
                { reason = "mailboxClaimRecoveryRequired:packageRecipeInvalid"; return false; }
                var row = workStore.Read(intent.Request.OperationId);
                if (row is null)
                {
                    var paths = WorkPaths(intent.Request.AccountId, intent.Request.CharacterUid);
                    if (!SnapshotFileLockCoordinator.ExecuteLocked(paths, () => HashesMatch(paths, description!.Before)))
                    { reason = "mailboxClaimRecoveryRequired:packageMissingWorkImagesUnproven"; return false; }
                }
                else if (!workStore.Reconcile(intent, row, out _))
                { reason = "mailboxClaimRecoveryRequired:packageWorkImagesUnproven"; return false; }
            }
            foreach (var row in workStore.ReadPendingNoAllocationRecords())
            {
                if (intents.Any(intent => intent.Request.OperationId.ToString("N") == row.Operation) ||
                    !workStore.ReconcileNoAllocation(row, out _))
                { reason = "mailboxClaimRecoveryRequired:noAllocationWorkImagesUnproven"; return false; }
            }
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:packageReconciliation:" + error.GetType().Name; return false; }
    }

    internal bool HasPendingAccountWork(long account, out bool pending, out string reason)
    {
        pending = true;
        try
        {
            if (account is <= 0 or > uint.MaxValue || !IsCurrent(UsersRoot, ItemsRoot, MailboxRoot, Recovery) ||
                !Registry.TryReadActiveProducerIntents(Domain, out var intents, out reason))
            { reason = "mailboxClaimRecoveryRequired:packageAccountOwner"; return false; }
            pending = awaitingSessionPublication.ContainsKey(account);
            foreach (var intent in intents.Where(row => row.Request.Writer == Writer && row.Request.AccountId == account))
            {
                if (!TryDescribe(intent, out _)) { pending = true; reason = "mailboxClaimRecoveryRequired:packageRecipeInvalid"; return false; }
                var row = workStore.Read(intent.Request.OperationId);
                if (row is null) { pending = true; continue; }
                if (!workStore.MatchesIntent(row, intent))
                { pending = true; reason = "mailboxClaimRecoveryRequired:packageWorkConflict"; return false; }
                if (!row.Completed) pending = true;
            }
            foreach (var row in workStore.ReadPendingNoAllocationRecords().Where(row => row.AccountId == account))
            {
                if (!TryValidateNoAllocationRecord(row, out _) ||
                    intents.Any(intent => intent.Request.OperationId.ToString("N") == row.Operation))
                { pending = true; reason = "mailboxClaimRecoveryRequired:noAllocationWorkConflict"; return false; }
                if (!row.Completed) pending = true;
            }
            reason = pending ? "mailboxClaimRecoveryRequired:packagePending" : string.Empty;
            return true;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { pending = true; reason = "mailboxClaimRecoveryRequired:packageAccountRead:" + error.GetType().Name; return false; }
    }

    internal bool TryRequireAccountWritable(long account, out string reason) =>
        HasPendingAccountWork(account, out var pending, out reason) && !pending;

    internal bool TryDescribe(LegacyItemUidProducerIntentSnapshot intent,
        out LegacyMailboxPackageRecipe.WorkDescription? description) =>
        LegacyMailboxPackageRecipe.TryReadWorkDescription(intent.CopyPayload(), Domain, intent.Request.AccountId,
            intent.Request.CharacterUid, out description) && description!.Count == intent.Request.Count &&
        LegacyMailboxPackageRecipe.TryReadOperationIdentity(intent.CopyPayload(), Domain, intent.Request.AccountId,
            intent.Request.CharacterUid, out var operation) && operation == intent.Request.OperationId;

    private string[] WorkPaths(uint account, uint uid)
    {
        var accountName = account.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var uidName = uid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return [Path.Combine(UsersRoot, accountName + ".ini"), Path.Combine(ItemsRoot, uidName + ".bin"),
            Path.Combine(MailboxRoot, uidName + ".bin"), Path.Combine(CombatGearRoot, uidName + ".ini")];
    }

    private static bool HashesMatch(string[] paths, string[] hashes)
    {
        for (var index = 0; index < paths.Length; index++)
            if (!CharacterSnapshotFileBaseline.TryReadBytes(paths[index], "mailboxPackageWork", out var bytes, out _) ||
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != hashes[index]) return false;
        return true;
    }

    private static bool IsWorkFailure(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or
        ArgumentException or InvalidOperationException or OverflowException or FormatException or JsonException or
        NotSupportedException or System.Security.SecurityException;
}
