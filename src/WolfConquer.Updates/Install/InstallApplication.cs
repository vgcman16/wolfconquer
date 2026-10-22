using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Install;

internal static class InstallApplication
{
    internal static async Task Apply(InstallSession session, string transaction, InstallJournal journal, CancellationToken cancellation)
    {
        var fs = session.Fs;
        await session.Stopped(cancellation).ConfigureAwait(false);
        session.Progress.Report(UpdateOperationPhase.Applying, "Applying verified payload files.", 0, journal.Files.Count, UpdateProgressUnit.Files);
        foreach (var directory in journal.CreatedDirectories) fs.DirectoryFor(fs.Target(directory));
        for (var index = 0; index < journal.Files.Count; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            await session.Stopped(cancellation).ConfigureAwait(false);
            var file = journal.Files[index];
            var target = fs.Target(file.Path);
            if (!InstallFileSystem.Same(fs.Hash(target), file.OriginalSha256) ||
                !InstallFileSystem.Same(fs.Hash(InstallDownloads.Staged(transaction, index)), file.Sha256))
                throw new InvalidDataException("Target or staged bytes changed before atomic apply.");
            fs.AtomicMove(InstallDownloads.Staged(transaction, index), target);
            session.Progress.Report(UpdateOperationPhase.Applying, "Applying verified payload files.",
                index + 1, journal.Files.Count, UpdateProgressUnit.Files, file.Path);
        }
        await session.Stopped(cancellation).ConfigureAwait(false);
        if (!InstallFileSystem.Same(fs.Hash(fs.StatePath), journal.BeforeStateSha256) ||
            !InstallFileSystem.Same(fs.Hash(Path.Combine(transaction, "state-next.json")), journal.NextStateSha256))
            throw new InvalidDataException("Updater state changed before commit.");
        fs.Copy(Path.Combine(transaction, "state-next.json"), Path.Combine(transaction, "state-install.json"));
        fs.AtomicMove(Path.Combine(transaction, "state-install.json"), fs.StatePath);
        InstallPreparation.Save(fs, transaction, journal with { Phase = "committed" });
    }
}
