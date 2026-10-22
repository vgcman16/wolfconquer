using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using HW = WolfConquer.Core.World.LegacyNativeHundredWeapons;
using Info = WolfConquer.Core.World.LegacyNativeHundredWeaponInfo;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeHundredWeaponsTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? priorBin = Bin.GetValue(null);
        private readonly Dictionary<uint, LegacyNativeHundredWeaponsTable.HundredWeapon> priorMetadata = LegacyNativeHundredWeaponsTable.HundredWeaponsList;
        private readonly LegacyNativeMagicType priorMagic = LegacyNativePoolMagic.Magic;
        private readonly LegacyNativeSafeRandom priorRandom = LegacyNativePoolRandom.GetRandom;
        private readonly LegacyNativeDbSafeDictionary<LegacyNativeHWRank.Type, LegacyNativeHWRank.Rank> priorRanks = LegacyNativeHWRank.Ranks;
        private readonly string priorLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture, priorUi = CultureInfo.CurrentUICulture;
        private readonly TextWriter priorOutput = Console.Out;
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConcurrentQueue<LegacyNativePacket> bin = new();
        private readonly List<LegacyNativePacket> packets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativeSpellStorage Spells => Native.Storage;
        internal Queue<byte[]> Queue => Native.Queue;
        internal HW Owner { get; }
        internal Dictionary<uint, LegacyNativeHundredWeaponsTable.HundredWeapon> Metadata => LegacyNativeHundredWeaponsTable.HundredWeaponsList;
        internal string Log => log.ToString();

        internal Scope()
        {
            // Scope the real shared native owner before Player's genuine constructor seed draw.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            Native = new NativeSpellTestFixture();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(log);
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = new();
            LegacyNativePoolMagic.Magic = new();
            LegacyNativeHWRank.Ranks = new();
            LegacyNativeHWRank.Create();
            Owner = new HW(Client);
            Client.HundredWeapons = Owner;
            Client.Equipment = new LegacyNativeEquipItemStorage(Client);
            Client.Player.Name = "Hundred Hero";
            SetClass(1001); SetLevel(40);
            for (int i = 0; i < 16; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet); packets.Add(packet); bin.Enqueue(packet);
            }
            Fill(Native.Packet);
            RestoreBin();
        }

        internal void SetClass(uint value) => PlayerField("_class", value);
        internal void SetLevel(ushort value) => PlayerField("_level", value);
        private void PlayerField(string name, object value) => typeof(LegacyNativePlayerRuntime)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Client.Player, value);
        internal void NullBin() => Bin.SetValue(null, null);
        internal void RestoreBin() => Bin.SetValue(null, bin);
        internal int Available => LegacyNativePacketRecycle.Count;

        internal Info Item(Weapon key, byte level = 1, Weapon? physical = null)
        {
            var row = new Info { WeaponSubtype = physical ?? key, Level = level, Attributes = Attributes(), BitVector = new LegacyNativeBitVector32(32) };
            Owner.Objects.Add(key, row);
            return row;
        }

        internal LegacyNativeHundredWeaponsTable.HundredWeapon Meta(Weapon subtype, byte level, ushort magic = 12000)
        {
            var row = new LegacyNativeHundredWeaponsTable.HundredWeapon { WeaponSubtype = subtype, Level = level, Attributes = Attributes(), MagicType = magic };
            Metadata.Add((uint)Metadata.Count + 1, row);
            return row;
        }

        internal LegacyNativeMagicType.Magic Magic(ushort id, ushort index, byte physicalLevel = 0, bool archive = true)
        {
            if (!LegacyNativePoolMagic.Magic.ContainsKey(id)) LegacyNativePoolMagic.Magic.Add(id, new());
            var row = new LegacyNativeMagicType.Magic { ID = id, Level = physicalLevel, isTrojanArchiveSkill = archive };
            LegacyNativePoolMagic.Magic[id].Add(index, row);
            return row;
        }

        internal Info Ready(Weapon type, byte level = 4, ushort magic = 12000)
        {
            var row = Item(type, level); Meta(type, level, magic); Magic(magic, (ushort)(level + 1), (byte)(level + 1));
            return row;
        }

        internal void Rank(LegacyNativeHWRank.Type type) => LegacyNativeHWRank.Ranks[type].UpdateItem(
            new LegacyNativeHWRank.Entry { UID = Client.Player.UID, TotalPoints = 9, Type = type, Name = "Old rank" });

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321; packet.Seek(237);
        }

        public void Dispose()
        {
            Bin.SetValue(null, priorBin);
            foreach (var packet in packets) packet.Dispose();
            Native.Dispose();
            LegacyNativeHundredWeaponsTable.HundredWeaponsList = priorMetadata;
            LegacyNativePoolMagic.Magic = priorMagic;
            LegacyNativePoolRandom.GetRandom = priorRandom;
            LegacyNativeHWRank.Ranks = priorRanks;
            LegacyNativeServerConfig.DbLocation = priorLocation;
            CultureInfo.CurrentCulture = priorCulture; CultureInfo.CurrentUICulture = priorUi;
            Console.SetOut(priorOutput); log.Dispose();
        }
    }

    private static Dictionary<Attr, int> Attributes() => new()
    {
        { Attr.Hitpoints, 0 }, { Attr.PhysicalAttack, 0 }, { Attr.PhysicalDefense, 0 }, { Attr.MagicAttack, 0 }, { Attr.MagicDefense, 0 },
    };

    private static ushort Opcode(byte[] frame) => BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2));
    private static byte[] Hex(string value) => Convert.FromHexString(value);
    private static byte[] SpellFrame(ushort id, ushort level)
    {
        var frame = new byte[25];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 25); BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2101);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8), id); BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(12), level);
        return frame;
    }
}
