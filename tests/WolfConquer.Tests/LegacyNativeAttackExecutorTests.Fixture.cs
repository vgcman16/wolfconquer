using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeAttackExecutorTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? oldBin = Bin.GetValue(null);
        private readonly LegacyNativeSafeRandom oldPool = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime oldReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeSafeRandom oldAttack = LegacyNativeAttackBase.MyRandom;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameClientRuntime>? oldClients = LegacyNativePoolClients.GamePoll;
        private readonly List<LegacyNativePacket> packets = new();
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativePacket Packet => Native.Packet;

        internal Scope()
        {
            LegacyNativePoolRandom.GetRandom = new(37); LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            LegacyNativeAttackBase.MyRandom = new(41); LegacyNativePoolClients.GamePoll = new();
            Native = new(); Client.Equipment = new(Client); Player.X = 32; Player.Y = 42; Player.Stamina = 100;
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 12; index++) { var p = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); packets.Add(p); bin.Enqueue(p); }
            Bin.SetValue(null, bin); Packet.InitWriter();
        }

        internal LegacyNativeGameClientRuntime Peer(Queue<byte[]> queue, uint uid = 21)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue); peer.Player.UID = uid; peer.Player.X = 12; peer.Player.Y = 22; return peer;
        }
        public void Dispose()
        {
            Bin.SetValue(null, oldBin); foreach (var p in packets) p.Dispose(); Native.Dispose();
            LegacyNativePoolRandom.GetRandom = oldPool; LegacyNativePoolRandom.ResetRandom = oldReset; LegacyNativeAttackBase.MyRandom = oldAttack;
            LegacyNativePoolClients.GamePoll = oldClients;
        }
    }

    // Invoke unchanged native private callees directly. No production bridge,
    // partial proc dispatcher, callback or replacement method is introduced.
    private static void Proc(string method, object? argument)
    {
        try { typeof(LegacyNativePerfectionEffects).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [argument]); }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }

    private static int SeedFor(int percent, bool success)
    {
        // Select an actual seeded native RNG state, never a forced RNG result/provider.
        for (int seed = 1; seed <= 1000; seed++) if ((new LegacyNativeSafeRandom(seed).Next() % 100 < percent) == success) return seed;
        throw new InvalidOperationException("No bounded owned RNG prestate found.");
    }
    private static ushort Opcode(byte[] frame) => BitConverter.ToUInt16(frame, 2);
    private static byte[] Refine(uint uid, uint parameter, byte effect)
    {
        var bytes = new List<byte>(); Field(bytes, 1, uid); Field(bytes, 2, parameter); Field(bytes, 3, effect); return Frame(2319, bytes);
    }
    private static byte[] Scalar(uint uid, LegacyNativeMsgUpdate.DataType kind, ulong value)
    {
        var entry = new List<byte>(); if (kind != 0) Field(entry, 1, (uint)kind); if (value != 0) Field(entry, 2, value);
        var bytes = new List<byte>(); Field(bytes, 1, uid); Varint(bytes, 18); Varint(bytes, (ulong)entry.Count); bytes.AddRange(entry); return Frame(2176, bytes);
    }
    private static byte[] Guard(uint uid)
    {
        var entry = new List<byte>(); Field(entry, 1, (uint)LegacyNativeMsgUpdate.DataType.AzureShield); Field(entry, 4, (uint)Flag.DivineGuard); Field(entry, 5, 15);
        var bytes = new List<byte>(); Field(bytes, 1, uid); Varint(bytes, 18); Varint(bytes, (ulong)entry.Count); bytes.AddRange(entry); return Frame(2176, bytes);
    }
    private static byte[] Blue(uint source, uint target, ushort x, ushort y, uint damage)
    {
        var bytes = new List<byte>(); Field(bytes, 4, source); Field(bytes, 5, target); Field(bytes, 7, x); Field(bytes, 8, y);
        Field(bytes, 13, 55); Field(bytes, 15, unchecked((ulong)(long)(int)damage)); Field(bytes, 17, 0);
        Field(bytes, 21, 0); Field(bytes, 25, 0); Field(bytes, 26, 0); return Frame(2135, bytes);
    }
    private static byte[] Frame(ushort opcode, List<byte> payload)
    {
        var result = new List<byte>(); result.AddRange(BitConverter.GetBytes((ushort)(payload.Count + 4))); result.AddRange(BitConverter.GetBytes(opcode)); result.AddRange(payload); return result.ToArray();
    }
    private static void Field(List<byte> bytes, uint tag, ulong value) { Varint(bytes, tag << 3); Varint(bytes, value); }
    private static void Varint(List<byte> bytes, ulong value)
    {
        while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
        bytes.Add((byte)value);
    }
}
