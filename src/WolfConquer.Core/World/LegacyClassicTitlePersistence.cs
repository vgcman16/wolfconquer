using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Real one-file MyTitle write, inside the existing current-session commit gate.
// Titles authorizes selection but is never rewritten or granted by this owner.
internal static partial class LegacyClassicTitlePersistence
{
    private const string Prefix = "classicTitle";
    internal const string RecoveryRequired = "classicTitleRecoveryRequired";

    internal static bool TryPersist(WorldSession expected, WorldSession updated, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (!TryPrepare(expected, updated, out var path, out var candidate, out reason)) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked(path,
                () => PersistLocked(path, expected, candidate, out failure));
            reason = failure;
            return saved;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            // Write-phase failures are classified inside PersistLocked, not here.
            reason = Prefix + "BaselineRead:" + ex.GetType().Name;
            return false;
        }
    }

    private static bool PersistLocked(string path, WorldSession expected,
        IReadOnlyDictionary<string, string> candidate, out string reason)
    {
        if (!TryReadState(path, expected.Character.Uid, expected.Character.Attributes,
            sessionProjection: true, out var before, out reason) ||
            !TryBoundCandidate(before!, candidate, out var projected, out reason)) return false;
        var failure = string.Empty;
        try
        {
            if (CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(path, expected.Character.Uid,
                candidate, detailedIoFailures: false, out failure))
                return TryValidateWrittenState(path, expected.Character.Uid, candidate, before!, projected, out reason);
        }
        catch (Exception ex) when (IsFileFailure(ex)) { failure = Prefix + "Write:" + ex.GetType().Name; }
        return ClassifyFailedWrite(path, before!.Bytes, failure, out reason);
    }

    private static bool TryValidateWrittenState(string path, long uid,
        IReadOnlyDictionary<string, string> candidate, CharacterSnapshotFileBaseline.Snapshot before, string[] projected,
        out string reason)
    {
        try
        {
            var wanted = new Dictionary<string, string>(before.Fields, StringComparer.OrdinalIgnoreCase)
            { [LegacyClassicTitleState.SelectedKey] = candidate[LegacyClassicTitleState.SelectedKey] };
            if (TryReadState(path, uid, wanted, sessionProjection: false, out var after, out _) &&
                LegacyClassicTitleState.SameExceptSelection(before.Fields, after!.Fields) &&
                after.Lines.SequenceEqual(projected, StringComparer.Ordinal) &&
                after.Encoding.CodePage == before.Encoding.CodePage &&
                after.Encoding.GetPreamble().AsSpan().SequenceEqual(before.Encoding.GetPreamble()))
            { reason = string.Empty; return true; }
        }
        catch (Exception ex) when (IsFileFailure(ex)) { }
        reason = RecoveryRequired + ":postWriteStateUnproven";
        return false;
    }

    private static bool ClassifyFailedWrite(string path, byte[] beforeBytes, string failure, out string reason)
    {
        try
        {
            if (CharacterSnapshotFileBaseline.TryReadBytes(path, Prefix, out var actual, out _) &&
                actual.AsSpan().SequenceEqual(beforeBytes))
            { reason = Prefix + "WriteFailed:" + failure; return false; }
        }
        catch (Exception ex) when (IsFileFailure(ex)) { }
        reason = RecoveryRequired + ":writeBaselineUnproven";
        return false;
    }

    private static bool IsFileFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or System.Security.SecurityException or OverflowException;
}
