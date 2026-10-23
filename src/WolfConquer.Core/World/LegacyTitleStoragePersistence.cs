using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// One character file, four title fields. Call inside the real current-session commit gate.
// A false recovery-required outcome is NOT evidence that the old live state matches disk.
internal static partial class LegacyTitleStoragePersistence
{
    private const string Prefix = "titleStorage";
    private const string RecoveryRequired = "titleStorageRecoveryRequired";
    private static readonly string[] TitleKeys =
        ["SpecialTitles", "SpecialTitleID", "SpecialWingID", "SpecialTitleScore"];

    internal static bool TryPersist(WorldSession expected, WorldSession updated,
        IReadOnlyDictionary<string, string> values, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (!TryPrepare(expected, updated, values, out var path, out var oldTitles,
                out var candidate, out reason)) return false;
            var failure = string.Empty;
            var saved = SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
                PersistLocked(path, expected.Character.Uid, oldTitles, candidate, out failure));
            reason = failure;
            return saved;
        }
        catch (Exception ex) when (IsFileFailure(ex))
        {
            // Every write-phase exception is classified INSIDE PersistLocked.
            reason = Prefix + "BaselineRead:" + ex.GetType().Name;
            return false;
        }
    }

    private static bool PersistLocked(string path, long uid,
        IReadOnlyDictionary<string, string> oldTitles, IReadOnlyDictionary<string, string> candidate,
        out string reason)
    {
        if (!TryReadState(path, uid, oldTitles, out var before, out reason)) return false;
        if (!TryBoundCandidate(before!, candidate, out reason)) return false;
        var writeReason = string.Empty;
        try
        {
            if (CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(path, uid, candidate,
                detailedIoFailures: false, out writeReason))
                return TryValidateWrittenState(path, uid, candidate, before!, out reason);
        }
        catch (Exception ex) when (IsFileFailure(ex))
        { writeReason = Prefix + "Write:" + ex.GetType().Name; }
        return ClassifyFailedWrite(path, before!.Bytes, writeReason, out reason);
    }

    private static bool TryValidateWrittenState(string path, long uid,
        IReadOnlyDictionary<string, string> candidate, CharacterSnapshotFileBaseline.Snapshot before,
        out string reason)
    {
        try
        {
            if (TryReadState(path, uid, candidate, out var after, out _) &&
                NonTitleFieldsEqual(before.Fields, after!.Fields))
            { reason = string.Empty; return true; }
        }
        catch (Exception ex) when (IsFileFailure(ex)) { }
        reason = RecoveryRequired + ":postWriteStateUnproven";
        return false;
    }

    // No accepting writer callback. The only writer above is the real file coordinator.
    private static bool ClassifyFailedWrite(string path, byte[] beforeBytes,
        string writeReason, out string reason)
    {
        try
        {
            if (CharacterSnapshotFileBaseline.TryReadBytes(path, Prefix, out var actual, out _) &&
                actual.AsSpan().SequenceEqual(beforeBytes))
            { reason = Prefix + "WriteFailed:" + writeReason; return false; }
        }
        catch (Exception ex) when (IsFileFailure(ex)) { }
        reason = RecoveryRequired + ":writeBaselineUnproven";
        return false;
    }

    private static bool IsFileFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or System.Security.SecurityException or OverflowException;
}
