using System.Buffers.Binary;
using System.Text;
using Gear = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using Skin = WolfConquer.Core.World.LegacyNativeMsgCombatGearSkin;
using Air = WolfConquer.Core.World.LegacyNativeMsgAirPowerUp;
using Type = WolfConquer.Core.World.LegacyNativeCombatGear.TypeID;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearWireTests
{
    // Literal payloads observed by Root with the preserved protobuf-net2.0.0.668,
    // exact source-shaped objects/object-typed Serialize and six runtime registrations.
    // These remain independent fixed-projection controls; fresh-child loader/object
    // tests separately exercise the genuine registration and serialization lifecycle.
    [Theory]
    [InlineData("gear-null-items", "08001011")]
    [InlineData("gear-empty", "08001000")]
    [InlineData("gear-zero", "080010001A18080010001800200028003000380040004800500058006000")]
    [InlineData("gear-max", "080A10FFFFFFFF0F1A4508BF0510FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F30FFFFFFFF0F38FFFFFFFF0F40FFFFFFFF0F48FFFFFFFF0F50FFFFFFFF0F58FFFFFFFF0F60FFFFFFFF0F")]
    [InlineData("gear-null-entry", "0800101D")]
    [InlineData("gear-unknown-uint-flags", "0800101F1A2108EFFDB6F5FDFFFFFFFF0110001800200028003000380040004800500058006000")]
    [InlineData("gear-negative-int-flags", "08FFFFFFFFFFFFFFFFFF011025")]
    [InlineData("skin-null-weapons", "0800")]
    [InlineData("skin-empty", "0800")]
    [InlineData("skin-zero", "08001212080010001800200028003000380040004800")]
    [InlineData("skin-max", "08FFFFFFFF0F123208FFFFFFFF0F100218FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F30FFFFFFFF0F38FFFFFFFF0F40FFFFFFFF0F48FFFFFFFF0F")]
    [InlineData("skin-null-entries", "0800121308C90110001800200028003000380040004800")]
    [InlineData("skin-unknown-int-flags", "0800121B080010EFFDB6F5FDFFFFFFFF011800200028003000380040004800")]
    [InlineData("air-default-empty", "080010001800")]
    [InlineData("air-zero", "080010001800221008001000180020002800300038004000")]
    [InlineData("air-max", "080510FFFFFFFF0F18FFFFFFFF0F223008FFFFFFFF0F10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F30FFFFFFFF0F38FFFFFFFF0F40FFFFFFFF0F")]
    [InlineData("air-null-entries", "08001000180022002210080710001800200028003000380040002200")]
    [InlineData("air-null-collection", "080010001800")]
    [InlineData("air-negative-int-flags", "08FFFFFFFFFFFFFFFFFF0110001800")]
    [InlineData("null-outer", "")]
    public void FixedProjectionMatchesPinnedRegisteredSourceContracts(string name, string payloadHex)
    {
        using var scope = new NativeCombatGearQueryScope();
        var message = Message(name);
        byte[] frame = message switch
        {
            Gear gear => LegacyNativeCombatGearWire.Encode(gear, 2420),
            Skin skin => LegacyNativeCombatGearWire.Encode(skin, 2422),
            Air air => LegacyNativeCombatGearWire.Encode(air, 2464),
            null => LegacyNativeCombatGearWire.Encode((Gear)null!, 2420),
            _ => throw new InvalidOperationException(),
        };
        var expected = Convert.FromHexString(payloadHex);
        Assert.Equal(expected, frame[4..^8]);
        Assert.Equal(expected.Length + 12, frame.Length);
        Assert.Equal(expected.Length + 4, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal((ushort)(name.StartsWith("skin", StringComparison.Ordinal) ? 2422 :
            name.StartsWith("air", StringComparison.Ordinal) ? 2464 : 2420),
            BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), frame[^8..]);
    }

    private static object? Message(string name) => name switch
    {
        "gear-null-items" => new Gear { UID = 17 },
        "gear-empty" => new Gear { Items = [] },
        "gear-zero" => new Gear { Items = [new()] },
        "gear-max" => new Gear
        {
            Type = Gear.TypeID.CrossServer, UID = uint.MaxValue,
            Items = [new() { ID = Type.Pirate3, Level = uint.MaxValue, Exp = uint.MaxValue,
                Equip = uint.MaxValue, Transform = uint.MaxValue, Data1 = uint.MaxValue,
                Data2 = uint.MaxValue, Data3 = uint.MaxValue, Data4 = uint.MaxValue,
                Data5 = uint.MaxValue, Data6 = uint.MaxValue, MasteryScore = uint.MaxValue }],
        },
        "gear-null-entry" => new Gear { UID = 29, Items = [null!] },
        "gear-unknown-uint-flags" => new Gear { UID = 31, Items = [new() { ID = (Type)0xDEADBEEF }] },
        "gear-negative-int-flags" => new Gear { UID = 37, Type = (Gear.TypeID)(-1), Items = [] },
        "skin-null-weapons" => new Skin(),
        "skin-empty" => new Skin { Weapon = [] },
        "skin-zero" => new Skin { Weapon = [new()] },
        "skin-max" => new Skin
        {
            Type = uint.MaxValue,
            Weapon = [new() { ID = uint.MaxValue, Type = Skin.TypeID.HolyOmen,
                Data1 = uint.MaxValue, Data2 = uint.MaxValue, Data3 = uint.MaxValue,
                Data4 = uint.MaxValue, Data5 = uint.MaxValue, Data6 = uint.MaxValue, Data7 = uint.MaxValue }],
        },
        "skin-null-entries" => new Skin { Weapon = [null!, new() { ID = 201 }, null!] },
        "skin-unknown-int-flags" => new Skin { Weapon = [new() { Type = unchecked((Skin.TypeID)(int)0xDEADBEEF) }] },
        "air-default-empty" => new Air(),
        "air-zero" => new Air { Powers = [new()] },
        "air-max" => new Air
        {
            Type = Air.Types.ADDPOINTS, UserId = uint.MaxValue, PowerType = uint.MaxValue,
            Powers = [new() { Id = uint.MaxValue, SenseLevel = uint.MaxValue, SenseAipPower = uint.MaxValue,
                ArmedLevel = uint.MaxValue, ArmedAipPower = uint.MaxValue, OverLoadLevel = uint.MaxValue,
                OverLoadAipPower = uint.MaxValue, Points = uint.MaxValue }],
        },
        "air-null-entries" => new Air { Powers = [null!, new() { Id = 7 }, null!] },
        "air-null-collection" => new Air { Powers = null! },
        "air-negative-int-flags" => new Air { Type = (Air.Types)(-1) },
        "null-outer" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };
}
