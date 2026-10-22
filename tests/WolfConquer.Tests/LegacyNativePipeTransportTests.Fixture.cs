using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;
using Pipe = WolfConquer.Core.World.LegacyNativePipeClientRuntime;
using Socket = WolfConquer.Core.Networking.LegacyNativeSecuritySocket;
using Extra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

// Transport/source-component proof only: no native Pipe.LoginQueue/ExecuteLogin,
// CompleteConnected, Inventory.Add/Update, host, real network pump or live client.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePipeTransportTests
{
    private static readonly FieldInfo QueueField = typeof(Socket).GetField(
        "SendQueue", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly LegacyNativeItemTypeRuneMetadata? previousItems = LegacyNativePoolItems.ItemsBase;
        internal NativeSpellTestFixture Native { get; }
        internal Pipe Pipe { get; }
        internal Socket Socket => Native.Owner.Socket!;
        internal LegacyNativeGameClientRuntime Owner => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativePacket Packet => Native.Packet;

        internal Scope()
        {
            // Real isolated native RNG BEFORE the physical player's seed draw.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolItems.ItemsBase = null;
            Native = new NativeSpellTestFixture();
            Pipe = new Pipe(Socket, Owner);
            ResetBuffer();
        }

        internal unsafe void ResetBuffer()
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Size = 321;
            Packet.Seek(237);
        }

        public void Dispose()
        {
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolItems.ItemsBase = previousItems;
        }
    }

    private static Socket NewSocket() => new(null!, null!);

    private static LegacyNativeTQCast5 Cipher()
    {
        var cipher = new LegacyNativeTQCast5();
        cipher.GenerateKey(Encoding.ASCII.GetBytes("0123456789ABCDEF"));
        return cipher;
    }

    private static Item PhysicalItem() => new() { Uid = 17, ITEM_ID = 1_000_000 };

    private static byte[] ItemFrame(uint id = 1_000_000, ushort mode = 0, ushort position = 0)
    {
        var bytes = new byte[206];
        Put16(bytes, 0, 206);
        Put16(bytes, 2, 2116);
        Put32(bytes, 4, 17);
        Put32(bytes, 8, id);
        Put16(bytes, 16, mode);
        bytes[18] = (byte)position;
        return bytes;
    }

    private static Extra.Purification Purification(uint uid = 71) => new()
    {
        ItemUID = uid,
        Typ = Extra.Typing.ExpireTime,
        PurificationItemID = 800001,
        PurificationLevel = 3,
        EffectPercent2 = 23,
    };

    private static Extra.Refinery Refinery(uint uid = 82) => new()
    {
        ItemUID = uid,
        Typ = Extra.Typing.StabilizationEffectRefined,
        EffectID = 724440,
        EffectLevel = 5,
        EffectPercent = 11,
        EffectPercent2 = 22,
    };

    private static bool PurificationAvailable(Extra.Purification value) =>
        (bool)typeof(Extra.Purification).GetField("Available",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;

    private static byte[] ExtraFrame(params uint[] words)
    {
        var bytes = new byte[4 + words.Length * 4];
        Put16(bytes, 0, (ushort)bytes.Length);
        Put16(bytes, 2, 2134);
        for (int index = 0; index < words.Length; index++) Put32(bytes, 4 + index * 4, words[index]);
        return bytes;
    }

    private static void Put16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);

    private static void Put32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private sealed class FailingTimer : IDisposable
    {
        internal readonly InvalidOperationException Fault = new("Owned pipe timer-disposal fault.");
        internal int Calls;
        public void Dispose() { Calls++; throw Fault; }
    }
}
