using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeGearReceiverTests
{
    private sealed class Scope : IDisposable
    {
        private readonly bool load = Table.m_Load;
        private readonly Dictionary<uint, Dictionary<uint, Table.CQ>> rows = Table.Info;
        private readonly Dictionary<uint, Jade.CQ> jades = Jade.Info;
        private readonly Dictionary<Rank.Type, Rank.Rank> ranks = Rank.Ranks;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? poll = LegacyNativePoolClients.GamePoll;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture, uiCulture = CultureInfo.CurrentUICulture;
        private readonly TextWriter console = Console.Out;
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativeGameClientRuntime User => Native.Owner;
        internal LegacyNativeMyCombatGear Gear { get; }
        internal Queue<byte[]> Queue => Native.Queue;
        internal byte[] InputBytes { get; private set; } = [];
        internal int Declared { get; private set; }

        internal Scope()
        {
            Table.m_Load = true; Table.Info = new(); Jade.Info = new();
            Rank.Ranks = new(); Rank.Create();
            LegacyNativePoolClients.GamePoll = new();
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(log);
            User.Player.UID = 17; User.Player.Name = "Wolf";
            // Genuine readonly accessors remain readonly; seed only their physical test prestate.
            typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(User.Player, 7001u);
            typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(User.Player, (ushort)120);
            Gear = new LegacyNativeMyCombatGear(User);
            User.CombatGear = Gear; Gear.AirPowers.Id = 9;
        }

        internal unsafe void Input(string payload, ushort opcode = ushort.MaxValue, int? declared = null)
        {
            byte[] bytes = Convert.FromHexString(payload);
            Declared = declared ?? bytes.Length + 4;
            InputBytes = new byte[bytes.Length + 12];
            BinaryPrimitives.WriteUInt16LittleEndian(InputBytes, checked((ushort)Declared));
            BinaryPrimitives.WriteUInt16LittleEndian(InputBytes.AsSpan(2), opcode);
            bytes.CopyTo(InputBytes, 4);
            "TQServer"u8.CopyTo(InputBytes.AsSpan(InputBytes.Length - 8));
            Assert.InRange(InputBytes.Length, 12, 512);
            Native.Packet.Seek(0);
            Native.Packet.ZeroFill(16383);
            Marshal.Copy(InputBytes, 0, (IntPtr)Native.Packet.Memory, InputBytes.Length);
            Native.Packet.Size = InputBytes.Length; // Actual copied frame and seal, not spare allocation.
            Native.Packet.Seek(11);
        }

        internal unsafe void UnchangedInput()
        {
            byte[] actual = new byte[InputBytes.Length];
            Marshal.Copy((IntPtr)Native.Packet.Memory, actual, 0, actual.Length);
            Assert.Equal(InputBytes, actual);
            Assert.Equal(InputBytes.Length, Native.Packet.Size);
            Assert.Equal(Declared, Native.Packet.Position);
        }

        internal LegacyNativeGameClientRuntime Target(uint uid, uint physicalItem, Queue<byte[]> output)
        {
            var target = NativeSpellTestFixture.CreateOwner(output);
            target.Player.UID = uid;
            typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(target.Player, 2001u);
            target.CombatGear = new LegacyNativeMyCombatGear(target);
            target.CombatGear.Items[(Gear.TypeID)999] = new Gear.Weapon
                { ItemID = (Gear.TypeID)physicalItem, Level = 50, Exp = 23, Equip = 1, Transform = 31 };
            return target;
        }

        public void Dispose()
        {
            Native.Dispose();
            Table.m_Load = load; Table.Info = rows; Jade.Info = jades; Rank.Ranks = ranks;
            LegacyNativePoolClients.GamePoll = poll;
            CultureInfo.CurrentCulture = culture; CultureInfo.CurrentUICulture = uiCulture;
            Console.SetOut(console); log.Dispose();
        }
    }

    private static void Receive(string method, LegacyNativeGameClientRuntime? user, LegacyNativePacket packet)
    {
        var body = typeof(LegacyNativeMyCombatGear).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(body);
        try { body.Invoke(null, [user, packet]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }

    private static string V(ulong value)
    {
        var bytes = new List<byte>();
        do { byte b = (byte)(value & 127); value >>= 7; bytes.Add((byte)(b | (value == 0 ? 0 : 128))); } while (value != 0);
        return Convert.ToHexString(bytes.ToArray());
    }

    private static string ViewRequest(int type = 1, uint uid = 77) => "08" + V(unchecked((ulong)(long)type)) + "10" + V(uid);
    private static string AirRequest(int type = 0, uint requestUid = 0, uint power = 0) =>
        "08" + V(unchecked((ulong)(long)type)) + "10" + V(requestUid) + "18" + V(power);

    private static byte[] Framed(ushort opcode, string hex)
    {
        byte[] payload = Convert.FromHexString(hex), frame = new byte[payload.Length + 12];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)(payload.Length + 4));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), opcode);
        payload.CopyTo(frame, 4); "TQServer"u8.CopyTo(frame.AsSpan(frame.Length - 8));
        return frame;
    }

    private static void AirResponse(byte[] frame, uint sense, uint armed = 0, uint overload = 0,
        uint sensePower = 0, uint armedPower = 0, uint overloadPower = 0, uint points = 0)
    {
        string nested = "080910" + V(sense) + "18" + V(sensePower) + "20" + V(armed) + "28" + V(armedPower) +
            "30" + V(overload) + "38" + V(overloadPower) + "40" + V(points);
        Assert.Equal(Framed(2464, "08031011180022" + V((uint)(nested.Length / 2)) + nested), frame);
    }

    private static (ushort Id, ushort Level) Spell(byte[] frame)
    {
        var spell = NativeSpellTestFixture.ReadSpell(frame);
        Assert.Equal(0, spell.Experience); Assert.Equal((byte)0, spell.SoulLevel); Assert.Equal((byte)0, spell.PreviousLevel);
        return (spell.ID, spell.Level);
    }

    private static void PirateRanks(uint score)
    {
        foreach (var type in new[] { Rank.Type.WEEK_Pirate, Rank.Type.Overall_Pirate })
        {
            var entry = Assert.Single(Rank.Ranks[type].RankingList).Value;
            Assert.Equal(17u, entry.UID); Assert.Equal("Wolf", entry.Name); Assert.Equal(7001u, entry.Class);
            Assert.Equal((byte)120, entry.Level); Assert.Equal(score, entry.TotalPoints); Assert.Equal(type, entry.Type);
        }
    }
}
