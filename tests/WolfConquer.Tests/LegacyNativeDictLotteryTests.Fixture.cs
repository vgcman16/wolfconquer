using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Table = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDictLotteryTests
{
    private sealed class Scope : IDisposable
    {
        private readonly bool priorLoad = GearTable.m_Load;
        private readonly Random priorRandom = Gear.GetRandom;
        private readonly Dictionary<uint, Table.CQ> priorLottery = Table.Info;
        private readonly Dictionary<uint, JadeTable.CQ> priorJades = JadeTable.Info;
        private readonly Dictionary<uint, Dictionary<uint, GearTable.CQ>> priorGear = GearTable.Info;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        private readonly TextWriter priorConsole = Console.Out;
        internal Gear User { get; } = new();
        internal StringWriter Output { get; } = new(CultureInfo.InvariantCulture);

        internal Scope()
        {
            GearTable.m_Load = true;
            Table.Info = new();
            JadeTable.Info = new();
            GearTable.Info = new();
            Gear.GetRandom = null!; // Type1/2 selection must not consult this separate Rate RNG.
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(Output);
        }

        internal static Table.CQ Row(uint type = 1, uint value = 7, uint id = 41)
        {
            var row = new Table.CQ { Id = id, Type = type, Value1 = uint.MaxValue,
                Value2 = value, Value3 = uint.MaxValue, rate = 0 };
            Table.Info[999] = row; // Reward identity is the row Id, not this dictionary key.
            return row;
        }

        internal static Random EligibleGearRate()
        {
            // Seed the genuine mutable global field only; Process's local new Random is untouched.
            // A singleton eligible row then makes its random selection deterministic.
            for (int seed = 0; seed < 100000; seed++)
                if (new Random(seed).Next() % 10000 == 0)
                {
                    Gear.GetRandom = new Random(seed);
                    return new Random(seed);
                }
            throw new InvalidOperationException("No bounded seeded native Rate fixture found.");
        }

        public void Dispose()
        {
            Console.SetOut(priorConsole);
            Output.Dispose();
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
            Gear.GetRandom = priorRandom;
            Table.Info = priorLottery;
            JadeTable.Info = priorJades;
            GearTable.Info = priorGear;
            GearTable.m_Load = priorLoad;
        }
    }

    private static string Payload(byte[] frame, ushort opcode)
    {
        Assert.InRange(frame.Length, 12, 4096);
        Assert.Equal(frame.Length - 8, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(opcode, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal("TQServer", Encoding.ASCII.GetString(frame[^8..]));
        return Convert.ToHexString(frame[4..^8]);
    }

    private static string V(uint value)
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

    // Literal required-field templates: no production serializer or packet builder supplies expectations.
    private static void Air(byte[] frame, uint points, uint user = 17) =>
        Assert.Equal("080510" + V(user) + "180022" + V((uint)(15 + V(points).Length / 2)) +
            "080710001800200028003000380040" + V(points), Payload(frame, 2464));

    private static void Pirate(byte[] frame, uint value) =>
        Assert.Equal("080B100018002000280030003800400048" + V(value) + "500058006000", Payload(frame, 2451));

    private static void Ack(byte[] frame, uint type, params (uint Id, bool Exp)[] rows) =>
        Assert.Equal("08" + V(type) + "1001" + string.Concat(rows.Select(row =>
            "1A" + V((uint)(3 + V(row.Id).Length / 2)) + "08" + V(row.Id) + "10" + (row.Exp ? "01" : "00"))),
            Payload(frame, 2465));
}
