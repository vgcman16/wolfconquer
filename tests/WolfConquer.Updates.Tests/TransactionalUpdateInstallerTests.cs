using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Updates.Install;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Tests;

public sealed class TransactionalUpdateInstallerTests
{
    [WindowsFact]
    public async Task FreshInstallVerifiesAllBytesAndRetainsCommittedCopiesThenSkips()
    {
        using var fixture = new Fixture();
        var verified = fixture.Release(1, fixture.Entry("ini/a.ini", "alpha"), fixture.Entry("Conquer.exe", "client"));
        var result = await fixture.Installer.InstallAsync(verified, fixture.Options(true));
        Assert.Equal(2, result.InstalledFiles);
        Assert.Equal("alpha", File.ReadAllText(fixture.PathOf("ini/a.ini")));
        Assert.Equal("client", File.ReadAllText(fixture.PathOf("Conquer.exe")));
        Assert.Contains("committed", File.ReadAllText(Path.Combine(result.TransactionDirectory!, "journal.json")));
        Assert.True(File.Exists(Path.Combine(result.TransactionDirectory!, "state-next.json")));
        var requests = fixture.Handler.Requests;
        var again = await fixture.Installer.InstallAsync(verified, fixture.Options());
        Assert.Equal(0, again.InstalledFiles);
        Assert.Equal(2, again.SkippedFiles);
        Assert.Equal(requests, fixture.Handler.Requests);
        Assert.Null(again.TransactionDirectory);
    }

    [WindowsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BadSecondBlobNeverMutatesFirstTarget(bool badLength)
    {
        using var fixture = new Fixture();
        fixture.Put("first", "old-a"); fixture.Put("second", "old-b");
        var first = fixture.Entry("first", "new-a", "old-a");
        var second = fixture.Entry("second", "new-b", "old-b");
        fixture.Handler.Blobs[second.Sha256] = Encoding.UTF8.GetBytes(badLength ? "extra-long" : "WRONG");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(fixture.Release(1, first, second), fixture.Options()));
        Assert.Equal("old-a", File.ReadAllText(fixture.PathOf("first")));
        Assert.Equal("old-b", File.ReadAllText(fixture.PathOf("second")));
        Assert.False(File.Exists(fixture.PathOf(".wolfconquer-updates/state.json")));
    }

