using System.Buffers;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNobilityBackendTests
{
    private const long AccountId = 1_293_600;
    private const uint Uid = 1_293_600;

    [Fact]
    public async Task DonateWithConquerPointsPromotesToKingPersistsAndSendsLegacyWalletUpdate()
    {
        var dbRoot = CreateTempRoot();
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            var snapshotPath = WriteSnapshot(usersRoot, donation: 0, conquerPoints: 500_000, boundConquerPoints: 456);
            var sent = new List<byte[]>();
            var connection = CreateConnection(sent);
            var sessions = new WorldSessionStore();
            var session = CreateSession(usersRoot, CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath)));
            sessions.Set(connection, session);
            using var log = new StringWriter();

            await DispatchDonationAsync(
                connection,
                log,
                sessions,
                session,
                donation: 250_000_000,
                LegacyNobilityDonationType.ConquerPoints);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(0, updatedSession.Character.ConquerPoints);
            Assert.Equal(456, updatedSession.Character.BoundConquerPoints);
            Assert.Equal("250000000", updatedSession.Character.Attributes["DonationNobility"]);
            Assert.Equal("12", updatedSession.Character.Attributes["NobilityRank"]);
            Assert.Equal("0", updatedSession.Character.Attributes["NobilityPosition"]);

            var icon = AssertNobilityIcon(sent[0]);
            Assert.Equal(250_000_000UL, icon.Donation);
            Assert.Equal((byte)12, icon.Rank);
            Assert.Equal(0, icon.Position);
            AssertSingleUpdate(sent[1], LegacyMsgUpdateDataType.ConquerPoints, 0);

            var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath));
            Assert.Equal(0, persisted.ConquerPoints);
            Assert.Equal(456, persisted.BoundConquerPoints);
            Assert.Equal("250000000", persisted.Attributes["DonationNobility"]);
            Assert.Equal("250000000", persisted.Attributes["LastNobilityDonation"]);
            Assert.Equal("12", persisted.Attributes["NobilityRank"]);
            Assert.Equal("0", persisted.Attributes["NobilityPosition"]);
        }
        finally
        {
            DeleteTempRoot(dbRoot);
        }
    }

    [Fact]
    public async Task DonateWithBoundConquerPointsFallbackPromotesToEarlPersistsAndSendsBoundWalletUpdate()
    {
        var dbRoot = CreateTempRoot();
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            var snapshotPath = WriteSnapshot(usersRoot, donation: 0, conquerPoints: 399_999, boundConquerPoints: 4_000_000);
            var sent = new List<byte[]>();
            var connection = CreateConnection(sent);
            var sessions = new WorldSessionStore();
            var session = CreateSession(usersRoot, CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath)));
            sessions.Set(connection, session);
            using var log = new StringWriter();

            await DispatchDonationAsync(
                connection,
                log,
                sessions,
                session,
                donation: 200_000_000,
                LegacyNobilityDonationType.ConquerPoints);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(399_999, updatedSession.Character.ConquerPoints);
            Assert.Equal(0, updatedSession.Character.BoundConquerPoints);
            Assert.Equal("200000000", updatedSession.Character.Attributes["DonationNobility"]);
            Assert.Equal("5", updatedSession.Character.Attributes["NobilityRank"]);
            Assert.Equal("-1", updatedSession.Character.Attributes["NobilityPosition"]);

            Assert.Equal(2, sent.Count);
            var icon = AssertNobilityIcon(sent[0]);
            Assert.Equal(200_000_000UL, icon.Donation);
            Assert.Equal((byte)5, icon.Rank);
            Assert.Equal(-1, icon.Position);
            AssertSingleUpdate(sent[1], LegacyMsgUpdateDataType.BoundConquerPoints, 0);

            var persisted = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath));
            Assert.Equal(399_999, persisted.ConquerPoints);
            Assert.Equal(0, persisted.BoundConquerPoints);
            Assert.Equal("200000000", persisted.Attributes["DonationNobility"]);
            Assert.Equal("200000000", persisted.Attributes["LastNobilityDonation"]);
            Assert.Equal("5", persisted.Attributes["NobilityRank"]);
            Assert.Equal("-1", persisted.Attributes["NobilityPosition"]);
        }
        finally
        {
            DeleteTempRoot(dbRoot);
        }
    }

    [Fact]
    public async Task RuntimeExpiryCreatesMissingPaidNobilitySectionAndClearsPaidFields()
    {
        var dbRoot = CreateTempRoot();
        try
        {
            var usersRoot = Path.Combine(dbRoot, "Users");
            Directory.CreateDirectory(usersRoot);
            // The loader uses real DateTime.Now; keep its declared paid period safely in the future.
            var expiresAt = DateTime.Now.AddDays(1);
            var clock = new PaidNobilityHeldTimeProvider(new DateTimeOffset(expiresAt.AddSeconds(-1)));
            var snapshotPath = WriteSnapshot(
                usersRoot,
                donation: 0,
                conquerPoints: 500_000,
                boundConquerPoints: 456,
                paidNobilityLines:
                    $"""
                    Identifier={Uid}
                    LastNobilityDonation=250000000
                    PaidRank=12
                    PeriodTime={expiresAt.ToBinary()}
                    IsActive=True
                    Rank=King
                    Position=0
                    """);
            using var log = new PaidNobilityRuntimeLog();
            using var cancel = new CancellationTokenSource();
            var handler = new WorldServerFrameHandler(
                new CharacterSnapshotLoader(usersRoot),
                handoffService: null,
                worldData: null,
                new LegacyRuntimeUpdateLoopOptions(
                    Enabled: true,
                    TimeSpan.FromMilliseconds(20),
                    clock),
                log);
            var connection = CreateConnection([]);

            try
            {
                await handler.HandleFrameAsync(
                    connection,
                    ReadFrame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()),
                    cancel.Token);

                var text = File.ReadAllText(snapshotPath);
                var sectionIndex = text.IndexOf("[PaidNobility]", StringComparison.Ordinal);
                Assert.True(sectionIndex >= 0);
                File.WriteAllText(snapshotPath, text[..sectionIndex].TrimEnd() + Environment.NewLine);
                Assert.DoesNotContain("[PaidNobility]", File.ReadAllText(snapshotPath), StringComparison.Ordinal);
                Assert.DoesNotContain("WorldServer applied LegacyPaidNobility online expiry ", log.ToString(), StringComparison.Ordinal);
                clock.AdvanceOneSecond(); // Only now enter inclusive native now>=PeriodTime.

                await WaitUntilAsync(
                    () =>
                    {
                        try
                        {
                            var persistedText = File.ReadAllText(snapshotPath);
                            if (!persistedText.Contains("[PaidNobility]", StringComparison.Ordinal))
                            {
                                return false;
                            }

                            var persisted = CharacterSnapshotParser.ParseText(persistedText);
                            // Persistence precedes icon/promotion sends and the awaited final diagnostic.
                            // Observe that same entered invocation's completion within the existing deadline.
                            var logged = log.ToString();
                            return persisted.PaidNobility.TryGetValue("IsActive", out var active) &&
                                string.Equals(active, bool.FalseString, StringComparison.OrdinalIgnoreCase) &&
                                logged.Contains("WorldServer applied LegacyPaidNobility online expiry count=1 ", StringComparison.Ordinal) &&
                                logged.Contains("persisted=1 skippedPersistence=0.", StringComparison.Ordinal);
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(4));

                var snapshot = CharacterSnapshotParser.ParseText(File.ReadAllText(snapshotPath));
                Assert.Equal("250000000", snapshot.Attributes["DonationNobility"]);
                Assert.Equal("0", snapshot.PaidNobility["Identifier"]);
                Assert.Equal("0", snapshot.PaidNobility["LastNobilityDonation"]);
                Assert.Equal("0", snapshot.PaidNobility["PaidRank"]);
                Assert.Equal("0", snapshot.PaidNobility["PeriodTime"]);
                Assert.Equal(bool.FalseString, snapshot.PaidNobility["IsActive"]);
                Assert.Contains("persisted=1", log.ToString(), StringComparison.Ordinal);
            }
            finally
            {
                await cancel.CancelAsync();
                await JoinPaidNobilityRuntimeAsync(handler, connection);
            }
        }
        finally
        {
            DeleteTempRoot(dbRoot);
        }
    }

    private static async Task JoinPaidNobilityRuntimeAsync(
        WorldServerFrameHandler handler, ConquerTcpConnection connection)
    {
        var global = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(global);
        await WaitUntilAsync(() => Equals(0, global.GetValue(handler)), TimeSpan.FromSeconds(4));
        var store = typeof(WorldServerFrameHandler).GetField("_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(store);
        Assert.IsType<WorldSessionStore>(store.GetValue(handler)).TryRemove(connection, out _);
    }

    // Runtime keeps its real20ms scheduling; only the expiry owner's observed clock is held.
    private sealed class PaidNobilityHeldTimeProvider : TimeProvider
    {
        private long utcTicks;
        public PaidNobilityHeldTimeProvider(DateTimeOffset initial) => utcTicks = initial.UtcDateTime.Ticks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);
        public void AdvanceOneSecond() => Interlocked.Add(ref utcTicks, TimeSpan.TicksPerSecond);
    }

    // This fixture writes strings through WriteLine/WriteLineAsync; reads share the same gate.
    private sealed class PaidNobilityRuntimeLog : StringWriter
    {
        private readonly object gate = new();
        public override void WriteLine(string? value) { lock (gate) base.WriteLine(value); }
        public override Task WriteLineAsync(string? value)
        {
            lock (gate) base.WriteLine(value);
            return Task.CompletedTask;
        }
        public override string ToString() { lock (gate) return base.ToString(); }
    }

    private static async Task DispatchDonationAsync(
        ConquerTcpConnection connection,
        TextWriter log,
        WorldSessionStore sessions,
        WorldSession session,
        ulong donation,
        LegacyNobilityDonationType donationType)
    {
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        await registry.HandleAsync(
            context,
            ReadFrame(
                new LegacyNobilityRequestPacket(
                        LegacyNobilityAction.Donate,
                        donation,
                        (byte)donationType)
                    .ToFrameBytes()));
    }

    private static WorldSession CreateSession(string usersRoot, CharacterSnapshot snapshot)
    {
        return new WorldSession(
            AccountId,
            "HeroOne",
            snapshot,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static string WriteSnapshot(
        string usersRoot,
        ulong donation,
        long conquerPoints,
        long boundConquerPoints,
        string? paidNobilityLines = null)
    {
        paidNobilityLines ??= """
            Identifier=0
            LastNobilityDonation=0
            PaidRank=0
            PeriodTime=0
            IsActive=False
            Rank=Serf
            Position=-1
            """;
        var path = Path.Combine(usersRoot, AccountId + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{Uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints={{conquerPoints}}
            BankCps=0
            BoundConquerPoints={{boundConquerPoints}}
            Money=0
            PkPoints=0
            DonationNobility={{donation}}
            LastNobilityDonation={{donation}}
            NobilityRank=0
            NobilityPosition=-1
            Online=0

            [PaidNobility]
            {{paidNobilityLines}}
            """);
        return path;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(predicate(), "Timed out waiting for paid nobility persistence.");
    }

    private static LegacyNobilityIconPacket AssertNobilityIcon(byte[] bytes)
    {
        Assert.True(LegacyNobilityIconPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(Uid, packet.Uid);
        return packet;
    }

    private static void AssertSingleUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(Uid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(type, update.Type);
        Assert.Equal(value, update.Value);
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            null,
            null,
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), "wolf-nobility-backend-" + Guid.NewGuid());
    }

    private static void DeleteTempRoot(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
