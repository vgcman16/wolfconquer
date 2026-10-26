using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearOptTests
{
    [Theory]
    [InlineData("default", "08001000180020002800")]
    [InlineData("empty-arrays", "08001000180020002800")]
    [InlineData("zero-material", "080010001800200028003A0408001000")]
    [InlineData("null-material", "08001000180020002800")]
    [InlineData("mixed-materials", "080010001800200728003A04080010003A08081110FFFFFFFF0F")]
    [InlineData("u-values", "080010001800200028003000301130FFFFFFFF0F")]
    [InlineData("max", "08FFFFFFFF0710FFFFFFFFFFFFFFFFFF0118FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F30FFFFFFFF0F3A0C08FFFFFFFF0F10FFFFFFFF0F")]
    [InlineData("negative-type", "08FFFFFFFFFFFFFFFFFF011000180020002800")]
    [InlineData("unknown-uint-item-id", "080010EFFDB6F5FDFFFFFFFF01180020002800")]
    [InlineData("null-outer", "")]
    public void WireRetainsValidOraclePayloadsAndRefusesMissingOuterMessage(string kind, string expected)
    {
        using var s = new Scope();
        // Root observed the ten controls using the preserved protobuf-net2.0.0.668,
        // exact native two registrations and Stream/object-typed Serialize.
        Opt? info = kind switch
        {
            "empty-arrays" => new Opt { U = [], SelectMaterial = [] },
            "zero-material" => new Opt { SelectMaterial = [new()] },
            "null-material" => new Opt { SelectMaterial = [null!] },
            "mixed-materials" => new Opt { UID = 7, SelectMaterial = [null!, new(),
                new() { UID = 17, Size = uint.MaxValue }, null!] },
            "u-values" => new Opt { U = [0, 17, uint.MaxValue] },
            "max" => new Opt { Type = (Opt.Types)int.MaxValue, ItemID = (Gear.TypeID)uint.MaxValue,
                Transform = uint.MaxValue, UID = uint.MaxValue, Exp = uint.MaxValue, U = [uint.MaxValue],
                SelectMaterial = [new() { UID = uint.MaxValue, Size = uint.MaxValue }] },
            "negative-type" => new Opt { Type = (Opt.Types)(-1) },
            "unknown-uint-item-id" => new Opt { ItemID = (Gear.TypeID)0xDEADBEEF },
            "null-outer" => null,
            _ => new Opt()
        };

        if (info is null)
        {
            Assert.Equal("message", Assert.Throws<ArgumentNullException>(() =>
                WolfConquer.Core.World.LegacyNativeCombatGearWire.Encode(info!, 2421)).ParamName);
            return;
        }
        Assert.Equal(expected, Payload(info, 2421));

        if (kind == "default")
        {
            Assert.Equal(Opt.Types.UPGRADE, info!.Type);
            Assert.Equal(Gear.TypeID.None, info.ItemID);
            Assert.Equal(0u, info.Transform);
            Assert.Equal(0u, info.UID);
            Assert.Equal(0u, info.Exp);
            Assert.Null(info.U);
            Assert.Null(info.SelectMaterial);
            Assert.Equal(0u, new Opt.Items().UID);
            Assert.Equal(0u, new Opt.Items().Size);
            Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(Opt.Types)));
            Assert.True(typeof(Opt.Types).IsDefined(typeof(FlagsAttribute), false));
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 14 },
                Enum.GetValues<Opt.Types>().Select(type => (int)type));
        }
    }
}
