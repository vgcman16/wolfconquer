using System.IO.Compression;
using System.Security.Cryptography;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Progress;

namespace WolfConquer.Updates.Install;

internal static class InstallDownloads
{
    internal static string Staged(string transaction, int index) => Path.Combine(transaction, "staged", index.ToString("D6") + ".bin");
    internal static async Task Stage(HttpClient client, InstallSession session, VerifiedUpdateManifest verified,
        List<JournalFile> changes, string transaction, CancellationToken cancellation, Uri? boundContentBase = null)
    {
        if (changes.Count == 0) return;
        var manifest = verified.Manifest;
        var contentBase = boundContentBase ?? manifest.ContentBaseUrl;
        if (manifest.Bundle is { } bundle &&
            (changes.Count == manifest.Files.Count || changes.Sum(file => file.Length) >= bundle.Length))
        {
            if (bundle.Length < 0 || bundle.Length > session.Options.MaxTotalBytes || !InstallFileSystem.IsHash(bundle.Sha256))
                throw new InvalidDataException("Bundle exceeds its configured bound.");
            var archivePath = Path.Combine(transaction, "bundle.zip");
            session.Progress.Report(UpdateOperationPhase.Downloading, "Downloading signed bundle bytes.", 0, bundle.Length, UpdateProgressUnit.Bytes);
            await Fetch(client, session, contentBase, bundle.Sha256, bundle.Length, archivePath, cancellation,
                count => session.Progress.Report(UpdateOperationPhase.Downloading, "Downloading signed bundle bytes.", count, bundle.Length, UpdateProgressUnit.Bytes)).ConfigureAwait(false);
            await Unpack(session, manifest, changes, transaction, archivePath, cancellation).ConfigureAwait(false);
        }
        else
        {
            var total = changes.Sum(file => file.Length);
            long downloaded = 0;
            session.Progress.Report(UpdateOperationPhase.Downloading, "Downloading changed signed payloads.", 0, total, UpdateProgressUnit.Bytes);
            for (var index = 0; index < changes.Count; index++)
            {
                var file = changes[index];
                await Fetch(client, session, contentBase, file.Sha256, file.Length, Staged(transaction, index), cancellation,
                    count => session.Progress.Report(UpdateOperationPhase.Downloading, "Downloading changed signed payloads.",
                        downloaded + count, total, UpdateProgressUnit.Bytes, file.Path)).ConfigureAwait(false);
                downloaded += file.Length;
            }
            session.Progress.Report(UpdateOperationPhase.Verifying, "Staged payloads passed signed length and SHA-256 verification.",
                changes.Count, changes.Count, UpdateProgressUnit.Files);
        }
    }

    private static async Task Fetch(HttpClient client, InstallSession session, Uri contentBase,
        string hash, long length, string destination, CancellationToken cancellation, Action<long> copied)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(session.Options.DownloadTimeout);
        var uri = new Uri(contentBase, "blobs/" + hash.ToLowerInvariant());
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri != uri)
            throw new InvalidDataException("Redirected or unattributed blob response refused.");
        if (response.Content.Headers.ContentLength is { } advertised && advertised != length)
            throw new InvalidDataException("Blob Content-Length does not match the signed length.");
        session.Fs.Check(destination);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using var input = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        await VerifiedCopy(input, output, length, hash, deadline.Token, copied).ConfigureAwait(false);
        output.Flush(flushToDisk: true);
    }

    private static async Task Unpack(InstallSession session, UpdateManifest manifest, List<JournalFile> changes,
        string transaction, string archivePath, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(session.Options.DownloadTimeout);
        var expected = manifest.Files.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        var changed = changes.Select((file, index) => (file.Path, index))
            .ToDictionary(pair => pair.Path, pair => pair.index, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        session.Fs.Check(archivePath);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count != expected.Count) throw new InvalidDataException("Bundle entry count does not match the signed snapshot.");
        var verifiedFiles = 0;
        session.Progress.Report(UpdateOperationPhase.Verifying, "Verifying signed bundle entries while staging.", 0, expected.Count, UpdateProgressUnit.Files);
        foreach (var entry in archive.Entries)
        {
            deadline.Token.ThrowIfCancellationRequested();
            UpdatePathPolicy.ValidateRelativePath(entry.FullName);
            if (!seen.Add(entry.FullName) || !expected.TryGetValue(entry.FullName, out var file) ||
                entry.FullName != file.Path || entry.Length != file.Length ||
                ((entry.ExternalAttributes >> 16) & 0xF000) is 0xA000 or 0x4000 ||
                (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Bundle contains an extra, aliased, linked, directory or malformed entry.");
            using var input = entry.Open();
            if (changed.TryGetValue(file.Path, out var index))
            {
                var destination = Staged(transaction, index);
                session.Fs.Check(destination);
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
                await VerifiedCopy(input, output, file.Length, file.Sha256, deadline.Token).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }
            else await VerifiedCopy(input, Stream.Null, file.Length, file.Sha256, deadline.Token).ConfigureAwait(false);
            session.Progress.Report(UpdateOperationPhase.Verifying, "Verifying signed bundle entries while staging.",
                ++verifiedFiles, expected.Count, UpdateProgressUnit.Files, file.Path);
        }
    }

    private static async Task VerifiedCopy(Stream input, Stream output, long length, string expectedHash,
        CancellationToken cancellation, Action<long>? copied = null)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long count = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellation).ConfigureAwait(false);
            if (read == 0) break;
            count = checked(count + read);
            if (count > length) throw new InvalidDataException("Blob expands beyond its signed length.");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
            copied?.Invoke(count);
        }
        if (count == 0) copied?.Invoke(0);
        if (count != length || !InstallFileSystem.Same(Convert.ToHexString(hash.GetHashAndReset()), expectedHash))
            throw new InvalidDataException("Blob length or SHA256 verification failed.");
    }
}
