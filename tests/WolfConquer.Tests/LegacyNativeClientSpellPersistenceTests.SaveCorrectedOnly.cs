using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientSpellPersistenceTests
{
    // The old ignored-read loop can run for the attacker-declared count.
    // Do not run these enormous counts on the previous source.
    [Theory]
    [Trait("SourceSafety", "CorrectedOnly")]
    [InlineData(65537)]
    [InlineData(int.MaxValue)]
    public void CorrectedOnlyImpossibleDeclaredCountsRefuseBeforeLoopingOrPublishing(int count)
    {
        using var files = new OwnedSpells();
        var image = Image(count);
        files.Seed(image);
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        var file = files.Open(FileMode.Open);
        try
        {
            Assert.Throws<InvalidDataException>(() => LegacyNativeServerDatabasePersistence.LoadClientSpellsOpenedFile(file, client));
            AssertFileAdmissionUnchanged(client, map, existing);
            AssertClosed(files.FilePath);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        }
        finally { file.Close(); }
    }

    [Fact]
    public void UndeclaredTrailingBytesRefuseInsteadOfBeingSilentlyDiscarded()
    {
        using var files = new OwnedSpells();
        var image = Image(0).Concat(new byte[] { 0xA5 }).ToArray();
        files.Seed(image);
        var client = Client();
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        var file = files.Open(FileMode.Open);
        try
        {
            Assert.Throws<InvalidDataException>(() => LegacyNativeServerDatabasePersistence.LoadClientSpellsOpenedFile(file, client));
            AssertFileAdmissionUnchanged(client, map, existing);
            AssertClosed(files.FilePath);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
        }
        finally { file.Close(); }
    }

    // Corrected source only: the old public method can leak its internally
    // created native file owner. Never execute that path for a failing baseline.
    [Theory]
    [Trait("SourceSafety", "CorrectedOnly")]
    [InlineData("missing-owner")]
    [InlineData("mismatched-key")]
    [InlineData("null-tail")]
    public void CorrectedOnlyPublicSaveRejectsInvalidStateBeforeTruncatingTheExistingFile(string invalid)
    {
        using var files = new OwnedSpells();
        var original = Image(1, Row(77, 3, 125, 2, 1, 1));
        files.Seed(original);
        var client = Client(spells: invalid != "missing-owner");
        if (invalid == "mismatched-key") client.MySpells!.ClientSpells.TryAdd(500, new() { ID = 501 });
        if (invalid == "null-tail")
        {
            SeedFileAdmissionSentinel(client);
            client.MySpells!.ClientSpells.TryAdd(700, null!);
        }

        Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(client));

        Assert.Equal(original, File.ReadAllBytes(files.FilePath));
        AssertClosed(files.FilePath);
    }
}
