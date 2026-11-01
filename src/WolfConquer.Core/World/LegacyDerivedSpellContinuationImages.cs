using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// DATA validation only. Runtime permission is the actual query participant;
// restart permission is the existing configured journal recovery owner.
internal static class LegacyDerivedSpellContinuationImages
{
    internal static LegacyExperienceContinuation? RequirePreserved(long account, uint uid, byte[] before, byte[] after)
    {
        var beforeText = CharacterSnapshotFileEncoding.Decode(before, out _);
        var afterText = CharacterSnapshotFileEncoding.Decode(after, out _);
        var beforeLines = beforeText.Split('\n'); var afterLines = afterText.Split('\n');
        if (!LegacyExperienceContinuationFence.ContainsCharacterMarker(beforeLines) &&
            !LegacyExperienceContinuationFence.ContainsCharacterMarker(afterLines)) return null;
        var pending = LegacyExperienceContinuationCodec.ReadCharacterImage(before, account, uid);
        if (pending is null || !Equals(pending, LegacyExperienceContinuationCodec.ReadCharacterImage(after, account, uid)) ||
            !LegacyExperienceContinuationFence.RecoveryProtectedLinesUnchanged(beforeLines, afterLines))
            throw new InvalidDataException("derivedSpellQuery23ChangedPendingExperience");
        return pending;
    }
}
