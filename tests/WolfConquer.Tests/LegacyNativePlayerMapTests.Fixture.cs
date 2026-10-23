using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;
using Inner = WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;
using InnerTable = WolfConquer.Core.Data.LegacyNativeInnerPowerTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativePlayerMapTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly Dictionary<uint, LegacyNativeProfessionTable.Benefit> oldBenefits = LegacyNativeProfessionTable.Benefits;
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> oldRanks = Ranking.Ranks;
        private readonly object oldRankSync = Ranking.SynRoot;
        private readonly uint oldInnerCount = InnerTable.Count;
        private readonly InnerTable.Stage[] oldInnerStages = InnerTable.Stages;
        private readonly ConcurrentDictionary<uint, Inner> oldInnerPool = Inner.InnerPowerPolle;
        private readonly ConcurrentDictionary<uint, LegacyNativeUnionRuntime> oldUnions = LegacyNativeUnionRuntime.UnionPoll;
        private readonly Dictionary<uint, LegacyNativeHundredWeaponsTable.HundredWeapon> oldWeapons = LegacyNativeHundredWeaponsTable.HundredWeaponsList;
        private readonly bool oldCombatLoad = LegacyNativeCombatGearTable.m_Load;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture, oldUiCulture = CultureInfo.CurrentUICulture;
        private readonly List<LegacyNativePacket> packets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Player Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;

        internal Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativeProfessionTable.Benefits = new();
            Ranking.Ranks = new(); Ranking.SynRoot = new object(); Ranking.Create();
            InnerTable.Count = 0; InnerTable.Stages = []; Inner.InnerPowerPolle = new();
            LegacyNativeUnionRuntime.UnionPoll = new();
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = new();
            LegacyNativeCombatGearTable.m_Load = false;
            Native = new NativeSpellTestFixture();
            Client.Equipment = new LegacyNativeEquipItemStorage(Client);
            Client.HundredWeapons = new LegacyNativeHundredWeapons(Client);
            Client.CombatGear = new LegacyNativeMyCombatGear(Client);
            Player.InnerPower = new Inner("Inner", Player.UID);
            Player.Nobility = new LegacyNativeNobility(Player.UID, "Noble", 0, 0, 0);
            var queue = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 8; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet); packets.Add(packet); queue.Enqueue(packet);
            }
            Bin.SetValue(null, queue); Fill(Packet);
        }

        internal void NullBin() => Bin.SetValue(null, null);

        public void Dispose()
        {
            Bin.SetValue(null, oldBin); foreach (var packet in packets) packet.Dispose(); Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom; LegacyNativeProfessionTable.Benefits = oldBenefits;
            Ranking.Ranks = oldRanks; Ranking.SynRoot = oldRankSync;
            InnerTable.Count = oldInnerCount; InnerTable.Stages = oldInnerStages; Inner.InnerPowerPolle = oldInnerPool;
            LegacyNativeUnionRuntime.UnionPoll = oldUnions; LegacyNativeHundredWeaponsTable.HundredWeaponsList = oldWeapons;
            LegacyNativeCombatGearTable.m_Load = oldCombatLoad;
            CultureInfo.CurrentCulture = oldCulture; CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    private static void Set(Player player, string field, object value) => typeof(Player)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, value);

    private static unsafe void Fill(LegacyNativePacket packet)
    {
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
            (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
        packet.Size = 321; packet.Seek(237);
    }

    private static Player.SpawnPacketProto Read(LegacyNativePacket packet)
    {
        byte[] bytes = NativeSpellTestFixture.Bytes(packet);
        Assert.Equal(bytes.Length, BitConverter.ToUInt16(bytes, 0)); Assert.Equal((ushort)2227, BitConverter.ToUInt16(bytes, 2));
        using var payload = new MemoryStream(bytes, 4, bytes.Length - 4);
        // Decode the wire collections, not protobuf-net's append-to-constructor-array
        // merge behavior. Production metadata/initializers are not modified.
        return Serializer.Merge(payload, new Player.SpawnPacketProto { StatusFlags = null!, Names = null! });
    }

    private static Dictionary<int, List<ulong>> Scalars(byte[] frame)
    {
        int offset = 4;
        ulong Varint()
        {
            ulong value = 0; int shift = 0;
            while (true)
            {
                Assert.InRange(offset, 4, frame.Length - 1); byte current = frame[offset++];
                value |= (ulong)(current & 127) << shift;
                if ((current & 128) == 0) return value;
                shift += 7; Assert.InRange(shift, 0, 63);
            }
        }
        var result = new Dictionary<int, List<ulong>>();
        while (offset < frame.Length)
        {
            ulong key = Varint(); int field = (int)(key >> 3); int wire = (int)(key & 7);
            if (wire == 0)
            {
                if (!result.TryGetValue(field, out var values)) result[field] = values = new();
                values.Add(Varint());
            }
            else if (wire == 2) { int length = checked((int)Varint()); offset = checked(offset + length); Assert.InRange(offset, 4, frame.Length); }
            else Assert.Fail("Unexpected protobuf wire type " + wire);
        }
        Assert.Equal(frame.Length, offset); return result;
    }
}
