using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real typed component fixtures; no host/client/login or modern capability delegates.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeAttackDispatchTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly Dictionary<ushort, List<ushort>> oldWeapons = LegacyNativePoolMagic.WeaponSpells;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeSafeRandom oldAttackRandom = LegacyNativeAttackBase.MyRandom;
        private readonly uint oldProfRate = LegacyNativeServerConfig.ExpRateProf;
        private readonly uint oldSpellRate = LegacyNativeServerConfig.ExpRateSpell;
        private readonly List<LegacyNativePacket> packets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeMsgSpell ClientSpell { get; } = new() { ID = 1195 };
        internal LegacyNativeMagicType.Magic Row { get; } = new() { ID = 1195, Type = LegacyNativeMagicType.MagicSort.AddMana };
        internal LegacyNativeInteractQuery Attack { get; } = new() { UID = 17, OpponentUID = 17, SpellID = 1195, AtkType = 24, X = 30, Y = 40 };
        internal Dictionary<ushort, LegacyNativeMagicType.Magic> Rows { get; } = new();

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new(73); LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeAttackBase.MyRandom = new(91);
            LegacyNativePoolClients.GamePoll = new(); LegacyNativePoolMagic.WeaponSpells = new();
            LegacyNativeServerConfig.ExpRateProf = 1; LegacyNativeServerConfig.ExpRateSpell = 1;
            Native = new(); Client.Equipment = new(Client); Client.MyProfs = new(Client); Client.Rune = new(Client); Client.MyNinja = new(Client);
            Player.Name = "Owner"; Player.Stamina = 100; Player.Mana = 100; Player.Map = 1002; Player.X = 32; Player.Y = 42;
            Set("_level", (ushort)140); Client.Status.MaxMana = 1000;
            Native.Storage.ClientSpells[1195] = ClientSpell; Rows[0] = Row;
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 12; index++) { var p = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); packets.Add(p); bin.Enqueue(p); }
            Bin.SetValue(null, bin); Packet.Size = 321; Packet.Seek(237);
        }

        internal void Set(string field, object value) => typeof(LegacyNativePlayerRuntime).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Player, value);
        internal void Skill(ushort id, ushort level = 0)
        {
            Native.Storage.ClientSpells.Clear(); Rows.Clear(); ClientSpell.ID = id; ClientSpell.Level = level; Row.ID = id;
            Attack.SpellID = id; Native.Storage.ClientSpells[id] = ClientSpell; Rows[level] = Row;
        }
        internal void NextLevel(uint threshold = 1000)
        {
            Row.Experience = threshold; Rows[(ushort)(ClientSpell.Level + 1)] = new() { ID = ClientSpell.ID };
        }
        public void Dispose()
        {
            Bin.SetValue(null, oldBin); foreach (var p in packets) p.Dispose(); Native.Dispose();
            LegacyNativePoolClients.GamePoll = oldClients; LegacyNativePoolMagic.WeaponSpells = oldWeapons;
            LegacyNativePoolRandom.GetRandom = oldRandom; LegacyNativePoolRandom.ResetRandom = oldReset; LegacyNativeAttackBase.MyRandom = oldAttackRandom;
            LegacyNativeServerConfig.ExpRateProf = oldProfRate; LegacyNativeServerConfig.ExpRateSpell = oldSpellRate;
        }
    }

    private static byte[] Animation(Scope s, ushort id, ushort level, ushort x, ushort y, params byte[][] targets) =>
        LegacyNativeSpellAnimationTests.Frame(LegacyNativeSpellAnimationTests.Payload(17, 0, x, y, id, (byte)level, s.ClientSpell.UseSpellSoul, 0, 0, targets));
    private static byte[] Target(uint uid, uint damage) => LegacyNativeSpellAnimationTests.Target(uid, damage, true, 0);
    private static ushort Opcode(byte[] bytes) => BitConverter.ToUInt16(bytes, 2);

    private static byte[] ShieldFrame(uint flag, uint duration, uint damage, uint level)
    {
        var nested = new List<byte>();
        void Field(uint tag, ulong value) { Varint(nested, tag << 3); Varint(nested, value); }
        Field(1, (uint)LegacyNativeMsgUpdate.DataType.AzureShield);
        if (flag != 0) Field(4, flag); if (duration != 0) Field(5, duration);
        if (damage != 0) Field(6, damage); if (level != 0) Field(7, level);
        var payload = new List<byte> { 8, 17, 18 }; Varint(payload, (uint)nested.Count); payload.AddRange(nested);
        var result = new List<byte>(); result.AddRange(BitConverter.GetBytes((ushort)(payload.Count + 4)));
        result.AddRange(BitConverter.GetBytes((ushort)2176)); result.AddRange(payload); return result.ToArray();
    }
    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }
}
