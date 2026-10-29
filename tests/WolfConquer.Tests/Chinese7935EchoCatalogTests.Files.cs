using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7935EchoCatalogTests
{
    [Fact]
    public void ExplicitDecodedFileAndPinProduceAnOwnedSnapshotAndReleaseTheReadLease()
    {
        using var fixture = new DecodedFileFixture(); var bytes = Module(Row()); fixture.Create(bytes);
        var expected = SHA256.HashData(bytes);
        Assert.True(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(fixture.PathName, Current, expected,
            out var snapshot, out var failure), failure.ToString());
        var loaded = Assert.IsType<Chinese7935EchoCatalogSnapshot>(snapshot);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), loaded.ContentSha256);
        Array.Clear(expected); Array.Fill(bytes, (byte)'!');
        Assert.Equal(17U, loaded.Rows[0].ExtraAttributeValue);
        using var reopened = new FileStream(fixture.PathName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(reopened.CanWrite); Assert.Equal(1, loaded.Count);
    }

    [Theory]
    [InlineData(0)] [InlineData(31)] [InlineData(33)]
    public void InvalidExpectedPinWidthCannotLoad(int length)
    {
        using var fixture = new DecodedFileFixture(); fixture.Create(Module(Row()));
        Assert.False(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(fixture.PathName, Current, new byte[length],
            out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.PinRequired, failure);
    }

    [Fact]
    public void RelativeDirectoryAndMissingPathsAreNeverFallbackCatalogs()
    {
        using var fixture = new DecodedFileFixture(); var bytes = Module(Row()); fixture.Create(bytes);
        var pin = SHA256.HashData(bytes);
        foreach (var pair in new[] { ("catalog.decoded", Chinese7935EchoCatalogFailure.PathRequired),
            (fixture.Root, Chinese7935EchoCatalogFailure.UnsafePath),
            (System.IO.Path.Combine(fixture.Root, "missing.decoded"), Chinese7935EchoCatalogFailure.UnsafePath) })
        {
            Assert.False(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(pair.Item1, Current, pin, out var snapshot, out var failure));
            Assert.Null(snapshot); Assert.Equal(pair.Item2, failure);
        }
        Assert.Equal(bytes, File.ReadAllBytes(fixture.PathName));
    }

    [Fact]
    public void FailedReplacementLoadCannotMutatePreviouslyReturnedData()
    {
        using var fixture = new DecodedFileFixture(); var original = Module(Row()); fixture.Create(original);
        var pin = SHA256.HashData(original);
        Assert.True(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(fixture.PathName, Current, pin, out var first, out _));
        var owned = Assert.IsType<Chinese7935EchoCatalogSnapshot>(first);
        var changed = Module(Row(extra: "29")); File.WriteAllBytes(fixture.PathName, changed);
        Assert.False(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(fixture.PathName, Current, pin, out var rejected, out var failure));
        Assert.Null(rejected); Assert.Equal(Chinese7935EchoCatalogFailure.PinMismatch, failure);
        Assert.Equal(17U, owned.Rows[0].ExtraAttributeValue);
        Assert.True(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(fixture.PathName, Current, SHA256.HashData(changed), out var next, out _));
        Assert.Equal(29U, Assert.IsType<Chinese7935EchoCatalogSnapshot>(next).Rows[0].ExtraAttributeValue);
        Assert.Equal(17U, owned.CopyThresholdRows()[0].Bonus);
    }

    [Theory]
    [InlineData("\\\\server\\share\\catalog.decoded")]
    [InlineData("\\\\?\\C:\\catalog.decoded")]
    public void NetworkAndDeviceSpellingsNeverCauseALoad(string path)
    {
        Assert.False(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(path, Current, new byte[32], out var snapshot, out var failure));
        Assert.Null(snapshot);
        Assert.Equal(OperatingSystem.IsWindows() ? Chinese7935EchoCatalogFailure.UnsafePath : Chinese7935EchoCatalogFailure.PathRequired, failure);
    }

    [Fact]
    public void PinnedMalformedAndOversizedFilesStillRefuseWithoutPublishing()
    {
        using var malformed = new DecodedFileFixture(); malformed.Create([0xff]);
        Assert.False(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(malformed.PathName, Current, SHA256.HashData(new byte[] { 0xff }),
            out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.InvalidUtf8, failure);
        using var oversized = new DecodedFileFixture(); var bytes = new byte[Chinese7935DecodedEchoCatalog.MaximumBytes + 1]; oversized.Create(bytes);
        Assert.False(Chinese7935DecodedEchoCatalog.TryLoadDecodedFile(oversized.PathName, Current, SHA256.HashData(bytes), out snapshot, out failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7935EchoCatalogFailure.InputBounds, failure);
    }

    private sealed class DecodedFileFixture : IDisposable
    {
        private const string Prefix = "wolf-echo-decoded-tests-";
        internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string PathName => System.IO.Path.Combine(Root, "catalog.decoded");
        internal DecodedFileFixture() => Directory.CreateDirectory(Root);
        internal void Create(byte[] bytes)
        {
            using var stream = new FileStream(PathName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
        }
        public void Dispose()
        {
            var path = System.IO.Path.GetFullPath(Root); var name = System.IO.Path.GetFileName(path);
            if (!string.Equals(System.IO.Path.GetDirectoryName(path),
                    System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[Prefix.Length..], "N", out _) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Invalid owned Echo catalog fixture cleanup root.");
            Directory.Delete(path, recursive: true);
        }
    }
}
