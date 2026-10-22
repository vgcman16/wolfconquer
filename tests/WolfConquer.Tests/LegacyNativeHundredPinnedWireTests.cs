#nullable disable
using ProtoBuf;
using Opt = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsOpt;
using Info = WolfConquer.Core.World.LegacyNativeMsgHundredWeaponsInfo;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

// Literal controls observed in the owned Framework/x86 probe with the hash-pinned
// protobuf2.0.0.668 dependency. Contract serializer proof, not Info producer/host proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeHundredPinnedWireTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void CompleteContractsMatchHeldFrameworkPayloadOrEnumRejection(
        string name, object value, string expectedHex, bool error)
    {
        using var output = new MemoryStream();
        if (error)
            Assert.Throws<ProtoException>(() => Serializer.Serialize((Stream)output, value));
        else
            Serializer.Serialize((Stream)output, value);
        Assert.Equal(expectedHex, Convert.ToHexString(output.ToArray()));
        Assert.NotEmpty(name);
    }

    private static readonly Dictionary<string, (string Hex, bool Error)> Controls = new()
    {
        ["opt-default"] = ("0800100018002000", false),
        ["opt-action-0"] = ("0800100018002000", false),
        ["opt-action-1"] = ("0801100018002000", false),
        ["opt-action-2"] = ("0802100018002000", false),
        ["opt-action-3"] = ("0803100018002000", false),
        ["opt-action-4"] = ("0804100018002000", false),
        ["opt-weapon-0"] = ("0800100018002000", false),
        ["opt-weapon-410"] = ("0800109A0318002000", false),
        ["opt-weapon-420"] = ("080010A40318002000", false),
        ["opt-weapon-421"] = ("080010A50318002000", false),
        ["opt-weapon-422"] = ("080010A60318002000", false),
        ["opt-weapon-430"] = ("080010AE0318002000", false),
        ["opt-weapon-440"] = ("080010B80318002000", false),
        ["opt-weapon-441"] = ("080010B90318002000", false),
        ["opt-weapon-450"] = ("080010C20318002000", false),
        ["opt-weapon-460"] = ("080010CC0318002000", false),
        ["opt-weapon-470"] = ("080010D60318002000", false),
        ["opt-weapon-480"] = ("080010E00318002000", false),
        ["opt-weapon-481"] = ("080010E10318002000", false),
        ["opt-weapon-490"] = ("080010EA0318002000", false),
        ["opt-weapon-491"] = ("080010EB0318002000", false),
        ["opt-weapon-492"] = ("080010EC0318002000", false),
        ["opt-weapon-493"] = ("080010ED0318002000", false),
        ["opt-weapon-500"] = ("080010F40318002000", false),
        ["opt-weapon-510"] = ("080010FE0318002000", false),
        ["opt-weapon-511"] = ("080010FF0318002000", false),
        ["opt-weapon-520"] = ("080010880418002000", false),
        ["opt-weapon-521"] = ("080010890418002000", false),
        ["opt-weapon-522"] = ("0800108A0418002000", false),
        ["opt-weapon-530"] = ("080010920418002000", false),
        ["opt-weapon-540"] = ("0800109C0418002000", false),
        ["opt-weapon-560"] = ("080010B00418002000", false),
        ["opt-weapon-561"] = ("080010B10418002000", false),
        ["opt-weapon-562"] = ("080010B20418002000", false),
        ["opt-weapon-570"] = ("080010BA0418002000", false),
        ["opt-weapon-580"] = ("080010C40418002000", false),
        ["opt-weapon-601"] = ("080010D90418002000", false),
        ["opt-weapon-610"] = ("080010E20418002000", false),
        ["opt-weapon-611"] = ("080010E30418002000", false),
        ["opt-weapon-612"] = ("080010E40418002000", false),
        ["opt-weapon-613"] = ("080010E50418002000", false),
        ["opt-weapon-614"] = ("080010E60418002000", false),
        ["opt-weapon-616"] = ("080010E80418002000", false),
        ["opt-weapon-617"] = ("080010E90418002000", false),
        ["opt-weapon-624"] = ("080010F00418002000", false),
        ["opt-weapon-626"] = ("080010F20418002000", false),
        ["opt-weapon-670"] = ("0800109E0518002000", false),
        ["opt-weapon-671"] = ("0800109F0518002000", false),
        ["opt-weapon-680"] = ("080010A80518002000", false),
        ["opt-weapon-681"] = ("080010A90518002000", false),
        ["opt-weapon-721"] = ("080010D10518002000", false),
        ["opt-weapon-900"] = ("080010840718002000", false),
        ["opt-exchange-zero"] = ("08001000180020003A0408001000", false),
        ["opt-max"] = ("080410E10318FFFFFFFF0720FFFFFFFFFFFFFFFFFF0128FF0130FFFFFFFF073A110880808080F8FFFFFFFF0110FFFFFFFF07", false),
        ["opt-negative"] = ("080310A4031880808080F8FFFFFFFF012001280130FFFFFFFFFFFFFFFFFF013A0D08FFFFFFFFFFFFFFFFFF011000", false),
        ["opt-unknown-action-five"] = ("08", true),
        ["opt-negative-action"] = ("08", true),
        ["opt-intmax-action"] = ("08", true),
        ["opt-unknown-weapon-one"] = ("080010", true),
        ["opt-unknown-weapon-ushortmax"] = ("080010", true),
        ["opt-null"] = ("", false),
        ["info-default"] = ("08001000", false),
        ["info-action-0"] = ("08001000", false),
        ["info-action-1"] = ("08011000", false),
        ["info-action-2"] = ("08021000", false),
        ["info-action-3"] = ("08031000", false),
        ["info-empty"] = ("08001000", false),
        ["info-zero-row"] = ("080010001A1608001000180020002800300038004000480050006000", false),
        ["info-null-row"] = ("08001000", false),
        ["info-mixed-rows"] = ("080310111A16080010001800200028003000380040004800500060001A28089A0310011881808080082009280730FFFFFFFFFFFFFFFFFF01380240034804500560FFFFFFFF0F", false),
        ["info-max-row"] = ("080210FFFFFFFF0F1A3E08E10310FFFFFFFF0F18FFFFFFFF0F20FF0128FFFFFFFF0F3080808080F8FFFFFFFF0138FFFFFFFF0740FFFFFFFFFFFFFFFFFF014800500160FFFFFFFF0F", false),
        ["info-wrapped-row"] = ("080010001A1E08A403100018002000280030914E389F9C0140004800500060E0E0FFFF0F", false),
        ["info-unknown-action-four"] = ("08", true),
        ["info-negative-action"] = ("08", true),
        ["info-unknown-weapon-one"] = ("", true),
        ["info-unknown-weapon-ushortmax"] = ("", true),
        ["info-null"] = ("", false),
    };
}
