using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using WolfConquer.Updates.Install;
using WolfConquer.Updates.Manifest;
using WolfConquer.Updates.Progress;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdateOperationProgressTests
{
    private sealed class Observer(bool throws = false, Action<UpdateOperationProgress>? onReport = null) : IProgress<UpdateOperationProgress>
    {
        internal List<UpdateOperationProgress> Events { get; } = [];
        public void Report(UpdateOperationProgress value)
        {
            Events.Add(value); onReport?.Invoke(value);
            if (throws) throw new InvalidOperationException("Synthetic observer failure.");
        }
    }

    private sealed class StopGuard : IUpdateProcessGuard
    {
        public Task EnsureStoppedAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    }

    private sealed class PayloadHandler : HttpMessageHandler
    {
        internal Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal List<string> Requested { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hash = request.RequestUri!.Segments[^1]; Requested.Add(hash);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { RequestMessage = request, Content = new ByteArrayContent(Blobs[hash]) });
        }
    }

    private sealed class InstallFixture : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("wolf progress-fixture ").FullName;
        internal PayloadHandler Handler { get; } = new();
        private HttpClient Client { get; }
        internal TransactionalUpdateInstaller Installer { get; }
        internal InstallFixture()
        { Client = new HttpClient(Handler); Installer = new(Client, new StopGuard()); }
        internal string PathOf(string path) => Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        internal UpdateInstallOptions Options(Observer? progress = null, bool fresh = false) =>
            new(Root, ["Conquer.exe"]) { Progress = progress, AllowFreshInstall = fresh };
        internal void Put(string path, byte[] data)
        { Directory.CreateDirectory(Path.GetDirectoryName(PathOf(path))!); File.WriteAllBytes(PathOf(path), data); }
        private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        internal UpdateFileEntry Entry(string path, byte[] data, byte[]? before = null)
        {
            var hash = Hash(data); Handler.Blobs[hash] = data;
            return new(path, data.Length, hash, before is null ? null : Hash(before));
        }
        internal UpdateBundle Bundle(UpdateFileEntry[] files)
        {
            using var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var file in files)
                { using var stream = zip.CreateEntry(file.Path).Open(); stream.Write(Handler.Blobs[file.Sha256]); }
            var bytes = memory.ToArray(); var hash = Hash(bytes); Handler.Blobs[hash] = bytes;
            return new(bytes.Length, hash);
        }
        internal VerifiedUpdateManifest Release(UpdateFileEntry[] files, UpdateBundle? bundle = null)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var manifest = new UpdateManifest(1, "synthetic", "stable", 1, DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1), new Uri("https://updates.example.test/"), files, bundle);
            return SignedUpdateManifest.VerifyEnvelope(SignedUpdateManifest.CreateEnvelope(manifest, "test-key", key),
                "test-key", key.ExportSubjectPublicKeyInfoPem(), new("synthetic", "stable", 1, DateTimeOffset.UtcNow));
        }
        internal string Transaction() => Directory.GetDirectories(PathOf(".wolfconquer-updates/transactions")).Single();
        internal async Task<string> PreparePartialApply(VerifiedUpdateManifest release)
        {
            using var session = new InstallSession(Options(), new StopGuard());
            await session.Acquire(default);
            var changes = session.Plan(release, session.State());
            var transaction = Path.Combine(session.Fs.Control, "transactions", Guid.NewGuid().ToString("N"));
            session.Fs.DirectoryFor(Path.Combine(transaction, "staged")); session.Fs.DirectoryFor(Path.Combine(transaction, "backups"));
            await InstallDownloads.Stage(Client, session, release, changes, transaction, default);
            InstallPreparation.Prepare(session, release, changes, transaction);
            for (var index = 0; index < changes.Count; index++)
                File.Move(InstallDownloads.Staged(transaction, index), PathOf(changes[index].Path), true);
            return transaction;
        }
        public void Dispose()
        {
            Client.Dispose();
            var resolved = Path.GetFullPath(Root);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("wolf progress-fixture ", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, recursive: true);
        }
    }
}
