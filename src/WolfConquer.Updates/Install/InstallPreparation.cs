using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Install;

internal static class InstallPreparation
{
    internal static string Backup(string transaction, int index) => Path.Combine(transaction, "backups", index.ToString("D6") + ".bin");
    internal static InstallJournal Prepare(InstallSession session, VerifiedUpdateManifest verified,
        List<JournalFile> changes, string transaction)
    {
        var fs = session.Fs;
        var before = fs.Read(fs.StatePath, InstallMetadata.MaximumBytes);
        if (!InstallFileSystem.Same(before is null ? null : Convert.ToHexString(SHA256.HashData(before)), session.InitialStateHash))
            throw new InvalidDataException("Installed state changed during download.");
        if (before is not null) fs.WriteNew(Path.Combine(transaction, "state-before.json"), before);
        var manifest = verified.Manifest;
        var next = InstallMetadata.Bytes(new InstalledState(manifest.ProductId, manifest.Channel,
            manifest.Sequence, verified.PayloadSha256, verified.EnvelopeSha256, verified.EnvelopeLength,
            manifest.Files.Select(file => new InstalledFile(file.Path, file.Length, file.Sha256)).ToList()));
        if (next.LongLength > InstallMetadata.MaximumBytes) throw new InvalidDataException("Installed state is too large.");
        fs.WriteNew(Path.Combine(transaction, "state-next.json"), next);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var backupCount = changes.Count(file => file.OriginalSha256 is not null);
        var backedUp = 0;
        session.Progress.Report(UpdateOperationPhase.BackingUp, "Creating verified original-file backups.", 0, backupCount, UpdateProgressUnit.Files);
        for (var index = 0; index < changes.Count; index++)
        {
            var file = changes[index];
            var target = fs.Target(file.Path);
            if (!InstallFileSystem.Same(fs.Hash(target), file.OriginalSha256))
                throw new InvalidDataException("Target changed during download: " + file.Path);
            if (file.OriginalSha256 is not null)
            {
                fs.Copy(target, Backup(transaction, index));
                if (!InstallFileSystem.Same(fs.Hash(Backup(transaction, index)), file.OriginalSha256))
                    throw new InvalidDataException("Backup hash mismatch.");
                session.Progress.Report(UpdateOperationPhase.BackingUp, "Creating verified original-file backups.",
                    ++backedUp, backupCount, UpdateProgressUnit.Files, file.Path);
            }
            for (var parent = Path.GetDirectoryName(target); parent != fs.Root && parent is not null; parent = Path.GetDirectoryName(parent))
            {
                fs.Check(parent);
                if (!Directory.Exists(parent)) directories.Add(Path.GetRelativePath(fs.Root, parent).Replace('\\', '/'));
            }
        }
        var journal = new InstallJournal(1, Path.GetFileName(transaction), "prepared", manifest.ProductId, manifest.Channel,
            manifest.Sequence, verified.PayloadSha256, before is null ? null : Convert.ToHexString(SHA256.HashData(before)),
            Convert.ToHexString(SHA256.HashData(next)), changes, directories.OrderBy(path => path.Count(character => character == '/')).ToList());
        Save(fs, transaction, journal); // Complete durable recovery record precedes the first target/parent mutation.
        return journal;
    }

    internal static void Save(InstallFileSystem fs, string transaction, InstallJournal journal)
    {
        InstallMetadata.Validate(journal, Path.GetFileName(transaction));
        var bytes = InstallMetadata.Bytes(journal);
        if (bytes.LongLength > InstallMetadata.MaximumBytes) throw new InvalidDataException("Recovery journal is too large.");
        fs.AtomicWrite(Path.Combine(transaction, "journal.json"), bytes);
    }
}
