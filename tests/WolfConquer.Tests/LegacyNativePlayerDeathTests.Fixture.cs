using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;
using Inner = WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;
using InnerTable = WolfConquer.Core.Data.LegacyNativeInnerPowerTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePlayerDeathTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly List<LegacyNativePacket> buffers = new();
        private readonly LegacyNativeSafeRandom random = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime reset = LegacyNativePoolRandom.ResetRandom;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? clients = LegacyNativePoolClients.GamePoll;
        private readonly LegacyNativeItemTypeRuneMetadata? items = LegacyNativePoolItems.ItemsBase;
        private readonly LegacyNativeMagicType spells = LegacyNativePoolMagic.Magic;
        private readonly Dictionary<LegacyNativeDbLevExp.Sort, Dictionary<byte, LegacyNativeDbLevExp>> levels = LegacyNativePoolLevelInfo.LevelInfo;
        private readonly Dictionary<uint, LegacyNativeProfessionTable.Benefit> benefits = LegacyNativeProfessionTable.Benefits;
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> ranks = Ranking.Ranks;
        private readonly object rankSync = Ranking.SynRoot;
        private readonly uint innerCount = InnerTable.Count;
        private readonly InnerTable.Stage[] innerStages = InnerTable.Stages;
        private readonly ConcurrentDictionary<uint, Inner> innerPool = Inner.InnerPowerPolle;
        private readonly Dictionary<uint, LegacyNativeHundredWeaponsTable.HundredWeapon> weapons = LegacyNativeHundredWeaponsTable.HundredWeaponsList;
        private readonly bool combatLoad = LegacyNativeCombatGearTable.m_Load;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo ui = CultureInfo.CurrentUICulture;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal readonly Queue<byte[]> KillerQueue = new();
        internal LegacyNativeGameClientRuntime Killer { get; }
        internal LegacyNativeGameMap Map { get; }
        internal readonly Dictionary<byte, LegacyNativeDbLevExp> Rows = new();

        internal unsafe Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            LegacyNativePoolRandom.GetRandom = new(769); LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativePoolClients.GamePoll = new();
            LegacyNativePoolItems.ItemsBase = new(); LegacyNativePoolMagic.Magic = new();
            LegacyNativePoolLevelInfo.LevelInfo = new() { [LegacyNativeDbLevExp.Sort.User] = Rows };
            LegacyNativeProfessionTable.Benefits = new(); Ranking.Ranks = new(); Ranking.SynRoot = new(); Ranking.Create();
            InnerTable.Count = 0; InnerTable.Stages = []; Inner.InnerPowerPolle = new();
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = new(); LegacyNativeCombatGearTable.m_Load = false;
            var queue = new ConcurrentQueue<LegacyNativePacket>();
            for (int n = 0; n < 64; n++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
                buffers.Add(packet); queue.Enqueue(packet);
            }
            Bin.SetValue(null, queue); Native = new(); Killer = NativeSpellTestFixture.CreateOwner(KillerQueue);
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0, (IntPtr)Packet.Memory, LegacyNativePacket.MAX_SIZE);
            Map = new(128, 128, 1082) { View = new LegacyNativeMapView(128, 128) };
            Initialize(Client, 17); Initialize(Killer, 31); Row(50, 100000); Row(1, 100);
        }
        private void Initialize(LegacyNativeGameClientRuntime client, uint id)
        {
            client.Player.UID = id; client.Player.Name = "Wolf" + id; client.Player.Map = Map.ID; client.Player.X = 60; client.Player.Y = 60;
            client.Equipment = new(client); client.Inventory = new(client); client.Rune = new(client); client.MyNinja = new(client);
            client.MySpells ??= new(client);
            client.HundredWeapons = new(client); client.CombatGear = new(client);
            client.Player.InnerPower = new("Inner" + id, id); client.Player.Nobility = new(id, "Noble" + id, 0, 0, 0);
            client.Player.Reborn = 2; Prestate(client, 50, 1001); Map.Enquer(client);
        }
        internal static void Prestate(LegacyNativeGameClientRuntime client, ushort level, uint job)
        {
            typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client.Player, level);
            typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(client.Player, job);
        }
        internal LegacyNativeDbLevExp Row(byte level, ulong exp, int up = 1, int mentor = 1)
        {
            var row = new LegacyNativeDbLevExp { Level = level, Action = LegacyNativeDbLevExp.Sort.User, Experience = exp, UpLevTime = up, MentorUpLevTime = mentor };
            Rows[level] = row; return row;
        }
        internal void Rune(LegacyNativeGameClientRuntime client, string name, byte level = 1)
        {
            uint id = 4020000u + level; LegacyNativePoolItems.ItemsBase![id] = new() { ID = id, Name = name, RuneSpellID = 1432000 };
            Assert.True(client.Rune!.Add(new() { Uid = id, ITEM_ID = id, Position = 102 }));
        }
        internal void Spell(LegacyNativeGameClientRuntime client, ushort id)
        {
            client.MySpells ??= new(client); client.MySpells.ClientSpells[id] = new() { ID = id };
            LegacyNativePoolMagic.Magic[id] = new() { [0] = new() { ID = id, Level = 0, ColdTime = 0, Duration = 20 } };
        }
        internal void FailNextTake() => Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>(new[] { (LegacyNativePacket)null! }));
        public void Dispose()
        {
            Bin.SetValue(null, oldBin); Native.Dispose(); foreach (var packet in buffers) packet.Dispose();
            LegacyNativePoolRandom.GetRandom = random; LegacyNativePoolRandom.ResetRandom = reset;
            LegacyNativePoolClients.GamePoll = clients;
            LegacyNativePoolItems.ItemsBase = items; LegacyNativePoolMagic.Magic = spells; LegacyNativePoolLevelInfo.LevelInfo = levels;
            LegacyNativeProfessionTable.Benefits = benefits; Ranking.Ranks = ranks; Ranking.SynRoot = rankSync;
            InnerTable.Count = innerCount; InnerTable.Stages = innerStages; Inner.InnerPowerPolle = innerPool;
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = weapons; LegacyNativeCombatGearTable.m_Load = combatLoad;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = ui;
        }
    }
    [ProtoContract]
    private sealed class Update
    {
        [ProtoMember(1)] public uint Uid { get; set; }
        [ProtoMember(2)] public List<Row> Rows { get; set; } = new();
    }
    [ProtoContract]
    private sealed class Row
    {
        [ProtoMember(1)] public uint Type { get; set; }
        [ProtoMember(2)] public ulong Value { get; set; }
        [ProtoMember(4)] public uint Flag { get; set; }
        [ProtoMember(5)] public uint Time { get; set; }
        [ProtoMember(6)] public uint Damage { get; set; }
        [ProtoMember(7)] public uint Level { get; set; }
    }
    private static Update ReadUpdate(byte[] frame)
    {
        Assert.Equal((ushort)2176, BitConverter.ToUInt16(frame, 2));
        using var body = new MemoryStream(frame, 4, frame.Length - 4); return Serializer.Deserialize<Update>(body);
    }
}
