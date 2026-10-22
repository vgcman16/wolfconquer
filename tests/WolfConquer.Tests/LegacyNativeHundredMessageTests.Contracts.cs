using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.Data;
using Opt = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsOpt;
using Info = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredMessageTests
{
    [Fact]
    public void OptContractsKeepNativeRequiredOptionalWidthsOrderAndUninitializedExchange()
    {
        Contract<Opt.MsgHundredWeaponsOptProto>(
            ["Type", "WeaponSubtype", "Position", "Combination", "TriggeredStage", "TriggeringDuration", "Exchange"],
            [typeof(Opt.ActionID), typeof(LegacyNativeMagicType.WeaponsType), typeof(int), typeof(ulong), typeof(byte), typeof(int), typeof(Opt.ExchangePositions)],
            [1, 2, 3, 4, 5, 6, 7], [true, true, true, true, false, false, false]);
        Contract<Opt.ExchangePositions>(["Position1", "Position2"], [typeof(int), typeof(int)], [1, 2], [true, true]);
        var value = new Opt.MsgHundredWeaponsOptProto(); Assert.Null(value.Exchange);
        Assert.Equal(Opt.ActionID.Equip, value.Type); Assert.Equal(0UL, value.Combination);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Opt.ActionID)));
        Assert.Equal(new[] { "Equip", "Unequip", "Swap", "Combine", "Trigger" }, Enum.GetNames<Opt.ActionID>());
        Assert.Equal(Enumerable.Range(0, 5), Enum.GetValues<Opt.ActionID>().Select(i => (int)i));
    }

    [Fact]
    public void InfoContractsKeepRequiredArrayNullBirthExactScoreTagHoleAndNativeWidths()
    {
        Contract<Info.MsgHundredWeaponsInfoProto>(["Type", "OwnerUID", "Weapons"],
            [typeof(Info.ActionID), typeof(uint), typeof(Info.WeaponInfo[])], [1, 2, 3], [true, true, true]);
        Contract<Info.WeaponInfo>(["SubType", "AppearancePosition", "PositionFlags", "Level", "Progress", "Hitpoints",
                "PhysicalAttack", "PhysicalDefense", "MagicAttack", "MagicDefense", "Score"],
            [typeof(LegacyNativeMagicType.WeaponsType), typeof(uint), typeof(uint), typeof(byte), typeof(uint),
                typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(uint)],
            [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12], Enumerable.Repeat(true, 11).ToArray());
        var value = new Info.MsgHundredWeaponsInfoProto(); Assert.Null(value.Weapons); Assert.Equal(0u, value.OwnerUID);
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Info.ActionID)));
        Assert.Equal(new[] { "None", "Update", "RequestInfo", "Add" }, Enum.GetNames<Info.ActionID>());
        Assert.Equal(Enumerable.Range(0, 4), Enum.GetValues<Info.ActionID>().Select(i => (int)i));
    }

    private static void Contract<T>(string[] names, Type[] types, int[] tags, bool[] required)
    {
        Assert.NotNull(typeof(T).GetCustomAttribute<ProtoContractAttribute>());
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.MetadataToken).ToArray();
        Assert.Equal(names, fields.Select(f => f.Name)); Assert.Equal(types, fields.Select(f => f.FieldType));
        Assert.Equal(tags, fields.Select(f => f.GetCustomAttribute<ProtoMemberAttribute>()!.Tag));
        Assert.Equal(required, fields.Select(f => f.GetCustomAttribute<ProtoMemberAttribute>()!.IsRequired));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
    }
}
