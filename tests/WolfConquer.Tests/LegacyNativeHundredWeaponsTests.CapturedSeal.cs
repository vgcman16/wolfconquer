using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    [Theory]
    [InlineData("equip", false, false)] [InlineData("equip", false, true)]
    [InlineData("equip", true, false)] [InlineData("equip", true, true)]
    [InlineData("unequip", false, false)] [InlineData("unequip", false, true)]
    [InlineData("unequip", true, false)] [InlineData("unequip", true, true)]
    [InlineData("swap", false, false)] [InlineData("swap", false, true)]
    [InlineData("swap", true, false)] [InlineData("swap", true, true)]
    public Task CapturedSealPhaseSwitchCannotChangePreparedBytesOrExactCapacity(
        string operation, bool capturedSeal, bool oneByteShort) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var (left, right, tail) = ReadySlotPrefix(scope);
        if (capturedSeal) LegacyNativePacket.SealString = "TQServer";
        var message = operation switch
        {
            "equip" => new LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto
            { Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Equip, WeaponSubtype = Weapon.Blade, Position = 5 },
            "unequip" => new LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto
            { Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Unequip, Position = 2 },
            _ => new LegacyNativeMsgHundredWeaponsOpt.MsgHundredWeaponsOptProto
            { Type = LegacyNativeMsgHundredWeaponsOpt.ActionID.Swap, Exchange = new() { Position1 = 2, Position2 = 3 } },
        };
        var expected = Hex(operation switch
        {
            "equip" => "0D0031090800109A0318052000",
            "unequip" => "0C0031090801100018022000",
            _ => "1200310908021000180020003A0408021003",
        });
        if (capturedSeal) expected = expected.Concat(Encoding.UTF8.GetBytes("TQServer")).ToArray();
        var type = typeof(LegacyNativeHundredWeaponsSlotPacket);
        var prepare = type.GetMethod("PrepareFrame", BindingFlags.Static | BindingFlags.NonPublic)!;
        var rent = type.GetMethod("TryRentFrame", BindingFlags.Static | BindingFlags.NonPublic)!;
        var frame = Assert.IsType<byte[]>(prepare.Invoke(null, [message]));
        Assert.Equal(expected, frame);

        // Genuine state change between two real production phases, no serializer
        // callback, alternate builder, forged bytes or racing worker. Scope restores
        // the actual private global seal through the existing native fixture owner.
        if (capturedSeal) typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, null);
        else LegacyNativePacket.SealString = "TQServer";
        Assert.Equal(capturedSeal ? 0 : 8, LegacyNativePacket.SealSize);
        var before = CaptureSlotState(scope, left, right, tail);
        int available = scope.Available;
        using (var pool = new SlotPacketPool("valid", expected.Length - (oneByteShort ? 1 : 0)))
        using (var prepared = (LegacyNativeHundredWeaponsSlotPacket?)rent.Invoke(null, [frame]))
        {
            if (oneByteShort)
            {
                Assert.Null(prepared);
                AssertSlotStateUnchanged(scope, before);
            }
            else
            {
                Assert.NotNull(prepared);
                var stream = Assert.IsType<LegacyNativePacket>(type.GetField("packet", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(prepared));
                Assert.Equal(expected.Length, stream.Capacity);
                Assert.Equal(expected.Length, stream.Size);
                Assert.Equal(4, stream.Position);
                prepared.Publish(scope.Client);
                var frames = scope.Queue.ToArray();
                Assert.Equal(before.Frames.Length + 1, frames.Length);
                Assert.Equal(expected, frames[^1]);
                Assert.Equal(4u, left.BitVector.bits[0]);
                Assert.Equal(8u, right.BitVector.bits[0]);
                Assert.Equal(16u, tail.BitVector.bits[0]);
                for (int index = 0; index < before.Frames.Length; index++)
                {
                    Assert.Same(before.Frames[index], frames[index]);
                    Assert.Equal(before.FrameBytes[index], frames[index]);
                }
            }
        }
        Assert.Equal(available, scope.Available);
    });

    [Theory]
    [InlineData("TQServer", "5451536572766572")]
    [InlineData("é1234567", "C3A9313233343536")]
    public Task CompleteMutationKeepsNativeEightByteUtf8SealAndGoldenUnsealedHeader(string seal, string sealHex)
        => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Item(Weapon.Blade);
        LegacyNativePacket.SealString = seal;
        var expected = Hex("0D0031090800109A0318022000").Concat(Hex(sealHex)).ToArray();
        using (var pool = new SlotPacketPool("valid", expected.Length))
            Assert.True(scope.Owner.Equip(Weapon.Blade, 2));
        Assert.Equal(4u, row.BitVector.bits[0]);
        Assert.Equal(expected, Assert.Single(scope.Queue));
        Assert.Equal((ushort)13, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(expected));
    });
}
