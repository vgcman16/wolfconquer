using System.Net;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Updates.ClientLayout;
using WolfConquer.Updates.Install;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Tests;

public sealed partial class ClientAssetLayoutTests
{
    // Independent fixed-layout expectation, not copied from the production collection at runtime.
    private static readonly string[] Assets = ["ani", "c3", "data", "data2", "Font", "Help", "ini", "map", "Script", "sound", "video"];
    private const string Preset = "legacy-dx8-v1";
    private const string Engine = "Env_DX8/FixtureClient.exe";

    private sealed class LayoutFixture : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("wolf-layout-" + Guid.NewGuid().ToString("N") + "-").FullName;
        internal string Client => Path.Combine(Root, "client");
        internal string State => Path.Combine(Client, ".wolfconquer-updates", "state.json");
        internal RecordingGuard Guard { get; } = new();
        internal VerifiedUpdateManifest Installed { get; private set; } = null!;
        private ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private PayloadHandler Handler { get; } = new();
        private HttpClient Http { get; }
        private Dictionary<string, string> OwnedLinks { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal TransactionalUpdateInstaller Installer { get; }

        internal LayoutFixture()
        {
            Directory.CreateDirectory(Client);
            Http = new HttpClient(Handler);
            Installer = new(Http, Guard);
        }

        internal string PathOf(string relative) => Path.Combine(Client, relative.Replace('/', Path.DirectorySeparatorChar));
        internal string Alias(string name) => PathOf("Env_DX8/" + name);
        internal string Target(string name) => PathOf(name);
        internal UpdateInstallOptions Options(bool fresh = false) => new(Client, [Engine]) { AllowFreshInstall = fresh };
        internal Task Prepare(IUpdateProcessGuard? guard = null) => ClientAssetLayout.PrepareAsync(Installed, Options(), Preset, guard ?? Guard);

        internal VerifiedUpdateManifest Release(long sequence, params (string Path, string Text)[] replacements)
        {
            var bytes = Assets.ToDictionary(name => name + "/asset.bin", name => Encoding.UTF8.GetBytes("owned " + name), StringComparer.Ordinal);
            bytes.Add(Engine, Encoding.UTF8.GetBytes("Synthetic fixture bytes; NEVER LAUNCH."));
            foreach (var pair in replacements) bytes[pair.Path] = Encoding.UTF8.GetBytes(pair.Text);
            var files = bytes.Select(pair =>
            {
                string hash = Convert.ToHexString(SHA256.HashData(pair.Value));
                Handler.Blobs[hash] = pair.Value;
                return new UpdateFileEntry(pair.Key, pair.Value.Length, hash, null);
            }).ToArray();
            var now = DateTimeOffset.UtcNow;
            var manifest = new UpdateManifest(1, "layout-fixture", "preview", sequence, now.AddMinutes(-1), now.AddHours(1),
                new Uri("https://updates.example.test/"), files);
            byte[] signed = SignedUpdateManifest.CreateEnvelope(manifest, "layout-key", Key);
            return SignedUpdateManifest.VerifyEnvelope(signed, "layout-key", Key.ExportSubjectPublicKeyInfoPem(),
                new("layout-fixture", "preview", 1, now));
        }

        internal async Task Install()
        {
            Installed = Release(1);
            var result = await Installer.InstallAsync(Installed, Options(fresh: true));
            Assert.Equal(12, result.InstalledFiles);
            Assert.True(File.Exists(State));
            Guard.Calls.Clear();
        }

        internal async Task<string> PreparePending()
        {
            var next = Release(2, ("data/asset.bin", "pending data"));
            using var session = new InstallSession(Options(), Guard);
            await session.Acquire(default);
            var changes = session.Plan(next, session.State());
            string transaction = Path.Combine(session.Fs.Control, "transactions", Guid.NewGuid().ToString("N"));
            session.Fs.DirectoryFor(Path.Combine(transaction, "staged"));
            session.Fs.DirectoryFor(Path.Combine(transaction, "backups"));
            await InstallDownloads.Stage(Http, session, next, changes, transaction, default);
            InstallPreparation.Prepare(session, next, changes, transaction);
            return transaction;
        }

        internal void Junction(string path, string target)
        {
            try { ClientDirectoryJunction.CreateNew(path, target); }
            finally { if (ExistsReparse(path)) OwnedLinks.Add(path, target); }
        }

        internal SortedDictionary<string, string> Snapshot()
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Walk(string root)
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(root))
                {
                    var attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) continue; // Never follow an alias in a fixture snapshot.
                    if ((attributes & FileAttributes.Directory) != 0) Walk(path);
                    else result.Add(Path.GetRelativePath(Client, path), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
                }
            }
            Walk(Client);
            return result;
        }

        public void Dispose()
        {
            var beforeLinkRemoval = Snapshot();
            foreach (string name in Assets)
            {
                string alias = Alias(name);
                if (ExistsReparse(alias) && !OwnedLinks.ContainsKey(alias)) OwnedLinks.Add(alias, Target(name));
            }
            foreach (var link in OwnedLinks.Reverse())
                if (ExistsReparse(link.Key)) ClientDirectoryJunction.DeleteExact(link.Key, link.Value);
            AssertSameFiles(beforeLinkRemoval, Snapshot());
            Http.Dispose(); Key.Dispose();
            string root = Path.GetFullPath(Root);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), root, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("wolf-layout-", Path.GetFileName(root), StringComparison.Ordinal);
            Assert.False(ExistsReparse(root));
            Directory.Delete(root, recursive: true);
        }
    }

    private static bool ExistsReparse(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static void AssertSameFiles(SortedDictionary<string, string> expected, SortedDictionary<string, string> actual)
    { Assert.Equal(expected.Keys, actual.Keys); foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]); }

    private sealed class RecordingGuard(Action<int>? onCheck = null) : IUpdateProcessGuard
    {
        internal List<string[]> Calls { get; } = [];
        internal List<CancellationToken> Tokens { get; } = [];
        public async Task EnsureStoppedAsync(IReadOnlyList<string> paths, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Calls.Add(paths.ToArray());
            Tokens.Add(cancellation);
            await new ConfiguredExecutableProcessGuard().EnsureStoppedAsync(paths, cancellation);
            onCheck?.Invoke(Calls.Count);
        }
    }

    private sealed class PayloadHandler : HttpMessageHandler
    {
        internal Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.OrdinalIgnoreCase);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { RequestMessage = request, Content = new ByteArrayContent(Blobs[request.RequestUri!.Segments[^1]]) });
        }
    }
}
