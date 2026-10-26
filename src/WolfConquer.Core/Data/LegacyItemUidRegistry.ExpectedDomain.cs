namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // Called only while the actual exclusive process lease is held, before any
    // pending/archive recovery can publish bytes. Legacy open APIs are unchanged.
    private static bool RequireExpectedDomain(RegistryPaths paths, LegacyItemUidRegistryOptions options,
        Guid expected, out string reason)
    {
        reason = "itemUidExpectedDomainMismatch";
        var found = false;
        foreach (var path in new[] { paths.State, paths.Pending })
        {
            if (!File.Exists(path)) continue;
            var retained = ReadState(path);
            if (Guid.ParseExact(retained.Domain, "N") != expected) return false;
            if (!MatchesConfiguration(retained, paths, options))
            { reason = RecoveryRequired + ":configurationChanged"; return false; }
            found = true;
        }
        if (!found) { reason = RecoveryRequired + ":registryStateMissing"; return false; }
        reason = string.Empty; return true;
    }
}
