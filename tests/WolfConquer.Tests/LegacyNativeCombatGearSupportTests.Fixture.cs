using System.Buffers.Binary;
using System.Text;
using CombatGear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;

namespace WolfConquer.Tests;

// Test-only lease of actual native statics; no production random provider or callbacks.
internal sealed class NativeCombatGearSupportFixture : IDisposable
{
    private readonly bool priorLoad = GearTable.m_Load;
    private readonly Random priorRandom = CombatGear.GetRandom;
    private readonly Dictionary<uint, JadeTable.CQ> priorMetadata = JadeTable.Info;
    internal CombatGear Gear { get; } = new();
    internal Dictionary<uint, JadeTable.CQ> Metadata { get; } = new();
    internal Random Control { get; }

    internal NativeCombatGearSupportFixture(bool loaded = true, int seed = 8117)
    {
        Control = new Random(seed);
        CombatGear.GetRandom = new Random(seed);
        GearTable.m_Load = loaded;
        JadeTable.Info = Metadata;
    }

    internal void AssertContinuation() => Assert.Equal(Control.Next(), CombatGear.GetRandom.Next());

    // Independent source-registered2451 fixture: twelve required scalars and optional single Item.
    internal static byte[] Frame(uint type, uint lottery = 0, uint reiki = 0, uint? item = null)
    {
        using var payload = new MemoryStream();
        uint[] values = [type, 0, reiki, lottery, 0, 0, 0, 0, 0, 0, 0, 0];
        for (var i = 0; i < values.Length; i++)
        {
            payload.WriteByte((byte)((i + 1) << 3));
            Varint(payload, values[i]);
        }
        if (item.HasValue)
        {
            using var nested = new MemoryStream();
            nested.WriteByte(8);
            Varint(nested, item.Value);
            payload.WriteByte(106); // field13, wire2
            Varint(payload, (uint)nested.Length);
            nested.WriteTo(payload);
        }
        var bytes = payload.ToArray();
        var frame = new byte[bytes.Length + 12];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)(bytes.Length + 4)));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2451);
        bytes.CopyTo(frame, 4);
        Encoding.ASCII.GetBytes("TQServer").CopyTo(frame, frame.Length - 8);
        return frame;
    }

    private static void Varint(Stream stream, uint value)
    {
        do
        {
            var next = (byte)(value & 127);
            value >>= 7;
            if (value != 0) next |= 128;
            stream.WriteByte(next);
        } while (value != 0);
    }

    public void Dispose()
    {
        JadeTable.Info = priorMetadata;
        GearTable.m_Load = priorLoad;
        CombatGear.GetRandom = priorRandom;
    }
}
