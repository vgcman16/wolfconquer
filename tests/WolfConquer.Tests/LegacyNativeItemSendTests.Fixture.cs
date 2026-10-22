using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using NativeExtra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

// Independent native prerequisites only. Full Send(GameClient), Inventory and the
// real ExecuteLogin worker are deliberately not invoked by this component batch.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeItemSendTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeItemTypeRuneMetadata? priorItems = LegacyNativePoolItems.ItemsBase;
        private readonly LegacyNativeGroupServerList.Server? priorServer = LegacyNativeGroupServerList.MyServerInfo;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        private readonly TextWriter priorConsole = Console.Out;
        private readonly StringWriter console = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativePacket Packet => Native.Packet;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeItemTypeRuneMetadata Items { get; } = new();

        internal Scope()
        {
            LegacyNativePoolItems.ItemsBase = Items;
            LegacyNativeGroupServerList.MyServerInfo = new() { ID = 2, Name = "Owned item test" };
            Client.Player.ServerID = 2;
            Console.SetOut(console);
            ResetBuffer();
        }

        internal unsafe void ResetBuffer(byte fill = 0xA5)
        {
            Marshal.Copy(Enumerable.Repeat(fill, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Size = 321;
            Packet.Seek(237);
        }

        internal unsafe byte[] Memory(int length)
        {
            var bytes = new byte[length];
            Marshal.Copy((IntPtr)Packet.Memory, bytes, 0, length);
            return bytes;
        }

        public void Dispose()
        {
            Native.Dispose();
            LegacyNativePoolItems.ItemsBase = priorItems;
            LegacyNativeGroupServerList.MyServerInfo = priorServer!;
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
            Console.SetOut(priorConsole);
            console.Dispose();
        }
    }

    private static LegacyNativeGameItemRuntime Item(uint id = 1_000_000) => new()
    {
        Uid = 17,
        ITEM_ID = id,
    };

    // Zero/default payload, independently framed with BCL primitives. It does not
    // call the native writer, an item DTO, or any item/extra production encoder.
    private static byte[] EmptyItemFrame(int relicCount = 5)
    {
        var bytes = new byte[186 + 4 * relicCount];
        Put16(bytes, 0, (ushort)bytes.Length);
        Put16(bytes, 2, 2116);
        return bytes;
    }

    private static byte[] PlainItemFrame(uint id = 1_000_000, ushort mode = 0, byte locked = 0)
    {
        byte[] expected = EmptyItemFrame();
        Put32(expected, 4, 17);
        Put32(expected, 8, id);
        Put16(expected, 16, mode);
        expected[40] = locked;
        return expected;
    }

    private static void Put16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static void Put32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static uint Read32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static ushort Read16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

    private static NativeExtra.Purification Purification(uint uid = 71) => new()
    {
        ItemUID = uid,
        Typ = NativeExtra.Typing.ExpireTime,
        PurificationItemID = 800001,
        PurificationLevel = 3,
        EffectPercent2 = 23,
    };

    private static NativeExtra.Refinery Refinery(uint uid = 82) => new()
    {
        ItemUID = uid,
        Typ = NativeExtra.Typing.StabilizationEffectRefined,
        EffectID = 724440,
        EffectLevel = 5,
        EffectPercent = 11,
        EffectPercent2 = 22,
    };

    private static bool PurificationAvailable(NativeExtra.Purification value) =>
        (bool)typeof(NativeExtra.Purification)
            .GetField("Available", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;

    private static byte[] ExtraFrame(params uint[] words)
    {
        var bytes = new byte[4 + words.Length * 4];
        Put16(bytes, 0, (ushort)bytes.Length);
        Put16(bytes, 2, 2134);
        for (int i = 0; i < words.Length; i++)
            Put32(bytes, 4 + 4 * i, words[i]);
        return bytes;
    }

    private static void AssertTail(Scope scope, int start, int length = 16) =>
        Assert.All(scope.Memory(start + length).Skip(start), value => Assert.Equal((byte)0xA5, value));
}
