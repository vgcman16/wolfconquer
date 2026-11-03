using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Core.World;

internal sealed partial class SqlOrdinaryAdmissionImagePlan
{
    // Pure storage projection only. The captured prefix is not a revision,
    // native, Store or authenticated admission owner.
    private static bool TryProject(SqlWorldStateBundle before, LegacyCapturedCharacterPrefix original,
        DateTime localNow, out SqlWorldStateBundle? after, out LegacySecondaryPasswordState? pin, out string reason)
    {
        after = null;
        pin = null;
        reason = "sqlAdmissionImageIdentityMismatch";
        if (before is null || original is null || !ReferenceEquals(original.Image, before.Core)) return false;
        try
        {
            var snapshot = original.Snapshot;
            if (!LegacySecondaryPasswordState.TryRead(snapshot.Attributes, out _, out reason)) return false;
            byte[] characterAfter = LegacyOrdinaryAdmissionImages.ProjectCharacter(before.Core.CopyCharacterBytes(), localNow);
            var inputs = ProjectSidecars(before, snapshot);
            if (!SqlWorldStateImage.TryCreate(before.AccountId, before.CharacterUid, characterAfter,
                    before.Core.ItemBytes, out var coreAfter, out _) || coreAfter is null ||
                !coreAfter.ItemUids.SequenceEqual(before.ItemUids))
            { reason = "sqlAdmissionCharacterInvalid"; return false; }
            if (!SqlWorldStateBundle.TryCreate(coreAfter, inputs, out var projected, out _) || projected is null)
            { reason = "sqlAdmissionBundleInvalid"; return false; }
            var projectedSnapshot = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterAfter, out _));
            if (!LegacySecondaryPasswordState.TryRead(projectedSnapshot.Attributes, out var projectedPin, out reason)) return false;
            after = projected;
            pin = projectedPin;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is FormatException or InvalidDataException or ArgumentException or
            InvalidOperationException or OverflowException or KeyNotFoundException or IndexOutOfRangeException)
        { reason = "sqlAdmissionProjectionInvalid"; return false; }
    }

    private static SqlWorldStateSidecarInput[] ProjectSidecars(SqlWorldStateBundle before, CharacterSnapshot snapshot)
    {
        var inputs = new SqlWorldStateSidecarInput[before.Sidecars.Length];
        for (int index = 0; index < inputs.Length; index++)
        {
            var sidecar = before.Sidecars[index];
            if (sidecar.Kind != SqlWorldStateSidecarKind.Spells)
            {
                inputs[index] = new(sidecar.Kind, sidecar.Presence, sidecar.CopyBytes());
                continue;
            }
            byte[]? old = sidecar.Presence == SqlWorldStateSidecarPresence.Missing ? null : sidecar.CopyBytes();
            byte[] projected = LegacyOrdinaryAdmissionSpellImages.Project(snapshot, old);
            // The actual helper represents a missing/no-grant book as an empty
            // four-byte serialized count; retain absence rather than inventing a file.
            bool stillMissing = old is null && projected.AsSpan().SequenceEqual(new byte[sizeof(int)]);
            inputs[index] = stillMissing
                ? new(sidecar.Kind, SqlWorldStateSidecarPresence.Missing, ReadOnlyMemory<byte>.Empty)
                : new(sidecar.Kind, SqlWorldStateSidecarPresence.Present, projected);
        }
        return inputs;
    }
}
