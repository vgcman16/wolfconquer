using WolfConquer.Core.Client;

namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private readonly object relicFuseRulesPublicationSync = new();
    private LegacyRelicFuseRuleGeneration? defaultRelicFuseRules;
    private LegacyRelicFuseRuleGeneration? currentRelicFuseRules;
    private int activeRelicFuseRuleLeases;
    internal LegacyRelicFuseRuleGeneration? LegacyRelicFuseRules => Volatile.Read(ref currentRelicFuseRules);

    // Constructor-owned capture. Optional invalid feature data cannot publish
    // an accepting rule owner and does not prevent unrelated catalog creation.
    private void InitializeLegacyRelicFuseRules()
    {
        try { defaultRelicFuseRules = LegacyRelicFuseRuleGeneration.CaptureDefault(this); }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or
            InvalidOperationException or OverflowException) { defaultRelicFuseRules = null; }
        currentRelicFuseRules = defaultRelicFuseRules;
    }

    // Prepare against prospective source references before either once-bound
    // pointer changes. The publication lock is shared with the real lease.
    private bool TryPrepareSelectedRelicFuseRules(Chinese7937XuanbaoAttributeCatalog? gold,
        Chinese7937XuanbaoRuntimeRules? rules, out LegacyRelicFuseRuleGeneration? next, out string reason)
    {
        if (!Monitor.IsEntered(relicFuseRulesPublicationSync))
            throw new InvalidOperationException("Relic generation preparation requires its publication owner.");
        next = null; reason = string.Empty;
        try { next = defaultRelicFuseRules?.Select(gold, rules); }
        catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException or OverflowException)
        { reason = "relicFuseRuleSuccessorUnavailable"; return false; }
        // Catalogs without an accepting generation remain free to bind data
        // for unrelated consumers; this cannot create an accepting owner.
        if (currentRelicFuseRules is not null && next is null)
        { reason = "relicFuseRuleSuccessorUnavailable"; return false; }
        return true;
    }
}
