using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static void ValidateOrdinaryAdmissionRoots(LegacyOrdinaryAdmissionRoots configured) => _ = GetAdmissionRoots(configured);

    internal static Dictionary<LegacyTradeJournalFileKind, byte[]?> CaptureOrdinaryAdmissionInputs(
        LegacyOrdinaryAdmissionRoots configured, long uid)
    {
        _ = GetAdmissionRoots(configured);
        var paths = configured.Kinds.Select(kind => configured.PathFor(kind, uid)).ToArray();
        return SnapshotFileLockCoordinator.ExecuteLocked(paths, () => configured.Kinds.ToDictionary(
            kind => kind, kind => ReadTarget(configured.PathFor(kind, uid))));
    }

    internal static bool AdmissionInputsAreCurrent(LegacyOrdinaryAdmissionRoots roots, long uid,
        IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> images)
    {
        try
        {
            _ = GetAdmissionRoots(roots);
            return images.All(row => Matches(ReadTarget(roots.PathFor(row.Key, uid)), row.Value));
        }
        catch (Exception error) when (IsJournalException(error)) { return false; }
    }
}
