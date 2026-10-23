using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeInventoryGrantPrerequisiteTests
{
    // Invoke the unchanged private native handlers without a production bridge.
    private static void CallHandler<T>(LegacyNativeGameClientRuntime client, LegacyNativePacket? stream)
    {
        MethodInfo handler = typeof(T).GetMethod("Handler", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.True(handler.IsPrivate);
        try
        {
            handler.Invoke(null, [client, stream]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo BinField = typeof(LegacyNativePacketRecycle)
            .GetField("bin", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? previousBin = BinField.GetValue(null);
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        private readonly ConcurrentDictionary<uint, LegacyNativeArenaUser> previousArena = LegacyNativeArenaRankings.ArenaPoll;
        private readonly ConcurrentDictionary<uint, LegacyNativeTeamArenaUser> previousTeam = LegacyNativeTeamArenaRankings.ArenaPoll;
        private readonly LegacyNativeArenaUser[] previousA10 = LegacyNativeArenaRankings.Top10;
        private readonly LegacyNativeArenaUser[] previousA1000 = LegacyNativeArenaRankings.Top1000;
        private readonly LegacyNativeArenaUser[] previousAToday = LegacyNativeArenaRankings.Top1000Today;
        private readonly LegacyNativeTeamArenaUser[] previousT10 = LegacyNativeTeamArenaRankings.Top10;
        private readonly LegacyNativeTeamArenaUser[] previousT1000 = LegacyNativeTeamArenaRankings.Top1000;
        private readonly LegacyNativeTeamArenaUser[] previousTToday = LegacyNativeTeamArenaRankings.Top1000Today;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal LegacyNativePlayerRuntime Player => Client.Player;
        internal Queue<byte[]> Queue => Native.Queue;
        internal List<LegacyNativePacket> Borrowed { get; } = new();

        internal unsafe Scope()
        {
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(41);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new NativeSpellTestFixture();
            LegacyNativeArenaRankings.ArenaPoll = new();
            LegacyNativeTeamArenaRankings.ArenaPoll = new();
            LegacyNativeArenaRankings.Top10 = new LegacyNativeArenaUser[10];
            LegacyNativeArenaRankings.Top1000 = new LegacyNativeArenaUser[1000];
            LegacyNativeArenaRankings.Top1000Today = new LegacyNativeArenaUser[1000];
            LegacyNativeTeamArenaRankings.Top10 = new LegacyNativeTeamArenaUser[10];
            LegacyNativeTeamArenaRankings.Top1000 = new LegacyNativeTeamArenaUser[1000];
            LegacyNativeTeamArenaRankings.Top1000Today = new LegacyNativeTeamArenaUser[1000];
            var packets = new ConcurrentQueue<LegacyNativePacket>();
            Fill(Native.Packet);
            for (int i = 0; i < 4; i++)
            {
                var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                Fill(packet);
                Borrowed.Add(packet);
                packets.Enqueue(packet);
            }
            BinField.SetValue(null, packets);
        }

        private static unsafe void Fill(LegacyNativePacket packet)
        {
            Marshal.Copy(Enumerable.Repeat((byte)0xA5, LegacyNativePacket.MAX_SIZE).ToArray(),
                0, (IntPtr)packet.Memory, LegacyNativePacket.MAX_SIZE);
            packet.Size = 321;
            packet.Seek(237);
        }

        internal void PreexistingLevelAndClass(ushort level, uint job)
        {
            typeof(LegacyNativePlayerRuntime).GetField("_level", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player, level);
            typeof(LegacyNativePlayerRuntime).GetField("_class", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Player, job);
        }

        public void Dispose()
        {
            BinField.SetValue(null, previousBin);
            foreach (LegacyNativePacket packet in Borrowed) packet.Dispose();
            Native.Dispose();
            LegacyNativeArenaRankings.ArenaPoll = previousArena;
            LegacyNativeTeamArenaRankings.ArenaPoll = previousTeam;
            LegacyNativeArenaRankings.Top10 = previousA10;
            LegacyNativeArenaRankings.Top1000 = previousA1000;
            LegacyNativeArenaRankings.Top1000Today = previousAToday;
            LegacyNativeTeamArenaRankings.Top10 = previousT10;
            LegacyNativeTeamArenaRankings.Top1000 = previousT1000;
            LegacyNativeTeamArenaRankings.Top1000Today = previousTToday;
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolRandom.ResetRandom = previousReset;
        }
    }

    private sealed class FileScope : IDisposable
    {
        private const string Prefix = "WolfConquer-NativeArena-";
        private readonly string previousDb = LegacyNativeServerConfig.DbLocation;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal FileScope()
        {
            Directory.CreateDirectory(Root);
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previousDb;
            string full = Path.GetFullPath(Root);
            string expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string name = Path.GetFileName(full);
            if (!string.Equals(Path.GetDirectoryName(full), expectedParent, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[Prefix.Length..], "N", out _))
            {
                throw new InvalidOperationException("Fixture cleanup escaped its owned GUID directory.");
            }
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }

    private static byte[] Words(ushort opcode, params uint[] words)
    {
        byte[] bytes = new byte[4 + words.Length * 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode);
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + i * 4), words[i]);
        return bytes;
    }

    private const string NativeRow = "1/Alpha/130/16004/1003/100/200/300/4/5/6/7/8/9/10/11/";

    private static LegacyNativeArenaUser ArenaRow()
    {
        var user = new LegacyNativeArenaUser { UID = 1, Name = "Alpha", Level = 130, Class = 16004,
            Mesh = 1003, LastSeasonArenaPoints = 8, LastSeasonWin = 9, LastSeasonLose = 10, LastSeasonRank = 11 };
        user.Info.ArenaPoints = 100; user.Info.CurrentHonor = 200; user.Info.HistoryHonor = 300;
        user.Info.TodayBattles = 4; user.Info.TodayWin = 5; user.Info.TotalLose = 6; user.Info.TotalWin = 7;
        return user;
    }

    private static LegacyNativeTeamArenaUser TeamRow()
    {
        var user = new LegacyNativeTeamArenaUser { UID = 1, Name = "Alpha", Level = 130, Class = 16004,
            Mesh = 1003, LastSeasonArenaPoints = 8, LastSeasonWin = 9, LastSeasonLose = 10, LastSeasonRank = 11 };
        user.Info.ArenaPoints = 100; user.Info.CurrentHonor = 200; user.Info.HistoryHonor = 300;
        user.Info.TodayBattles = 4; user.Info.TodayWin = 5; user.Info.TotalLose = 6; user.Info.TotalWin = 7;
        return user;
    }
}
