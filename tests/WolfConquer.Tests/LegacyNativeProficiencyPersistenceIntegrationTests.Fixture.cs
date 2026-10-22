using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Cross-owner component integration only: actual progression, packet queue and
// Windows binary persistence, without a native socket Create/pump or login host.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeProficiencyPersistenceIntegrationTests
{
    // Independently pinned native protobuf-net2.0.0.668 object-typed contract;
    // the message-contract tests own its full provenance and separate coverage.
    private static readonly byte[] LevelMessage = Convert.FromHexString(
        "58008308088080FC0710D50F18002000280030003800400148005000" +
        "720653595354454D7203414C4C72007225596F7520696E637265617365642074686520" +
        "776561706F6E2070726F66696369656E637921720072007200");

    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object priorBin = Bin.GetValue(null)!;
        private readonly List<LegacyNativePacket> messagePackets = new();
        internal NativeSpellTestFixture Native { get; } = new();
        internal LegacyNativePacket Packet => Native.Packet;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativeProficiencyStorage Storage { get; }

        internal Scope()
        {
            Client.Player.UID = 1293700;
            Storage = new LegacyNativeProficiencyStorage(Client);
            Client.MyProfs = Storage;
            var ownedBin = new ConcurrentQueue<LegacyNativePacket>();
            for (int index = 0; index < 4; index++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Initialize(packet);
                messagePackets.Add(packet);
                ownedBin.Enqueue(packet);
            }
            Initialize(Packet);
            Bin.SetValue(null, ownedBin);
        }

        private static unsafe void Initialize(LegacyNativePacket packet)
        {
            Marshal.Copy(new byte[LegacyNativePacket.MAX_SIZE], 0,
                (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Seek(0);
        }

        internal LegacyNativeGameClientRuntime NewClient()
        {
            var client = NativeSpellTestFixture.CreateOwner(Queue);
            client.Player.UID = 1293700;
            client.MyProfs = new LegacyNativeProficiencyStorage(client);
            return client;
        }

        public void Dispose()
        {
            Bin.SetValue(null, priorBin);
            foreach (var packet in messagePackets) packet.Dispose();
            Native.Dispose(); // Restores the exact prior native Packet seal.
        }
    }

    private static byte[] Proficiency(uint id, uint level, uint experience, uint needed)
    {
        var bytes = new byte[20];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 20);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2041);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), id);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), level);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), experience);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), needed);
        return bytes;
    }

    private static byte[] Experience(uint experience, uint needed, uint id)
    {
        var bytes = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 24);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2199);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), experience);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), needed);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), id);
        return bytes;
    }

    private static void AssertFrames(Scope scope, params byte[][] expected)
    {
        byte[][] actual = scope.Queue.ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], actual[index]);
    }

    private static void AssertRecord(LegacyNativeMsgProficiency actual,
        uint id, uint level, uint experience, uint needed, byte previous = 0)
    {
        Assert.Equal(id, actual.ID);
        Assert.Equal(level, actual.Level);
        Assert.Equal(experience, actual.Experience);
        Assert.Equal(needed, actual.NeededExperience);
        Assert.Equal(previous, actual.PreviouseLevel);
    }

    private static void AssertSaved(LegacyNativeClientProficiencyPersistenceTests.OwnedProfs owned,
        uint id, uint level, uint experience, uint needed, byte previous = 0)
    {
        byte[] expected = LegacyNativeClientProficiencyPersistenceTests.Image(1,
            LegacyNativeClientProficiencyPersistenceTests.Row(id, level, previous, experience, needed));
        Assert.Equal(expected, File.ReadAllBytes(owned.FilePath));
        // Both genuine public Save/Load success phases must release this exact file.
        using var exclusive = new FileStream(owned.FilePath, FileMode.Open,
            FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }
}
