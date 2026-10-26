namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageProducer
{
    internal bool TryRequireCharacterWritable(uint uid, out string reason)
    {
        reason = "mailboxClaimRecoveryRequired:packageCharacterOwner";
        if (!IsCurrent(UsersRoot, ItemsRoot, MailboxRoot, Recovery) ||
            !Registry.TryReadProducerIntents(Domain, out var intents, out reason)) return false;
        foreach (var account in intents.Where(row => row.Request.Writer == Writer && row.Request.CharacterUid == uid)
            .Select(row => row.Request.AccountId).Distinct())
            if (!TryRequireAccountWritable(account, out reason)) return false;
        reason = string.Empty;
        return true;
    }
}
