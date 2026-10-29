using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoInfoTests
{
    [Fact]
    public void ExplicitPinnedOrdinaryDecodedFileProducesOwnedDataAndReleasesItsReadLease()
    {
        using var fixture = new InfoFileFixture(); var bytes = Gbk(Module()); fixture.Create(bytes);
        var pin = SHA256.HashData(bytes);
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(fixture.PathName, Current, pin, out var parsed, out var failure), failure.ToString());
        var snapshot = Assert.IsType<Chinese7937XuanbaoInfoSnapshot>(parsed);
        Array.Clear(pin); Array.Fill(bytes, (byte)'!');
        Assert.True(snapshot.TryGetValue("alpha", out var value)); Assert.Equal(7U, value);
        using var reopened = new FileStream(fixture.PathName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(reopened.CanWrite);
    }

    [Theory]
    [InlineData(0)] [InlineData(31)] [InlineData(33)]
    public void MissingOrWrongWidthPinCannotLoad(int length)
    {
        using var fixture = new InfoFileFixture(); fixture.Create(Gbk(Module()));
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(fixture.PathName, Current, new byte[length], out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7937XuanbaoInfoFailure.PinRequired, failure);
    }

    [Fact]
    public void FailedNewLoadCannotMutateThePreviouslyReturnedSnapshotOrRepairTheInput()
    {
        using var fixture = new InfoFileFixture(); var first = Gbk(Module()); fixture.Create(first);
        var pin = SHA256.HashData(first);
        Assert.True(Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(fixture.PathName, Current, pin, out var parsed, out _));
        var snapshot = Assert.IsType<Chinese7937XuanbaoInfoSnapshot>(parsed);
        var changed = Gbk(Module("[\"alpha\"] = 19,")); File.WriteAllBytes(fixture.PathName, changed);
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(fixture.PathName, Current, pin, out var rejected, out var failure));
        Assert.Null(rejected); Assert.Equal(Chinese7937XuanbaoInfoFailure.PinMismatch, failure);
        Assert.True(snapshot.TryGetValue("alpha", out var value)); Assert.Equal(7U, value);
        Assert.Equal(changed, File.ReadAllBytes(fixture.PathName));
    }

    [Fact]
    public void RelativeMissingAndDirectoryPathsAreNotFallbackCatalogs()
    {
        using var fixture = new InfoFileFixture(); var bytes = Gbk(Module()); fixture.Create(bytes);
        var pin = SHA256.HashData(bytes);
        foreach (var pair in new[] { ("info.decoded", Chinese7937XuanbaoInfoFailure.PathRequired),
            (fixture.Root, Chinese7937XuanbaoInfoFailure.UnsafePath),
            (Path.Combine(fixture.Root, "missing.decoded"), Chinese7937XuanbaoInfoFailure.UnsafePath) })
        {
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(pair.Item1, Current, pin, out var snapshot, out var failure));
            Assert.Null(snapshot); Assert.Equal(pair.Item2, failure);
        }
        Assert.Equal(bytes, File.ReadAllBytes(fixture.PathName));
    }

    [Theory]
    [InlineData("\\\\server\\share\\info.decoded")]
    [InlineData("\\\\?\\C:\\info.decoded")]
    public void NetworkAndDeviceSpellingsNeverLoad(string path)
    {
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(path, Current, new byte[32], out var snapshot, out var failure));
        Assert.Null(snapshot);
        Assert.Equal(OperatingSystem.IsWindows() ? Chinese7937XuanbaoInfoFailure.UnsafePath : Chinese7937XuanbaoInfoFailure.PathRequired, failure);
    }

    [Fact]
    public void PinnedMalformedAndOversizedFilesStillRefuseWithoutDirtyOutputs()
    {
        using var malformed = new InfoFileFixture(); malformed.Create([0x81]);
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(malformed.PathName, Current, SHA256.HashData(new byte[] { 0x81 }), out var snapshot, out var failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7937XuanbaoInfoFailure.InvalidGbk, failure);
        using var oversized = new InfoFileFixture(); var bytes = new byte[Chinese7937DecodedXuanbaoInfo.MaximumBytes + 1]; oversized.Create(bytes);
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadDecodedFile(oversized.PathName, Current, SHA256.HashData(bytes), out snapshot, out failure));
        Assert.Null(snapshot); Assert.Equal(Chinese7937XuanbaoInfoFailure.InputBounds, failure);
    }

    private sealed class InfoFileFixture : IDisposable
    {
        private const string Prefix = "wolf-xuanbao-info-tests-";
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string PathName => Path.Combine(Root, "info.decoded");
        internal InfoFileFixture() => Directory.CreateDirectory(Root);
        internal void Create(byte[] bytes)
        {
            using var stream = new FileStream(PathName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root); var name = Path.GetFileName(path);
            if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[Prefix.Length..], "N", out _) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Invalid owned XuanBao Info fixture cleanup root.");
            Directory.Delete(path, recursive: true);
        }
    }
}
