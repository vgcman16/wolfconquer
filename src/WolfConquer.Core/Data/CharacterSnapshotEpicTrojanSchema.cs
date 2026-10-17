namespace WolfConquer.Core.Data;

internal static class CharacterSnapshotEpicTrojanSchema
{
    internal const string StorageAttributeKey = "EpicTrojan";

    private const int StoredSlotCount = 20;

    private static readonly string[] DefaultStoredSlots =
    [
        "0", "0", "0", "0", "1", "1", "0", "0", "0", "0",
        "0", "0", "0", "0", "1", "0", "0", "0", "1", "0",
    ];

    private static readonly string?[] RuntimeAttributeKeysBySlot =
    [
        null, // 0 ItemChange0
        null, // 1 ItemChange1
        "StageEpicTrojanQuest",
        "ChangeEpicTrojan",
        null, // 4 CanChangeEpicMaterial
        null, // 5 CanChangeArrayEpicMaterial
        "ChangeArrayEpicTrojan",
        null, // 7 ArrayItem0
        null, // 8 ArrayItem1
        "EpicTrojanEvilArrayPoints",
        "EpicTrojanAbysalStage",
        null, // 11 MirrorItem0
        null, // 12 MirrorItem1
        "ChangeMr_MirrorEpicTrojan",
        null, // 14 CanChangeMr_MirrorEpicMaterial
        null, // 15 GeneralItem0
        null, // 16 GeneralItem1
        "ChangeGeneralPakEpicTrojan",
        null, // 18 CanChangGeneralPakMaterial
        "EpicTrojanMrMirrorPrograss",
    ];

    internal static void ProjectRuntimeAttributes(
        IReadOnlyDictionary<string, string> storageValues,
        IDictionary<string, string> runtimeAttributes)
    {
        if (!TryReadStoredSlots(storageValues, out var slots))
        {
            return;
        }

        for (var index = 0; index < RuntimeAttributeKeysBySlot.Length; index++)
        {
            if (RuntimeAttributeKeysBySlot[index] is { } attributeKey)
            {
                runtimeAttributes[attributeKey] = slots[index];
            }
        }
    }

    internal static bool TryBuildPersistedComposite(
        IReadOnlyDictionary<string, string> runtimeAttributes,
        out string composite)
    {
        composite = string.Empty;
        if (!TryReadStoredSlots(runtimeAttributes, out var slots))
        {
            if (runtimeAttributes.ContainsKey(StorageAttributeKey))
            {
                return false;
            }

            slots = DefaultStoredSlots.ToArray();
        }

        for (var index = 0; index < RuntimeAttributeKeysBySlot.Length; index++)
        {
            if (RuntimeAttributeKeysBySlot[index] is { } attributeKey &&
                runtimeAttributes.TryGetValue(attributeKey, out var value))
            {
                slots[index] = value;
            }
        }

        composite = string.Join('/', slots) + "/";
        return true;
    }

    internal static bool HasModeledRuntimeChanges(
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current)
    {
        foreach (var attributeKey in RuntimeAttributeKeysBySlot)
        {
            if (attributeKey is null)
            {
                continue;
            }

            var previousValue = previous.TryGetValue(attributeKey, out var previousRaw) ? previousRaw : null;
            var currentValue = current.TryGetValue(attributeKey, out var currentRaw) ? currentRaw : null;
            if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasMalformedStoredComposite(IReadOnlyDictionary<string, string> attributes)
    {
        return attributes.ContainsKey(StorageAttributeKey) &&
            !TryReadStoredSlots(attributes, out _);
    }

    private static bool TryReadStoredSlots(
        IReadOnlyDictionary<string, string> attributes,
        out string[] slots)
    {
        slots = [];
        if (!attributes.TryGetValue(StorageAttributeKey, out var composite))
        {
            return false;
        }

        var parts = composite.Split('/', StringSplitOptions.None);
        if (parts.Length != StoredSlotCount + 1 || parts[^1].Length != 0)
        {
            return false;
        }

        slots = parts[..^1];
        return true;
    }
}
