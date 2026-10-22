using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Actual mutable proficiency/message/packet/socket owners only. No live socket,
// progression provider, message callback, login worker or host adoption is used.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeProficiencyProgressionTests
{
    private static readonly uint[] Thresholds =
    [
        0, 1200, 68000, 250000, 640000, 1600000, 4000000,
        10000000, 22000000, 40000000, 90000000, 95000000,
        142500000, 213750000, 320625000, 480937500, 721406250,
        1082109375, 1623164062, 2100000000, 3200000000,
    ];

    // Root observed this object-typed control with the preserved exact pinned
    // protobuf-net2.0.0.668 DLL on .NET Framework/x86. Includes all seven strings.
    private static readonly byte[] LevelMessage = Convert.FromHexString(
        "58008308088080FC0710D50F18002000280030003800400148005000" +
        "720653595354454D7203414C4C72007225596F7520696E637265617365642074686520" +
        "776561706F6E2070726F66696369656E637921720072007200");

    private sealed class Scope : IDisposable
    {
        internal static readonly FieldInfo Table = typeof(LegacyNativeBaseFunc)
            .GetField("proficiencyLevelExperience", BindingFlags.Static | BindingFlags.NonPublic)!;
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? priorTable = Table.GetValue(null);
        private readonly object priorBin = Bin.GetValue(null)!;
        private readonly CultureInfo priorCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo priorUiCulture = CultureInfo.CurrentUICulture;
        private readonly TextWriter priorConsole = Console.Out;
        private readonly StringWriter console = new();
        private readonly List<LegacyNativePacket> messagePackets = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativePacket Packet => Native.Packet;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeProficiencyStorage Storage { get; }

        internal Scope()
        {
            Storage = new LegacyNativeProficiencyStorage(Client);
            Client.MyProfs = Storage;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Console.SetOut(console);
            var bin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 4; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet);
                messagePackets.Add(packet);
                bin.Enqueue(packet);
            }
            Bin.SetValue(null, bin);
            Reset();
        }

        private static unsafe void Fill(LegacyNativePacket packet) =>
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);

        internal void Reset()
        {
            Fill(Packet);
            Packet.Size = 321;
            Packet.Seek(237);
        }

        internal unsafe void Place(int offset, byte[] bytes) =>
            Marshal.Copy(bytes, 0, (IntPtr)(Packet.Memory + offset), bytes.Length);

        internal unsafe byte[] Memory(int size)
        {
            var bytes = new byte[size];
            Marshal.Copy((IntPtr)Packet.Memory, bytes, 0, size);
            return bytes;
        }

        internal LegacyNativeMsgProficiency Record(uint level = 5, uint experience = 0,
            uint needed = 991, byte previous = 0, uint key = 410, uint id = 410)
        {
            var value = new LegacyNativeMsgProficiency
            {
                ID = id, Level = level, Experience = experience,
                NeededExperience = needed, PreviouseLevel = previous,
            };
            Assert.True(Storage.ClientProf.TryAdd(key, value));
            return value;
        }

        public void Dispose()
        {
            Table.SetValue(null, priorTable);
            Bin.SetValue(null, priorBin);
            foreach (var packet in messagePackets) packet.Dispose();
            Native.Dispose();
            CultureInfo.CurrentCulture = priorCulture;
            CultureInfo.CurrentUICulture = priorUiCulture;
            Console.SetOut(priorConsole);
            console.Dispose();
        }
    }

    // Independent whole-frame controls; expected thresholds are literals at each
    // call site, never a call through the production lookup or native encoder.
    private static byte[] Proficiency(uint id, uint level, uint experience, uint needed)
    {
        var bytes = new byte[20];
        Put16(bytes, 0, 20);
        Put16(bytes, 2, 2041);
        Put32(bytes, 4, id);
        Put32(bytes, 8, level);
        Put32(bytes, 12, experience);
        Put32(bytes, 16, needed);
        return bytes;
    }

    private static byte[] Experience(uint experience, uint needed, uint id)
    {
        var bytes = new byte[24];
        Put16(bytes, 0, 24);
        Put16(bytes, 2, 2199);
        Put32(bytes, 4, experience);
        Put32(bytes, 8, needed);
        Put32(bytes, 12, id);
        return bytes;
    }

    private static void Put16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    private static void Put32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    private static uint Read32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static void AssertRecord(LegacyNativeMsgProficiency actual,
        uint id, uint level, uint experience, uint needed, byte previous = 0)
    {
        Assert.Equal(id, actual.ID);
        Assert.Equal(level, actual.Level);
        Assert.Equal(experience, actual.Experience);
        Assert.Equal(needed, actual.NeededExperience);
        Assert.Equal(previous, actual.PreviouseLevel);
    }

    private static void AssertFrames(Scope scope, params byte[][] expected)
    {
        byte[][] actual = scope.Queue.ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }

    private static void AssertUntouched(Scope scope)
    {
        Assert.Equal(237, scope.Packet.Position);
        Assert.Equal(321, scope.Packet.Size);
        Assert.All(scope.Memory(64), value => Assert.Equal((byte)0xA5, value));
        Assert.Empty(scope.Queue);
    }
}
