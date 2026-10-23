using System.Reflection;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearMessage = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using SkinMessage = WolfConquer.Core.World.LegacyNativeMsgCombatGearSkin;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearMessageTests
{
    [Fact]
    public void MutableMessagesRetainNativeNullDefaultsFlagsAndFieldWidths()
    {
        var gear = new GearMessage();
        var skin = new SkinMessage();
        Assert.Null(gear.Items);
        Assert.Null(skin.Weapon);
        Assert.Equal(0U, gear.UID);
        Assert.Equal(GearMessage.TypeID.Load, gear.Type);
        Assert.Equal(0U, skin.Type);
        var item = new GearMessage.Item();
        Assert.Equal(Gear.TypeID.None, item.ID);
        Assert.All(typeof(GearMessage.Item).GetFields().Where(field => field.FieldType == typeof(uint)),
            field => Assert.Equal(0U, field.GetValue(item)));
        var entry = new SkinMessage.WeaponInfo();
        Assert.Equal(0, (int)entry.Type);
        Assert.All(typeof(SkinMessage.WeaponInfo).GetFields().Where(field => field.FieldType == typeof(uint)),
            field => Assert.Equal(0U, field.GetValue(entry)));
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(GearMessage.TypeID)));
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(SkinMessage.TypeID)));
        Assert.NotNull(typeof(GearMessage.TypeID).GetCustomAttribute<FlagsAttribute>());
        Assert.NotNull(typeof(SkinMessage.TypeID).GetCustomAttribute<FlagsAttribute>());
        Assert.Equal(new[] { 0, 1, 2, 3, 10 }, Enum.GetValues<GearMessage.TypeID>().Select(value => (int)value));
        Assert.Equal(new[] { 1, 2 }, Enum.GetValues<SkinMessage.TypeID>().Select(value => (int)value));
        GearMessage.Item[] items = [item, item];
        SkinMessage.WeaponInfo[] weapons = [entry, entry];
        gear.Items = items;
        skin.Weapon = weapons;
        Assert.Same(items, gear.Items);
        Assert.Same(weapons, skin.Weapon);
        Assert.Same(gear.Items[0], gear.Items[1]);
        Assert.Same(skin.Weapon[0], skin.Weapon[1]);
    }

    [Fact]
    public void SingleGearBuilderProjectsAllTwelveFieldsIntoLiteralGoldenFrame()
    {
        using var load = new LoadScope();
        var weapon = Weapon();
        var jades = weapon.BlackJades;
        var frame = GearMessage.Create(17, weapon, GearMessage.TypeID.Update);
        Assert.Equal(Convert.FromHexString(
            "23007409080310111A1908C90110021803200428053006380740084809500A580B600C5451536572766572"), frame);
        var fields = ReadFrame(frame, 2420);
        Assert.Equal(3UL, Scalar(fields, 1));
        Assert.Equal(17UL, Scalar(fields, 2));
        AssertItem(Entry(Assert.Single(fields, field => field.Number == 3)), weapon);
        Assert.Same(jades, weapon.BlackJades);
        Assert.Equal(3U, weapon.Exp);
        var retained = frame.ToArray();
        weapon.Exp = 99;
        weapon.BlackJades[0].Type = 77;
        Assert.Equal(retained, frame);
        Assert.NotEqual(Convert.ToHexString(frame), Convert.ToHexString(
            GearMessage.Create(17, weapon, GearMessage.TypeID.Update)));
    }

    [Fact]
    public void SingleSkinBuilderProjectsBothSevenWordRowsIntoLiteralGoldenFrame()
    {
        using var load = new LoadScope();
        var weapon = Weapon();
        var anima = weapon.TypeAnima;
        var frame = SkinMessage.Create(weapon);
        Assert.Equal(Convert.FromHexString(
            "300076090801121308C90110011801200228033004380540064807121308C9011002180B200C280D300E380F401048115451536572766572"), frame);
        var fields = ReadFrame(frame, 2422);
        Assert.Equal(1UL, Scalar(fields, 1));
        var entries = fields.Where(field => field.Number == 2).Select(Entry).ToArray();
        Assert.Equal(2, entries.Length);
        AssertSkin(entries[0], weapon, 0);
        AssertSkin(entries[1], weapon, 1);
        Assert.Same(anima, weapon.TypeAnima);
        var retained = frame.ToArray();
        weapon.TypeAnima[1][6] = 99;
        Assert.Equal(retained, frame);
        Assert.NotEqual(Convert.ToHexString(frame), Convert.ToHexString(SkinMessage.Create(weapon)));
    }

    [Fact]
    public void MainBatchFiltersByActualWeaponIdWhileSkinBatchKeepsAllValuesAndNativePairOrder()
    {
        using var load = new LoadScope();
        var gear = new Gear();
        var warrior = Weapon();
        var secondWarrior = Weapon(Gear.TypeID.Bloodlust);
        var pirate = Weapon(Gear.TypeID.Pirate1);
        pirate.Equip = 0;
        gear.Items[Gear.TypeID.Taoist1] = warrior; // Map keys are not item identity/class authority.
        gear.Items[Gear.TypeID.None] = pirate;
        gear.Items[Gear.TypeID.Pirate3] = secondWarrior;
        gear.SetClass(14000); // Skin does not filter by this stored class.
        var all = gear.Items.Values.ToArray();
        var selected = all.Where(value => ReferenceEquals(value, warrior) || ReferenceEquals(value, secondWarrior)).ToArray();

        var main = ReadFrame(GearMessage.Create(gear, GearMessage.TypeID.View, 41, 2000), 2420);
        Assert.Equal(41UL, Scalar(main, 2));
        var mainItems = main.Where(field => field.Number == 3).Select(Entry).ToArray();
        Assert.Equal(2, mainItems.Length);
        for (var i = 0; i < selected.Length; i++) AssertItem(mainItems[i], selected[i]);
        var skin = ReadFrame(SkinMessage.Create(gear), 2422);
        Assert.Equal(0UL, Scalar(skin, 1));
        var skinItems = skin.Where(field => field.Number == 2).Select(Entry).ToArray();
        Assert.Equal(6, skinItems.Length);
        for (var i = 0; i < all.Length; i++)
        {
            AssertSkin(skinItems[i * 2], all[i], 0);
            AssertSkin(skinItems[i * 2 + 1], all[i], 1);
        }
        Assert.Same(warrior, gear.Items[Gear.TypeID.Taoist1]);
        Assert.Same(pirate, gear.Items[Gear.TypeID.None]);
    }

    [Fact]
    public void EmptyAndUnknownClassBatchesKeepRequiredOuterFieldsWithoutInventingItems()
    {
        using var load = new LoadScope();
        var gear = new Gear();
        gear.Items[Gear.TypeID.Dragonhowl] = Weapon();
        var fields = ReadFrame(GearMessage.Create(gear, GearMessage.TypeID.CrossServer, 9, 999), 2420);
        Assert.Equal(new[] { 1, 2 }, fields.Select(field => field.Number));
        Assert.Equal(10UL, Scalar(fields, 1));
        Assert.Equal(9UL, Scalar(fields, 2));
        Assert.Equal(Convert.FromHexString("0600760908005451536572766572"), SkinMessage.Create(new Gear()));
    }
}
