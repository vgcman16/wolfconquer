using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearWireTests
{
    [Fact]
    public void DisabledGatePrecedesNullMessagesAndNullStream()
    {
        using var scope = new NativeCombatGearQueryScope(false);
        Assert.Null(LegacyNativeCombatGearWire.Encode((LegacyNativeMsgCombatGear)null!, 2420));
        Assert.Null(LegacyNativeCombatGearWire.Encode((LegacyNativeMsgCombatGearSkin)null!, 2422));
        Assert.Null(LegacyNativeCombatGearWire.Encode((LegacyNativeMsgAirPowerUp)null!, 2464));
        Assert.Null(LegacyNativeCombatGearWire.WrapStream(null!));
    }

    [Fact]
    public void StreamWrapperCopiesExistingHeaderBytesKeepsPositionAndPreservesNativeEmptyQuirk()
    {
        using var scope = new NativeCombatGearQueryScope();
        using var stream = new MemoryStream(new byte[] { 0, 0, 0x74, 9, 1, 2, 3, 4 });
        stream.Position = 5;
        var frame = LegacyNativeCombatGearWire.WrapStream(stream);
        Assert.Equal(Convert.FromHexString("08007409010203045451536572766572"), frame);
        Assert.Equal(5, stream.Position);
        Assert.Equal(new byte[] { 0, 0, 0x74, 9, 1, 2, 3, 4 }, stream.ToArray());
        using var empty = new MemoryStream();
        Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), LegacyNativeCombatGearWire.WrapStream(empty));
        Assert.Throws<NullReferenceException>(() => LegacyNativeCombatGearWire.WrapStream(null!));
    }

    [Fact]
    public void NativeStreamLengthCastWrapsWithoutIntroducingHostFrameSizeRejection()
    {
        using var scope = new NativeCombatGearQueryScope();
        using var stream = new MemoryStream(new byte[70000]);
        var frame = LegacyNativeCombatGearWire.WrapStream(stream);
        Assert.Equal(70008, frame.Length);
        Assert.Equal(unchecked((ushort)70000), BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), frame[^8..]);
        // Managed wrapper only. Never pass this oversized buffer to native Packet/Send.
    }

    [Fact]
    public Task ActualAirStateAndNativeByteSendKeepThePayloadBeforeTheReservedSeal() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeCombatGearQueryScope();
        var gear = new LegacyNativeCombatGear();
        gear.AirPowers.Id = 7;
        var originalPowers = gear.AirPowers;
        byte[] frame = gear.AirPowers.Send(LegacyNativeMsgAirPowerUp.Types.INFO, 17);
        var before = frame.ToArray();
        Assert.Same(originalPowers, gear.AirPowers);
        Assert.Equal(1u, gear.AirPowers.SenseLevel);
        Assert.Equal(Convert.FromHexString("080010111800221008071001180020002800300038004000"), frame[4..^8]);
        var queue = new Queue<byte[]>();
        var socket = new LegacyNativeSecuritySocket(null!, null!) { Alive = true };
        typeof(LegacyNativeSecuritySocket).GetField("SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(socket, queue); // Explicit MODEL queue, not native Create or live delivery.
        var owner = new LegacyNativeGameClientRuntime { Socket = socket };

        owner.Send(frame);

        Assert.Equal(before, frame);
        var queued = Assert.Single(queue);
        Assert.NotSame(frame, queued);
        Assert.Equal(before, queued);
        Assert.Null(socket.Connection);
        Assert.Null(owner.Map);
        Assert.False(owner.FullLoading);
    });
}
