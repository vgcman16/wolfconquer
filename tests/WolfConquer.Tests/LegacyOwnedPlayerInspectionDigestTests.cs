using System.Security.Cryptography;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyOwnedPlayerInspectionDigestTests
{
    [Fact]
    public void ActualStableFileImagesReportExactOptionalDigestsIncludingOpaqueItemBytes()
    {
        using var scope = new Scope();
        var first = scope.Inspect();
        Assert.Equal(6, first.Files.Length);
        foreach (var file in first.Files)
        {
            Assert.True(file.BytesAndMetadataUnchanged);
            var path = scope.PathFor(file.Kind);
            if (file.Present)
                Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), file.ContentSha256);
            else { Assert.Null(file.ContentSha256); Assert.False(File.Exists(path)); }
        }
        var itemsPath = scope.PathFor("Items"); var raw = File.ReadAllBytes(itemsPath);
        Assert.True(raw.Length > 18); raw[4 + 14] = 0xA5; // Codec-reserved DBItem byte; actual complete image.
        File.WriteAllBytes(itemsPath, raw);
        var files = Directory.GetFiles(scope.Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        var second = scope.Inspect();
        Assert.Equal(first.Items, second.Items); Assert.Equal(first.PhysicalItemCount, second.PhysicalItemCount);
        Assert.NotEqual(first.Files.Single(file => file.Kind == "Items").ContentSha256,
            second.Files.Single(file => file.Kind == "Items").ContentSha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(raw)), second.Files.Single(file => file.Kind == "Items").ContentSha256);
        foreach (var pair in files) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
    }

    [Fact]
    public void ExistingSixArgumentFileEvidenceConstructionKeepsDigestOptional()
    {
        var oldShape = new LegacyOwnedPlayerFileEvidence("Spells", false, 0, null, null, true);
        Assert.Null(oldShape.ContentSha256);
    }

    private sealed class Scope : IDisposable
    {
        private const uint Account = 2749600;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wolf-owned-admission-" + Guid.NewGuid().ToString("N"));
        private readonly LegacyOwnedAdmissionFixtureResult fixture;
        private readonly LegacyOwnedAdmissionOptions options;
        internal Scope()
        {
            Assert.True(LegacyOwnedAdmissionFixture.TryCreate(Root, Account, Account, 8013201,
                ConquerProtocolProfile.VisibleDx8D13, out var owned, out var reason), reason);
            fixture = Assert.IsType<LegacyOwnedAdmissionFixtureResult>(owned);
            Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(fixture.ConfigurationPath, out var routes, out reason), reason);
            options = Assert.IsType<LegacyOwnedAdmissionOptions>(routes);
        }
        internal LegacyOwnedPlayerInspectionReport Inspect()
        {
            Assert.True(LegacyOwnedPlayerInspection.TryInspect(fixture.ConfigurationPath, Account, out var report, out var reason), reason);
            return Assert.IsType<LegacyOwnedPlayerInspectionReport>(report);
        }
        internal string PathFor(string kind) => kind switch
        {
            "Configuration" => fixture.ConfigurationPath,
            "Character" => Path.Combine(options.UsersRoot, Account + ".ini"),
            "Items" => Path.Combine(options.ItemsRoot, Account + ".bin"),
            "Spells" => Path.Combine(options.SpellsRoot, Account + ".bin"),
            "Proficiencies" => Path.Combine(options.ProficienciesRoot, Account + ".bin"),
            "Mailbox" => Path.Combine(options.MailboxRoot, Account + ".bin"),
            _ => throw new InvalidOperationException("Unexpected inspector label.")
        };
        public void Dispose()
        {
            var full = Path.GetFullPath(Root); var leaf = Path.GetFileName(full);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), Path.GetDirectoryName(full), ignoreCase: true);
            Assert.StartsWith(LegacyOwnedAdmissionFixture.DirectoryPrefix, leaf, StringComparison.Ordinal);
            Assert.True(Guid.TryParseExact(leaf[LegacyOwnedAdmissionFixture.DirectoryPrefix.Length..], "N", out _));
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }
    }
}
