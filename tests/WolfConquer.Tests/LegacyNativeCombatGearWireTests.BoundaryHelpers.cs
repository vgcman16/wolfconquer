using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearWireTests
{
    private static readonly Func<MemoryStream, ushort, byte[]> ObjectPayloadWrapper =
        (Func<MemoryStream, ushort, byte[]>)typeof(LegacyNativeCombatGearWire)
            .GetMethod("WrapObjectPayload", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Func<MemoryStream, ushort, byte[]>));

    private static byte[] FramePattern(int length) => Enumerable.Range(0, length).Select(index => (byte)((index * 37) & 255)).ToArray();

    private static void AssertInputUnchanged(MemoryStream stream, byte[] before, long position)
    {
        Assert.Equal(before.LongLength, stream.Length);
        Assert.Equal(position, stream.Position);
        Assert.Equal(before, stream.ToArray());
    }

    private static void AssertBoundedFrame(byte[] frame, byte[] payload, ushort opcode)
    {
        // Independent framing contract: header length includes4 header bytes,
        // excludes8 ASCII seal bytes, and must fit UInt16 without truncation.
        Assert.Equal(payload.Length + 12, frame.Length);
        Assert.Equal(payload.Length + 4, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(opcode, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal(payload, frame[4..^8]);
        Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), frame[^8..]);
    }

    private static Func<byte[]>[] NullProjectionRoutes() =>
    [
        () => LegacyNativeCombatGearWire.Encode((LegacyNativeMsgCombatGear)null!, 2420),
        () => LegacyNativeCombatGearWire.Encode((LegacyNativeMsgCombatGearSkin)null!, 2422),
        () => LegacyNativeCombatGearWire.Encode((LegacyNativeMsgCombatGearOpt)null!, 2421),
        () => LegacyNativeCombatGearWire.Encode((LegacyNativeMsgDaoQi)null!, 2451),
        () => LegacyNativeCombatGearWire.Encode((LegacyNativeMsgAirPowerUp)null!, 2464),
        () => LegacyNativeCombatGearWire.Encode((LegacyNativeMsgDictLottery)null!, 2465),
    ];
}
