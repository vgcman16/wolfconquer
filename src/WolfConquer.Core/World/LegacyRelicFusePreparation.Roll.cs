using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyRelicFusePreparation
{
    internal WorldDataCatalog Catalog => data;
    internal LegacyRelicFuseInputs Inputs => inputs;

    // Called once by the actual evidence owner after global/native/file
    // preflight. No reservation or publication is performed here.
    internal bool TryRoll(PlayerItemSnapshot owner, Random random,
        out LegacyRelicFuseRollData? roll, out string reason)
    {
        ArgumentNullException.ThrowIfNull(random);
        roll = null;
        reason = "relicFusePreparedOwnerChanged";
        if (!IsCurrent(owner)) return false;
        var durability = checked((ushort)(random.Next(30, 101) * 100));
        if (!TryGenerateWords(owner, random, out var words, out reason)) return false;
        if (!IsCurrent(owner)) { reason = "relicFusePreparedOwnerChanged"; return false; }
        if (!LegacyRelicFuseRollData.TryFromRecorded(inputs.First.Uid, inputs.Second.Uid, inputs.First.ItemId,
            inputs.ResultBound, durability, words[0], words[1], words[2], words[3], words[4], out roll))
        { reason = "relicFuseGeneratedOutcomeInvalid"; return false; }
        reason = string.Empty;
        return true;
    }
}
