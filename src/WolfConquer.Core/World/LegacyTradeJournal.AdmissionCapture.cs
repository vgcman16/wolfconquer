using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static void ValidateOrdinaryAdmissionRoots(LegacyOrdinaryAdmissionRoots configured) => _ = GetAdmissionRoots(configured);

    internal static Dictionary<LegacyTradeJournalFileKind, byte[]?> CaptureOrdinaryAdmissionInputs(
        LegacyOrdinaryAdmissionRoots configured, long uid) => CaptureOrdinaryAdmissionInputs(configured, uid, uid);

    internal static Dictionary<LegacyTradeJournalFileKind, byte[]?> CaptureOrdinaryAdmissionInputs(
        LegacyOrdinaryAdmissionRoots configured, long account, long uid)
    {
        _ = GetAdmissionRoots(configured);
        RequireExistingAdmissionAccountMapping(configured.Users, account, uid);
        var paths = configured.Kinds.Select(kind => configured.PathFor(kind,
            kind == LegacyTradeJournalFileKind.Character ? account : uid)).ToArray();
        return SnapshotFileLockCoordinator.ExecuteLocked(paths, () => configured.Kinds.ToDictionary(
            kind => kind, kind => ReadTarget(configured.PathFor(kind, kind == LegacyTradeJournalFileKind.Character ? account : uid))));
    }

    internal static bool AdmissionInputsAreCurrent(LegacyOrdinaryAdmissionRoots roots, long uid,
        IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> images) => AdmissionInputsAreCurrent(roots, uid, uid, images);

    internal static bool AdmissionInputsAreCurrent(LegacyOrdinaryAdmissionRoots roots, long account, long uid,
        IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> images)
    {
        try
        {
            _ = GetAdmissionRoots(roots);
            RequireExistingAdmissionAccountMapping(roots.Users, account, uid);
            return images.All(row => Matches(ReadTarget(roots.PathFor(row.Key,
                row.Key == LegacyTradeJournalFileKind.Character ? account : uid)), row.Value));
        }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }
}
