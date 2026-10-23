using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeArenaLoadTests
{
    private sealed class Scope : IDisposable
    {
        private readonly LegacyNativeSafeRandom previousRandom = LegacyNativePoolRandom.GetRandom;
        private readonly DateTime previousReset = LegacyNativePoolRandom.ResetRandom;
        private readonly ConcurrentDictionary<uint, LegacyNativeArenaUser> previousPool = LegacyNativeArenaRankings.ArenaPoll;
        private readonly LegacyNativeArenaUser[] previous10 = LegacyNativeArenaRankings.Top10;
        private readonly LegacyNativeArenaUser[] previous1000 = LegacyNativeArenaRankings.Top1000;
        private readonly LegacyNativeArenaUser[] previousToday = LegacyNativeArenaRankings.Top1000Today;
        private readonly Dictionary<LegacyNativeTournamentType, ILegacyNativeTournament> previousTournaments = LegacyNativeMsgSchedules.Tournaments;
        private readonly ILegacyNativeTournament? previousTournament = LegacyNativeMsgSchedules.CurrentTournament;
        private readonly LegacyNativeArenaRuntime? previousArena = LegacyNativeMsgSchedules.Arena;
        internal NativeSpellTestFixture Native { get; }
        internal LegacyNativeGameClientRuntime Client => Native.Owner;
        internal Queue<byte[]> Queue => Native.Queue;

        internal Scope()
        {
            // Bind before real Player birth, then restore after disposing the fixture.
            LegacyNativePoolRandom.GetRandom = new LegacyNativeSafeRandom(43);
            LegacyNativePoolRandom.ResetRandom = new DateTime(2026, 1, 1);
            Native = new NativeSpellTestFixture();
            Client.ArenaStatistic = new LegacyNativeArenaUser();
            LegacyNativeArenaRankings.ArenaPoll = new();
            LegacyNativeArenaRankings.Top10 = new LegacyNativeArenaUser[10];
            LegacyNativeArenaRankings.Top1000 = new LegacyNativeArenaUser[1000];
            LegacyNativeArenaRankings.Top1000Today = new LegacyNativeArenaUser[1000];
            LegacyNativeMsgSchedules.Tournaments = new();
            LegacyNativeMsgSchedules.CurrentTournament = null;
            LegacyNativeMsgSchedules.Arena = null;
        }

        internal LegacyNativeGameClientRuntime Peer(uint uid)
        {
            var peer = NativeSpellTestFixture.CreateOwner(Queue);
            peer.Player.UID = uid;
            peer.ArenaStatistic = new LegacyNativeArenaUser();
            return peer;
        }

        public void Dispose()
        {
            Native.Dispose();
            LegacyNativeArenaRankings.ArenaPoll = previousPool;
            LegacyNativeArenaRankings.Top10 = previous10;
            LegacyNativeArenaRankings.Top1000 = previous1000;
            LegacyNativeArenaRankings.Top1000Today = previousToday;
            LegacyNativeMsgSchedules.Tournaments = previousTournaments;
            LegacyNativeMsgSchedules.CurrentTournament = previousTournament;
            LegacyNativeMsgSchedules.Arena = previousArena;
            LegacyNativePoolRandom.GetRandom = previousRandom;
            LegacyNativePoolRandom.ResetRandom = previousReset;
        }
    }

    private sealed class FileScope : IDisposable
    {
        private const string Prefix = "WolfConquer-NativeArenaLoad-";
        private readonly string previousDb = LegacyNativeServerConfig.DbLocation;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string ArenaFile => Path.Combine(Root, "Arena.ini");

        internal FileScope()
        {
            Directory.CreateDirectory(Root);
            LegacyNativeServerConfig.DbLocation = Root + Path.DirectorySeparatorChar;
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation = previousDb;
            string full = Path.GetFullPath(Root);
            string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            string name = Path.GetFileName(full);
            if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[Prefix.Length..], "N", out _))
            {
                throw new InvalidOperationException("Fixture cleanup escaped its owned GUID directory.");
            }
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }

    private const string NativeRow = "1/Alpha/130/16004/1003/100/200/300/4/5/6/7/8/9/10/11/";

    private static LegacyNativeArenaUser Row(uint uid = 1)
    {
        var row = new LegacyNativeArenaUser { UID = uid, Name = "Alpha", Class = 16004, Level = 130,
            Mesh = 1003, LastSeasonArenaPoints = 8, LastSeasonWin = 9, LastSeasonLose = 10, LastSeasonRank = 11 };
        row.Info.ArenaPoints = 100; row.Info.CurrentHonor = 200; row.Info.HistoryHonor = 300;
        row.Info.TodayWin = 5; row.Info.TodayBattles = 4; row.Info.TotalLose = 6;
        row.Info.TotalWin = 7; row.Info.TodayRank = 3;
        return row;
    }
}
