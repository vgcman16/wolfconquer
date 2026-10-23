using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearMessage = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using SkinMessage = WolfConquer.Core.World.LegacyNativeMsgCombatGearSkin;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearMessageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildersDereferenceRequiredWeaponBeforeTheFinalCodecGate(bool loaded)
    {
        using var load = new LoadScope(loaded);
        Assert.Throws<NullReferenceException>(() => GearMessage.Create(7, null!, GearMessage.TypeID.Add));
        Assert.Throws<NullReferenceException>(() => SkinMessage.Create((Gear.Weapon)null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MainBuilderDoesNotRepairMissingShortOrNullJadeSlots(int shape)
    {
        using var load = new LoadScope();
        var weapon = Weapon();
        if (shape == 0) weapon.BlackJades = null!;
        if (shape == 1) weapon.BlackJades = new Gear.BlackJade[5]
        {
            new(), new(), new(), new(), new(),
        };
        if (shape == 2) weapon.BlackJades[0] = null!;
        if (shape == 1)
            Assert.Throws<IndexOutOfRangeException>(() => GearMessage.Create(7, weapon, GearMessage.TypeID.Add));
        else
            Assert.Throws<NullReferenceException>(() => GearMessage.Create(7, weapon, GearMessage.TypeID.Add));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SkinBuilderDoesNotRepairMissingShortOrNullAnimaRows(int shape)
    {
        using var load = new LoadScope();
        var weapon = Weapon();
        if (shape == 0) weapon.TypeAnima = null!;
        if (shape == 1) weapon.TypeAnima = [new uint[7]];
        if (shape == 2) weapon.TypeAnima[1] = null!;
        if (shape == 3) weapon.TypeAnima[0] = new uint[6];
        if (shape == 1 || shape == 3)
            Assert.Throws<IndexOutOfRangeException>(() => SkinMessage.Create(weapon));
        else
            Assert.Throws<NullReferenceException>(() => SkinMessage.Create(weapon));
    }

    [Fact]
    public void MainAndSkinReadOnlyTheirOwnNativeWeaponStorage()
    {
        using var load = new LoadScope();
        var mainOnly = Weapon();
        mainOnly.TypeAnima = null!;
        AssertItem(Entry(Assert.Single(ReadFrame(
            GearMessage.Create(7, mainOnly, GearMessage.TypeID.Load), 2420), field => field.Number == 3)), mainOnly);
        var skinOnly = Weapon();
        skinOnly.BlackJades = null!;
        var skinEntries = ReadFrame(SkinMessage.Create(skinOnly), 2422)
            .Where(field => field.Number == 2).Select(Entry).ToArray();
        AssertSkin(skinEntries[0], skinOnly, 0);
        AssertSkin(skinEntries[1], skinOnly, 1);
    }

    [Fact]
    public void DisabledCodecReturnsNullOnlyAfterBuildersHaveCompletedTheirNativeReads()
    {
        using var load = new LoadScope(false);
        byte[] main = new GearMessage { Items = [null!] };
        byte[] skin = new SkinMessage { Weapon = [null!] };
        Assert.Null(main);
        Assert.Null(skin);
        Assert.Null(GearMessage.Create(7, Weapon(), GearMessage.TypeID.Add));
        Assert.Null(SkinMessage.Create(Weapon()));
        Assert.Null(SkinMessage.Create(new Gear()));
        Assert.Throws<NullReferenceException>(() =>
            GearMessage.Create(new Gear(), GearMessage.TypeID.Load, 7, 2000)); // GetItems returns null, then Length faults.
        var malformed = Weapon();
        malformed.TypeAnima = null!;
        var gear = new Gear();
        gear.Items[Gear.TypeID.Dragonhowl] = malformed;
        Assert.Throws<NullReferenceException>(() => SkinMessage.Create(gear));
    }
}
