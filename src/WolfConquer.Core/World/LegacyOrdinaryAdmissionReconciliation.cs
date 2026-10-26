using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Namespace metadata can only refuse an unconfigured writer. Actual acceptance
// still requires the claimed live producer whose factory reconciled real work.
internal static class LegacyOrdinaryAdmissionReconciliation
{
    internal static bool NamespaceMayExist(CharacterSnapshotLoader loader)
    {
        if (loader.PlayerItemsRoot is not { } items) return false;
        try
        {
            _ = File.GetAttributes(Path.Combine(items, ".managed-item-uids"));
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException) { return true; }
    }

    internal static bool TryRequireOwner(CharacterSnapshotLoader loader, LegacyOrdinaryAdmissionUidOwner? owner, out string reason)
    {
        reason = "ordinaryAdmissionReconciliationOwnerMissing";
        if (owner is null)
        {
            if (NamespaceMayExist(loader)) return false;
            reason = string.Empty; return true;
        }
        if (!owner.IsCurrent(LegacyOrdinaryAdmissionRoots.FromLoader(loader))) return false;
        reason = string.Empty; return true;
    }
}
