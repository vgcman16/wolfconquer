using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// DATA only; the configured Frame, query and file writer retain authority.
internal sealed class LegacyManagedCharacterImage
{
    private readonly LegacyNativeAttributeMapProof attributes, paid;
    internal CharacterSnapshot Source { get; }
    internal LegacyManagedCharacterImage(CharacterSnapshot source)
    {
        Source = source;
        attributes = LegacyNativeAttributeMapProof.CaptureRaw(source.Attributes);
        paid = LegacyNativeAttributeMapProof.CaptureRaw(source.PaidNobility);
    }
    internal bool IsCurrent() => attributes.IsCurrentPlain() && paid.IsCurrentPlain();
    internal static bool MatchesSaved(CharacterSnapshot saved, CharacterSnapshot runtime, uint vigor)
    {
        if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(saved, runtime) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(saved, runtime) ||
            LegacyEquipmentQueryBounds.ReadVigor(runtime) != vigor) return false;
        foreach (var pair in saved.Attributes)
        {
            if (pair.Key.Equals("Vigor", StringComparison.OrdinalIgnoreCase)) continue;
            if (!runtime.Attributes.TryGetValue(pair.Key, out var value) || value != pair.Value) return false;
        }
        return true;
    }
    internal CharacterSnapshot WithSavedMirror(byte[] savedAfter)
    {
        if (!IsCurrent()) throw new InvalidOperationException("managedCharacterInputsChanged");
        var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(savedAfter, out _));
        if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(parsed, Source) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(parsed, Source))
            throw new InvalidOperationException("managedCharacterProjectionIncomplete");
        var fields = new Dictionary<string, string>(attributes.CapturedComparer);
        foreach (var pair in attributes.Fields) fields.Add(pair.Key, pair.Value);
        var mirrored = new HashSet<string>(CharacterSnapshotScalarPersistence.BuildPersistedValues(Source).Keys,
            StringComparer.OrdinalIgnoreCase)
        {
            "MainFlag", "StageEpicTrojanQuest", "ChangeEpicTrojan", "ChangeArrayEpicTrojan",
            "EpicTrojanEvilArrayPoints", "EpicTrojanAbysalStage", "ChangeMr_MirrorEpicTrojan",
            "ChangeGeneralPakEpicTrojan", "EpicTrojanMrMirrorPrograss"
        };
        foreach (var pair in parsed.Attributes)
        {
            // The saved Vigor seed must never replace its genuine query value.
            // Only values actually projected by scalar persistence (and its
            // parser-generated aliases) belong to this mirror.
            if (!mirrored.Contains(pair.Key)) continue;
            var keys = fields.Keys.Where(key => key.Equals(pair.Key, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (keys.Length > 1) throw new InvalidOperationException("managedCharacterOwnedKeyAmbiguous");
            fields[keys.Length == 1 ? keys[0] : pair.Key] = pair.Value;
        }
        return Source with { Attributes = fields };
    }
}
