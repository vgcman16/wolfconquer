using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Associate = WolfConquer.Core.World.LegacyNativeAssociateRuntime;
using AuraFlag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

// Real prerequisite components; deliberately NOT a Team/HP/level lifecycle acceptance suite.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeTeamPrerequisiteTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        private readonly ConcurrentDictionary<uint, Associate.MyAsociats> previousAssociates = Associate.Associates;
        private readonly LegacyNativeAtributesStatus previousAttributes = LegacyNativeDataCoreAttributes.AtributeStatus;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(43);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Associate.Associates = new();
            LegacyNativeDataCoreAttributes.AtributeStatus = new();
            Native = new NativeSpellTestFixture();
            Fill(Packet);
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321; packet.Seek(237);
        }

        public void Dispose()
        {
            Native.Dispose();
            Associate.Associates = previousAssociates;
            LegacyNativeDataCoreAttributes.AtributeStatus = previousAttributes;
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolRandom.ResetRandom = previousReset;
        }
    }

    private static void InvokeAura(LegacyNativePlayerRuntime player, string method, params object?[] arguments)
    {
        try
        {
            typeof(LegacyNativePlayerRuntime).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(player, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static uint[] AuraValues(LegacyNativeMsgStatus status) =>
    [status.Immunity, status.CriticalStrike, status.MetalResistance, status.WoodResistance,
        status.WaterResistance, status.FireResistance, status.EarthResistance];

    private static byte[] AuraWire(uint type, uint uid, uint icon, uint level, uint mode, uint damage)
    {
        var body = new List<byte>();
        uint[] values = [type, uid, icon, level, mode, damage];
        for (int index = 0; index < values.Length; index++)
        {
            body.Add((byte)((index + 1) << 3));
            uint value = values[index];
            while (value >= 128) { body.Add((byte)(value | 128)); value >>= 7; }
            body.Add((byte)value);
        }
        var bytes = new byte[body.Count + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)bytes.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2175);
        body.CopyTo(bytes, 4);
        return bytes;
    }

    private static void Put32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
