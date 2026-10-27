using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeClientSpellPersistenceTests
{
    [Fact]
    public void ConflictingReverseRunePermissionRowCannotAuthorizeOrPublishAnEarlierPrefix()
    {
        using var files = new OwnedSpells();
        var image = Image(2, Row(10, 0, 15), Row(900, 0, 16));
        files.Seed(image);
        files.Magic[900] = new() { [0] = new() { isRuneSkill = true } };
        files.Items[7] = new() { ID = 100, RuneSpellID = 90000, Class = 2000 };
        files.Items[100] = new() { ID = 222, RuneSpellID = 0, Class = 1000 };
        var client = Client();
        SetClass(client, 1004);
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        var file = files.Open(FileMode.Open);
        try
        {
            Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.LoadClientSpellsOpenedFile(file, client));
            AssertFileAdmissionUnchanged(client, map, existing);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
            AssertClosed(files.FilePath);
        }
        finally { file.Close(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OpenedLoadRequiresTheActualPositiveCurrentSpellOwner(bool zeroUid)
    {
        using var files = new OwnedSpells();
        var image = Image(1, Row(77, 0, 10));
        files.Seed(image);
        var client = Client(uid: zeroUid ? 0u : 1293600u);
        if (!zeroUid) client.MySpells = Client().MySpells;
        var existing = SeedFileAdmissionSentinel(client);
        var map = client.MySpells!.ClientSpells;
        var file = files.Open(FileMode.Open);
        try
        {
            Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.LoadClientSpellsOpenedFile(file, client));
            AssertFileAdmissionUnchanged(client, map, existing);
            Assert.Equal(image, File.ReadAllBytes(files.FilePath));
            AssertClosed(files.FilePath);
        }
        finally { file.Close(); }
    }
}
