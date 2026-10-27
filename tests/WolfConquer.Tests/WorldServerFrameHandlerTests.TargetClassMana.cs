using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(13052)]
    [InlineData(13053)]
    [InlineData(13054)]
    [InlineData(13055)]
    [InlineData(13056)]
    [InlineData(13057)]
    public void KnownWaterStagesRefreshRealSnapshotManaThroughExistingProgressionApi(long characterClass)
    {
        var initial = CreateProgressionSnapshot(characterClass, level: 111, attributePoints: 12);
        var attributes = new Dictionary<string, string>(initial.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Spirit"] = "37",
            ["MinMana"] = "5",
            ["MaxMana"] = "9",
        };
        var character = initial with { Spirit = 37, MinMana = 5, MaxMana = 9, Attributes = attributes };

        var refreshed = LegacyClassLevelStatProgression.RefreshVitals(
            character, PlayerItemSnapshot.Empty, CombatGearSnapshot.Empty, worldData: null);

        Assert.Equal(1110L, refreshed.MinMana);
        Assert.Equal(1110L, refreshed.MaxMana);
        Assert.Equal("1110", refreshed.Attributes["MinMana"]);
        Assert.Equal("1110", refreshed.Attributes["MaxMana"]);
        Assert.Equal(characterClass, refreshed.Class);
        Assert.Equal(37L, refreshed.Spirit);
        Assert.Equal(12L, refreshed.AttributePoints);
        Assert.Equal(character.Uid, refreshed.Uid);
        Assert.Equal(5L, character.MinMana);
        Assert.Equal(9L, character.MaxMana);
        Assert.Equal("5", attributes["MinMana"]);
        Assert.Equal("9", attributes["MaxMana"]);
        Assert.Equal(1_966_050L, LegacyFirstCharacterStarterProfiles.CalculateMana(characterClass, ushort.MaxValue));
    }

    [Theory]
    [InlineData(13051, 210)]
    [InlineData(13058, 35)]
    [InlineData(14051, 210)]
    [InlineData(14057, 210)]
    [InlineData(14058, 35)]
    public void OrdinaryManaKeepsExistingFireAndUnconfirmedWaterArithmetic(long characterClass, long expected)
    {
        Assert.Equal(expected, LegacyFirstCharacterStarterProfiles.CalculateMana(characterClass, spirit: 7));
    }
}
