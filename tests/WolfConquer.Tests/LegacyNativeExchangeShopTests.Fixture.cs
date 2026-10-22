using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Goods = WolfConquer.Core.Data.LegacyNativeExchangeShopGoods;
using GoodsEx = WolfConquer.Core.Data.LegacyNativeExchangeShopGoodsEx;
using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

// Complete native shop/storage/metadata leaves, without buy handlers, inventory
// transactions, automatic host birth, workers, OS socket pumps or live clients.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeExchangeShopTests
{
    private sealed class State : IDisposable
    {
        private readonly Dictionary<uint, Goods.item>? oldGoods = Goods.exchange_shop_goods;
        private readonly Dictionary<uint, GoodsEx.item>? oldEx = GoodsEx.exchange_shop_goods_ex;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo oldUi = CultureInfo.CurrentUICulture;
        private readonly TextWriter oldConsole = Console.Out;
        internal Dictionary<uint, Goods.item> GoodsRows { get; } = new();
        internal Dictionary<uint, GoodsEx.item> ExRows { get; } = new();
        internal StringWriter ConsoleText { get; } = new();
        internal Shop Shop { get; } = new(null!);

        internal State()
        {
            Goods.exchange_shop_goods = GoodsRows;
            GoodsEx.exchange_shop_goods_ex = ExRows;
            // A genuine native shared RNG instance, not a forced-result provider.
            // Its displaced owner and unconsumed random stream are restored below.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(17);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(ConsoleText);
        }

        internal void Metadata(bool ex, uint key, uint id, uint shop = 7, uint amount = 3)
        {
            if (ex) ExRows[key] = new GoodsEx.item { id = id, shopid = shop, amount = amount };
            else GoodsRows[key] = new Goods.item { id = id, shopid = shop, amount = amount };
        }

        internal Shop.Item Record(uint key, uint shop, uint id, uint times = 0)
        {
            var row = new Shop.Item { shopid = shop, goods_rec_id = id, times = times };
            Shop.Items.Add(key, row);
            return row;
        }

        internal bool Add(bool ex, uint shop, uint id, uint count) =>
            ex ? Shop.AddItemEx(shop, id, count) : Shop.AddItem(shop, id, count);

        public void Dispose()
        {
            Goods.exchange_shop_goods = oldGoods!;
            GoodsEx.exchange_shop_goods_ex = oldEx!;
            LegacyNativePoolRandom.GetRandom = oldRandom;
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUi;
            Console.SetOut(oldConsole);
            ConsoleText.Dispose();
        }
    }

    private sealed class PacketScope : IDisposable
    {
        internal State State { get; } = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Shop Shop { get; }

        internal PacketScope()
        {
            Shop = new Shop(Client);
            Reset();
        }

        internal unsafe void Reset()
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Packet.Size = 321;
            Packet.Seek(237);
        }

        internal unsafe void Unwritten()
        {
            Assert.Empty(Queue);
            Assert.Equal(321, Packet.Size);
            Assert.Equal(237, Packet.Position);
            Assert.Equal((byte)0xA5, Packet.Memory[0]);
        }

        internal unsafe void Sent(uint npc, uint shop, DateTime before, DateTime after,
            params (uint Index, uint Count)[] items)
        {
            byte[] actual = Assert.Single(Queue);
            int cursor = 4;
            Assert.Equal((byte)8, actual[cursor++]);
            Assert.Equal(npc, Read(actual, ref cursor));
            Assert.Equal((byte)16, actual[cursor++]);
            Assert.Equal(shop, Read(actual, ref cursor));
            Assert.Equal((byte)24, actual[cursor++]);
            Assert.Equal(Client.Player.UID, Read(actual, ref cursor));
            Assert.Equal((byte)32, actual[cursor++]);
            uint time = Read(actual, ref cursor);
            TimeBetween(time, before, after);
            Assert.Equal(Frame(npc, shop, Client.Player.UID, time, items), actual);
            Assert.Equal(actual, NativeSpellTestFixture.Bytes(Packet));
            Assert.Equal(actual.Length, Packet.Size);
            Assert.Equal(4, Packet.Position);
            Assert.Equal((byte)0xA5, Packet.Memory[actual.Length]);
        }

        public void Dispose()
        {
            Native.Dispose();
            State.Dispose();
        }
    }

    private sealed class OwnedFiles : IDisposable
    {
        private readonly string previous = LegacyNativeServerConfig.DbLocation;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(),
            "wolf-native-exchange-db-" + Guid.NewGuid().ToString("N"));
        internal string PathName(bool ex) => Path.Combine(Root,
            ex ? "exchange_shop_goods_ex.txt" : "exchange_shop_goods.txt");

        internal OwnedFiles()
        {
            CheckChain(Path.GetTempPath());
            if (Directory.Exists(Root) || File.Exists(Root))
                throw new IOException("Owned exchange fixture already exists.");
            Directory.CreateDirectory(Root);
            LegacyNativeServerConfig.DbLocation = Root; // Native Load appends its own backslash.
        }

        internal void Text(bool ex, string text) =>
            File.WriteAllText(PathName(ex), text, new UTF8Encoding(false));

        private static void CheckChain(string path)
        {
            for (string? cursor = Path.GetFullPath(path); cursor is not null; cursor = Path.GetDirectoryName(cursor))
                if (Directory.Exists(cursor) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked exchange fixture rejected.");
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previous;
            string full = Path.GetFullPath(Root);
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full),
                    Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)) ||
                !Regex.IsMatch(Path.GetFileName(full), "^wolf-native-exchange-db-[0-9a-f]{32}$"))
                throw new IOException("Exchange fixture cleanup containment failed.");
            CheckChain(full);
            foreach (string entry in Directory.GetFileSystemEntries(full))
                if ((!StringComparer.OrdinalIgnoreCase.Equals(entry, PathName(false)) &&
                     !StringComparer.OrdinalIgnoreCase.Equals(entry, PathName(true))) ||
                    (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Unexpected exchange fixture entry; preserving files.");
            foreach (string path in new[] { PathName(false), PathName(true) })
                if (File.Exists(path)) File.Delete(path);
            Directory.Delete(full, recursive: false);
        }
    }

    private static void ItemIs(Shop.Item row, uint shop, uint id, uint times)
    {
        Assert.Equal(shop, row.shopid);
        Assert.Equal(id, row.goods_rec_id);
        Assert.Equal(times, row.times);
    }

    private static int Delta(DateTime time) =>
        (23 - time.Hour) * 3600 + (60 - time.Minute) * 60 + (60 - time.Second);

    private static void TimeBetween(uint actual, DateTime before, DateTime after)
    {
        // Native uses three Now samples and subtraction of large double-valued
        // TimeSpan totals. Allow their measured elapsed time plus 1s rounding;
        // do not replace the source's midnight+60s arithmetic with midnight.
        int elapsed = checked((int)Math.Ceiling(Math.Abs((after - before).TotalSeconds)));
        uint low = (uint)Math.Max(0, Math.Min(Delta(before), Delta(after)) - 1);
        uint high = (uint)(Math.Max(Delta(before), Delta(after)) + elapsed + 1);
        Assert.InRange(actual, low, high);
    }

    private static byte[] Frame(uint npc, uint shop, uint uid, uint time,
        params (uint Index, uint Count)[] items)
    {
        var payload = new List<byte>();
        Field(payload, 1, npc); Field(payload, 2, shop); Field(payload, 3, uid);
        Field(payload, 4, time); Field(payload, 5, 0);
        foreach (var item in items)
        {
            var nested = new List<byte>();
            Field(nested, 1, item.Index); Field(nested, 2, item.Count); Field(nested, 3, 0);
            payload.Add(0x32); Varint(payload, (uint)nested.Count); payload.AddRange(nested);
        }
        var frame = new byte[payload.Count + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)frame.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2317);
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static void Field(List<byte> bytes, int tag, uint value)
    {
        bytes.Add((byte)(tag << 3)); Varint(bytes, value);
    }

    private static void Varint(List<byte> bytes, uint value)
    {
        while (value > 127) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }

    private static uint Read(byte[] bytes, ref int cursor)
    {
        uint value = 0;
        for (int shift = 0; shift < 35; shift += 7)
        {
            byte current = bytes[cursor++];
            value |= (uint)(current & 127) << shift;
            if (current < 128) return value;
        }
        throw new InvalidDataException("Owned exchange uint exceeded five bytes.");
    }
}
