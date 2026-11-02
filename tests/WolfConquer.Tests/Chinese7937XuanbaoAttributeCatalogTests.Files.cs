using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAttributeCatalogTests
{
    [Fact]
    public void ExplicitPinnedOrdinaryFileReturnsOwnedCatalogAndReleasesReadLease()
    {
        WithAttributeFixture(fixture =>
        {
            var bytes = Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module());
            fixture.Create(bytes);
            var pin = SHA256.HashData(bytes);
            Assert.True(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(fixture.PathName, Current, pin,
                out var parsed, out var failure), failure.ToString());
            var catalog = Assert.IsType<Chinese7937XuanbaoAttributeCatalog>(parsed);
            Array.Clear(pin); Array.Fill(bytes, (byte)'!');
            Assert.True(catalog.TryGetGoldLimitFlag(10, 1, out var flag)); Assert.True(flag);
            using var reopened = new FileStream(fixture.PathName, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(reopened.CanWrite);
        });
    }

    [Fact]
    public void WrongPinnedLaterImageDoesNotRepairInputOrMutateEarlierCatalog()
    {
        WithAttributeFixture(fixture =>
        {
            var first = Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module());
            fixture.Create(first);
            var pin = SHA256.HashData(first);
            Assert.True(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(fixture.PathName, Current, pin, out var result, out _));
            var catalog = Assert.IsType<Chinese7937XuanbaoAttributeCatalog>(result);
            var changed = Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module(goldRows: "[10] = { [1] = false, },"));
            File.WriteAllBytes(fixture.PathName, changed);
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(fixture.PathName, Current, pin, out var rejected, out var failure));
            Assert.Null(rejected); Assert.Equal(Chinese7937XuanbaoInfoFailure.PinMismatch, failure);
            Assert.True(catalog.TryGetGoldLimitFlag(10, 1, out var flag)); Assert.True(flag);
            Assert.Equal(changed, File.ReadAllBytes(fixture.PathName));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    [InlineData(33)]
    public void WrongPinWidthRefusesBeforeFilePublication(int length)
    {
        WithAttributeFixture(fixture =>
        {
            fixture.Create(Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module()));
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(fixture.PathName, Current, new byte[length], out var catalog, out var failure));
            Assert.Null(catalog); Assert.Equal(Chinese7937XuanbaoInfoFailure.PinRequired, failure);
        });
    }

    [Fact]
    public void RelativeMissingDirectoryAndWrongVersionDoNotFindFallbackData()
    {
        WithAttributeFixture(fixture =>
        {
            var bytes = Chinese7937XuanbaoAttributeFixture.Gbk(Chinese7937XuanbaoAttributeFixture.Module());
            fixture.Create(bytes);
            var pin = SHA256.HashData(bytes);
            foreach (var entry in new[] { ("relative.lua", Chinese7937XuanbaoInfoFailure.PathRequired),
                (fixture.Root, Chinese7937XuanbaoInfoFailure.UnsafePath),
                (Path.Combine(fixture.Root, "missing.lua"), Chinese7937XuanbaoInfoFailure.UnsafePath) })
            {
                Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(entry.Item1, Current, pin, out var catalog, out var failure));
                Assert.Null(catalog); Assert.Equal(entry.Item2, failure);
            }
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(fixture.PathName,
                Chinese7937XuanbaoInfoVersion.Catalog7931, pin, out var rejected, out var versionFailure));
            Assert.Null(rejected); Assert.Equal(Chinese7937XuanbaoInfoFailure.UnsupportedVersion, versionFailure);
            Assert.Equal(bytes, File.ReadAllBytes(fixture.PathName));
        });
    }

    [Theory]
    [InlineData("\\\\server\\share\\catalog.lua")]
    [InlineData("\\\\?\\C:\\catalog.lua")]
    public void NetworkAndDeviceSpellingsRefuseWithoutReading(string path)
    {
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(path, Current, new byte[32], out var catalog, out var failure));
        Assert.Null(catalog);
        Assert.Equal(OperatingSystem.IsWindows() ? Chinese7937XuanbaoInfoFailure.UnsafePath : Chinese7937XuanbaoInfoFailure.PathRequired, failure);
    }

    [Fact]
    public void PinDoesNotMakeMalformedOrOversizedCatalogAcceptable()
    {
        WithAttributeFixture(malformed =>
        {
            malformed.Create([0x81]);
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(malformed.PathName, Current,
                SHA256.HashData(new byte[] { 0x81 }), out var catalog, out var failure));
            Assert.Null(catalog); Assert.Equal(Chinese7937XuanbaoInfoFailure.InvalidGbk, failure);
        });
        WithAttributeFixture(oversized =>
        {
            var bytes = new byte[Chinese7937DecodedXuanbaoInfo.MaximumBytes + 1]; oversized.Create(bytes);
            Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadAttributeCatalogFile(oversized.PathName, Current,
                SHA256.HashData(bytes), out var catalog, out var failure));
            Assert.Null(catalog); Assert.Equal(Chinese7937XuanbaoInfoFailure.InputBounds, failure);
        });
    }

    private static void WithAttributeFixture(Action<AttributeFileFixture> action)
    {
        var fixture = new AttributeFileFixture();
        Exception? primary = null;
        try { action(fixture); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { fixture.Dispose(); }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }
    }

    private sealed class AttributeFileFixture : IDisposable
    {
        private const string Prefix = "wolf-xuanbao-attribute-tests-";
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string PathName => Path.Combine(Root, "catalog.lua");
        internal AttributeFileFixture() => Directory.CreateDirectory(Root);
        internal void Create(byte[] bytes)
        {
            using var output = new FileStream(PathName, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            output.Write(bytes);
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root); var name = Path.GetFileName(path);
            if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[Prefix.Length..], "N", out _) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Invalid owned XuanBao attribute fixture cleanup root.");
            Directory.Delete(path, recursive: true);
        }
    }
}
