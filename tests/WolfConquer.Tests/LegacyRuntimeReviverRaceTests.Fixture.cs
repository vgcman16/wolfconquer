using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuntimeReviverRaceTests
{
    private sealed partial class Fixture
    {
        internal static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);
        private const string RootPrefix = "wolf-runtime-reviver-race-";
        private const long Uid = 1_293_600;
        private static readonly DateTimeOffset DeadAt = new(2026, 6, 13, 15, 10, 0, TimeSpan.Zero);
        private readonly CancellationTokenSource lifetime = new();
        private readonly object framesGate = new();
        private readonly List<byte[]> frames = [];
        private readonly ConcurrentQueue<Task> frameCalls = new();
        private readonly HashSet<Task> runtimeWorkers = new(ReferenceEqualityComparer.Instance);
        private bool capturedInitialWorkers;
        private readonly RaceLog log;
        private WorldServerFrameHandler? handler;
        private ConquerTcpConnection? connection;
        private bool ownsRoot;
        private int holding;
        internal bool ActualRearmsObserved { get; set; }
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), RootPrefix + Guid.NewGuid().ToString("N"));
        private string Users => Path.Combine(Root, "Users");
        private string CharacterPath => Path.Combine(Users, "1293600.ini");
        internal OrderedTimeProvider Clock { get; } = new(DeadAt.AddSeconds(2));
        internal TaskCompletionSource PlayerFiveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleasePlayerFive { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReviverCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Fixture() { log = new RaceLog(ReviverCompleted); }

        internal async Task StartAsync(ConquerProtocolProfile profile)
        {
            ValidateRoot();
            if (Directory.Exists(Root) || File.Exists(Root)) throw new IOException("Owned root already exists.");
            Directory.CreateDirectory(Users); ownsRoot = true;
            var vector = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
            vector[0] = (1UL << 5) | (1UL << 10);
            File.WriteAllText(CharacterPath, CharacterText +
                $"StatusFlags={string.Join('/', vector)}/\r\nStamina=0\r\nDeadStamp={DeadAt:O}\r\nGhostStamp={DeadAt.AddSeconds(1):O}\r\nDead_X=197\r\nDead_Y=230\r\n",
                new UTF8Encoding(false, true));
            var family = new Monster(10000, "Reviver", new Dictionary<string, string>
            {
                ["life"] = "70000", ["lookface"] = "10000", ["level"] = "0200", ["attack_range"] = "0015",
                ["view_range"] = "0015", ["attack_speed"] = "1", ["move_speed"] = "1",
            }, []);
            var spawn = new MonsterSpawn(110, 1015, 10000, 199, 230, 1, 1, 1, 10, 1, 0, 0, 0, 0,
                1015, "Reviver", Path.Combine("1015", "Reviver", "1.ini"), new Dictionary<string, string>
                { ["id"] = "0110", ["mapid"] = "1015", ["npctype"] = "10000" });
            var catalog = MsgTeamRuntimeTests.CreateTeamArenaRewardCatalog(monsters: [family], monsterSpawns: [spawn]);
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(Uid, "runtime-fixture", ConquerRuntimeDefaults.WorldServer);
            handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(Users), handoff, catalog,
                new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromMilliseconds(20), Clock), log,
                protocolProfile: profile);
            connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), SendAsync);
            await CallAsync(ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
            await CallAsync(ReadFrame(new MsgActionPacket { ObjId = (uint)Uid, Timestamp = 1234,
                Type = MsgActionType.SetLocation }.ToFrameBytes()));
            await Clock.WaitForCountAsync(2).WaitAsync(Bound);
            // Genuine worker registration must precede the completed Frame entry.
            // Retain the exact two pending tasks before either manual timer fires;
            // a later empty active dictionary cannot recover a retired task.
            var startedWorkers = handler.CaptureLegacyRuntimeWorkerTasks();
            runtimeWorkers.UnionWith(startedWorkers);
            Assert.Equal(2, startedWorkers.Length);
            Assert.NotSame(startedWorkers[0], startedWorkers[1]);
            Assert.All(startedWorkers, worker => Assert.False(worker.IsCompleted));
            Assert.Equal(0L, handler.LegacyRuntimeFaultedWorkerCount);
            capturedInitialWorkers = true;
            Assert.Equal(0, Current.Character.MinHitPoints);
            Assert.Equal("0", Current.Character.Attributes["Stamina"]);
            lock (framesGate) frames.Clear();
        }

        private async ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
        {
            var bytes = frame.ToArray();
            if (Stamina(bytes) == 5 && Interlocked.CompareExchange(ref holding, 1, 0) == 0)
            {
                PlayerFiveEntered.TrySetResult();
                await ReleasePlayerFive.Task;
            }
            cancellationToken.ThrowIfCancellationRequested();
            lock (framesGate) frames.Add(bytes);
        }

        private async Task CallAsync(PacketFrame frame)
        {
            var actual = handler!.HandleFrameAsync(connection!, frame, lifetime.Token).AsTask();
            frameCalls.Enqueue(actual);
            await actual.WaitAsync(Bound);
        }

        private WorldSessionStore Store => ReadField<WorldSessionStore>("_sessions");
        private WorldSession Current
        { get { Assert.True(Store.TryGet(connection!, out var current)); return Assert.IsType<WorldSession>(current); } }
        private T ReadField<T>(string name) => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
        private static PacketFrame ReadFrame(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            return frame;
        }
        private static ulong? Stamina(byte[] bytes) => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Uid == Uid && update.Updates.Count == 1 && update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina
                ? update.Updates[0].Value : null;

        internal void AssertFinalRevival()
        {
            var current = Current;
            Assert.True(current.Character.MinHitPoints > 0);
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, 5));
            Assert.False(LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, 10));
            Assert.Equal("100", current.Character.Attributes["Stamina"]);
            byte[][] snapshot; lock (framesGate) snapshot = frames.ToArray();
            var hundred = Array.FindIndex(snapshot, frame => Stamina(frame) == 100);
            Assert.True(hundred >= 0, "Actual revive stamina100 was not delivered.");
            Assert.DoesNotContain(snapshot.Skip(hundred + 1), frame => Stamina(frame) == 5);
            Assert.Contains(snapshot, frame => LegacySpellAnimationPacket.TryParse(ReadFrame(frame), out var spell) &&
                spell.SpellId == 1100 && spell.Targets.Count == 1 && spell.Targets[0].Uid == Uid);
            // The real scalar save participates for positive HP. Stamina itself
            // is NOT a current persisted scalar key; do not invent that proof.
            var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(current.Character.MinHitPoints, saved.MinHitPoints);
            Assert.Equal(current.Character.Money, saved.Money);
            Assert.Equal(current.Character.ConquerPoints, saved.ConquerPoints);
            Assert.DoesNotContain("PARITY-BUG", log.ToString());
        }

        private const string CharacterText = "[Character]\r\nUID=1293600\r\nName=RuntimeRace\r\nBody=1003\r\nFace=0\r\nHaire=0\r\n" +
            "Class=7051\r\nFirstClass=7051\r\nSecoundeClass=0\r\nReborn=0\r\nLevel=140\r\nExperience=0\r\nMap=1015\r\nX=197\r\nY=230\r\n" +
            "PMap=1015\r\nPMapX=197\r\nPMapY=230\r\nStrength=10\r\nAgility=20\r\nVitaliti=30\r\nSpirit=40\r\nAtributes=5\r\n" +
            "MinHitPoints=0\r\nMinMana=0\r\nMoney=1234\r\nConquerPoints=55\r\nBoundConquerPoints=37\r\nBankCps=50\r\nPkPoints=0\r\nOnline=0\r\n";
    }

    private sealed class RaceLog(TaskCompletionSource revived) : StringWriter
    {
        private readonly object gate = new();
        public override void WriteLine(string? value) { lock (gate) base.WriteLine(value); Signal(value); }
        public override Task WriteLineAsync(string? value) { WriteLine(value); return Task.CompletedTask; }
        public override void Write(string? value) { lock (gate) base.Write(value); }
        public override string ToString() { lock (gate) return base.ToString(); }
        private void Signal(string? value)
        { if (value?.Contains("WorldServer sent legacy Reviver revive", StringComparison.Ordinal) == true) revived.TrySetResult(); }
    }
}
