using WolfConquer.Core.Client;

namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private Chinese7937XuanbaoRuntimeRules? selected7937XuanbaoRules;

    public Chinese7937XuanbaoRuntimeRules? Selected7937XuanbaoRules => Volatile.Read(ref selected7937XuanbaoRules);

    /// <summary>Uses immutable selected rows in captured server order when explicitly bound.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> EffectiveXuanbaoRandomAttributesByItemTypeId =>
        Selected7937XuanbaoRules?.RandomAttributesByItemTypeId ?? XuanbaoRandomAttributesByItemTypeId;

    public bool TryGetEffectiveXuanbaoComposeLimit(int id, out XuanbaoComposeAttributeLimit? row)
    {
        if (Selected7937XuanbaoRules is { } selected) return selected.TryGetComposeLimit(id, out row);
        return XuanbaoComposeAttributeLimitsById.TryGetValue(id, out row);
    }

    public bool TryBindChinese7937XuanbaoRules(Chinese7937XuanbaoRuleCatalogs source, out string reason)
    {
        lock (relicFuseRulesPublicationSync)
        {
            if (activeRelicFuseRuleLeases != 0) { reason = "relicFuseRuleGenerationInUse"; return false; }
            if (!Chinese7937XuanbaoRuntimeRules.TryPrepare(this, source, out var prepared, out reason)) return false;
            var prior = selected7937XuanbaoRules;
            if (prior is not null)
            {
                if (ReferenceEquals(prior.Source, source)) return true;
                reason = "selected7937RuleCatalogAlreadyBound"; return false;
            }
            if (!TryPrepareSelectedRelicFuseRules(selected7937XuanbaoAttributes, prepared, out var next, out reason)) return false;
            Volatile.Write(ref selected7937XuanbaoRules, prepared);
            Volatile.Write(ref currentRelicFuseRules, next);
            return true;
        }
    }
}
