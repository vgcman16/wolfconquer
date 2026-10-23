using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Chi = WolfConquer.Core.World.LegacyNativeChiRuntime;
using Attr = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiAttribute;
using Power = WolfConquer.Core.World.LegacyNativeChiRuntime.ChiPower;
using Kind = WolfConquer.Core.World.LegacyNativeMsgChiInfo.ChiPowerType;
using Stat = WolfConquer.Core.World.LegacyNativeChiAttribute;

namespace WolfConquer.Tests;

// Genuine native Chi component owners, never a SaveClient/login/online ranking claim.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeChiTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly ConcurrentDictionary<uint, Chi> oldChiPool = Chi.ChiPool;
        private readonly Dictionary<uint, LegacyNativeFateExpTable.Value> oldFate = LegacyNativeFateExpTable.Values;
        private readonly object oldSync = Chi.SynRoot;
        private readonly LegacyNativeSafeRandom oldRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        private readonly Dictionary<FieldInfo, object?> oldPowerRandom = typeof(LegacyNativeRandomLite)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(uint))
            .ToDictionary(field => field, field => field.GetValue(Power.Rand));
        private readonly List<LegacyNativePacket> ownedPackets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativePacket Packet => Native.Packet;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal ConcurrentDictionary<uint, LegacyNativeGameClientRuntime> Clients { get; } = new();

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(71);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            LegacyNativeFateExpTable.Values = new();
            Chi.ChiPool = new();
            Chi.SynRoot = new object();
            LegacyNativePoolClients.GamePoll = Clients;
            Power.Rand.Reseed(97);
            Native = new NativeSpellTestFixture();
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 8; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet);
                ownedPackets.Add(packet);
                bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
            Fill(Packet);
        }

        internal void NullBin() => Bin.SetValue(null, null);
        internal LegacyNativePacket Recycled(int index) => ownedPackets[index];

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        public void Dispose()
        {
            Bin.SetValue(null, oldBin);
            foreach (var packet in ownedPackets) packet.Dispose();
            Native.Dispose();
            foreach (var pair in oldPowerRandom) pair.Key.SetValue(Power.Rand, pair.Value);
            LegacyNativePoolRandom.GetRandom = oldRandom;
            LegacyNativePoolRandom.ResetRandom = oldReset;
            LegacyNativePoolClients.GamePoll = oldClients;
            LegacyNativeFateExpTable.Values = oldFate;
            Chi.ChiPool = oldChiPool;
            Chi.SynRoot = oldSync;
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    private static Power MinimumPower(Kind kind, uint uid = 7, int rank = 0) => new(kind)
    {
        UID = uid,
        Name = "ChiTester",
        Rank = rank,
        UnLocked = true,
        Attributes = Enumerable.Repeat(new Attr(Stat.CriticalStrike, 10, false), 4).ToArray(),
    };

    private static Power PerfectPower(Kind kind, uint uid = 7, int rank = 0)
    {
        var power = MinimumPower(kind, uid, rank);
        power.Attributes = Enumerable.Repeat(new Attr(Stat.CriticalStrike, 200, false), 4).ToArray();
        return power;
    }

    private static void SetPower(Chi chi, Power power)
    {
        switch (power.Type)
        {
            case Kind.Dragon: chi.Dragon = power; break;
            case Kind.Phoenix: chi.Phoenix = power; break;
            case Kind.Tiger: chi.Tiger = power; break;
            case Kind.Turtle: chi.Turtle = power; break;
            default: throw new ArgumentOutOfRangeException(nameof(power));
        }
    }

    private static uint[] Totals(Chi chi) =>
    [
        chi.CriticalStrike, chi.SkillCriticalStrike, chi.Immunity, chi.Breakthrough, chi.Counteraction,
        chi.MaxLife, chi.AddAttack, chi.AddMagicAttack, chi.AddMagicDefense,
        chi.FinalAttack, chi.FinalMagicAttack, chi.FinalDefense, chi.FinalMagicDefense,
    ];

    private static void SeedTotals(Chi chi)
    {
        chi.CriticalStrike = chi.SkillCriticalStrike = chi.Immunity = chi.Breakthrough = chi.Counteraction =
            chi.MaxLife = chi.AddAttack = chi.AddMagicAttack = chi.AddMagicDefense = chi.FinalAttack =
            chi.FinalMagicAttack = chi.FinalDefense = chi.FinalMagicDefense = uint.MaxValue;
    }

    private static byte[] ChiMessage(Kind kind, Attr attr, string name = "ChiTester")
    {
        var bytes = new byte[41];
        Put16(bytes, 0, 41); Put16(bytes, 2, 2276);
        bytes[4] = (byte)kind;
        Put32(bytes, 5, (uint)((byte)attr.Type * 10000 + attr.Value));
        byte[] text = Encoding.UTF8.GetBytes(name);
        text.AsSpan(0, Math.Min(name.Length, 32)).CopyTo(bytes.AsSpan(9));
        return bytes;
    }

    private static void Put16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static void Put32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
}
