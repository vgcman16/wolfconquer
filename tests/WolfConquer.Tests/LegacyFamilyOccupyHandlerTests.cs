using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyFamilyOccupyHandlerTests
{
    [Fact]
    public async Task HandlerRespondsToShowInformationAndStoresActiveNpc()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(context, ReadFrame(CreateFamilyOccupyQuery(LegacyFamilyOccupyType.ShowInformation, 101915)));

        var response = Assert.Single(sent);
        Assert.True(LegacyFamilyOccupyPacket.TryParseShowInformation(ReadFrame(response), out var information));
        Assert.Equal(101915U, information.NpcUid);
        Assert.Equal("None", information.ClanName);
        Assert.Equal("Bird", information.MapName);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(101915U, updatedSession?.ActiveNpcId);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyFamilyOccupy", output);
        Assert.Contains("type=ShowInformation", output);
        Assert.DoesNotContain("WorldServer deferred LegacyFamilyOccupy", output);
        Assert.DoesNotContain("no character session is available", output);
    }

    [Fact]
    public async Task HandlerRespondsToShowInformationFromSourceClanWarFile()
    {
        using var fixture = CreateClanWarFixture(
            "Twin",
            "148/WalkingDead/0/722454/1/722454/17/Gipsy/0/722454/9/722454/");
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(usersRoot: fixture.UsersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(context, ReadFrame(CreateFamilyOccupyQuery(LegacyFamilyOccupyType.ShowInformation, 101888)));

        var response = Assert.Single(sent);
        Assert.True(LegacyFamilyOccupyPacket.TryParseShowInformation(ReadFrame(response), out var information));
        Assert.Equal(148U, information.ClanId);
        Assert.Equal(101888U, information.NpcUid);
        Assert.Equal("WalkingDead", information.ClanName);
        Assert.Equal("Twin", information.MapName);
        Assert.Equal(1U, information.OccupationDays);
        Assert.Equal(722454U, information.Reward);
        Assert.Equal(722454U, information.NextReward);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyFamilyOccupy", output);
        Assert.Contains("type=ShowInformation", output);
        Assert.Contains("clanId=148", output);
        Assert.Contains("clanName=WalkingDead", output);
        Assert.DoesNotContain("WorldServer deferred LegacyFamilyOccupy", output);
    }

    [Fact]
    public async Task HandlerSkipsShowInformationWithoutSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var context = new WorldPacketHandlerContext(connection, log);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(context, ReadFrame(CreateFamilyOccupyQuery(LegacyFamilyOccupyType.ShowInformation, 101907)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains(
            "WorldServer skipped LegacyFamilyOccupy session=none type=ShowInformation npcUid=101907; no character session is available.",
            output);
        Assert.DoesNotContain("WorldServer handled LegacyFamilyOccupy", output);
        Assert.DoesNotContain("WorldServer deferred LegacyFamilyOccupy", output);
    }

    [Fact]
    public async Task HandlerLogsMalformedFamilyOccupyPacket()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var context = new WorldPacketHandlerContext(connection, log);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyFamilyOccupy, payloadLength: sizeof(uint));
        writer.WriteUInt32(0, (uint)LegacyFamilyOccupyType.ShowInformation);

        await registry.HandleAsync(context, ReadFrame(writer.ToArray()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer rejected malformed LegacyFamilyOccupy packet session=none.", output);
        Assert.Contains("opcode=2096 payloadLength=4", output);
        Assert.DoesNotContain("WorldServer handled LegacyFamilyOccupy", output);
        Assert.DoesNotContain("WorldServer deferred LegacyFamilyOccupy", output);
    }

    [Fact]
    public async Task HandlerRespondsToFamilyOccupyJoinWithoutClan()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(context, ReadFrame(CreateFamilyOccupyQuery(LegacyFamilyOccupyType.ShowInformation, 101915)));
        sent.Clear();
        await registry.HandleAsync(context, ReadFrame(CreateFamilyOccupyQuery(LegacyFamilyOccupyType.Join, 101915)));

        var response = Assert.Single(sent);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(response), out var message));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Contains(LegacyFamilyOccupyPacket.SourceNoClanJoinMessage, message.Content);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyFamilyOccupy", output);
        Assert.Contains("accountId=1293600", output);
        Assert.Contains("type=Join npcUid=101915 activeNpcUid=101915 response=no-clan", output);
        Assert.Contains("sourceName=MsgFamilyOccupy.Process", output);
        Assert.DoesNotContain("WorldServer deferred LegacyFamilyOccupy", output);
    }

    [Fact]
    public async Task HandlerKeepsFamilyOccupyJoinClanMemberOnDeadDefaultWarState()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateConnection(sent);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["ClanID"] = "329",
        });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(context, ReadFrame(CreateFamilyOccupyQuery(LegacyFamilyOccupyType.ShowInformation, 101915)));
        sent.Clear();
        await registry.HandleAsync(context, ReadFrame(CreateFamilyOccupyQuery(LegacyFamilyOccupyType.Join, 101915)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyFamilyOccupy", output);
        Assert.Contains("type=Join npcUid=101915 activeNpcUid=101915 city=Bird state=dead", output);
        Assert.Contains("sourceName=MsgClanWar.CityWar.Join", output);
        Assert.DoesNotContain("response=no-clan", output);
        Assert.DoesNotContain("WorldServer deferred LegacyFamilyOccupy", output);
    }

    private static byte[] CreateFamilyOccupyQuery(LegacyFamilyOccupyType type, uint npcUid)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyFamilyOccupy, LegacyFamilyOccupyPacket.QueryPayloadLength);
        writer.WriteUInt32(0, (uint)type);
        writer.WriteUInt32(4, npcUid);
        return writer.ToArray();
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
        IReadOnlyDictionary<string, string>? attributes = null,
        string? usersRoot = null)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "user02",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
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
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes ?? new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static Fixture CreateClanWarFixture(string city, string row)
    {
        var root = Path.Combine(Path.GetTempPath(), "wolfconquer-family-handler-" + Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(root, "Users");
        var clanWarRoot = Path.Combine(root, "ClanWar");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(clanWarRoot);
        File.WriteAllText(Path.Combine(clanWarRoot, city + ".txt"), "Count=1" + Environment.NewLine + row);
        return new Fixture(root, usersRoot);
    }

    private sealed class Fixture(string dbRoot, string usersRoot) : IDisposable
    {
        public string DbRoot { get; } = dbRoot;

        public string UsersRoot { get; } = usersRoot;

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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
