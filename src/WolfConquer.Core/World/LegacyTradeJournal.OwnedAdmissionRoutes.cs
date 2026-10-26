namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    // Read-only validation of the actual runtime participants, not a journal
    // permission or an arbitrary-path writer. Missing optional sidecars stay absent.
    internal static void ValidateOwnedAdmissionRoutes(LegacyOrdinaryAdmissionRoots roots, string mailbox,
        string redeem, string claim)
    {
        ValidateOrdinaryAdmissionRoots(roots);
        var directories = roots.Kinds.Select(kind => roots.Root(kind)!).Append(NormalizeQuestRoot(mailbox)).ToArray();
        foreach (var path in directories)
        {
            EnsureSafePath(path);
            if (File.Exists(path)) throw new IOException("A participant directory is a file.");
        }
        for (var index = 0; index < directories.Length; index++)
            for (var other = index + 1; other < directories.Length; other++)
                if (QuestRootsOverlap(directories[index], directories[other]))
                    throw new ArgumentException("Runtime participant directories overlap.");
        RequireAdmissionPhysicalRoots(directories);
        foreach (var file in new[] { redeem, claim })
        {
            EnsureSafePath(file);
            if (Directory.Exists(file) || directories.Any(directory => QuestRootsOverlap(directory, file)))
                throw new ArgumentException("A retained container overlaps a participant directory.");
        }
    }
}
