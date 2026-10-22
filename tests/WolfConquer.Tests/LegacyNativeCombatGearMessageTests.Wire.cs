using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearMessage = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using SkinMessage = WolfConquer.Core.World.LegacyNativeMsgCombatGearSkin;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearMessageTests
{
    [Fact]
    public void ImplicitConversionsIncludeRequiredZeroOuterFieldsAndNativeEightByteSeal()
    {
        using var load = new LoadScope();
        byte[] main = new GearMessage();
        byte[] skin = new SkinMessage();
        Assert.Equal(Convert.FromHexString("08007409080010005451536572766572"), main);
        Assert.Equal(Convert.FromHexString("0600760908005451536572766572"), skin);
        Assert.Equal(new[] { 1, 2 }, ReadFrame(main, 2420).Select(field => field.Number));
        Assert.Equal(0UL, Scalar(ReadFrame(main, 2420), 1));
        Assert.Equal(0UL, Scalar(ReadFrame(main, 2420), 2));
        Assert.Equal(0UL, Scalar(ReadFrame(skin, 2422), 1));
    }

    [Fact]
    public void RepeatedMutableItemAliasesRemainRepeatedAndRequiredZeroNestedFieldsAreWritten()
    {
        using var load = new LoadScope();
        var item = new GearMessage.Item();
        GearMessage.Item[] entries = [item, item];
        var message = new GearMessage { Items = entries };
        byte[] first = message;
        var firstEntries = ReadFrame(first, 2420).Where(field => field.Number == 3).Select(Entry).ToArray();
        Assert.Equal(2, firstEntries.Length);
        foreach (var fields in firstEntries)
        {
            Assert.Equal(Enumerable.Range(1, 12), fields.Select(field => field.Number));
            Assert.All(fields, field => Assert.Equal(0UL, field.Value));
        }
        item.Exp = uint.MaxValue;
        message.UID = uint.MaxValue;
        byte[] second = message;
        var fieldsAfter = ReadFrame(second, 2420);
        Assert.Equal((ulong)uint.MaxValue, Scalar(fieldsAfter, 2));
        var secondEntries = fieldsAfter.Where(field => field.Number == 3).Select(Entry).ToArray();
        Assert.All(secondEntries, fields => Assert.Equal((ulong)uint.MaxValue, Scalar(fields, 3)));
        Assert.All(firstEntries, fields => Assert.Equal(0UL, Scalar(fields, 3)));
        Assert.Same(entries, message.Items);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void SkinMutableAliasesPreserveTwoEntriesAndAllRequiredZeroDataFields()
    {
        using var load = new LoadScope();
        var entry = new SkinMessage.WeaponInfo { Type = SkinMessage.TypeID.Gloryfate };
        SkinMessage.WeaponInfo[] entries = [entry, entry];
        var message = new SkinMessage { Type = uint.MaxValue, Weapon = entries };
        byte[] first = message;
        var fields = ReadFrame(first, 2422);
        Assert.Equal((ulong)uint.MaxValue, Scalar(fields, 1));
        var skins = fields.Where(field => field.Number == 2).Select(Entry).ToArray();
        Assert.Equal(2, skins.Length);
        foreach (var skin in skins)
        {
            Assert.Equal(Enumerable.Range(1, 9), skin.Select(field => field.Number));
            Assert.Equal(1UL, Scalar(skin, 2));
            Assert.All(skin.Where(field => field.Number != 2), field => Assert.Equal(0UL, field.Value));
        }
        entry.Data7 = uint.MaxValue;
        byte[] second = message;
        Assert.All(ReadFrame(second, 2422).Where(field => field.Number == 2).Select(Entry),
            skin => Assert.Equal((ulong)uint.MaxValue, Scalar(skin, 9)));
        Assert.Same(entries, message.Weapon);
        Assert.NotSame(first, second);
    }
}
