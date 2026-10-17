using System.Buffers;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class AccountHandoffWorkflowTests : IDisposable
{
    private const string SigningSecret = "test-handoff-signing-secret";
    private readonly string _usersRoot = Path.Combine(Path.GetTempPath(), "wolf-handoff-workflow-" + Guid.NewGuid());
    private readonly string _handoffRoot = Path.Combine(Path.GetTempPath(), "wolf-handoff-workflow-store-" + Guid.NewGuid());

    [Fact]
    public async Task SuccessfulLoginTicketCanEnterWorldOnce()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600);
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var accountServer = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1293600, "2", "2", entityId: 2))),
            handoff);
        var worldServer = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff);

        var accountSent = new List<byte[]>();
        await accountServer.HandleFrameAsync(
            CreateSendingConnection(accountSent),
            ReadFrame(new InternalAccountLoginPacket("2", "2").ToFrameBytes()));
        Assert.True(PacketReader.TryRead(accountSent.Single(), out var loginReader));
        Assert.Equal(1293600, AccountLoginResponsePacket.ReadAccountId(loginReader));
        Assert.True(AccountLoginResponsePacket.TryReadHandoff(loginReader, out var ticket, out _));

        var worldSent = new List<byte[]>();
        await worldServer.HandleFrameAsync(
            CreateSendingConnection(worldSent),
            ReadFrame(new InternalWorldEnterRequestPacket(ticket).ToFrameBytes()));
        await worldServer.HandleFrameAsync(
            CreateSendingConnection(worldSent),
            ReadFrame(new InternalWorldEnterRequestPacket(ticket).ToFrameBytes()));

        Assert.True(worldSent.Count >= 29);
        Assert.True(PacketReader.TryRead(worldSent[0], out var successReader));
        Assert.Equal(WorldSnapshotWireStatus.Success, WorldSnapshotResponsePacket.ReadStatus(successReader));
        Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadAccountId(successReader));
        Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadUid(successReader));
        AssertLegacyWorldEntryStartupFrames(worldSent, startIndex: 1);

        Assert.True(PacketReader.TryRead(worldSent[^1], out var duplicateReader));
        Assert.Equal(WorldSnapshotWireStatus.AlreadyRedeemedTicket, WorldSnapshotResponsePacket.ReadStatus(duplicateReader));
        Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadAccountId(duplicateReader));
    }

    [Fact]
    public async Task FailedLoginCannotEnterWorldBecauseNoTicketIsIssued()
    {
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var accountServer = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1293600, "2", "2", entityId: 2))),
            handoff);
        var accountSent = new List<byte[]>();

        await accountServer.HandleFrameAsync(
            CreateSendingConnection(accountSent),
            ReadFrame(new InternalAccountLoginPacket("2", "wrong").ToFrameBytes()));

        Assert.True(PacketReader.TryRead(accountSent.Single(), out var loginReader));
        Assert.Equal(AccountLoginWireStatus.WrongPassword, AccountLoginResponsePacket.ReadStatus(loginReader));
        Assert.False(AccountLoginResponsePacket.TryReadHandoff(loginReader, out _, out _));
    }

    [Fact]
    public async Task SuccessfulLoginTicketCanEnterWorldThroughSeparateFileBackedHandoffServicesOnce()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600);
        var accountServer = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1293600, "2", "2", entityId: 2))),
            new AccountHandoffService(new FileAccountHandoffTicketStore(_handoffRoot, SigningSecret)));
        var worldServer = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            new AccountHandoffService(new FileAccountHandoffTicketStore(_handoffRoot, SigningSecret)));

        var accountSent = new List<byte[]>();
        await accountServer.HandleFrameAsync(
            CreateSendingConnection(accountSent),
            ReadFrame(new InternalAccountLoginPacket("2", "2").ToFrameBytes()));
        Assert.True(PacketReader.TryRead(accountSent.Single(), out var loginReader));
        Assert.Equal(1293600, AccountLoginResponsePacket.ReadAccountId(loginReader));
        Assert.True(AccountLoginResponsePacket.TryReadHandoff(loginReader, out var ticket, out _));

        var worldSent = new List<byte[]>();
        await worldServer.HandleFrameAsync(
            CreateSendingConnection(worldSent),
            ReadFrame(new InternalWorldEnterRequestPacket(ticket).ToFrameBytes()));
        await new WorldServerFrameHandler(
                new CharacterSnapshotLoader(_usersRoot),
                new AccountHandoffService(new FileAccountHandoffTicketStore(_handoffRoot, SigningSecret)))
            .HandleFrameAsync(
                CreateSendingConnection(worldSent),
                ReadFrame(new InternalWorldEnterRequestPacket(ticket).ToFrameBytes()));

        Assert.True(worldSent.Count >= 29);
        Assert.True(PacketReader.TryRead(worldSent[0], out var successReader));
        Assert.Equal(WorldSnapshotWireStatus.Success, WorldSnapshotResponsePacket.ReadStatus(successReader));
        Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadAccountId(successReader));
        Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadUid(successReader));
        AssertLegacyWorldEntryStartupFrames(worldSent, startIndex: 1);

        Assert.True(PacketReader.TryRead(worldSent[^1], out var duplicateReader));
        Assert.Equal(WorldSnapshotWireStatus.AlreadyRedeemedTicket, WorldSnapshotResponsePacket.ReadStatus(duplicateReader));
        Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadAccountId(duplicateReader));
    }

    [Fact]
    public async Task SuccessfulLoginTicketLogsSelectedSnapshotUidMismatch()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 999);
        var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var accountServer = new AccountServerFrameHandler(
            new AccountLoginService(new FakeAccountRepository(Account(1293600, "2", "2", entityId: 2))),
            handoff);
        var worldServer = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot), handoff, log);

        var accountSent = new List<byte[]>();
        await accountServer.HandleFrameAsync(
            CreateSendingConnection(accountSent),
            ReadFrame(new InternalAccountLoginPacket("2", "2").ToFrameBytes()));
        Assert.True(PacketReader.TryRead(accountSent.Single(), out var loginReader));
        Assert.True(AccountLoginResponsePacket.TryReadHandoff(loginReader, out var ticket, out _));

        var worldSent = new List<byte[]>();
        await worldServer.HandleFrameAsync(
            CreateSendingConnection(worldSent),
            ReadFrame(new InternalWorldEnterRequestPacket(ticket).ToFrameBytes()));

        Assert.Single(worldSent);
        Assert.True(PacketReader.TryRead(worldSent.Single(), out var errorReader));
        Assert.Equal(WorldSnapshotWireStatus.SnapshotError, WorldSnapshotResponsePacket.ReadStatus(errorReader));
        Assert.Equal(1293600, WorldSnapshotResponsePacket.ReadAccountId(errorReader));
        var output = log.ToString();
        Assert.Contains("WorldServer snapshot error", output);
        Assert.Contains("accountId=1293600", output);
        Assert.Contains("file=1293600.ini", output);
        Assert.Contains("reason=uidMismatch", output);
        Assert.Contains("status=UidMismatch", output);
        Assert.Contains("UID 999", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_usersRoot))
        {
            Directory.Delete(_usersRoot, recursive: true);
        }

        if (Directory.Exists(_handoffRoot))
        {
            Directory.Delete(_handoffRoot, recursive: true);
        }
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, null, (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static void AssertLegacyWorldEntryStartupFrames(IReadOnlyList<byte[]> sent, int startIndex)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex]), out var versionFrame, out _, out _));
        Assert.True(LegacyServerVersionPacket.TryParse(versionFrame, out _));

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 1]), out var timerFrame, out _, out _));
        Assert.True(LegacyServerTimerPacket.TryParse(timerFrame, out _));

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 2]), out var configFrame, out _, out _));
        Assert.True(LegacyServerConfigPacket.TryParse(configFrame, out _));

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 3]), out var loaderFrame, out _, out _));
        Assert.True(LegacyLoaderHeroInfoPacket.TryParse(loaderFrame, out _));

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 4]), out var protoHeroFrame, out _, out _));
        Assert.True(LegacyProtoHeroInfoPacket.TryParse(protoHeroFrame, out _));

        AssertLegacyChiRankingFrames(sent, startIndex + 5);

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 9]), out var chiFrame, out _, out _));
        Assert.True(LegacyChiInfoPacket.TryParse(chiFrame, out var chi));
        Assert.Equal(LegacyChiInfoAction.Upgrade, chi.Action);
        Assert.Equal(1_293_600U, chi.Uid);
        Assert.Equal(153_850, chi.ChiPoints);
        Assert.Equal(4, chi.Powers.Count);

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 10]), out var achievementFrame, out _, out _));
        Assert.True(LegacyAchievementPacket.TryParse(achievementFrame, out var achievement));
        Assert.Equal(1_293_600U, achievement.Uid);

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 11]), out var nobilityFrame, out _, out _));
        Assert.True(LegacyNobilityIconPacket.TryParse(nobilityFrame, out _));

        LegacySelectedEquipmentQueryAssertions.NeutralQuery(sent, startIndex + 12, 1_293_600U);

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 22]), out var autoHuntFrame, out _, out _));
        Assert.True(LegacyAutoHuntPacket.TryParse(autoHuntFrame, out var autoHunt));
        Assert.Equal(LegacyAutoHuntPacket.Startup, autoHunt);

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 23]), out var tasksFrame, out _, out _));
        Assert.True(LegacyActivityTasksPacket.TryParse(tasksFrame, out var tasks));
        Assert.Equal(
        [
            new LegacyActivityTaskState(1, Progress: 10, Completed: 1),
            new LegacyActivityTaskState(22, Progress: 0, Completed: 0),
        ], tasks.Tasks);

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 24]), out var rewardsFrame, out _, out _));
        Assert.True(LegacyActivityRewardsInfoPacket.TryParse(rewardsFrame, out var rewards));
        Assert.Equal(80U, rewards.ActivityPoints);

        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + 25]), out var claimsFrame, out _, out _));
        Assert.True(LegacyActivityClaimsPacket.TryParse(claimsFrame, out var claims));
        Assert.Equal(0, claims.ClaimRewards);

        LegacyRuneStoragePacket? runeStorage = null;
        for (var index = startIndex + 26; index < sent.Count - 1; index++)
        {
            if (LegacyRuneStoragePacket.TryParse(ReadFrame(sent[index]), out var candidate))
            {
                runeStorage = candidate;
                break;
            }
        }

        Assert.Equal(LegacyRuneStoragePacket.Startup(), runeStorage);
    }

    private static void AssertLegacyChiRankingFrames(IReadOnlyList<byte[]> sent, int startIndex)
    {
        var expectedRankTypes = new[]
        {
            LegacyGenericRankingRankType.DragonChi,
            LegacyGenericRankingRankType.PhoenixChi,
            LegacyGenericRankingRankType.TigerChi,
            LegacyGenericRankingRankType.TurtleChi,
        };

        for (var index = 0; index < expectedRankTypes.Length; index++)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(sent[startIndex + index]), out var rankingFrame, out _, out _));
            Assert.True(LegacyGenericRankingPacket.TryParse(rankingFrame, out var ranking));
            Assert.Equal(LegacyGenericRankingAction.QueryCount, ranking.Action);
            Assert.Equal(expectedRankTypes[index], ranking.RankType);
            var entry = Assert.Single(ranking.Entries);
            Assert.Equal(400, entry.Amount);
            Assert.Equal(1_293_600U, entry.Uid);
            Assert.Equal("HeroOne", entry.Name);
        }
    }

    private static AccountRecord Account(long id, string username, string password, long entityId = 0)
    {
        return new AccountRecord(id, username, password, entityId, BannedId: AccountLoginService.AllowedBannedId, IsOnline: false);
    }

    private void WriteSnapshot(long accountId, long? uid = null)
    {
        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid ?? accountId}}
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
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            PkPoints=0
            Online=7
            Achivement=0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/
            Activeness=0/80/2/1/10/1/22/0/0/
            Dragon=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Pheonix=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Tiger=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Turtle=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            ChiPoints=153850
            """);
    }

    private sealed class FakeAccountRepository(params AccountRecord[] accounts) : IAccountRepository
    {
        public ValueTask<IReadOnlyList<AccountRecord>> FindByUsernameAsync(
            string username,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AccountRecord> matches = accounts
                .Where(account => string.Equals(account.Username, username, StringComparison.Ordinal))
                .ToArray();
            return ValueTask.FromResult(matches);
        }
    }
}
