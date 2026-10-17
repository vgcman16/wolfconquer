using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class CharacterSnapshotScalarPersistenceConcurrencyTests : IDisposable
{
    private const long CharacterUid = 1_293_600;

    private readonly string _usersRoot = Path.Combine(
        Path.GetTempPath(),
        "wolf-character-scalar-concurrency-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task RetriesTransientSameDestinationContentionAndPreservesEncoding()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot();
        var original = LoadSnapshot();
        var updated = original with { X = original.X + 1 };
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<(bool Succeeded, string Path, string Reason)> persistence;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            persistence = Task.Run(() =>
            {
                started.SetResult(true);
                var succeeded = CharacterSnapshotScalarPersistence.TryPersist(
                    _usersRoot,
                    updated,
                    out var persistedPath,
                    out var reason);
                return (succeeded, persistedPath, reason);
            });

            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(50);
        }

        var result = await persistence.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(path, result.Path);
        var bytes = File.ReadAllBytes(path);
        Assert.Contains((byte)0xE9, bytes);
        Assert.Throws<DecoderFallbackException>(() => StrictUtf8().GetString(bytes));
        Assert.Contains("Name=Andr\u00e9", Windows1252().GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(updated.X, LoadSnapshot().X);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public async Task EquivalentRootsSerializeConcurrentWritesToSameDestination()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot(payloadLineCount: 50_000);
        var original = LoadSnapshot();
        var roots = Enumerable.Range(0, 8)
            .Select(depth => Path.Combine(
                new[] { _usersRoot }.Concat(Enumerable.Repeat(".", depth)).ToArray()))
            .ToArray();
        using var ready = new CountdownEvent(roots.Length);
        using var start = new ManualResetEventSlim();

        var writes = roots.Select((root, index) => Task.Run(() =>
        {
            ready.Signal();
            start.Wait(TimeSpan.FromSeconds(5));
            var succeeded = CharacterSnapshotScalarPersistence.TryPersist(
                root,
                original with { X = original.X + index + 1 },
                out _,
                out var reason);
            return (Succeeded: succeeded, Reason: reason);
        })).ToArray();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
        start.Set();
        var results = await Task.WhenAll(writes).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.All(results, result => Assert.True(result.Succeeded, result.Reason));
        var persistedText = Windows1252().GetString(File.ReadAllBytes(path));
        Assert.Contains("Payload49999=preserve", persistedText, StringComparison.Ordinal);
        Assert.InRange(LoadSnapshot().X, original.X + 1, original.X + roots.Length);
        AssertNoTransactionArtifacts();
    }

    [Fact]
    public void FailedCommitLeavesOriginalSnapshotAndNoTransactionArtifacts()
    {
        Directory.CreateDirectory(_usersRoot);
        var path = WriteWindows1252Snapshot();
        var originalBytes = File.ReadAllBytes(path);
        var updated = LoadSnapshot() with { X = 999 };

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(CharacterSnapshotScalarPersistence.TryPersist(
                _usersRoot,
                updated,
                out _,
                out var reason));
            Assert.StartsWith("snapshotWriteFailed:attempts=5:hresult=0x", reason, StringComparison.Ordinal);
        }

        Assert.Equal(originalBytes, File.ReadAllBytes(path));
        AssertNoTransactionArtifacts();
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }
    }

    private string WriteWindows1252Snapshot(int payloadLineCount = 0)
    {
        var text = new StringBuilder()
            .AppendLine("[Character]")
            .AppendLine($"UID={CharacterUid}")
            .AppendLine("Name=Andr\u00e9")
            .AppendLine("Body=1003")
            .AppendLine("Face=154")
            .AppendLine("Haire=27")
            .AppendLine("Class=15")
            .AppendLine("FirstClass=15")
            .AppendLine("SecoundeClass=15")
            .AppendLine("Reborn=0")
            .AppendLine("Level=110")
            .AppendLine("Experience=1000")
            .AppendLine("Map=1002")
            .AppendLine("X=430")
            .AppendLine("Y=377")
            .AppendLine("PMap=1002")
            .AppendLine("PMapX=430")
            .AppendLine("PMapY=377")
            .AppendLine("Strength=10")
            .AppendLine("Agility=20")
            .AppendLine("Vitaliti=30")
            .AppendLine("Spirit=40")
            .AppendLine("Atributes=5")
            .AppendLine("MinHitPoints=1000")
            .AppendLine("MinMana=500")
            .AppendLine("Money=10000")
            .AppendLine("ConquerPoints=100")
            .AppendLine("BoundConquerPoints=50")
            .AppendLine("BankCps=25")
            .AppendLine("PkPoints=0")
            .AppendLine("Online=1")
            .AppendLine()
            .AppendLine("[Custom]");
        for (var index = 0; index < payloadLineCount; index++)
        {
            text.Append("Payload").Append(index).AppendLine("=preserve");
        }

        var path = Path.Combine(_usersRoot, CharacterUid + ".ini");
        File.WriteAllBytes(path, Windows1252().GetBytes(text.ToString()));
        return path;
    }

    private CharacterSnapshot LoadSnapshot()
    {
        var snapshot = new CharacterSnapshotLoader(_usersRoot).LoadByAccountId(CharacterUid);
        Assert.NotNull(snapshot);
        return snapshot;
    }

    private void AssertNoTransactionArtifacts()
    {
        Assert.Empty(Directory.EnumerateFiles(
            _usersRoot,
            "*.character-scalar-*",
            SearchOption.TopDirectoryOnly));
    }

    private static Encoding Windows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private static UTF8Encoding StrictUtf8()
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }
}
