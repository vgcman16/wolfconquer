using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeItemTransportTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeCounter previousCounter = LegacyNativePoolItems.ItemCounter;
        private readonly LegacyNativeItemTypeRuneMetadata? previousItems = LegacyNativePoolItems.ItemsBase;
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativePacket Packet => Native.Packet;
        internal LegacyNativeEquipItemStorage Equipment { get; }

        internal unsafe Scope()
        {
            LegacyNativePoolItems.ItemCounter = new LegacyNativeCounter(40);
            LegacyNativePoolItems.ItemsBase = new LegacyNativeItemTypeRuneMetadata();
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(53);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new NativeSpellTestFixture();
            Equipment = new LegacyNativeEquipItemStorage(Client);
            Client.Equipment = Equipment;
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
                (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Size = 333; Packet.Seek(211);
        }

        public void Dispose()
        {
            Native.Dispose();
            LegacyNativePoolItems.ItemCounter = previousCounter; LegacyNativePoolItems.ItemsBase = previousItems;
            LegacyNativePoolRandom.GetRandom = previousRandom; LegacyNativePoolRandom.ResetRandom = previousReset;
        }
    }

    private static object? InvokePrivate(Type owner, string method, object? target, params object[] arguments)
    {
        MethodInfo info = owner.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!;
        Assert.True(info.IsPrivate);
        try { return info.Invoke(target, arguments); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw;
        }
    }

    private static void Admit(LegacyNativeEquipItemStorage equipment, LegacyNativeGameItemRuntime item) =>
        InvokePrivate(typeof(LegacyNativeEquipItemStorage), "CheakUp", equipment, item);

    private static void U16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void U32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void FixedAscii(byte[] bytes, int offset, int length, string value)
    {
        byte[] text = Encoding.ASCII.GetBytes(value); Array.Copy(text, 0, bytes, offset, Math.Min(length, text.Length));
    }

    private static LegacyNativeGameItemRuntime ViewItem() => new()
    {
        Uid = 0x10203040, ITEM_ID = 410003, Durability = 123, MaximDurability = 456,
        Position = 0x1234, SocketProgress = 0x50607080,
        SocketOne = LegacyNativeGameItemFlags.Gem.SuperDragonGem, SocketTwo = LegacyNativeGameItemFlags.Gem.EmptySocket,
        SpellID = 0xABCDEF01, Effect = LegacyNativeGameItemFlags.ItemEffect.Poison,
        Plus = 12, Bless = 7, Bound = 1, Enchant = 99, ProgresGreen = 0x12345678,
        Suspicious = 3, Locked = 2, PlusProgress = 0x11223344, Activate = 2, StackSize = 11,
        Purification = new LegacyNativeItemExtra.Purification { PurificationItemID = 1234567 },
        PerfectionLevel = 54, PerfectionProgress = 200, OwnerUID = 0x55667788,
        OwnerName = "Owner", Signature = "Signature", RuneEXP = 1000,
        RelicAttributes = new[] { new LegacyNativeRelicAttribute(1), new LegacyNativeRelicAttribute(100104) },
        AnimaItemID = 4200001, MythSoul = 300, MythSoulPoints = 400
    };

    private static byte[] ViewBytes()
    {
        // Literal inactive two-relic source layout: 188 + 8 relic + 16 tail =212.
        byte[] bytes = new byte[212]; U16(bytes, 0, 212); U16(bytes, 2, 2275);
        U32(bytes, 4, 0x10203040); U32(bytes, 8, 0x88776655);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(12), -1234567890123L);
        U32(bytes, 20, 410003); U16(bytes, 24, 123); U16(bytes, 26, 456); U16(bytes, 28, 4);
        bytes[30] = 0x34; U32(bytes, 31, 0x50607080); bytes[35] = 13; bytes[36] = 255;
        U32(bytes, 37, 0xABCDEF01); bytes[41] = 200; bytes[42] = 12; bytes[43] = 7;
        bytes[44] = 1; bytes[45] = 99; U32(bytes, 46, 0x12345678); bytes[50] = 3; bytes[52] = 2;
        U32(bytes, 54, 0x11223344); U32(bytes, 58, 2); U16(bytes, 66, 11);
        U32(bytes, 68, 1234567); U32(bytes, 72, 1234567); U32(bytes, 76, 54); U32(bytes, 80, 200);
        U32(bytes, 84, 0x55667788); FixedAscii(bytes, 88, 32, "Owner"); FixedAscii(bytes, 120, 64, "Signature");
        U32(bytes, 184, 1000); U32(bytes, 188, 1); U32(bytes, 192, 100104);
        U32(bytes, 196, 4200001); U32(bytes, 204, 300); U32(bytes, 208, 400);
        return bytes;
    }

    private static byte[] ProtoFrame(params byte[] payload)
    {
        byte[] frame = new byte[4 + payload.Length]; U16(frame, 0, (ushort)frame.Length); U16(frame, 2, 2266);
        payload.CopyTo(frame, 4); return frame;
    }
}
