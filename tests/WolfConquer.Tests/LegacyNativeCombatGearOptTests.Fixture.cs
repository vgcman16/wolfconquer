using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatGearOptTests
{
    private sealed class Scope : IDisposable
    {
        private readonly bool load = Table.m_Load;
        private readonly Dictionary<uint, Dictionary<uint, Table.CQ>> rows = Table.Info;
        private readonly Random random = Gear.GetRandom;
        private readonly TextWriter console = Console.Out;
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        internal Gear User { get; } = new();
        internal StringWriter Output { get; } = new(CultureInfo.InvariantCulture);

        internal Scope()
        {
            Table.m_Load = true;
            Table.Info = new();
            Gear.GetRandom = null!; // None of the native operation branches draw randomness.
            Console.SetOut(Output);
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        internal Gear.Weapon Weapon(uint level = 1, uint exp = 0, uint id = 201)
        {
            var weapon = new Gear.Weapon { ItemID = (Gear.TypeID)id, Level = level, Exp = exp };
            User.Items[Gear.TypeID.Dragonhowl] = weapon; // Requested key can differ from physical ItemID.
            return weapon;
        }

        internal static Table.CQ Row(uint level, uint exp, uint id = 201)
        {
            if (!Table.Info.TryGetValue(id, out var levels)) Table.Info[id] = levels = new();
            var row = new Table.CQ { ID = 999, Level = 999, Exp = exp };
            return levels[level] = row; // Native dictionary keys, not mutable row fields, select it.
        }

        public void Dispose()
        {
            Console.SetOut(console);
            Output.Dispose();
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            Gear.GetRandom = random;
            Table.Info = rows;
            Table.m_Load = load;
        }
    }

    private static Opt Request(Opt.Types type) => new()
    {
        Type = type, ItemID = Gear.TypeID.Dragonhowl, UID = 77, Exp = 13, Transform = 7
    };

    private static string Payload(byte[] frame, ushort opcode)
    {
        Assert.InRange(frame.Length, 12, 4096);
        Assert.Equal(frame.Length - 8, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(opcode, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal("TQServer", Encoding.ASCII.GetString(frame[^8..]));
        return Convert.ToHexString(frame[4..^8]);
    }

    private static string V(ulong value)
    {
        var bytes = new List<byte>();
        do
        {
            byte current = (byte)(value & 127);
            value >>= 7;
            bytes.Add((byte)(current | (value == 0 ? 0 : 128)));
        } while (value != 0);
        return Convert.ToHexString(bytes.ToArray());
    }

    private static string Signed(uint value) => V(unchecked((ulong)(long)(int)value));

    // Literal required-field templates; no production serializer/builder supplies expectations.
    private static void Operation(byte[] frame, Opt.Types type, uint uid = 1, uint transform = 7,
        uint exp = 13, uint id = 201, string extra = "") =>
        Assert.Equal("08" + V(unchecked((ulong)(long)(int)type)) + "10" + Signed(id) +
            "18" + V(transform) + "20" + V(uid) + "28" + V(exp) + extra, Payload(frame, 2421));

    private static void Updated(byte[] frame, uint level, uint exp = 0, uint transform = 0,
        uint mastery = 0, uint equip = 0, uint id = 201)
    {
        string item = "08" + Signed(id) + "10" + V(level) + "18" + V(exp) + "20" + V(equip) +
            "28" + V(transform) + "30003800400048005000580060" + V(mastery);
        Assert.Equal("080310111A" + V((uint)(item.Length / 2)) + item, Payload(frame, 2420));
    }

    private static void Reiki(byte[] frame, uint value) =>
        Assert.Equal(NativeCombatGearSupportFixture.Frame(3, reiki: value), frame);

    private static void Pirate(byte[] frame, uint value) =>
        Assert.Equal("080B100018002000280030003800400048" + V(value) + "500058006000", Payload(frame, 2451));
}
