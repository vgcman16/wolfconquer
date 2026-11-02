using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

/// <summary>Input and published metadata preparation only; no UID or transaction authority.</summary>
internal sealed partial class LegacyRelicFusePreparation
{
    private readonly WorldDataCatalog data;
    private readonly LegacyRelicFuseInputs inputs;
    private readonly LegacyRelicFuseRuleGeneration rules;
    private readonly byte[] beforeImage;
    private readonly bool[] allowed;
    private readonly int[] caps;
    private readonly int[] candidates;
    private readonly AttributeValue[] first, second;

    private LegacyRelicFusePreparation(WorldDataCatalog data, LegacyRelicFuseInputs inputs,
        LegacyRelicFuseRuleGeneration rules, byte[] beforeImage,
        bool[] allowed, int[] caps, AttributeValue[] first, AttributeValue[] second)
    {
        this.data = data; this.inputs = inputs; this.rules = rules; this.beforeImage = beforeImage;
        this.allowed = allowed; this.caps = caps; this.first = first; this.second = second;
        candidates = Enumerable.Range(1, 14).Where(type => allowed[type]).ToArray();
    }

    internal static bool TryPrepare(WorldDataCatalog data, LegacyRelicFuseInputs inputs,
        out LegacyRelicFusePreparation? preparation, out string reason)
    {
        preparation = null;
        reason = string.Empty;
        if (data is null || inputs is null) { reason = "relicFuseRealMetadataAndInputsRequired"; return false; }
        if (inputs.MaximumUid == uint.MaxValue) { reason = "relicFuseLocalUidSpaceExhausted"; return false; }
        if (inputs.MaximumDepth > 1) { reason = "relicFuseItemGraphNotPersistable"; return false; }
        var rules = data.LegacyRelicFuseRules;
        if (rules is null || !rules.IsCurrent(data) ||
            !rules.TryCopyFor(inputs.First.ItemId, out var allowed, out var caps) ||
            !rules.TryCopyFor(inputs.Second.ItemId, out _, out _))
        { reason = "relicFusePublishedRuleOwnerRequired"; return false; }
        if (!TryDecodeWords(inputs.First, out var first) || !TryDecodeWords(inputs.Second, out var second))
        { reason = "relicFuseAttributeEncodingInvalid"; return false; }
        if (!LegacyRelicFuseImages.TryCapture(inputs.Owner, addsResult: true, out var image, out reason)) return false;
        var prepared = new LegacyRelicFusePreparation(data, inputs, rules, image, allowed, caps, first, second);
        if (!prepared.IsCurrent(inputs.Owner)) { reason = "relicFusePreparedOwnerChanged"; return false; }
        preparation = prepared;
        return true;
    }

    internal bool IsCurrent(PlayerItemSnapshot owner) => rules.IsCurrent(data) && inputs.IsCurrent(owner);
    internal byte[] CopyBeforeImage() => beforeImage.ToArray();
    internal LegacyRelicFuseRuleGeneration RuleGeneration => rules;
}
