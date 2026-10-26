namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // Live registry/domain/root proof, not gameplay or materialization authority.
    internal bool TryValidateProducerBinding(Guid domain, string usersRoot, string itemsRoot,
        string mailboxRoot, out string reason)
    {
        lock (gate)
        {
            reason = "itemUidProducerBindingInvalid";
            try
            {
                if (!SameProducerRoot(paths.Users, usersRoot) || !SameProducerRoot(paths.Items, itemsRoot) ||
                    !SameProducerRoot(paths.Mailbox, mailboxRoot)) return false;
                return TryAdmitProducerOwner(domain, out reason);
            }
            catch (Exception error) when (IsFileFailure(error))
            { reason = "itemUidProducerBindingInvalid:" + error.GetType().Name; return false; }
        }
    }

    private static bool SameProducerRoot(string actual, string expected) =>
        !string.IsNullOrWhiteSpace(expected) && Path.IsPathFullyQualified(expected) &&
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(actual)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
