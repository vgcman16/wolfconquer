using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WolfConquer.Updater;
using WolfConquer.Updates.Hosting;
using WolfConquer.Updates.Publishing;

namespace WolfConquer.Updates.Tests;

public sealed partial class UpdaterPipelineTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-updater-pipeline-" + Guid.NewGuid().ToString("N"));
        internal string ExecutableName => "WolfPipelineFixture-" + Path.GetFileName(Root)["wolf-updater-pipeline-".Length..] + ".exe";
        internal string Source => Path.Combine(Root, "source");
        internal string Published => Path.Combine(Root, "published");
        internal string Bootstrap => Path.Combine(Root, "bootstrap");
        internal string Client => Path.Combine(Root, "client");
        internal byte[] Executable { get; } = Encoding.UTF8.GetBytes("Synthetic fixture bytes. NEVER LAUNCH.");
        private ECDsa Key { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        internal string PublicKey => Key.ExportSubjectPublicKeyInfoPem();
        internal ReadOnlyFeedHost Host { get; private set; } = null!;

        private Fixture()
        {
            ValidateOwnedRoot();
            if (Directory.Exists(Root)) throw new IOException("Owned pipeline root already exists.");
            Directory.CreateDirectory(Source); Directory.CreateDirectory(Published); Directory.CreateDirectory(Bootstrap);
            Directory.CreateDirectory(Path.Combine(Source, "data"));
            File.WriteAllBytes(Path.Combine(Source, ExecutableName), Executable);
        }

        internal static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            try
            {
                fixture.Host = await ReadOnlyFeedHost.StartAsync(new FeedHostOptions(fixture.Published, Port: 0));
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        internal PublishedRelease Publish(long sequence, string data, string? previousDataHash)
        {
            File.WriteAllText(Path.Combine(Source, "data/catalog.dat"), data, new UTF8Encoding(false));
            var now = DateTimeOffset.UtcNow;
            return ReleasePublisher.Publish(new ReleasePublishOptions(Source, Published, "fixture-client", "preview", sequence,
                Host.BaseAddress, now.AddMinutes(-1), now.AddHours(1), includeBundle: true, allowLoopbackHttp: true),
                [new(ExecutableName, sequence == 1 ? null : Convert.ToHexString(SHA256.HashData(Executable))),
                 new("data/catalog.dat", previousDataHash)], Key, "pipeline-test-key");
        }

        internal string WriteConfig(string client, string name = "wolfconquer-updates.json")
        {
            string path = Path.Combine(Bootstrap, name);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, productId = "fixture-client", channel = "preview",
                feedUrl = new Uri(Host.BaseAddress, "feeds/fixture-client-preview.json").AbsoluteUri,
                keyId = "pipeline-test-key", publicKeySpkiPem = PublicKey, installDirectory = client,
                launchRelativePath = ExecutableName, launchArguments = Array.Empty<string>(),
                protectedExecutables = new[] { ExecutableName }, allowLoopbackHttp = true
            });
            File.WriteAllBytes(path, bytes);
            return path;
        }

        internal string ClientFile(string relative) => Path.Combine(Client, relative);
        internal string Blob(string hash) => Path.Combine(Published, "blobs", hash.ToLowerInvariant());

        internal async Task RemoveBlob(string hash)
        {
            // A just-completed HEAD may still be disposing its owned read handle. Never touch another path.
            string path = Blob(hash);
            for (int attempt = 0; ; attempt++)
            {
                try { File.Delete(path); return; }
                catch (IOException) when (attempt < 50) { await Task.Delay(20); }
            }
        }

        internal async Task<RunResult> Run(params string[] args)
        {
            using var output = new StringWriter(); using var error = new StringWriter();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            int code = await UpdaterApplication.RunAsync(args, output, error, deadline.Token);
            return new RunResult(code, output.ToString(), error.ToString());
        }

        internal static SortedDictionary<string, Stamp> Snapshot(string root)
        {
            var result = new SortedDictionary<string, Stamp>(StringComparer.Ordinal);
            foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                result.Add(Path.GetRelativePath(root, path), new Stamp(new FileInfo(path).Length,
                    File.GetLastWriteTimeUtc(path).Ticks, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))));
            return result;
        }

        internal static void AssertSnapshot(SortedDictionary<string, Stamp> expected, SortedDictionary<string, Stamp> actual)
        {
            Assert.Equal(expected.Keys, actual.Keys);
            foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
        }

        public async ValueTask DisposeAsync()
        {
            try { if (Host is not null) await Host.DisposeAsync(); }
            finally
            {
                Key.Dispose();
                ValidateOwnedRoot();
                if (Directory.Exists(Root)) Directory.Delete(Path.GetFullPath(Root), recursive: true);
            }
        }

        private void ValidateOwnedRoot()
        {
            string root = Path.GetFullPath(Root);
            string parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            const string prefix = "wolf-updater-pipeline-";
            string name = Path.GetFileName(root);
            if (!string.Equals(Path.GetDirectoryName(root), parent, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[prefix.Length..], "N", out _) ||
                (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0 ||
                (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0))
                throw new InvalidOperationException("Refusing unsafe pipeline-fixture boundary.");
        }
    }

    private sealed record Stamp(long Length, long LastWriteTicks, string Sha256);
    private sealed record RunResult(int ExitCode, string Output, string Error);
}
