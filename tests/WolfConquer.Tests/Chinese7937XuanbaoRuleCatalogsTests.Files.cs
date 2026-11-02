using System.Security.Cryptography;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoRuleCatalogsTests
{
    [Fact]
    public void TwoPinnedOrdinaryFilesProduceOwnedRecordsAndReleaseBothLeases()
    {
        WithFiles(fixture =>
        {
            var composePin = SHA256.HashData(fixture.ComposeBytes); var randomPin = SHA256.HashData(fixture.RandomBytes);
            Assert.True(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(fixture.ComposePath, fixture.RandomPath, 7937,
                composePin, randomPin, out var parsed, out var failure), failure.ToString());
            var catalogs = Assert.IsType<Chinese7937XuanbaoRuleCatalogs>(parsed);
            Array.Clear(composePin); Array.Clear(randomPin);
            Assert.Equal(Chinese7937XuanbaoRuleCatalogs.ComposeSourceFile, catalogs.ComposeLimits[0].SourceFile);
            Assert.Equal(Chinese7937XuanbaoRuleCatalogs.RandomSourceFile, catalogs.RandomAttributes[0].SourceFile);
            Assert.False(Path.IsPathFullyQualified(catalogs.ComposeLimits[0].SourceFile));
            Assert.False(Path.IsPathFullyQualified(catalogs.RandomAttributes[0].SourceFile));
            using var compose = new FileStream(fixture.ComposePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var random = new FileStream(fixture.RandomPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(compose.CanWrite); Assert.True(random.CanWrite);
        });
    }

    [Fact]
    public void FailedLaterRandomPinDoesNotReplaceOneTableOrRepairAnyInput()
    {
        WithFiles(fixture =>
        {
            var composePin = SHA256.HashData(fixture.ComposeBytes); var randomPin = SHA256.HashData(fixture.RandomBytes);
            Assert.True(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(fixture.ComposePath, fixture.RandomPath, 7937,
                composePin, randomPin, out var result, out _));
            var earlier = Assert.IsType<Chinese7937XuanbaoRuleCatalogs>(result);
            var changed = Chinese7937XuanbaoRulesFixture.Pack("7@@2@@1@@999@@\r\n");
            File.WriteAllBytes(fixture.RandomPath, changed);
            Assert.False(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(fixture.ComposePath, fixture.RandomPath, 7937,
                composePin, randomPin, out var rejected, out var failure));
            Assert.Null(rejected); Assert.Equal(Chinese7937XuanbaoRulesFailure.PinMismatch, failure);
            Assert.Equal(201, earlier.RandomAttributes[0].MaxValue);
            Assert.Equal(fixture.ComposeBytes, File.ReadAllBytes(fixture.ComposePath));
            Assert.Equal(changed, File.ReadAllBytes(fixture.RandomPath));
        });
    }

    [Fact]
    public void VersionPinsAndEachOrdinaryPathAreRequiredWithoutFallback()
    {
        WithFiles(fixture =>
        {
            var composePin = SHA256.HashData(fixture.ComposeBytes); var randomPin = SHA256.HashData(fixture.RandomBytes);
            Assert.False(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(fixture.ComposePath, fixture.RandomPath, 7931,
                composePin, randomPin, out var rejected, out var failure));
            Assert.Null(rejected); Assert.Equal(Chinese7937XuanbaoRulesFailure.UnsupportedVersion, failure);
            Assert.False(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(fixture.ComposePath, fixture.RandomPath, 7937,
                [], randomPin, out rejected, out failure));
            Assert.Null(rejected); Assert.Equal(Chinese7937XuanbaoRulesFailure.PinRequired, failure);
            foreach (var entry in new[] { ("relative.dat", Chinese7937XuanbaoRulesFailure.PathRequired),
                (fixture.Root, Chinese7937XuanbaoRulesFailure.UnsafePath),
                (Path.Combine(fixture.Root, "missing.dat"), Chinese7937XuanbaoRulesFailure.UnsafePath) })
            {
                Assert.False(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(entry.Item1, fixture.RandomPath, 7937,
                    composePin, randomPin, out rejected, out failure));
                Assert.Null(rejected); Assert.Equal(entry.Item2, failure);
                Assert.False(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(fixture.ComposePath, entry.Item1, 7937,
                    composePin, randomPin, out rejected, out failure));
                Assert.Null(rejected); Assert.Equal(entry.Item2, failure);
            }
            Assert.Equal(fixture.ComposeBytes, File.ReadAllBytes(fixture.ComposePath));
            Assert.Equal(fixture.RandomBytes, File.ReadAllBytes(fixture.RandomPath));
        });
    }

    [Theory]
    [InlineData("\\\\server\\share\\catalog.dat")]
    [InlineData("\\\\?\\C:\\catalog.dat")]
    public void NetworkAndDevicePathsDoNotLoad(string path)
    {
        Assert.False(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(path, path, 7937, new byte[32], new byte[32],
            out var catalogs, out var failure));
        Assert.Null(catalogs);
        Assert.Equal(OperatingSystem.IsWindows() ? Chinese7937XuanbaoRulesFailure.UnsafePath : Chinese7937XuanbaoRulesFailure.PathRequired, failure);
    }

    [Fact]
    public void PinDoesNotAcceptACompleteWrongFormatOrAnOversizedFile()
    {
        WithFiles(fixture =>
        {
            var composePin = SHA256.HashData(fixture.ComposeBytes);
            var malformed = Chinese7937XuanbaoRulesFixture.Pack("1@@2@@3@@4@@\n");
            File.WriteAllBytes(fixture.RandomPath, malformed);
            Assert.False(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(fixture.ComposePath, fixture.RandomPath, 7937,
                composePin, SHA256.HashData(malformed), out var catalogs, out var failure));
            Assert.Null(catalogs); Assert.Equal(Chinese7937XuanbaoRulesFailure.InvalidTable, failure);
            var oversized = new byte[Chinese7937XuanbaoRuleCatalogs.MaximumPackedBytes + 1];
            File.WriteAllBytes(fixture.RandomPath, oversized);
            Assert.False(Chinese7937XuanbaoRuleCatalogs.TryLoadPackedFiles(fixture.ComposePath, fixture.RandomPath, 7937,
                composePin, SHA256.HashData(oversized), out catalogs, out failure));
            Assert.Null(catalogs); Assert.Equal(Chinese7937XuanbaoRulesFailure.InputBounds, failure);
            Assert.Equal(fixture.ComposeBytes, File.ReadAllBytes(fixture.ComposePath));
        });
    }

    private static void WithFiles(Action<RuleFilesFixture> action)
    {
        var fixture = new RuleFilesFixture();
        Exception? primary = null;
        try { fixture.Initialize(); action(fixture); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { fixture.Dispose(); }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }
    }

    private sealed class RuleFilesFixture : IDisposable
    {
        private const string Prefix = "wolf-xuanbao-rule-tests-";
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string ComposePath => Path.Combine(Root, "compose.dat");
        internal string RandomPath => Path.Combine(Root, "random.dat");
        internal byte[] ComposeBytes { get; } = Chinese7937XuanbaoRulesFixture.ComposePacked();
        internal byte[] RandomBytes { get; } = Chinese7937XuanbaoRulesFixture.RandomPacked();
        internal RuleFilesFixture()
        {
            Directory.CreateDirectory(Root);
        }
        internal void Initialize()
        {
            Create(ComposePath, ComposeBytes); Create(RandomPath, RandomBytes);
        }
        private static void Create(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes);
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root); var name = Path.GetFileName(path);
            if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[Prefix.Length..], "N", out _) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Invalid owned XuanBao rule fixture cleanup root.");
            Directory.Delete(path, recursive: true);
        }
    }
}