    [WindowsFact]
    public async Task BasePreconditionPreservesCustomFilesAndFreshModeRefusesNonemptyRoots()
    {
        using var fixture = new Fixture();
        fixture.Put("target", "custom"); fixture.Put("other-user-file", "keep");
        var release = fixture.Release(1, fixture.Entry("target", "new", "expected-old"));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(release, fixture.Options()));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(
            fixture.Release(1, fixture.Entry("missing", "new", "old")), fixture.Options()));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(
            fixture.Release(1, fixture.Entry("missing", "new")), fixture.Options(true)));
        Assert.Equal("custom", File.ReadAllText(fixture.PathOf("target")));
        Assert.Equal("keep", File.ReadAllText(fixture.PathOf("other-user-file")));
    }

    [WindowsFact]
    public async Task InstalledOwnershipAllowsDirectVersionJumpButNotUserEditsDowngradeOrConflict()
    {
        using var fixture = new Fixture();
        await fixture.Installer.InstallAsync(fixture.Release(1, fixture.Entry("owned", "one")), fixture.Options(true));
        var third = fixture.Release(3, fixture.Entry("owned", "three", "two"));
        await fixture.Installer.InstallAsync(third, fixture.Options());
        Assert.Equal("three", File.ReadAllText(fixture.PathOf("owned")));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(fixture.Release(2, fixture.Entry("owned", "two", "three")), fixture.Options()));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(fixture.Release(3, fixture.Entry("owned", "other", "three")), fixture.Options()));
        fixture.Put("owned", "user-modified");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(fixture.Release(4, fixture.Entry("owned", "four", "three")), fixture.Options()));
        Assert.Equal("user-modified", File.ReadAllText(fixture.PathOf("owned")));
    }

    [WindowsFact]
    public async Task CachedReleaseQueryIsExplicitlyMetadataOnlyAndRefusesUnfinishedTransactions()
    {
        using var fixture = new Fixture();
        var release = fixture.Release(1, fixture.Entry("owned", "one"));
        await fixture.Installer.InstallAsync(release, fixture.Options(true));
        fixture.Put("owned", "local-edit");
        var cached = await fixture.Installer.GetInstalledReleaseAsync(fixture.Options());
        Assert.Equal(release.EnvelopeSha256, cached!.EnvelopeSha256);
        Assert.Equal(release.EnvelopeLength, cached.EnvelopeLength);
        Assert.Equal("local-edit", File.ReadAllText(fixture.PathOf("owned")));
        fixture.Put("owned", "one");
        await fixture.Prepare(fixture.Release(2, fixture.Entry("owned", "two", "one")));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.GetInstalledReleaseAsync(fixture.Options()));
        Assert.Equal("one", File.ReadAllText(fixture.PathOf("owned")));
    }

    [WindowsFact]
    public async Task ReadonlySecondTargetRollsBackFirstAndKeepsOriginalBackup()
    {
        using var fixture = new Fixture();
        fixture.Put("first", "old-a"); fixture.Put("second", "old-b");
        var secondPath = fixture.PathOf("second");
        File.SetAttributes(secondPath, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => fixture.Installer.InstallAsync(fixture.Release(1,
                fixture.Entry("first", "new-a", "old-a"), fixture.Entry("second", "new-b", "old-b")), fixture.Options()));
            Assert.Equal("old-a", File.ReadAllText(fixture.PathOf("first")));
            Assert.Equal("old-b", File.ReadAllText(secondPath));
            var transaction = Directory.GetDirectories(fixture.PathOf(".wolfconquer-updates/transactions")).Single();
            Assert.Equal("old-a", File.ReadAllText(Path.Combine(transaction, "backups", "000000.bin")));
            Assert.Contains("rolledback", File.ReadAllText(Path.Combine(transaction, "journal.json")));
            Assert.False(File.Exists(fixture.PathOf(".wolfconquer-updates/state.json")));
        }
        finally { File.SetAttributes(secondPath, FileAttributes.Normal); }
    }

    [WindowsFact]
    public async Task RecoveryRestoresPreparedPartialApplyAndPreviousState()
    {
        using var fixture = new Fixture();
        await fixture.Installer.InstallAsync(fixture.Release(1, fixture.Entry("owned", "one")), fixture.Options(true));
        var originalState = File.ReadAllBytes(fixture.PathOf(".wolfconquer-updates/state.json"));
        var verified = fixture.Release(2, fixture.Entry("owned", "two", "one"), fixture.Entry("new-dir/new", "new"));
        var transaction = await fixture.Prepare(verified);
        File.Move(Path.Combine(transaction, "staged", "000000.bin"), fixture.PathOf("owned"), true);
        Directory.CreateDirectory(fixture.PathOf("new-dir"));
        File.Move(Path.Combine(transaction, "staged", "000001.bin"), fixture.PathOf("new-dir/new"));
        File.Copy(Path.Combine(transaction, "state-next.json"), fixture.PathOf(".wolfconquer-updates/state.json"), true);
        Assert.Equal(1, await fixture.Installer.RecoverAsync(fixture.Options()));
        Assert.Equal("one", File.ReadAllText(fixture.PathOf("owned")));
        Assert.Equal(originalState, File.ReadAllBytes(fixture.PathOf(".wolfconquer-updates/state.json")));
        Assert.False(File.Exists(fixture.PathOf("new-dir/new")));
        Assert.False(Directory.Exists(fixture.PathOf("new-dir")));
        Assert.Equal(0, await fixture.Installer.RecoverAsync(fixture.Options()));
        Assert.True(File.Exists(Path.Combine(transaction, "backups", "000000.bin")));
    }

    [WindowsFact]
    public async Task RecoveryRefusesExternalChangesAndRetainsPreparedJournal()
    {
        using var fixture = new Fixture(); fixture.Put("owned", "one");
        var transaction = await fixture.Prepare(fixture.Release(1, fixture.Entry("owned", "two", "one")));
        fixture.Put("owned", "unrelated");
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.RecoverAsync(fixture.Options()));
        Assert.Equal("unrelated", File.ReadAllText(fixture.PathOf("owned")));
        Assert.Contains("prepared", File.ReadAllText(Path.Combine(transaction, "journal.json")));
    }

    [WindowsFact]
    public async Task ActualFileLockAndBoundProcessDenialLeavePayloadUntouched()
    {
        using var fixture = new Fixture(); fixture.Put("owned", "one");
        var verified = fixture.Release(1, fixture.Entry("owned", "two", "one"));
        using (var held = new FileStream(fixture.PathOf("owned"), FileMode.Open, FileAccess.Read, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => fixture.Installer.InstallAsync(verified, fixture.Options()));
        var denied = new TransactionalUpdateInstaller(fixture.Client, new DeniedGuard());
        await Assert.ThrowsAsync<IOException>(() => denied.InstallAsync(verified, fixture.Options()));
        Assert.Equal("one", File.ReadAllText(fixture.PathOf("owned")));
    }

    [WindowsFact]
    public async Task LeaseAndConfiguredLimitsRefuseBeforeDownloadingAndStateParsingIsStrict()
    {
        using var fixture = new Fixture();
        var verified = fixture.Release(1, fixture.Entry("owned", "one"));
        Directory.CreateDirectory(fixture.PathOf(".wolfconquer-updates"));
        using (var lease = new FileStream(fixture.PathOf(".wolfconquer-updates/install.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => fixture.Installer.InstallAsync(verified, fixture.Options(true)));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(verified,
            fixture.Options(true) with { MaxFileBytes = 2 }));
        Assert.Equal(0, fixture.Handler.Requests);
        await fixture.Installer.InstallAsync(verified, fixture.Options(true));
        var statePath = fixture.PathOf(".wolfconquer-updates/state.json");
        var state = File.ReadAllText(statePath);
        File.WriteAllText(statePath, state.Replace("\"Path\":\"owned\"", "\"Path\":\"../owned\"", StringComparison.Ordinal));
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.GetInstalledReleaseAsync(fixture.Options()));
        Assert.Equal("one", File.ReadAllText(fixture.PathOf("owned")));
    }

    [WindowsFact]
    public async Task VerifiedBoundaryStillRejectsTraversalAndCaseCollisionBeforeDownloading()
    {
        using var fixture = new Fixture();
        foreach (var files in new[]
        {
            new[] { fixture.Entry("../escape", "bad") },
            new[] { fixture.Entry("A", "a"), fixture.Entry("a", "b") }
        })
        {
            // Deliberately malformed internal wrapper exercises installer's own filesystem defense.
            var manifest = new UpdateManifest(1, "product", "stable", 1, DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1), new Uri("https://updates.example.test/"), files);
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(
                new VerifiedUpdateManifest(manifest, new string('A', 64), "test-key", new string('B', 64), 1, new string('C', 64)), fixture.Options(true)));
        }
        Assert.Equal(0, fixture.Handler.Requests);
    }

    [WindowsTheory]
    [InlineData("valid")]
    [InlineData("extra")]
    [InlineData("traversal")]
    [InlineData("alias")]
    [InlineData("symlink")]
    [InlineData("bad-entry-hash")]
    public async Task BundleTransportRequiresExactSafeSnapshotEntries(string scenario)
    {
        using var fixture = new Fixture();
        var file = fixture.Entry("ini/a", "data");
        using var archiveBytes = new MemoryStream();
        using (var zip = new ZipArchive(archiveBytes, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(scenario == "traversal" ? "../a" : scenario == "alias" ? "INI/a" : "ini/a");
            if (scenario == "symlink") entry.ExternalAttributes = unchecked((int)0xA1FF0000);
            using (var stream = entry.Open()) stream.Write(Encoding.UTF8.GetBytes(scenario == "bad-entry-hash" ? "nope" : "data"));
            if (scenario == "extra") zip.CreateEntry("extra");
        }
        var bytes = archiveBytes.ToArray(); var hash = Hash(bytes); fixture.Handler.Blobs[hash] = bytes;
        var verified = fixture.Release(1, new[] { file }, new UpdateBundle(bytes.Length, hash));
        if (scenario == "valid")
        {
            await fixture.Installer.InstallAsync(verified, fixture.Options(true));
            Assert.Equal("data", File.ReadAllText(fixture.PathOf("ini/a")));
            Assert.Equal(1, fixture.Handler.Requests);
            Assert.Equal(hash.ToLowerInvariant(), fixture.Handler.RequestedHashes.Single());
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(verified, fixture.Options(true)));
            Assert.False(File.Exists(fixture.PathOf("ini/a")));
        }
    }

    [WindowsFact]
    public async Task SmallPatchFetchesOnlyChangedBlobWhileFreshInstallUsesTheSameFullBundle()
    {
        using var fixture = new Fixture();
        var large = fixture.Entry("large", new string('L', 4096));
        await fixture.Installer.InstallAsync(fixture.Release(1, fixture.Entry("small", "old"), large), fixture.Options(true));
        var small = fixture.Entry("small", "new", "old");
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var stream = archive.CreateEntry("small", CompressionLevel.NoCompression).Open())
                stream.Write(Encoding.UTF8.GetBytes("new"));
            using (var stream = archive.CreateEntry("large", CompressionLevel.NoCompression).Open())
                stream.Write(Encoding.UTF8.GetBytes(new string('L', 4096)));
        }
        var bytes = memory.ToArray(); var hash = Hash(bytes); var bundle = new UpdateBundle(bytes.Length, hash);
        fixture.Handler.Blobs[hash] = bytes;
        var priorRequests = fixture.Handler.Requests;
        var patched = await fixture.Installer.InstallAsync(fixture.Release(2, new[] { small, large }, bundle), fixture.Options());
        Assert.Equal(1, patched.InstalledFiles);
        Assert.Equal(1, patched.SkippedFiles);
        Assert.Equal(small.Sha256.ToLowerInvariant(), fixture.Handler.RequestedHashes.Skip(priorRequests).Single());
        Assert.False(File.Exists(Path.Combine(patched.TransactionDirectory!, "bundle.zip")));
        using var fresh = new Fixture();
        fresh.Handler.Blobs[hash] = bytes; // Individual blobs deliberately unavailable on this fresh transport.
        await fresh.Installer.InstallAsync(fresh.Release(2, new[] { small, large }, bundle), fresh.Options(true));
        Assert.Equal(hash.ToLowerInvariant(), fresh.Handler.RequestedHashes.Single());
        Assert.Equal("new", File.ReadAllText(fresh.PathOf("small")));
        Assert.Equal(new string('L', 4096), File.ReadAllText(fresh.PathOf("large")));
    }

    [WindowsFact]
    public async Task ReparseTargetIsRejectedWithoutFollowingIt()
    {
        using var fixture = new Fixture(); using var outside = new Fixture();
        outside.Put("preserved", "outside");
        try { Directory.CreateSymbolicLink(fixture.PathOf("linked"), outside.Root); }
        catch (UnauthorizedAccessException) { throw Xunit.Sdk.SkipException.ForSkip("Windows symlink privilege/developer mode is unavailable for this reparse fixture."); }
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Installer.InstallAsync(
            fixture.Release(1, fixture.Entry("linked/preserved", "new", "outside")), fixture.Options()));
        Assert.Equal("outside", File.ReadAllText(outside.PathOf("preserved")));
        Directory.Delete(fixture.PathOf("linked"));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private sealed class StoppedGuard : IUpdateProcessGuard
    {
        public Task EnsureStoppedAsync(IReadOnlyList<string> paths, CancellationToken cancellation) => Task.CompletedTask;
    }
    private sealed class DeniedGuard : IUpdateProcessGuard
    {
        public Task EnsureStoppedAsync(IReadOnlyList<string> paths, CancellationToken cancellation) => throw new IOException("Synthetic bound process guard denies installation.");
    }
    private sealed class BlobHandler : HttpMessageHandler
    {
        internal Dictionary<string, byte[]> Blobs { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal int Requests;
        internal List<string> RequestedHashes { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Requests++;
            var hash = request.RequestUri!.Segments[^1];
            RequestedHashes.Add(hash);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { RequestMessage = request, Content = new ByteArrayContent(Blobs[hash]) });
        }
    }
    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-update-test-" + Guid.NewGuid().ToString("N"));
        internal BlobHandler Handler { get; } = new();
        internal HttpClient Client { get; }
        internal TransactionalUpdateInstaller Installer { get; }
        internal Fixture()
        {
            Directory.CreateDirectory(Root); Client = new HttpClient(Handler);
            Installer = new(Client, new StoppedGuard());
        }
        internal string PathOf(string path) => Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
        internal UpdateInstallOptions Options(bool fresh = false) => new(Root, new[] { "Conquer.exe" }) { AllowFreshInstall = fresh };
        internal void Put(string path, string value)
        {
            var destination = PathOf(path); Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllText(destination, value, new UTF8Encoding(false));
        }
        internal UpdateFileEntry Entry(string path, string value, string? original = null)
        {
            var bytes = Encoding.UTF8.GetBytes(value); var hash = Hash(bytes); Handler.Blobs[hash] = bytes;
            return new(path, bytes.Length, hash, original is null ? null : Hash(Encoding.UTF8.GetBytes(original)));
        }
        internal VerifiedUpdateManifest Release(long sequence, params UpdateFileEntry[] files) => Release(sequence, files, null);
        internal VerifiedUpdateManifest Release(long sequence, UpdateFileEntry[] files, UpdateBundle? bundle)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var manifest = new UpdateManifest(1, "product", "stable", sequence, DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddDays(1), new Uri("https://updates.example.test/"), files, bundle);
            return SignedUpdateManifest.VerifyEnvelope(SignedUpdateManifest.CreateEnvelope(manifest, "test-key", key),
                "test-key", key.ExportSubjectPublicKeyInfoPem(), new UpdateVerificationContext("product", "stable", 1, DateTimeOffset.UtcNow));
        }
        internal async Task<string> Prepare(VerifiedUpdateManifest verified)
        {
            using var session = new InstallSession(Options(), new StoppedGuard()); await session.Acquire(default);
            var changes = session.Plan(verified, session.State());
            var transaction = Path.Combine(session.Fs.Control, "transactions", Guid.NewGuid().ToString("N"));
            session.Fs.DirectoryFor(Path.Combine(transaction, "staged")); session.Fs.DirectoryFor(Path.Combine(transaction, "backups"));
            await InstallDownloads.Stage(Client, session, verified, changes, transaction, default);
            InstallPreparation.Prepare(session, verified, changes, transaction);
            return transaction;
        }
        public void Dispose() { Client.Dispose(); Directory.Delete(Root, recursive: true); }
    }
}

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Transactional native file moves require Windows."; }
}
public sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute() { if (!OperatingSystem.IsWindows()) Skip = "Transactional native file moves require Windows."; }
}
