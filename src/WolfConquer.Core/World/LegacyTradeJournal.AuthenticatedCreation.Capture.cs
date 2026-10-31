using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    internal static Dictionary<LegacyTradeJournalFileKind, byte[]?> CaptureAuthenticatedCreationInputs(
        LegacyOrdinaryAdmissionRoots roots, string mailbox, uint account)
    {
        var paths = AuthenticatedCreationPaths(roots, mailbox, account);
        return SnapshotFileLockCoordinator.ExecuteLocked(paths.Values.ToArray(), () =>
        {
            _ = AuthenticatedCreationPaths(roots, mailbox, account);
            return paths.ToDictionary(pair => pair.Key, pair => ReadTarget(pair.Value));
        });
    }

    private static Dictionary<LegacyTradeJournalFileKind, string> AuthenticatedCreationPaths(
        LegacyOrdinaryAdmissionRoots roots, string mailbox, uint account)
    {
        if (account == 0 || LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds.Any(kind => roots.Root(kind) is null))
            throw new InvalidDataException("Authenticated creation requires every ordinary participant.");
        ValidateAuthenticatedCreationMailboxRoute(roots, mailbox);
        var paths = LegacyAuthenticatedOrdinaryCreationRecipe.TargetKinds.ToDictionary(kind => kind,
            kind => roots.PathFor(kind, account));
        paths.Add(LegacyTradeJournalFileKind.Mailbox,
            Path.Combine(mailbox, account.ToString(CultureInfo.InvariantCulture) + ".bin"));
        foreach (var path in paths.Values) EnsureSafePath(path);
        return paths;
    }

    private static void ValidateAuthenticatedCreationMailboxRoute(LegacyOrdinaryAdmissionRoots roots, string mailbox)
    {
        _ = GetAdmissionRoots(roots);
        if (Path.TrimEndingDirectorySeparator(Path.GetFullPath(mailbox)) != mailbox ||
            roots.Kinds.Any(kind => QuestRootsOverlap(roots.Root(kind)!, mailbox)))
            throw new InvalidDataException("Authenticated creation mailbox route is not distinct and canonical.");
        EnsureSafePath(mailbox);
        if (File.Exists(mailbox)) throw new IOException("Authenticated creation mailbox root is a file.");
    }
}
