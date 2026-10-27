using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClanHandlerTests
{
    [Fact]
    public async Task HandlerAppliesLegacyClanQuitClearsMembershipAndSendsCallback()
    {
        using var fixture = CreateFixture(money: 1_000, clanDonation: 500);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(fixture.UsersRoot, money: 1_000, clanDonation: 500);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(LegacyClanPacket.QuitMode, 329, 12, ["leave"]).ToFrameBytes()));

        var response = Assert.Single(sent);
        Assert.True(LegacyClanPacket.TryParse(ReadFrame(response), out var callback));
        Assert.Equal(LegacyClanPacket.QuitMode, callback.Mode);
        Assert.Equal(329U, callback.ClanId);
        Assert.Equal(12U, callback.Parameter);
        Assert.Equal(["leave"], callback.Values);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal("0", updatedSession?.Character.Attributes["ClanID"]);
        Assert.Equal("0", updatedSession?.Character.Attributes["ClanUID"]);
        Assert.Equal("0", updatedSession?.Character.Attributes["ClanRank"]);
        Assert.Equal("0", updatedSession?.Character.Attributes["ClanDonation"]);

        var persisted = File.ReadAllText(fixture.UserPath);
        Assert.Contains("ClanID=0", persisted);
        Assert.Contains("ClanUID=0", persisted);
        Assert.Contains("ClanRank=0", persisted);
        Assert.Contains("ClanDonation=0", persisted);

        var output = log.ToString();
        Assert.Contains("WorldServer persisted LegacyClan", output);
        Assert.Contains("action=Quit", output);
        Assert.Contains("WorldServer applied LegacyClan Quit", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClan", output);
    }

    [Fact]
    public async Task HandlerAppliesLegacyClanKickMemberClearsTargetAndRefreshesMembers()
    {
        using var fixture = CreateFixture(money: 1_000, clanDonation: 500);
        var targetPath = AddMemberFile(
            fixture.UsersRoot,
            accountId: 1_293_601,
            uid: 1_293_601,
            name: "Target",
            money: 250,
            clanRank: 10,
            clanDonation: 125);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var leaderConnection = CreateConnection(sent);
        var targetConnection = CreateConnection([]);
        var leaderSession = CreateSession(fixture.UsersRoot, money: 1_000, clanDonation: 500);
        var targetSession = CreateSession(
            fixture.UsersRoot,
            money: 250,
            clanDonation: 125,
            uid: 1_293_601,
            name: "Target",
            clanRank: 10);
        var sessions = new WorldSessionStore();
        sessions.Set(leaderConnection, leaderSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(leaderConnection, log, leaderSession, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(LegacyClanPacket.KickMemberMode, 0, 0, ["Target"]).ToFrameBytes()));

        var response = Assert.Single(sent);
        Assert.True(LegacyClanMemberListPacket.TryParse(ReadFrame(response), out var members));
        Assert.DoesNotContain(members.Members, member => string.Equals(member.Name, "Target", StringComparison.Ordinal));
        Assert.Contains(members.Members, member => string.Equals(member.Name, "HeroOne", StringComparison.Ordinal));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.Equal("0", updatedTargetSession?.Character.Attributes["ClanID"]);
        Assert.Equal("0", updatedTargetSession?.Character.Attributes["ClanUID"]);
        Assert.Equal("0", updatedTargetSession?.Character.Attributes["ClanRank"]);
        Assert.Equal("0", updatedTargetSession?.Character.Attributes["ClanDonation"]);

        var persisted = File.ReadAllText(targetPath);
        Assert.Contains("ClanID=0", persisted);
        Assert.Contains("ClanUID=0", persisted);
        Assert.Contains("ClanRank=0", persisted);
        Assert.Contains("ClanDonation=0", persisted);

        var output = log.ToString();
        Assert.Contains("WorldServer applied LegacyClan KickMember", output);
        Assert.Contains("target=Target", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClan", output);
    }

    [Fact]
    public async Task HandlerAppliesLegacyClanTransferLeaderUpdatesRanksClanFileAndRefreshesBothClients()
    {
        using var fixture = CreateFixture(money: 1_000, clanDonation: 500);
        var targetPath = AddMemberFile(
            fixture.UsersRoot,
            accountId: 1_293_601,
            uid: 1_293_601,
            name: "Target",
            money: 250,
            clanRank: 10,
            clanDonation: 125);
        using var log = new StringWriter();
        var leaderSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var leaderConnection = CreateConnection(leaderSent);
        var targetConnection = CreateConnection(targetSent);
        var leaderSession = CreateSession(fixture.UsersRoot, money: 1_000, clanDonation: 500);
        var targetSession = CreateSession(
            fixture.UsersRoot,
            money: 250,
            clanDonation: 125,
            uid: 1_293_601,
            name: "Target",
            clanRank: 10);
        var sessions = new WorldSessionStore();
        sessions.Set(leaderConnection, leaderSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(leaderConnection, log, leaderSession, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(LegacyClanPacket.TransferLeaderMode, 0, 0, ["Target"]).ToFrameBytes()));

        var targetFrame = Assert.Single(targetSent);
        Assert.True(LegacyClanPacket.TryParse(ReadFrame(targetFrame), out var targetClan));
        Assert.Equal(LegacyClanPacket.InfoMode, targetClan.Mode);
        Assert.Equal("Target", targetClan.Values[2]);
        Assert.Equal("100", targetClan.Values[0].Split(' ')[5]);

        var leaderFrame = Assert.Single(leaderSent);
        Assert.True(LegacyClanPacket.TryParse(ReadFrame(leaderFrame), out var leaderClan));
        Assert.Equal(LegacyClanPacket.InfoMode, leaderClan.Mode);
        Assert.Equal("Target", leaderClan.Values[2]);
        Assert.Equal("10", leaderClan.Values[0].Split(' ')[5]);

        Assert.True(sessions.TryGet(leaderConnection, out var updatedLeaderSession));
        Assert.Equal("10", updatedLeaderSession?.Character.Attributes["ClanRank"]);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.Equal("100", updatedTargetSession?.Character.Attributes["ClanRank"]);

        Assert.Contains("ClanRank=10", File.ReadAllText(fixture.UserPath));
        Assert.Contains("ClanRank=100", File.ReadAllText(targetPath));
        Assert.Contains("329/TestClan/Target/", File.ReadAllText(fixture.ClanPath));

        var output = log.ToString();
        Assert.Contains("WorldServer applied LegacyClan TransferLeader", output);
        Assert.Contains("target=Target", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClan", output);
    }

    [Fact]
    public async Task HandlerLegacyClanJoinSendsSingleClientRequestToOnlineLeader()
    {
        using var log = new StringWriter();
        var requesterSent = new List<byte[]>();
        var leaderSent = new List<byte[]>();
        var requesterConnection = CreateConnection(requesterSent);
        var leaderConnection = CreateConnection(leaderSent);
        var requesterSession = CreateSession(
            usersRoot: null,
            money: 0,
            clanDonation: 0,
            uid: 1_293_600,
            name: "Requester",
            clanId: 0,
            clanRank: 0);
        var leaderSession = CreateSession(
            usersRoot: null,
            money: 1_000,
            clanDonation: 500,
            uid: 1_293_601,
            name: "Leader",
            clanId: 329,
            clanRank: 100);
        var sessions = new WorldSessionStore();
        sessions.Set(requesterConnection, requesterSession);
        sessions.Set(leaderConnection, leaderSession);
        var context = new WorldPacketHandlerContext(requesterConnection, log, requesterSession, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(LegacyClanPacket.JoinMode, 1_293_601, 0, []).ToFrameBytes()));

        Assert.Empty(requesterSent);
        var response = Assert.Single(leaderSent);
        Assert.True(LegacyClanSingleClientPacket.TryParse(ReadFrame(response), out var request));
        Assert.Equal(LegacyClanPacket.JoinMode, request.Mode);
        Assert.Equal(1_293_600U, request.Uid);
        Assert.Equal(0U, request.Parameter);
        Assert.Equal("Requester", request.Name);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyClan Join", output);
        Assert.Contains("leaderUid=1293601", output);
        Assert.Contains("requesterName=Requester", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClan", output);
    }

    [Fact]
    public async Task HandlerLegacyClanJoinSkipsWhenRequesterAlreadyHasClan()
    {
        using var log = new StringWriter();
        var requesterSent = new List<byte[]>();
        var leaderSent = new List<byte[]>();
        var requesterConnection = CreateConnection(requesterSent);
        var leaderConnection = CreateConnection(leaderSent);
        var requesterSession = CreateSession(null, money: 0, clanDonation: 0);
        var leaderSession = CreateSession(
            usersRoot: null,
            money: 1_000,
            clanDonation: 500,
            uid: 1_293_601,
            name: "Leader",
            clanId: 329,
            clanRank: 100);
        var sessions = new WorldSessionStore();
        sessions.Set(requesterConnection, requesterSession);
        sessions.Set(leaderConnection, leaderSession);
        var context = new WorldPacketHandlerContext(requesterConnection, log, requesterSession, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(LegacyClanPacket.JoinMode, 1_293_601, 0, []).ToFrameBytes()));

        Assert.Empty(requesterSent);
        Assert.Empty(leaderSent);
        Assert.Contains("WorldServer skipped LegacyClan Join", log.ToString());
        Assert.Contains("reason=already-in-clan", log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyClanAcceptJoinRequestAddsRequesterAndSendsClanInfo()
    {
        using var fixture = CreateFixture(money: 1_000, clanDonation: 500);
        var requesterPath = AddMemberFile(
            fixture.UsersRoot,
            accountId: 1_293_601,
            uid: 1_293_601,
            name: "Requester",
            money: 250,
            clanRank: 0,
            clanDonation: 77,
            clanId: 0);
        using var log = new StringWriter();
        var leaderSent = new List<byte[]>();
        var requesterSent = new List<byte[]>();
        var leaderConnection = CreateConnection(leaderSent);
        var requesterConnection = CreateConnection(requesterSent);
        var leaderSession = CreateSession(fixture.UsersRoot, money: 1_000, clanDonation: 500);
        var requesterSession = CreateSession(
            fixture.UsersRoot,
            money: 250,
            clanDonation: 77,
            uid: 1_293_601,
            name: "Requester",
            clanId: 0,
            clanRank: 0);
        var sessions = new WorldSessionStore();
        sessions.Set(leaderConnection, leaderSession);
        sessions.Set(requesterConnection, requesterSession);
        var context = new WorldPacketHandlerContext(leaderConnection, log, leaderSession, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(
                LegacyClanPacket.AcceptJoinRequestMode,
                1_293_601,
                0,
                ["Requester"]).ToFrameBytes()));

        Assert.Empty(leaderSent);
        var response = Assert.Single(requesterSent);
        Assert.True(LegacyClanPacket.TryParse(ReadFrame(response), out var startup));
        Assert.Equal(LegacyClanPacket.InfoMode, startup.Mode);
        Assert.Equal(329U, startup.ClanId);
        Assert.Equal("TestClan", startup.Values[1]);
        Assert.Equal("HeroOne", startup.Values[2]);
        var stats = startup.Values[0].Split(' ');
        Assert.Equal("329", stats[0]);
        Assert.Equal("2", stats[1]);
        Assert.Equal("10", stats[5]);
        Assert.Equal("0", stats[11]);

        Assert.True(sessions.TryGet(requesterConnection, out var updatedRequesterSession));
        Assert.Equal("329", updatedRequesterSession?.Character.Attributes["ClanID"]);
        Assert.Equal("329", updatedRequesterSession?.Character.Attributes["ClanUID"]);
        Assert.Equal("10", updatedRequesterSession?.Character.Attributes["ClanRank"]);
        Assert.Equal("0", updatedRequesterSession?.Character.Attributes["ClanDonation"]);

        var persisted = File.ReadAllText(requesterPath);
        Assert.Contains("ClanID=329", persisted);
        Assert.Contains("ClanUID=329", persisted);
        Assert.Contains("ClanRank=10", persisted);
        Assert.Contains("ClanDonation=0", persisted);

        var output = log.ToString();
        Assert.Contains("WorldServer persisted LegacyClan", output);
        Assert.Contains("action=AcceptJoinRequest", output);
        Assert.Contains("WorldServer applied LegacyClan AcceptJoinRequest", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClan", output);
    }

    [Fact]
    public async Task HandlerLegacyClanAcceptJoinRequestWithoutValuesSendsSourceCallback()
    {
        using var fixture = CreateFixture(money: 1_000, clanDonation: 500);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(fixture.UsersRoot, money: 1_000, clanDonation: 500);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(LegacyClanPacket.AcceptJoinRequestMode, 1_293_601, 42, []).ToFrameBytes()));

        var response = Assert.Single(sent);
        Assert.True(LegacyClanPacket.TryParse(ReadFrame(response), out var callback));
        Assert.Equal(LegacyClanPacket.AcceptJoinRequestMode, callback.Mode);
        Assert.Equal(1_293_601U, callback.ClanId);
        Assert.Equal(42U, callback.Parameter);
        Assert.Empty(callback.Values);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyClan AcceptJoinRequest", output);
        Assert.Contains("callback=source", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClan", output);
    }

    [Fact]
    public async Task HandlerAppliesLegacyClanDedicateUpdatesMoneyDonationAndSendsCallback()
    {
        using var fixture = CreateFixture(money: 1_000, clanDonation: 500);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(fixture.UsersRoot, money: 1_000, clanDonation: 500);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(LegacyClanPacket.DedicateMode, 300, 7, ["donate"]).ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[0]), out var moneyUpdate));
        var update = Assert.Single(moneyUpdate.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Money, update.Type);
        Assert.Equal(700U, update.Value);

        Assert.True(LegacyClanPacket.TryParse(ReadFrame(sent[1]), out var callback));
        Assert.Equal(LegacyClanPacket.DedicateMode, callback.Mode);
        Assert.Equal(300U, callback.ClanId);
        Assert.Equal(7U, callback.Parameter);
        Assert.Equal(["donate"], callback.Values);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(700, updatedSession?.Character.Money);
        Assert.Equal("800", updatedSession?.Character.Attributes["ClanDonation"]);

        var persisted = File.ReadAllText(fixture.UserPath);
        Assert.Contains("Money=700", persisted);
        Assert.Contains("ClanDonation=800", persisted);

        var output = log.ToString();
        Assert.Contains("WorldServer persisted LegacyClan", output);
        Assert.Contains("action=Dedicate", output);
        Assert.Contains("WorldServer applied LegacyClan Dedicate", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClan", output);
    }

    [Fact]
    public async Task HandlerSkipsLegacyClanDedicateWithoutEnoughMoney()
    {
        using var fixture = CreateFixture(money: 100, clanDonation: 500);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(fixture.UsersRoot, money: 100, clanDonation: 500);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, session, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyClanPacket(LegacyClanPacket.DedicateMode, 300, 0, []).ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(100, updatedSession?.Character.Money);
        Assert.Equal("500", updatedSession?.Character.Attributes["ClanDonation"]);

        var output = log.ToString();
        Assert.Contains("WorldServer skipped LegacyClan Dedicate", output);
        Assert.Contains("amount=300 money=100", output);
        Assert.DoesNotContain("WorldServer applied LegacyClan Dedicate", output);
    }

    private static Fixture CreateFixture(long money, ulong clanDonation)
    {
        var dbRoot = Path.Combine(Path.GetTempPath(), "wolfconquer-clan-handler-" + Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(dbRoot, "Users");
        var clansRoot = Path.Combine(dbRoot, "Clans");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(clansRoot);
        var clanPath = Path.Combine(clansRoot, "329.txt");
        File.WriteAllText(
            clanPath,
            """
            Count=3
            329/TestClan/HeroOne/0/500000/None/0/0/0/0/
            0/
            0/
            """);
        var userPath = AddMemberFile(
            usersRoot,
            accountId: 1_293_600,
            uid: 1_293_600,
            name: "HeroOne",
            money: money,
            clanRank: 100,
            clanDonation: clanDonation);
        return new Fixture(dbRoot, usersRoot, userPath, clanPath);
    }

    private static string AddMemberFile(
        string usersRoot,
        long accountId,
        long uid,
        string name,
        long money,
        ushort clanRank,
        ulong clanDonation,
        uint clanId = 329)
    {
        var userPath = Path.Combine(usersRoot, accountId + ".ini");
        File.WriteAllText(
            userPath,
            $"""
            [Character]
            UID={uid}
            Name={name}
            Money={money}
            Class=7051
            Level=140
            ClanID={clanId}
            ClanUID={clanId}
            ClanRank={clanRank}
            ClanDonation={clanDonation}
            """);
        return userPath;
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static WorldSession CreateSession(
        string? usersRoot,
        long money,
        ulong clanDonation,
        long uid = 1_293_600,
        string name = "HeroOne",
        uint clanId = 329,
        ushort clanRank = 100)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name,
            Character: new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1002,
                X: 577,
                Y: 586,
                PreviousMapId: 1002,
                PreviousMapX: 577,
                PreviousMapY: 586,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: money,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ClanID"] = clanId.ToString(),
                    ["ClanUID"] = clanId.ToString(),
                    ["ClanRank"] = clanRank.ToString(),
                    ["ClanDonation"] = clanDonation.ToString(),
                },
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed class Fixture(string dbRoot, string usersRoot, string userPath, string clanPath) : IDisposable
    {
        public string DbRoot { get; } = dbRoot;

        public string UsersRoot { get; } = usersRoot;

        public string UserPath { get; } = userPath;

        public string ClanPath { get; } = clanPath;

        public void Dispose()
        {
            try
            {
                Directory.Delete(DbRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
