using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    [Theory]
    [InlineData(null, 10L)]
    [InlineData(ConquerProtocolProfile.RootD11_7899, 10L)]
    [InlineData(ConquerProtocolProfile.VisibleDx8D13, 1000L)]
    public void ExplicitFixtureProfileSelectsOnlyTheSavedClassWhileLegacyApiRetainsClassTen(
        ConquerProtocolProfile? profile, long expectedClass)
    {
        if (profile is null) _ = Create();
        else
        {
            Assert.True(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid,
                profile.Value, out var result, out var reason), reason);
            Assert.NotNull(result);
        }
        AssertProfileInitial(ReadProfileOptions(), expectedClass);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void InvalidExplicitProfileRefusesBeforeCreatingAnyRootOrUidHistory(int raw)
    {
        Assert.False(LegacyOwnedAdmissionFixture.TryCreate(Destination, AccountUid, AccountUid, ItemUid,
            (ConquerProtocolProfile)raw, out var result, out var reason));
        Assert.Null(result);
        Assert.Equal("ownedAdmissionFixtureProfileInvalid", reason);
        Assert.False(Directory.Exists(Destination));
        Assert.False(Directory.Exists(Staging));
        Assert.False(File.Exists(ConfigurationPath));
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    private LegacyOwnedAdmissionOptions ReadProfileOptions()
    {
        Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(ConfigurationPath, out var options, out var reason), reason);
        return Assert.IsType<LegacyOwnedAdmissionOptions>(options);
    }

    private void AssertProfileInitial(LegacyOwnedAdmissionOptions options, long expectedClass)
    {
        var loader = options.CreateSnapshotLoader();
        AssertProfileSaved(loader, expectedClass);
        var characterPath = Path.Combine(options.UsersRoot, AccountUid + ".ini");
        var text = CharacterSnapshotFileEncoding.ReadAllText(characterPath, out _);
        Assert.Single(Regex.Matches(text, "(?m)^Class=" + expectedClass + "(?=\\r?$)"));
        Assert.Equal(new byte[4], File.ReadAllBytes(Path.Combine(options.SpellsRoot, AccountUid + ".bin")));
        Assert.Empty(loader.LoadPlayerSpellsByCharacterUid(AccountUid).Spells);
        AssertInitialNamespace(Destination, options.ExpectedDomain);
        Assert.Equal(9, Directory.GetFiles(Destination, "*", SearchOption.AllDirectories).Length);
        var saved = loader.LoadByAccountId(AccountUid)!;
        foreach (var key in new[] { "IsGM", "Admin", "CompleteLogin", "IsCheckedPass", "ServerID" })
            Assert.False(saved.Attributes.ContainsKey(key));
    }

    private static void AssertProfileSaved(CharacterSnapshotLoader loader, long expectedClass)
    {
        var saved = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(AccountUid));
        Assert.Equal((long)AccountUid, saved.Uid);
        Assert.Equal((120L, expectedClass, 0L), (saved.Level, saved.Class, saved.Experience));
        Assert.Equal((1008L, 154L, 27L), (saved.Body, saved.Face, saved.Hair));
        Assert.Equal((100L, 50L, 0L, 0L),
            (saved.Money, saved.ConquerPoints, saved.BankConquerPoints, saved.BoundConquerPoints));
        Assert.Equal("0,0,0", saved.Attributes["SecurityPass"]);
        Assert.Equal("0", saved.Attributes["CollectionID"]);
        Assert.Equal("0/0/", saved.Attributes["CollectionStorage"]);
        var items = loader.LoadPlayerItemsByCharacterUid(AccountUid);
        var item = Assert.Single(items.InventoryItems);
        Assert.Equal((ItemUid, 1_001_000u, (ushort)1), (item.Uid, item.ItemId, item.StackSize));
        Assert.Empty(items.EquipmentItems);
        Assert.Empty(items.WarehouseItems);
        Assert.Empty(loader.LoadPlayerProficienciesByCharacterUid(AccountUid).Proficiencies);
        Assert.Empty(loader.LoadPlayerQuestsByCharacterUid(AccountUid).Quests);
        Assert.Empty(loader.LoadCombatGearByCharacterUid(AccountUid).Items);
    }
}
