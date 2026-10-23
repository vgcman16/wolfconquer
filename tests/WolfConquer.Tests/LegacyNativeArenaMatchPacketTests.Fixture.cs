using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeArenaMatchPacketTests
{
    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo BinField = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? previousBin = BinField.GetValue(null);
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        private readonly LegacyNativeGroupServerList.Server? previousServer = LegacyNativeGroupServerList.MyServerInfo;
        private readonly LegacyNativeArenaUser[] previous10 = LegacyNativeArenaRankings.Top10;
        private readonly LegacyNativeArenaUser[] previous1000 = LegacyNativeArenaRankings.Top1000;
        private readonly LegacyNativeArenaUser[] previousToday = LegacyNativeArenaRankings.Top1000Today;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;
        internal LegacyNativePacket Packet => Native.Packet;
        internal LegacyNativePacket Borrowed { get; }

        internal unsafe Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(47);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new NativeSpellTestFixture();
            Client.ArenaStatistic = new LegacyNativeArenaUser { Name = "RecordOne", Damage = 0x12345678 };
            Client.Player.Name = "PlayerOne";
            LegacyNativeGroupServerList.MyServerInfo = new LegacyNativeGroupServerList.Server { ID = 0x11223344 };
            LegacyNativeArenaRankings.Top10 = new LegacyNativeArenaUser[10];
            LegacyNativeArenaRankings.Top1000 = new LegacyNativeArenaUser[1000];
            LegacyNativeArenaRankings.Top1000Today = new LegacyNativeArenaUser[1000];
            Borrowed = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
            Poison(Packet); Poison(Borrowed);
            var ownedBin = new ConcurrentQueue<LegacyNativePacket>(); ownedBin.Enqueue(Borrowed);
            BinField.SetValue(null, ownedBin);
        }

        private static unsafe void Poison(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(), 0,
                (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321; packet.Seek(237);
        }

        internal LegacyNativeGameClientRuntime Peer(uint uid, Queue<byte[]> queue)
        {
            var peer = NativeSpellTestFixture.CreateOwner(queue); peer.Player.UID = uid;
            peer.Player.Name = "PlayerTwo";
            peer.ArenaStatistic = new LegacyNativeArenaUser { Name = "RecordTwo", Damage = 0x87654321 };
            return peer;
        }

        internal void LevelAndClass(ushort level, uint job)
        {
            typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Client.Player, level);
            typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Client.Player, job);
        }

        public void Dispose()
        {
            BinField.SetValue(null, previousBin); Borrowed.Dispose(); Native.Dispose();
            LegacyNativeGroupServerList.MyServerInfo = previousServer!;
            LegacyNativeArenaRankings.Top10 = previous10; LegacyNativeArenaRankings.Top1000 = previous1000;
            LegacyNativeArenaRankings.Top1000Today = previousToday;
            LegacyNativePoolRandom.GetRandom = previousRandom; LegacyNativePoolRandom.ResetRandom = previousReset;
        }
    }

    private static void InvokePrivate<T>(LegacyNativeGameClientRuntime client, LegacyNativePacket packet) where T : struct
    {
        MethodInfo handler = typeof(T).GetMethod("Handler", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.True(handler.IsPrivate);
        try { handler.Invoke(null, new object[] { client, packet }); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static void U16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void U32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void Ascii32(byte[] bytes, int offset, string value)
    {
        byte[] text = Encoding.ASCII.GetBytes(value); Array.Copy(text, 0, bytes, offset, Math.Min(32, text.Length));
    }
    private static byte[] ScoreBytes(uint firstUid, string firstName, uint firstDamage,
        uint secondUid, string secondName, uint secondDamage, uint server = 0x11223344)
    {
        byte[] bytes = new byte[96]; U16(bytes, 0, 96); U16(bytes, 2, 2059);
        U32(bytes, 4, firstUid); U32(bytes, 8, server); Ascii32(bytes, 12, firstName); U32(bytes, 44, firstDamage);
        U32(bytes, 48, secondUid); U32(bytes, 52, server); Ascii32(bytes, 56, secondName); U32(bytes, 88, secondDamage);
        U32(bytes, 92, uint.MaxValue); return bytes;
    }
    private static LegacyNativeArenaUser Row(uint uid)
    {
        var row = new LegacyNativeArenaUser { UID = uid, Name = "Rank" + uid, Level = 130, Class = 16004,
            Mesh = 1003, LastSeasonRank = 9, LastSeasonArenaPoints = 10, LastSeasonWin = 11, LastSeasonLose = 12 };
        row.Info.TodayRank = uid; row.Info.ArenaPoints = 100 + uid; row.Info.HistoryHonor = 200 + uid;
        return row;
    }
    private static void RankingQuery(LegacyNativePacket packet, ushort rank, ushort page)
    {
        packet.InitWriter(); packet.Write(rank); packet.Write(page); packet.Seek(4);
    }
    private static byte[] RankingHeader(ushort rank, ushort page, uint count, int rows)
    {
        byte[] bytes = new byte[20 + rows * 56]; U16(bytes, 0, (ushort)bytes.Length); U16(bytes, 2, 2093);
        U16(bytes, 4, rank); U16(bytes, 6, page); U32(bytes, 8, count); U32(bytes, 12, 10); U32(bytes, 16, 10);
        return bytes;
    }
    private static void RankingItem(byte[] bytes, int offset, ushort rank, string name, ushort unknown,
        uint points, uint job, uint level, uint marker, uint mesh)
    {
        U16(bytes, offset, rank); Ascii32(bytes, offset + 2, name); U16(bytes, offset + 34, unknown);
        U32(bytes, offset + 36, points); U32(bytes, offset + 40, job); U32(bytes, offset + 44, level);
        U32(bytes, offset + 48, marker); U32(bytes, offset + 52, mesh);
    }
}
