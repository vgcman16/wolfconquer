using System.Security.Cryptography;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Install;

internal static class InstallRecovery
{
    internal static async Task<int> Recover(InstallSession session, CancellationToken cancellation)
    {
        var pending = Pending(session, cancellation);
        foreach (var entry in pending)
            await Rollback(session, entry.Path, entry.Journal, cancellation).ConfigureAwait(false);
        return pending.Count;
    }

    internal static void AssertNoPending(InstallSession session, CancellationToken cancellation)
    {
        if (Pending(session, cancellation).Count != 0)
            throw new InvalidDataException("Unfinished installation must be recovered before reading committed release metadata.");
    }

    private static List<(string Path, InstallJournal Journal)> Pending(InstallSession session, CancellationToken cancellation)
    {
        var fs = session.Fs;
        var transactions = Path.Combine(fs.Control, "transactions");
        fs.Check(transactions);
        var pending = new List<(string Path, InstallJournal Journal)>();
        if (!Directory.Exists(transactions)) return pending;
        foreach (var path in Directory.EnumerateFileSystemEntries(transactions))
        {
            cancellation.ThrowIfCancellationRequested();
            fs.Check(path);
            if (!Directory.Exists(path) || !Guid.TryParseExact(Path.GetFileName(path), "N", out _))
                throw new InvalidDataException("Unrecognized updater transaction directory.");
            var bytes = fs.Read(Path.Combine(path, "journal.json"), InstallMetadata.MaximumBytes);
            if (bytes is null) continue; // Download-only/orphan staging never changes a target.
            var journal = InstallMetadata.Parse<InstallJournal>(bytes);
            InstallMetadata.Validate(journal, Path.GetFileName(path));
            if (journal.Phase == "prepared") pending.Add((path, journal));
        }
        if (pending.Count > 1) throw new InvalidDataException("Ambiguous unfinished transactions; manual recovery required.");
        return pending;
    }

