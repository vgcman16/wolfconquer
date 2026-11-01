using WolfConquer.Core.Data;

namespace WolfConquer.Core.Runtime;

public sealed partial class LegacyNativeCombatGearRuntime
{
    internal bool IsCurrentFor(WorldDataCatalog catalog) => proof.MatchesCatalog(catalog);

    internal bool TryCapture(WorldDataCatalog catalog, out LegacyNativeCombatGearTable.MailboxLoadProof? captured)
    {
        captured = null;
        if (!IsCurrentFor(catalog)) return false;
        captured = proof;
        return true;
    }
}
