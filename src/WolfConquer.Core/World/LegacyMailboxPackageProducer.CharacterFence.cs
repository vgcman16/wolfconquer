namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    internal bool TryRequireCharacterWritable(uint uid, out string reason)
    {
        reason = "mailboxClaimRecoveryRequired:packageCharacterOwner";
        try
        {
            if (!IsCurrent(UsersRoot, ItemsRoot, MailboxRoot, Recovery) ||
                !Registry.TryReadActiveProducerIntents(Domain, out var intents, out reason)) return false;
            var accounts = intents.Where(row => row.Request.Writer == Writer && row.Request.CharacterUid == uid)
                .Select(row => row.Request.AccountId).Concat(workStore.ReadPendingNoAllocationRecords()
                    .Where(row => row.CharacterUid == uid).Select(row => row.AccountId))
                .Concat(ReadAwaitingPublicationAccounts(uid)).Distinct();
            foreach (var account in accounts)
                if (!TryRequireAccountWritable(account, out reason)) return false;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (IsWorkFailure(error))
        { reason = "mailboxClaimRecoveryRequired:packageCharacterRead:" + error.GetType().Name; return false; }
    }
}
