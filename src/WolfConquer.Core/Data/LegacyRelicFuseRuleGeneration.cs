using WolfConquer.Core.Client;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Published by the actual catalog lifecycle. The arrays are private scalar
// copies; a mutable catalog input cannot alter this generation after publication.
internal sealed partial class LegacyRelicFuseRuleGeneration
{
    private readonly int[] caps;
    private readonly bool[][] allowed;
    private readonly WorldDataCatalog owner;
    private LegacyRelicFuseRuleGeneration(WorldDataCatalog owner, int[] caps, bool[][] allowed,
        Chinese7937XuanbaoAttributeCatalog? gold, Chinese7937XuanbaoRuntimeRules? rules)
    { this.owner = owner; this.caps = caps; this.allowed = allowed; GoldSource = gold; RuleSource = rules; }

    internal Chinese7937XuanbaoAttributeCatalog? GoldSource { get; }
    internal Chinese7937XuanbaoRuntimeRules? RuleSource { get; }
    internal bool IsCurrent(WorldDataCatalog actual) => ReferenceEquals(owner, actual) &&
        ReferenceEquals(actual.LegacyRelicFuseRules, this) &&
        ReferenceEquals(GoldSource, actual.Selected7937XuanbaoAttributes) &&
        ReferenceEquals(RuleSource, actual.Selected7937XuanbaoRules);

    internal bool TryCopyFor(uint itemId, out bool[] types, out int[] limits)
    {
        types = []; limits = [];
        if (itemId is < 4_100_001 or > 4_100_005) return false;
        types = allowed[itemId - 4_100_001].ToArray();
        limits = caps.ToArray();
        return true;
    }

    internal static LegacyRelicFuseRuleGeneration? CaptureDefault(WorldDataCatalog owner)
    {
        if (!RealItemGroups(owner) || !TryCopyCaps(owner.XuanbaoComposeAttributeLimits,
            owner.XuanbaoComposeAttributeLimitsById, out var caps)) return null;
        var allowed = new bool[5][];
        for (var item = 0; item < allowed.Length; item++)
        {
            allowed[item] = new bool[15];
            for (var type = 1; type <= 14; type++)
                allowed[item][type] = WorldPacketHandlers.IsAllowedLegacyRelicAttribute(
                    (uint)(4_100_001 + item), (LegacyChiAttributeType)type);
        }
        return RequiredCapsExist(caps, allowed) ? new(owner, caps, allowed, null, null) : null;
    }

    internal LegacyRelicFuseRuleGeneration? Select(Chinese7937XuanbaoAttributeCatalog? gold,
        Chinese7937XuanbaoRuntimeRules? rules)
    {
        var selectedCaps = caps.ToArray();
        if (rules is not null && (!TryCopyCaps(rules.Source.ComposeLimits,
            rules.Source.ComposeLimits.ToDictionary(row => row.Id), out selectedCaps) ||
            !selectedCaps.AsSpan().SequenceEqual(caps))) return null;
        var selectedAllowed = allowed.Select(row => row.ToArray()).ToArray();
        if (gold is not null)
        {
            for (var item = 0; item < selectedAllowed.Length; item++)
            {
                var itemId = (uint)(4_100_001 + item);
                if (!gold.HasGoldLimitItem(itemId)) return null;
                for (var type = 1; type <= 14; type++)
                    selectedAllowed[item][type] = !gold.TryGetGoldLimitFlag(itemId, (uint)type, out var excluded) || !excluded;
            }
        }
        return RequiredCapsExist(selectedCaps, selectedAllowed)
            ? new(owner, selectedCaps, selectedAllowed, gold, rules) : null;
    }

    private static bool RequiredCapsExist(int[] caps, bool[][] allowed) => allowed.All(row =>
        row.Any(value => value) && Enumerable.Range(1, 13).All(type => !row[type] || caps[type] >= 30));
}
