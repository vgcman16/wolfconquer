using System.Buffers.Binary;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Query = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaInfo;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeNinjaInfoPinnedWireTests
{
    // Complete source-shaped contract, observed object Serialize controls from an
    // owned Framework/x86 probe using held protobuf-net2.0.0.668 SHA66653D78....
    [Theory]
    [InlineData("default", "0800100018002000")]
    [InlineData("unequip", "0801100018002000")]
    [InlineData("unlock", "080B100718082009")]
    [InlineData("all-max", "08FFFFFFFF0710FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F")]
    [InlineData("gap-three", "0803100018002000")]
    [InlineData("gap-ten", "080A100018002000")]
    [InlineData("negative", "08FFFFFFFFFFFFFFFFFF01100018002000")]
    [InlineData("minimum", "0880808080F8FFFFFFFF01100018002000")]
    [InlineData("null", "")]
    public Task GenuineInfoEncoderMatchesEveryHeldLiteralControl(string name, string hex) =>
        NativePacketTestThread.Run(() =>
        {
            using var native = new NativeSpellTestFixture();
            Fill(native.Packet);
            Query? value = name switch
            {
                "default" => new(),
                "unequip" => new() { Type = Query.TypeID.Unequip },
                "unlock" => new() { Type = Query.TypeID.UnlockMastery, ID = 7, Points = 8, Position = 9 },
                "all-max" => new() { Type = (Query.TypeID)int.MaxValue, ID = uint.MaxValue, Points = uint.MaxValue, Position = uint.MaxValue },
                "gap-three" => new() { Type = (Query.TypeID)3 },
                "gap-ten" => new() { Type = (Query.TypeID)10 },
                "negative" => new() { Type = (Query.TypeID)(-1) },
                "minimum" => new() { Type = (Query.TypeID)int.MinValue },
                "null" => null,
                _ => throw new ArgumentOutOfRangeException(nameof(name)),
            };
            Assert.Same(native.Packet, native.Packet.CreateNinjaInfo(value!));
            byte[] payload = Convert.FromHexString(hex);
            byte[] expected = new byte[payload.Length + 4];
            BinaryPrimitives.WriteUInt16LittleEndian(expected, (ushort)expected.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2394);
            payload.CopyTo(expected, 4);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(native.Packet));
            Assert.Equal(expected.Length, native.Packet.Size);
            Assert.Equal(4, native.Packet.Position);
            Tail(native.Packet);
            Assert.Empty(native.Queue);
        });

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
            0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        packet.Size = 321; packet.Seek(237);
    }

    private static unsafe void Tail(LegacyNativePacket packet) =>
        Assert.Equal((byte)0xA5, packet.Memory[packet.Size]);
}
