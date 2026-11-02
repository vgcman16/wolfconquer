using WolfConquer.Core.Client;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private Chinese7937XuanbaoAttributeCatalog? selected7937XuanbaoAttributes;

    /// <summary>Explicit once-bound read-only reference; no item stats, profile or transaction ownership is replaced.</summary>
    public Chinese7937XuanbaoAttributeCatalog? Selected7937XuanbaoAttributes =>
        Volatile.Read(ref selected7937XuanbaoAttributes);

    public bool TryBindChinese7937XuanbaoAttributes(Chinese7937XuanbaoAttributeCatalog selected, out string reason)
    {
        lock (relicFuseRulesPublicationSync)
        {
            if (activeRelicFuseRuleLeases != 0) { reason = "relicFuseRuleGenerationInUse"; return false; }
            reason = string.Empty;
            if (selected is null) { reason = "selected7937AttributeCatalogRequired"; return false; }
            try { _ = LegacyBossSpecialDeathDropCatalog.FromWorldData(this, selected); }
            catch (Exception error) when (error is InvalidDataException or ArgumentException)
            { reason = "selected7937ActualRelicMetadataRequired"; return false; }
            var prior = selected7937XuanbaoAttributes;
            if (prior is not null)
            {
                if (ReferenceEquals(prior, selected)) return true;
                reason = "selected7937AttributeCatalogAlreadyBound"; return false;
            }
            if (!TryPrepareSelectedRelicFuseRules(selected, selected7937XuanbaoRules, out var next, out reason)) return false;
            // Writers use the same publication lock. Readers reject the brief
            // source/generation mismatch until this prepared pair is visible.
            Volatile.Write(ref selected7937XuanbaoAttributes, selected);
            Volatile.Write(ref currentRelicFuseRules, next);
            return true;
        }
    }
}
