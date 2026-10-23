using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Explicit no-SQL component tests. Never invoke whole LoadCharacter from this suite.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCharacterReadTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly string oldDb = LegacyNativeServerConfig.DbLocation;
        private readonly LegacyNativeItemTypeRuneMetadata? oldItems = LegacyNativePoolItems.ItemsBase;
        private readonly LegacyNativeCounter oldDetainedCounter = LegacyNativeConfiscatorRuntime.CounterUID;
        private readonly LegacyNativeConfiscatorTable oldContainers = LegacyNativePoolConfiscator.QueueContainer;
        private readonly ConcurrentDictionary<uint, LegacyNativeUnionRuntime> oldUnions = LegacyNativeUnionRuntime.UnionPoll;
        private readonly List<LegacyNativeHairfaceStorageTable.Hairface> oldHairfaces = LegacyNativeHairfaceStorageTable.Hairfaces;
        private readonly List<LegacyNativeHairfaceStorageTable.HairColor> oldColors = LegacyNativeHairfaceStorageTable.HairColors;
        private readonly Dictionary<uint, LegacyNativeExchangeShopGoodsEx.item>? oldGoods = LegacyNativeExchangeShopGoodsEx.exchange_shop_goods_ex;
        private readonly ConcurrentDictionary<uint, LegacyNativeJiangHu> oldJiang = LegacyNativeJiangHu.Poll;
        private readonly List<LegacyNativePacket> packets = new();
        internal readonly NativeSpellTestFixture Native;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativePacket Packet => Native.Packet;
        internal string? OwnedRoot;

        internal unsafe Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(137);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolItems.ItemsBase = new();
            LegacyNativeConfiscatorRuntime.CounterUID = new(1);
            LegacyNativePoolConfiscator.QueueContainer = new(); LegacyNativeUnionRuntime.UnionPoll = new();
            LegacyNativeHairfaceStorageTable.Hairfaces = new();
            LegacyNativeHairfaceStorageTable.HairColors = new();
            LegacyNativeExchangeShopGoodsEx.exchange_shop_goods_ex = new(); LegacyNativeJiangHu.Poll = new();
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int i = 0; i < 64; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                packets.Add(packet); bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin); Native = new NativeSpellTestFixture();
            Prepare(Client, 17); Player.Name = "Owner";
        }

        private static void Prepare(LegacyNativeGameClientRuntime client, uint uid)
        {
            client.Player.UID = uid; client.Player.Name = "User" + uid;
            LegacyNativeDataCoreAttributes.LoadClient(client.Player);
            client.MyWardrobe = new(client); client.GearMythsoulBag = new(); client.RelicFuse = new();
            client.DemonExterminator = new(); client.Confiscator = new(); client.HairfaceStorage = new(client);
            typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client.Player, 1001u);
            typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client.Player, (ushort)100);
        }

        internal (LegacyNativeGameClientRuntime Client, Queue<byte[]> Queue) Other(uint uid)
        {
            var queue = new Queue<byte[]>(); var client = NativeSpellTestFixture.CreateOwner(queue);
            Prepare(client, uid); return (client, queue);
        }
        internal LegacyNativeItemRuneMetadata Metadata(uint id, uint job = 0)
        {
            var row = new LegacyNativeItemRuneMetadata { ID = id, Name = "Owned", Class = job };
            LegacyNativePoolItems.ItemsBase![id] = row; return row;
        }
        internal LegacyNativeGameItemRuntime Item(uint uid = 71, uint id = 410009)
        {
            Metadata(id); return new LegacyNativeGameItemRuntime { Uid = uid, ITEM_ID = id };
        }
        internal void OwnFiles()
        {
            OwnedRoot = Path.Combine(Path.GetTempPath(), "WolfConquer-CharacterRead-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(OwnedRoot); Directory.CreateDirectory(Path.Combine(OwnedRoot, "PlayersItems"));
            LegacyNativeServerConfig.DbLocation = OwnedRoot;
        }
        internal string ItemPath => Path.Combine(OwnedRoot!, "PlayersItems", Player.UID + ".bin");
        internal LegacyNativeGameMap Map(bool valid = true)
        {
            var map = new LegacyNativeGameMap(16, 16, 1002) { View = new LegacyNativeMapView(16, 16) };
            for (int x = 0; x < 16; x++) for (int y = 0; y < 16; y++) map.cells[x, y] = valid ? LegacyNativeMapFlagType.Valid : 0;
            Client.Map = map; Player.Map = 1002; Player.X = 4; Player.Y = 4; return map;
        }
        public void Dispose()
        {
            Bin.SetValue(null, oldBin); foreach (var packet in packets) packet.Dispose(); Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom; LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativePoolItems.ItemsBase = oldItems; LegacyNativeServerConfig.DbLocation = oldDb;
            LegacyNativeConfiscatorRuntime.CounterUID = oldDetainedCounter; LegacyNativePoolConfiscator.QueueContainer = oldContainers;
            LegacyNativeUnionRuntime.UnionPoll = oldUnions; LegacyNativeHairfaceStorageTable.Hairfaces = oldHairfaces;
            LegacyNativeHairfaceStorageTable.HairColors = oldColors;
            LegacyNativeExchangeShopGoodsEx.exchange_shop_goods_ex = oldGoods; LegacyNativeJiangHu.Poll = oldJiang;
            if (OwnedRoot != null)
            {
                string exact = Path.GetFullPath(OwnedRoot);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                Assert.StartsWith(temp, exact, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("WolfConquer-CharacterRead-", Path.GetFileName(exact), StringComparison.Ordinal);
                if (Directory.Exists(exact)) Directory.Delete(exact, true);
            }
        }
    }
    private static ushort Opcode(byte[] bytes) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static byte[] Raw<T>(T value) where T : unmanaged => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref value, 1)).ToArray();
    private static LegacyNativeClientItemRecord Row(uint uid, uint id = 410009, ushort position = 0, uint warehouse = 0) => new()
    { UID = uid, ITEM_ID = id, Position = position, WH_ID = warehouse, StackSize = 1, Inscribed = 9 };
    private static byte[] ItemsFile(int count, IEnumerable<LegacyNativeClientItemRecord> rows,
        params LegacyNativeClientItemPerfectionRecord[] perfection)
    {
        var bytes = new List<byte>(BitConverter.GetBytes(count));
        foreach (var row in rows) bytes.AddRange(Raw(row));
        bytes.AddRange(BitConverter.GetBytes(perfection.Length));
        foreach (var item in perfection) bytes.AddRange(Raw(item)); return bytes.ToArray();
    }
}