    internal static async Task Rollback(InstallSession session, string transaction, InstallJournal journal, CancellationToken cancellation)
    {
        var fs = session.Fs;
        InstallMetadata.Validate(journal, Path.GetFileName(transaction));
        await session.Stopped(cancellation).ConfigureAwait(false);
        ValidateBackups(fs, transaction, journal);
        cancellation.ThrowIfCancellationRequested();
        // Restoration is a critical section. Honor cancellation before entering,
        // but finish recovery once entered; process-stop guards still run normally.
        var restoration = CancellationToken.None;
        var stateHash = fs.Hash(fs.StatePath);
        if (stateHash is not null && !InstallFileSystem.Same(stateHash, journal.BeforeStateSha256) &&
            !InstallFileSystem.Same(stateHash, journal.NextStateSha256))
            throw new InvalidDataException("Updater state was changed externally; recovery refused.");
        session.Progress.Report(UpdateOperationPhase.RollingBack, "Checking and restoring transaction files.", 0, journal.Files.Count, UpdateProgressUnit.Files);
        for (var index = journal.Files.Count - 1; index >= 0; index--)
        {
            await session.Stopped(restoration).ConfigureAwait(false);
            var file = journal.Files[index];
            var target = fs.Target(file.Path);
            var current = fs.Hash(target);
            var restored = false;
            if (!InstallFileSystem.Same(current, file.OriginalSha256))
            {
                if (current is not null && !InstallFileSystem.Same(current, file.Sha256))
                    throw new InvalidDataException("A target changed externally; recovery refused: " + file.Path);
                if (file.OriginalSha256 is null)
                {
                    if (current is not null) fs.AtomicMove(target, Path.Combine(transaction, "removed-" + index + "-" + Guid.NewGuid().ToString("N")));
                }
                else
                {
                    fs.DirectoryFor(Path.GetDirectoryName(target)!);
                    var temporary = Path.Combine(transaction, "restore-" + Guid.NewGuid().ToString("N"));
                    fs.Copy(InstallPreparation.Backup(transaction, index), temporary);
                    fs.AtomicMove(temporary, target);
                    restored = true;
                }
            }
            if (restored)
            {
                fs.Check(target);
                File.SetLastWriteTimeUtc(target, new DateTime(file.OriginalWriteTicks, DateTimeKind.Utc));
                File.SetAttributes(target, (FileAttributes)file.OriginalAttributes);
            }
            session.Progress.Report(UpdateOperationPhase.RollingBack, "Checking and restoring transaction files.",
                journal.Files.Count - index, journal.Files.Count, UpdateProgressUnit.Files, file.Path);
        }
        await session.Stopped(restoration).ConfigureAwait(false);
        if (!InstallFileSystem.Same(fs.Hash(fs.StatePath), journal.BeforeStateSha256))
        {
            if (journal.BeforeStateSha256 is null)
            {
                if (File.Exists(fs.StatePath)) fs.AtomicMove(fs.StatePath, Path.Combine(transaction, "removed-state-" + Guid.NewGuid().ToString("N")));
            }
            else
            {
                var temporary = Path.Combine(transaction, "restore-state-" + Guid.NewGuid().ToString("N"));
                fs.Copy(Path.Combine(transaction, "state-before.json"), temporary);
                fs.AtomicMove(temporary, fs.StatePath);
            }
        }
        foreach (var directory in journal.CreatedDirectories.OrderByDescending(path => path.Count(character => character == '/')))
        {
            var path = fs.Target(directory);
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) Directory.Delete(path);
        }
        InstallPreparation.Save(fs, transaction, journal with { Phase = "rolledback" });
    }

    private static void ValidateBackups(InstallFileSystem fs, string transaction, InstallJournal journal)
    {
        var nextBytes = fs.Read(Path.Combine(transaction, "state-next.json"), InstallMetadata.MaximumBytes)
            ?? throw new InvalidDataException("Missing next-state recovery copy.");
        if (!InstallFileSystem.Same(Convert.ToHexString(SHA256.HashData(nextBytes)), journal.NextStateSha256))
            throw new InvalidDataException("Corrupt next-state recovery copy.");
        var next = InstallMetadata.Parse<InstalledState>(nextBytes);
        InstallMetadata.Validate(next);
        if (next.ProductId != journal.ProductId || next.Channel != journal.Channel || next.Sequence != journal.Sequence ||
            !InstallFileSystem.Same(next.PayloadSha256, journal.PayloadSha256))
            throw new InvalidDataException("Recovery state identity mismatch.");
        var nextFiles = next.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var file in journal.Files)
            if (!nextFiles.TryGetValue(file.Path, out var target) || target.Length != file.Length ||
                !InstallFileSystem.Same(target.Sha256, file.Sha256))
                throw new InvalidDataException("Recovery file is not authorized by the recorded next state.");
        if (journal.BeforeStateSha256 is not null)
        {
            var before = fs.Read(Path.Combine(transaction, "state-before.json"), InstallMetadata.MaximumBytes)
                ?? throw new InvalidDataException("Missing prior-state recovery copy.");
            if (!InstallFileSystem.Same(Convert.ToHexString(SHA256.HashData(before)), journal.BeforeStateSha256))
                throw new InvalidDataException("Corrupt prior-state recovery copy.");
            var previous = InstallMetadata.Parse<InstalledState>(before);
            InstallMetadata.Validate(previous);
            if (previous.ProductId != journal.ProductId || previous.Channel != journal.Channel || previous.Sequence > journal.Sequence)
                throw new InvalidDataException("Prior recovery state identity mismatch.");
        }
        for (var index = 0; index < journal.Files.Count; index++)
            if (journal.Files[index].OriginalSha256 is { } expected &&
                !InstallFileSystem.Same(fs.Hash(InstallPreparation.Backup(transaction, index)), expected))
                throw new InvalidDataException("Missing or corrupt target backup.");
    }
}
