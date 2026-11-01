using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Reuse the existing closed dictionary captures and raw witnesses. This is
    // immutable input evidence, not a second HP/persistence authority.
    internal sealed class BoundHitpointsInputs
    {
        private readonly LegacyNativeAttributeMapProof beforeAttributes, beforePaid, afterAttributes, afterPaid;
        private readonly LegacyNativeAttributeMapProof stableBeforeAttributes, stableBeforePaid;
        private readonly LegacyNativeAttributeMapProof finalAttributes, finalPaid;
        private bool armed, beforeConsumed, afterConsumed, postludeConsumed;
        internal CharacterSnapshot Before { get; }
        internal CharacterSnapshot After { get; }
        internal BoundHitpointsInputs(CharacterSnapshot before, CharacterSnapshot after)
        {
            beforeAttributes = LegacyNativeAttributeMapProof.CaptureRaw(before.Attributes);
            beforePaid = LegacyNativeAttributeMapProof.CaptureRaw(before.PaidNobility);
            afterAttributes = LegacyNativeAttributeMapProof.CaptureRaw(after.Attributes);
            afterPaid = LegacyNativeAttributeMapProof.CaptureRaw(after.PaidNobility);
            Before = before with { Attributes = Copy(beforeAttributes), PaidNobility = Copy(beforePaid) };
            stableBeforeAttributes = LegacyNativeAttributeMapProof.CaptureRaw(Before.Attributes);
            stableBeforePaid = LegacyNativeAttributeMapProof.CaptureRaw(Before.PaidNobility);
            var stable = after with { Attributes = Copy(afterAttributes), PaidNobility = Copy(afterPaid) };
            var attributes = Copy(afterAttributes);
            var modeled = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var scalarKeys = CharacterSnapshotScalarPersistence.BuildPersistedValues(stable).Keys;
            var owned = new HashSet<string>(scalarKeys, StringComparer.OrdinalIgnoreCase);
            owned.UnionWith(ModeledAttributeKeys);
            var beforeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in beforeAttributes.Fields)
                if (owned.Contains(pair.Key) && !beforeKeys.Add(pair.Key))
                    throw new InvalidOperationException("ordinaryHitpointsOwnedAttributeAmbiguous");
            foreach (var pair in afterAttributes.Fields)
                if (owned.Contains(pair.Key) && !modeled.TryAdd(pair.Key, pair.Value))
                    throw new InvalidOperationException("ordinaryHitpointsOwnedAttributeAmbiguous");
            var modeledCharacter = stable with { Attributes = modeled };
            var projected = new Dictionary<string, string>(modeled, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in CharacterSnapshotScalarPersistence.BuildPersistedValues(modeledCharacter))
            { SetOwned(attributes, pair.Key, pair.Value); projected[pair.Key] = pair.Value; }
            CharacterSnapshotEpicTrojanSchema.ProjectRuntimeAttributes(projected, projected);
            foreach (var key in EpicRuntimeKeys)
                if (projected.TryGetValue(key, out var value)) SetOwned(attributes, key, value);
            After = stable with { Attributes = attributes };
            finalAttributes = LegacyNativeAttributeMapProof.CaptureRaw(After.Attributes);
            finalPaid = LegacyNativeAttributeMapProof.CaptureRaw(After.PaidNobility);
        }
        internal void Arm()
        {
            if (armed) throw new InvalidOperationException("ordinaryHitpointsInputsAlreadyArmed");
            armed = true;
        }
        internal bool ConsumeBeforePlain()
        {
            if (!armed || beforeConsumed) return false;
            beforeConsumed = true;
            return InputsCurrent() && FinalsCurrent();
        }
        internal bool ConsumeAfterPlain()
        {
            if (!armed || afterConsumed) return false;
            afterConsumed = true;
            return InputsCurrent() && FinalsCurrent();
        }
        internal bool ConsumePostludePlain()
        {
            if (!armed || postludeConsumed) return false;
            postludeConsumed = true;
            return FinalsCurrent();
        }
        private bool InputsCurrent() => beforeAttributes.IsCurrentPlain() && beforePaid.IsCurrentPlain() &&
            stableBeforeAttributes.IsCurrentPlain() && stableBeforePaid.IsCurrentPlain() &&
            afterAttributes.IsCurrentPlain() && afterPaid.IsCurrentPlain();
        private bool FinalsCurrent() => finalAttributes.IsCurrentPlain() && finalPaid.IsCurrentPlain();
        private static Dictionary<string, string> Copy(LegacyNativeAttributeMapProof proof) =>
            new(proof.Fields, proof.CapturedComparer);
        private static void SetOwned(Dictionary<string, string> map, string key, string value)
        {
            var matches = map.Keys.Where(existing => existing.Equals(key, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("ordinaryHitpointsOwnedAttributeAmbiguous");
            if (matches.Length == 1 && !string.Equals(matches[0], key, StringComparison.Ordinal)) map.Remove(matches[0]);
            map[key] = value;
        }
        private static readonly string[] EpicRuntimeKeys =
        ["StageEpicTrojanQuest", "ChangeEpicTrojan", "ChangeArrayEpicTrojan", "EpicTrojanEvilArrayPoints",
            "EpicTrojanAbysalStage", "ChangeMr_MirrorEpicTrojan", "ChangeGeneralPakEpicTrojan", "EpicTrojanMrMirrorPrograss"];
        private static readonly string[] ModeledAttributeKeys =
        ["MainFlag", "ClaimStateGift", "CanChangeWindWalkerFree", "ClassExperience", "WHMoney", "WarehouseMoney",
            "VotePoints", "PinCodeAnima", "Dragon", "Pheonix", "Phoenix", "Tiger", "Turtle", "ChiPoints",
            "DragonTime", "PhoenixTime", "TigerTime", "TurtleTime", "RetreatedDragon", "RetreatedPhoenix",
            "RetreatedTiger", "RetreatedTurtle", "SkyTowerCompletedChallengeId", "SkyTowerAvailableAttempts",
            "EpicTrojan", .. EpicRuntimeKeys];
        internal static bool ScalarsEqual(CharacterSnapshot first, CharacterSnapshot second) => FullAccountCharacterScalarsEqual(first, second);
        internal static bool PaidEqual(CharacterSnapshot first, CharacterSnapshot second) =>
            FullAccountStoredFieldsMatch(first.PaidNobility, second.PaidNobility) && FullAccountStoredFieldsMatch(second.PaidNobility, first.PaidNobility);
    }
}
