using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Reuse the existing closed dictionary captures and raw witnesses. This is
    // immutable input evidence, not a second HP/persistence authority.
    internal sealed class BoundHitpointsInputs
    {
        private readonly FullAccountStringMap beforeAttributes, beforePaid, afterAttributes, afterPaid;
        private Dictionary<string, string>.Enumerator baPre, bpPre, aaPre, apPre, baPost, bpPost, aaPost, apPost;
        private readonly FullAccountStringMap finalAttributes, finalPaid;
        private Dictionary<string, string>.Enumerator faPre, fpPre, faPost, fpPost, faPostlude, fpPostlude;
        private bool armed, beforeConsumed, afterConsumed, postludeConsumed;
        internal CharacterSnapshot Before { get; }
        internal CharacterSnapshot After { get; }
        internal BoundHitpointsInputs(CharacterSnapshot before, CharacterSnapshot after)
        {
            beforeAttributes = new(before.Attributes); beforePaid = new(before.PaidNobility);
            afterAttributes = new(after.Attributes); afterPaid = new(after.PaidNobility);
            Before = before with { Attributes = beforeAttributes.Copy, PaidNobility = beforePaid.Copy };
            var stable = after with { Attributes = afterAttributes.Copy, PaidNobility = afterPaid.Copy };
            var attributes = new Dictionary<string, string>(stable.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(stable)) attributes[pair.Key] = pair.Value;
            CharacterSnapshotEpicTrojanSchema.ProjectRuntimeAttributes(attributes, attributes);
            After = stable with { Attributes = attributes };
            finalAttributes = new(After.Attributes); finalPaid = new(After.PaidNobility);
        }
        internal void Arm()
        {
            if (armed) throw new InvalidOperationException("ordinaryHitpointsInputsAlreadyArmed");
            baPre = beforeAttributes.Open(); bpPre = beforePaid.Open(); aaPre = afterAttributes.Open(); apPre = afterPaid.Open();
            baPost = beforeAttributes.Open(); bpPost = beforePaid.Open(); aaPost = afterAttributes.Open(); apPost = afterPaid.Open();
            faPre = finalAttributes.Open(); fpPre = finalPaid.Open();
            faPost = finalAttributes.Open(); fpPost = finalPaid.Open();
            faPostlude = finalAttributes.Open(); fpPostlude = finalPaid.Open();
            armed = true;
        }
        internal bool ConsumeBeforePlain()
        {
            if (!armed || beforeConsumed) return false;
            beforeConsumed = true;
            return Match(beforeAttributes, baPre) && Match(beforePaid, bpPre) && Match(afterAttributes, aaPre) && Match(afterPaid, apPre) &&
                Match(finalAttributes, faPre) && Match(finalPaid, fpPre);
        }
        internal bool ConsumeAfterPlain()
        {
            if (!armed || afterConsumed) return false;
            afterConsumed = true;
            return Match(beforeAttributes, baPost) && Match(beforePaid, bpPost) && Match(afterAttributes, aaPost) && Match(afterPaid, apPost) &&
                Match(finalAttributes, faPost) && Match(finalPaid, fpPost);
        }
        internal bool ConsumePostludePlain()
        {
            if (!armed || postludeConsumed) return false;
            postludeConsumed = true;
            return Match(finalAttributes, faPostlude) && Match(finalPaid, fpPostlude);
        }
        private static bool Match(FullAccountStringMap map, Dictionary<string, string>.Enumerator witness)
        {
            try { return map.Matches(witness); }
            catch (InvalidOperationException) { return false; } // Raw dictionary replacement/resize is a failed proof.
        }
        internal static bool ScalarsEqual(CharacterSnapshot first, CharacterSnapshot second) => FullAccountCharacterScalarsEqual(first, second);
        internal static bool PaidEqual(CharacterSnapshot first, CharacterSnapshot second) =>
            FullAccountStoredFieldsMatch(first.PaidNobility, second.PaidNobility) && FullAccountStoredFieldsMatch(second.PaidNobility, first.PaidNobility);
    }
}
