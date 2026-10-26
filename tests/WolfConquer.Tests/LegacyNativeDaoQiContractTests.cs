using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Literal observations from Framework/x86 PinnedDaoQiProtoProbe and the preserved
// protobuf-net DLL. Source-shaped contracts only, not original game-method execution.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeDaoQiContractTests
{
    [Theory]
    [InlineData("default", "080010001800200028003000380040004800500058006000")]
    [InlineData("null-items", "080010111800200028003000380040004800500058006000")]
    [InlineData("zero-item", "0800100018002000280030003800400048005000580060006A020800")]
    [InlineData("null-item", "0800100018002000280030003800400048005000580060006A00")]
    [InlineData("mixed-items", "0800100018002000280030003800400048005000580060006A0208116A006A0608FFFFFFFF0F")]
    [InlineData("empty-positions", "080010001800200028003000380040004800500058006000")]
    [InlineData("positions", "0800100018002000280030003800400048005000580060007000701170FFFFFFFF0F")]
    [InlineData("add-object", "0807100018002000280030003800400048005000580060006A020871")]
    [InlineData("max", "08FFFFFFFF0710FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F28FFFFFFFF0F30FFFFFFFF0F38FFFFFFFF0F40FFFFFFFF0F48FFFFFFFF0F50FFFFFFFF0F58FFFFFFFF0F60FFFFFFFF0F")]
    [InlineData("negative-type", "08FFFFFFFFFFFFFFFFFF0110001800200028003000380040004800500058006000")]
    [InlineData("null-outer", "")]
    public void RegisteredDaoQiPayloadAndNativeFrameMatchThePinnedSerializer(string name, string hex)
    {
        bool priorReady = LegacyNativeCombatGearTable.m_Load;
        try
        {
            LegacyNativeCombatGearTable.m_Load = true;
            var message = Create(name);
            if (message is null)
            {
                Assert.Equal("message", Assert.Throws<ArgumentNullException>(() =>
                    LegacyNativeCombatGearWire.Encode(message!, 2451)).ParamName);
                return;
            }
            byte[] frame = message;
            byte[] expected = Convert.FromHexString(hex);
            Assert.Equal(expected.Length + 12, frame.Length);
            Assert.Equal((ushort)(frame.Length - 8), BinaryPrimitives.ReadUInt16LittleEndian(frame));
            Assert.Equal((ushort)2451, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
            Assert.Equal(expected, frame.AsSpan(4, expected.Length).ToArray());
            Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), frame.AsSpan(frame.Length - 8).ToArray());
        }
        finally
        {
            LegacyNativeCombatGearTable.m_Load = priorReady;
        }
    }

    private static LegacyNativeMsgDaoQi? Create(string name) => name switch
    {
        "default" => new(),
        "null-items" => new() { ItemID = 17, Items = null! },
        "zero-item" => new() { Items = [new()] },
        "null-item" => new() { Items = [null!] },
        "mixed-items" => new() { Items = [new() { Id = 17 }, null!, new() { Id = uint.MaxValue }] },
        "empty-positions" => new() { Positions = [] },
        "positions" => new() { Positions = [0, 17, uint.MaxValue] },
        "add-object" => new() { Type = LegacyNativeMsgDaoQi.Types.ADDOBJ, Items = [new() { Id = 113 }] },
        "max" => new()
        {
            Type = (LegacyNativeMsgDaoQi.Types)int.MaxValue, ItemID = uint.MaxValue,
            TaoistReiki = uint.MaxValue, LOTTERY = uint.MaxValue, Data5 = uint.MaxValue,
            Data6 = uint.MaxValue, MasteryPoints = uint.MaxValue, Data8 = uint.MaxValue,
            PirateExp = uint.MaxValue, Data10 = uint.MaxValue, Position = uint.MaxValue, Done = uint.MaxValue
        },
        "negative-type" => new() { Type = (LegacyNativeMsgDaoQi.Types)(-1) },
        "null-outer" => null,
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };
}
