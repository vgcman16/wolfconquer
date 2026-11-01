using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private sealed partial class ManagedStartupPersistenceState
    {
        private static CharacterSnapshot NormalizeCommittedSnapshot(CharacterSnapshot expected,
            CharacterSnapshot proposed, byte[] committedImage)
        {
            if (!ReferenceEquals(expected.PlayerQuests, proposed.PlayerQuests) ||
                !SameRawMap(expected.PaidNobility, proposed.PaidNobility)) throw Refused("unownedProposalChanged");
            var original = LegacyNativeAttributeMapProof.CaptureRaw(expected.Attributes);
            var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(committedImage, out _));
            var result = new Dictionary<string, string>(original.Fields, original.CapturedComparer);
            var owned = new HashSet<string>(CharacterSnapshotScalarPersistence.BuildPersistedValues(proposed).Keys,
                StringComparer.OrdinalIgnoreCase)
            {
                // The stored composite can be materialized by this scalar write;
                // the parser then adds these runtime aliases to its exact image.
                "MainFlag", "StageEpicTrojanQuest", "ChangeEpicTrojan", "ChangeArrayEpicTrojan",
                "EpicTrojanEvilArrayPoints", "EpicTrojanAbysalStage", "ChangeMr_MirrorEpicTrojan",
                "ChangeGeneralPakEpicTrojan", "EpicTrojanMrMirrorPrograss"
            };
            foreach (var pair in parsed.Attributes)
            {
                if (!owned.Contains(pair.Key)) continue;
                var matches = result.Keys.Where(key => key.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length > 1) throw Refused("ownedFieldAmbiguous");
                if (matches.Length == 1 && matches[0] != pair.Key) result.Remove(matches[0]);
                result[pair.Key] = pair.Value;
            }
            if (!original.IsCurrentPlain()) throw Refused("proposalAttributesChanged");
            var normalized = proposed with
            {
                Attributes = result, PaidNobility = expected.PaidNobility,
                SourceFile = expected.SourceFile, PlayerQuests = expected.PlayerQuests,
            };
            if (!MatchesImage(normalized, committedImage)) throw Refused("committedProjectionMismatch");
            return normalized;
        }

        private static bool MatchesImage(CharacterSnapshot expected, byte[] bytes)
        {
            var text = CharacterSnapshotFileEncoding.Decode(bytes, out var encoding);
            if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(bytes)) return false;
            var parsed = CharacterSnapshotParser.ParseText(text);
            var empty = new Dictionary<string, string>();
            return (parsed with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty }) ==
                (expected with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty }) &&
                SameRawMap(parsed.Attributes, expected.Attributes) && SameRawMap(parsed.PaidNobility, expected.PaidNobility);
        }

        private static bool SameRawMap(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second)
        {
            var left = LegacyNativeAttributeMapProof.CaptureRaw(first);
            var right = LegacyNativeAttributeMapProof.CaptureRaw(second);
            var rows = new Dictionary<string, string>(left.Fields, StringComparer.Ordinal);
            return rows.Count == right.Fields.Count && right.Fields.All(pair =>
                rows.TryGetValue(pair.Key, out var value) && value == pair.Value) && left.IsCurrentPlain() && right.IsCurrentPlain();
        }
    }
}
