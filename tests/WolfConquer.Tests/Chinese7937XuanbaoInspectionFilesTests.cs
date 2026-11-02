using System.Security.Cryptography;
using System.Text;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoInspectionFilesTests
{
    private static void WithFixture(Action<Fixture> body)
    {
        var fixture = new Fixture(); var errors = new List<Exception>();
        try { body(fixture); } catch (Exception primary) { errors.Add(primary); }
        try { fixture.Dispose(); } catch (Exception cleanup) { errors.Add(cleanup); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed class Fixture : IDisposable
    {
        private const string Prefix = "wolf-xuanbao-pinned-pair-";
        internal readonly string Root = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string InfoPath => Path.Combine(Root, "selected-info.lua");
        internal string AdditionPath => Path.Combine(Root, "selected-addition.dat");
        internal readonly byte[] Info = Chinese7937XuanbaoResolveFixture.Gbk(Chinese7937XuanbaoResolveFixture.Module());
        internal readonly byte[] Addition = ClientItemCatalogCodec.Encode(Encoding.ASCII.GetBytes("8@@17@@5@@000@@27@@\r\n2@@1@@2@@3@@4@@\r\n"));
        internal Fixture()
        {
            try { Directory.CreateDirectory(Root); File.WriteAllBytes(InfoPath, Info); File.WriteAllBytes(AdditionPath, Addition); }
            catch (Exception primary)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
                throw;
            }
        }
        internal byte[] InfoPin => SHA256.HashData(Info);
        internal byte[] AdditionPin => SHA256.HashData(Addition);
        internal bool Read(out Chinese7937XuanbaoResolveSnapshot? resolve, out Chinese7937XuanbaoAdditionRawCatalog? addition, out string failure) =>
            Chinese7937DecodedXuanbaoInfo.TryLoadResolveAndAdditionFiles(InfoPath, AdditionPath, 7937, InfoPin, AdditionPin,
                out resolve, out addition, out failure);
        internal void AssertReleased()
        {
            using var info = new FileStream(InfoPath, FileMode.Open, FileAccess.Read, FileShare.None);
            using var addition = new FileStream(AdditionPath, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        public void Dispose()
        {
            var full = Path.GetFullPath(Root); var name = Path.GetFileName(full);
            if (!string.Equals(Path.GetDirectoryName(full), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[Prefix.Length..], "N", out _))
                throw new InvalidOperationException("Invalid owned pinned-pair fixture root.");
            if (!Directory.Exists(full)) return;
            if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked fixture cleanup root refused.");
            Directory.Delete(full, recursive: true);
        }
    }

    [Fact]
    public void ActualPinnedPairPublishesBothOnlyAfterCaptureAndReleasesBothLeases() => WithFixture(f =>
    {
        var infoTime = File.GetLastWriteTimeUtc(f.InfoPath); var additionTime = File.GetLastWriteTimeUtc(f.AdditionPath);
        Assert.True(f.Read(out var resolve, out var addition, out var failure), failure);
        Assert.Equal("None", failure); Assert.NotNull(resolve); Assert.NotNull(addition);
        Assert.Equal(3, resolve.Fields.Length); Assert.Equal(2, addition.Rows.Count);
        Assert.Equal(Convert.ToHexString(f.InfoPin).ToLowerInvariant(), resolve.ContentSha256);
        Assert.Equal(Convert.ToHexString(f.AdditionPin).ToLowerInvariant(), addition.PackedSha256);
        Assert.Equal(f.Info, File.ReadAllBytes(f.InfoPath)); Assert.Equal(f.Addition, File.ReadAllBytes(f.AdditionPath));
        Assert.Equal(infoTime, File.GetLastWriteTimeUtc(f.InfoPath)); Assert.Equal(additionTime, File.GetLastWriteTimeUtc(f.AdditionPath));
        Assert.Equal(2, Directory.GetFiles(f.Root).Length); f.AssertReleased();
        File.WriteAllBytes(f.InfoPath, Encoding.ASCII.GetBytes("later-invalid"));
        Assert.False(f.Read(out var replacementResolve, out var replacementAddition, out failure));
        Assert.Null(replacementResolve); Assert.Null(replacementAddition); Assert.Equal("PinMismatch", failure);
        Assert.Equal(7U, resolve.SelectNumber(false)); Assert.Equal(27U, addition.Rows[0].NumericColumns[4]);
        f.AssertReleased();
    });

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void EitherPinMismatchPublishesNeitherSnapshotAndClosesBothReaders(bool info) => WithFixture(f =>
    {
        var infoPin = f.InfoPin; var additionPin = f.AdditionPin;
        (info ? infoPin : additionPin)[0] ^= 1;
        Assert.False(Chinese7937DecodedXuanbaoInfo.TryLoadResolveAndAdditionFiles(f.InfoPath, f.AdditionPath, 7937,
            infoPin, additionPin, out var resolve, out var addition, out var failure));
        Assert.Null(resolve); Assert.Null(addition); Assert.Equal("PinMismatch", failure); f.AssertReleased();
    });

    [Fact]
    public void AnUnopenableSecondFileReleasesTheFirstWithoutAPartialResolve() => WithFixture(f =>
    {
        using (var denied = new FileStream(f.AdditionPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(f.Read(out var resolve, out var addition, out var failure));
            Assert.Null(resolve); Assert.Null(addition); Assert.Equal("FileRead", failure);
            using var releasedFirst = new FileStream(f.InfoPath, FileMode.Open, FileAccess.Read, FileShare.None);
        }
        f.AssertReleased();
    });
}
