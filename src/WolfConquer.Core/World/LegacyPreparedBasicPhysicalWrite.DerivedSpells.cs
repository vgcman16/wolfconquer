using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalWrite
{
    private static void RequireRecordedDerivedOrigins(byte[] image, CharacterSnapshot proposed, PlayerSpellSnapshot spells)
    {
        var recorded = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(image, out _));
        LegacyDerivedSpellOrigins.RequireRows(recorded, spells);
        LegacyDerivedSpellOrigins.RequireRows(proposed, spells);
        var left = new LegacyNativeAttributeMapProof(recorded.Attributes).Fields;
        var right = new LegacyNativeAttributeMapProof(proposed.Attributes).Fields;
        bool hasLeft = left.TryGetValue(LegacyDerivedSpellOrigins.Key, out var leftValue);
        bool hasRight = right.TryGetValue(LegacyDerivedSpellOrigins.Key, out var rightValue);
        if (hasLeft != hasRight || leftValue != rightValue) throw Unavailable();
    }
}
