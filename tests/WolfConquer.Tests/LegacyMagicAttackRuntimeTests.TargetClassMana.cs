using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(13052)]
    [InlineData(13053)]
    [InlineData(13054)]
    [InlineData(13055)]
    [InlineData(13056)]
    [InlineData(13057)]
    [InlineData(14052)]
    [InlineData(14053)]
    [InlineData(14054)]
    [InlineData(14055)]
    [InlineData(14056)]
    [InlineData(14057)]
    public async Task EquipmentQueryPrefixPublishesKnownWaterFireManaUsingOwnedSessionAndItems(int classCode)
    {
        using var fixture = new NativeEquipmentCacheFixture(classCode, spirit: 10);
        var character = fixture.Owner.Character;

        await fixture.QueryAsync();

        Assert.Equal(450U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).MaxMana);
        Assert.Equal(450U, (await fixture.ReadStatusAsync()).MaxMana);
        Assert.Equal(character.Uid, fixture.Owner.Character.Uid);
        Assert.Equal((long)classCode, fixture.Owner.Character.Class);
        Assert.Equal(character.MaxMana, fixture.Owner.Character.MaxMana);
    }

    [Theory]
    [InlineData(13058)]
    [InlineData(14058)]
    public async Task EquipmentQueryPrefixRetainsFallbackForTheFirstUnconfirmedWaterFireStage(int classCode)
    {
        using var fixture = new NativeEquipmentCacheFixture(classCode, spirit: 10);
        await fixture.QueryAsync();

        Assert.Equal(200U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).MaxMana);
        Assert.Equal(200U, (await fixture.ReadStatusAsync()).MaxMana);
    }

    [Theory]
    [InlineData(13057)]
    [InlineData(14057)]
    public async Task EquipmentQueryPrefixKeepsUshortClassContributionDistinctFromOrdinaryLongMana(int classCode)
    {
        using var fixture = new NativeEquipmentCacheFixture(classCode, spirit: ushort.MaxValue);
        await fixture.QueryAsync();

        // Genuine item/purification contribution 150 plus narrowed class contribution 65506.
        Assert.Equal(65_656U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).MaxMana);
        Assert.Equal(65_656U, (await fixture.ReadStatusAsync()).MaxMana);
        Assert.Equal(1_966_050L, LegacyFirstCharacterStarterProfiles.CalculateMana(classCode, ushort.MaxValue));
    }
}
