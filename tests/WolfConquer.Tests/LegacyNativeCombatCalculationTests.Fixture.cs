using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Player = WolfConquer.Core.World.LegacyNativePlayerRuntime;
using Disdain = WolfConquer.Core.Data.LegacyNativeDisdain;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatCalculationTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeSafeRandom oldAttackRandom = LegacyNativeAttackBase.MyRandom;
        private readonly bool oldCombatLoad = LegacyNativeCombatGearTable.m_Load;
        private readonly LegacyNativeItemTypeRuneMetadata? oldItems = LegacyNativePoolItems.ItemsBase;
        private readonly Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem> oldNinja = LegacyNativeNinjaFile.gouyu_type;
        private readonly uint oldPhysicalDamage = LegacyNativeServerConfig.PhysicalDamage;
        private readonly Dictionary<uint, LegacyNativeProfessionTable.Benefit> oldBenefits = LegacyNativeProfessionTable.Benefits;
        private readonly Dictionary<int, Disdain.DisdainItem> oldRecords = Disdain.DisdainRecords;
        private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture;
        private readonly List<LegacyNativePacket> packets = new();
        internal readonly NativeSpellTestFixture Native;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Player Player => Client.Player;
        internal Queue<byte[]> Queue => Native.Queue;
        internal string? Temp;

        internal Scope()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(73);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeAttackBase.MyRandom = new LegacyNativeSafeRandom(79);
            LegacyNativeCombatGearTable.m_Load = true;
            LegacyNativePoolItems.ItemsBase = new();
            LegacyNativeNinjaFile.gouyu_type = new();
            LegacyNativeServerConfig.PhysicalDamage = 100;
            LegacyNativeProfessionTable.Benefits = new();
            Disdain.DisdainRecords = new();
            Native = new NativeSpellTestFixture();
            Client.Equipment = new LegacyNativeEquipItemStorage(Client);
            Client.CombatGear = new LegacyNativeMyCombatGear(Client);
            Client.Rune = new LegacyNativeRuneItemStorage(Client);
            Client.MyNinja = new LegacyNativeNinja(Client);
            Player.Nobility = new LegacyNativeNobility(Player.UID, "Noble", 0, 0, 0);
            Client.ExtraStatus = new();
            var queue = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 4; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                packets.Add(packet); queue.Enqueue(packet);
            }
            Bin.SetValue(null, queue);
        }

        internal void NullBin() => Bin.SetValue(null, null);
        internal void Gear(LegacyNativeCombatGear.TypeID type, uint job)
        {
            Set(Player, "_class", job);
            Client.CombatGear!.Items[type] = new LegacyNativeCombatGear.Weapon { ItemID = type, Equip = 1 };
        }
        internal void Flag(Flag flag) => Player.BitVector.TryAdd((int)flag, 100, true, 0);
        internal LegacyNativeMonsterRole Monster(int power, int health = 10000, int extraDamage = 100)
            => new(new LegacyNativeMonsterFamily { ID = 51, Name = "Actual", MaxHealth = health, extra_battlelev = power, extra_damage = extraDamage }, 400001, "", null);

        internal void Power(int value, ushort level = 0, uint job = 2000, byte reborn = 0)
        {
            Set(Player, "_level", level); Set(Player, "_class", job);
            Player.Reborn = reborn; Player.NobilityRank = LegacyNativeNobilityRank.Serf;
            Client.Equipment!.BattlePower = value - level - reborn * 5;
            Assert.Equal(value, Player.BattlePower);
        }

        internal Player Peer(int power, ushort level, uint job, byte reborn)
        {
            var owner = new LegacyNativeGameClientRuntime();
            owner.Equipment = new LegacyNativeEquipItemStorage(owner);
            owner.Player.Nobility = new LegacyNativeNobility(25, "Target", 0, 0, 0);
            Set(owner.Player, "_level", level); Set(owner.Player, "_class", job);
            owner.Player.Reborn = reborn; owner.Player.NobilityRank = LegacyNativeNobilityRank.Serf;
            owner.Equipment.BattlePower = power - level - reborn * 5;
            Assert.Equal(power, owner.Player.BattlePower);
            return owner.Player;
        }

        internal void Directory()
        {
            Temp = Path.Combine(Path.GetTempPath(), "wolf-native-disdain-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Temp);
            LegacyNativeServerConfig.DbLocation = Temp + Path.DirectorySeparatorChar;
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (var packet in packets) packet.Dispose();
            Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativeAttackBase.MyRandom = oldAttackRandom;
            LegacyNativeCombatGearTable.m_Load = oldCombatLoad;
            LegacyNativePoolItems.ItemsBase = oldItems;
            LegacyNativeNinjaFile.gouyu_type = oldNinja;
            LegacyNativeServerConfig.PhysicalDamage = oldPhysicalDamage;
            LegacyNativeProfessionTable.Benefits = oldBenefits;
            Disdain.DisdainRecords = oldRecords;
            LegacyNativeServerConfig.DbLocation = oldLocation;
            CultureInfo.CurrentCulture = oldCulture;
            if (Temp != null)
            {
                string path = Path.GetFullPath(Temp);
                string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!path.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                    !Path.GetFileName(path).StartsWith("wolf-native-disdain-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing cleanup outside the owned temporary fixture directory.");
                System.IO.Directory.Delete(path, true);
            }
        }
    }

    private static void Set(Player player, string field, object value)
        => typeof(Player).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(player, value);

    private static LegacyNativeRoleStatus Status(uint power, bool expired)
        => new(power, 0) { Stamp = expired ? DateTime.Now.AddDays(-1) : DateTime.Now.AddDays(1) };

    private static LegacyNativeMsgMessage.MsgTalkProto Talk(byte[] bytes)
    {
        Assert.Equal((ushort)2179, BitConverter.ToUInt16(bytes, 2));
        Assert.Equal(bytes.Length, BitConverter.ToUInt16(bytes, 0));
        using var stream = new MemoryStream(bytes, 4, bytes.Length - 4);
        return Serializer.Deserialize<LegacyNativeMsgMessage.MsgTalkProto>(stream);
    }
}
