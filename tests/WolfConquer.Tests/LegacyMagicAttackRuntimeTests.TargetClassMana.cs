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
    public async Task EquipmentQueryPrefixPreservesValidClassContributionAndItemMana(int classCode)
    {
        using var fixture = new NativeEquipmentCacheFixture(classCode, spirit: 2_000);
        await fixture.QueryAsync();

        // Genuine item/purification contribution 150 plus class contribution 60000.
        Assert.Equal(60_150U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).MaxMana);
        Assert.Equal(60_150U, (await fixture.ReadStatusAsync()).MaxMana);
        Assert.Equal(60_000L, LegacyFirstCharacterStarterProfiles.CalculateMana(classCode, 2_000));
    }

    [Fact]
    public void EquipmentQueryClassAndAttributeContributionsRefuseNativeWidthOverflow()
    {
        using var fixture = new NativeEquipmentCacheFixture(14_057, spirit: ushort.MaxValue);
        var character = fixture.Owner.Character;
        Assert.Throws<OverflowException>(() => LegacyMaxHitpointsCalculator.CalculateAttributeHitpointsChecked(character));
        Assert.Throws<OverflowException>(() => LegacyEquipmentNativeMana.ClassAndSpirit(character));
        Assert.Empty(fixture.Frames);
    }
}
