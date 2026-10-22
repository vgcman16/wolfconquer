using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using WolfConquer.Core.World;
using Packet = WolfConquer.Core.Networking.LegacyNativePacket;
using Gear = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

// The serial collection's fixture registers the actual production contracts once.
// Every unsafe read/write is inside a fresh owned <=1024-byte initialized allocation.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed unsafe partial class LegacyNativePacketProtobufTests
{
    [Fact]
    public Task SerializeUsesCurrentCursorWithoutHeaderSealOrSizeChangesAndPreservesOwners() => NativePacketTestThread.Run(() =>
    {
        byte[] original = Enumerable.Repeat((byte)0xCC, 128).ToArray();
        byte[] expected = original.ToArray();
        byte[] payload = Convert.FromHexString("080010111A18080010001800200028003000380040004800500058006000");
        payload.CopyTo(expected, 7);
        var row = new Gear.Item();
        var items = new[] { row };
        var message = new Gear { UID = 17, Items = items };
        string sourceBefore = JsonSerializer.Serialize(message, new JsonSerializerOptions { IncludeFields = true });
        using var packet = new Packet(original);
        packet.Size = 91;
        packet.Seek(7);

        packet.ProtoBufferSerialize(message);

        Assert.Equal(7 + payload.Length, packet.Position);
        Assert.Equal(91, packet.Size);
        Assert.Equal(expected, Copy(packet, original.Length));
        Assert.Equal(Enumerable.Repeat((byte)0xCC, 128), original);
        Assert.Same(items, message.Items);
        Assert.Same(row, message.Items[0]);
        Assert.Equal(17u, message.UID);
        Assert.Equal(Gear.TypeID.Load, message.Type);
        Assert.Equal(0u, row.Level);
        Assert.Equal(LegacyNativeCombatGear.TypeID.None, row.ID);
        Assert.Equal(sourceBefore, JsonSerializer.Serialize(message, new JsonSerializerOptions { IncludeFields = true }));
    });

    [Fact]
    public Task SerializeNullIsZeroBytesAndDoesNotTouchInitializedBufferCursorOrSize() => NativePacketTestThread.Run(() =>
    {
        byte[] original = Enumerable.Repeat((byte)0xA5, 64).ToArray();
        using var packet = new Packet(original);
        packet.Size = 27;
        packet.Seek(13);

        packet.ProtoBufferSerialize(null!);

        Assert.Equal(13, packet.Position);
        Assert.Equal(27, packet.Size);
        Assert.Equal(original, Copy(packet, original.Length));
    });

    [Fact]
    public Task SerializeHasNoCombatGearGateAndWritesRegisteredPayloadWhileGateFalse() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeCombatGearQueryScope(false);
        byte[] original = Enumerable.Repeat((byte)0xD3, 32).ToArray();
        using var packet = new Packet(original);
        packet.Seek(3);

        packet.ProtoBufferSerialize(new Dict { Type = 1, Done = 17 });

        Assert.False(Table.m_Load);
        Assert.Equal(7, packet.Position);
        Assert.Equal(32, packet.Size);
        Assert.Equal(Convert.FromHexString("08011011"), Copy(packet, 7)[3..]);
        Assert.Equal(original[..3], Copy(packet, 3));
        Assert.Equal(original[7..], Copy(packet, original.Length)[7..]);
    });

    private static byte[] Copy(Packet packet, int count)
    {
        byte[] result = new byte[count];
        Marshal.Copy((IntPtr)packet.Memory, result, 0, count);
        return result;
    }

    private static byte[] Frame(ushort declaredLength, ushort opcode, params byte[] payload)
    {
        byte[] result = Enumerable.Repeat((byte)0xF3, 64).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(result, declaredLength);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), opcode);
        payload.CopyTo(result, 4); // All test payloads and declared reads fit these initialized64 bytes.
        return result;
    }

    private sealed class SealScope : IDisposable
    {
        private static readonly FieldInfo Field = typeof(Packet).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? old = Field.GetValue(null);
        internal SealScope(string? value) => Field.SetValue(null, value);
        public void Dispose() => Field.SetValue(null, old);
    }
}
