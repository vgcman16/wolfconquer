using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySubclassMasterJoinMutationTests : IDisposable
{
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;
    private const uint WarlockNpcId = 7_779;
    private const uint MartialArtistNpcId = 7_778;
    private const uint PerformerNpcId = 7_776;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WolfConquer-SubclassJoin-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StudyPointJoinAbove800Spends800LearnsAndPersistsReloadableState()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = CreateUsersRoot("801#0#0#", "0");
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: usersRoot, subProfInfo: "801#0#0#");
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WarlockNpcId,
            optionId: 5,
            CancellationToken.None);

        Assert.True(handled);
        var updated = AssertStoredSession(sessions, connection);
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updated.Character.Attributes["SubProfInfo"], out var state));
        Assert.Equal(1, state.StudyPoints);
        var learned = Assert.Single(state.Entries);
        Assert.Equal(2, learned.Id);
        Assert.Equal(1, learned.Level);
        Assert.Equal(1, learned.Phrase);
        Assert.Equal("10", updated.Character.Attributes["SubClassHashPoint"]);

        var animation = Assert.Single(
            ParseSubClassGuiPackets(sent),
            packet => packet.Action == LegacySubClassAction.Animation);
        Assert.Equal(1, animation.StudyPoints);
        var update = Assert.Single(
            ParseSubClassUpdates(sent),
            packet => packet.Action == LegacySubClassAction.LearnSubClass);
        Assert.Equal(LegacySubClassAction.LearnSubClass, update.Action);
        Assert.Equal(2, update.SubClassId);
        Assert.Equal(1, update.Level);

        var persisted = File.ReadAllText(Path.Combine(usersRoot, "100.ini"));
        Assert.Contains("SubProfInfo=1#0#1#2#1#1#", persisted);
        Assert.Contains("SubClassHashPoint=10", persisted);
    }

    [Fact]
    public async Task StudyPointJoinAtExactly800KeepsSourceRemoveStudyEdgeAndStillLearns()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = CreateUsersRoot("800#0#0#", "0");
        var sessions = new WorldSessionStore();
        var session = CreateSession(usersRoot: usersRoot, subProfInfo: "800#0#0#");
        sessions.Set(connection, session);

        Assert.True(await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WarlockNpcId,
            optionId: 5,
            CancellationToken.None));

        var updated = AssertStoredSession(sessions, connection);
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updated.Character.Attributes["SubProfInfo"], out var state));
        Assert.Equal(800, state.StudyPoints);
        Assert.Equal(2, Assert.Single(state.Entries).Id);
        Assert.DoesNotContain(
            ParseSubClassGuiPackets(sent),
            packet => packet.Action == LegacySubClassAction.Animation);
        Assert.Single(ParseSubClassUpdates(sent), packet =>
            packet.Action == LegacySubClassAction.LearnSubClass && packet.SubClassId == 2);
        Assert.Contains("SubProfInfo=800#0#1#2#1#1#", File.ReadAllText(Path.Combine(usersRoot, "100.ini")));
    }

    [Fact]
    public async Task ItemJoinConsumesRequiredCountLearnsAndPersistsItems()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = CreateUsersRoot("0#0#0#", "0");
        var playerItemsRoot = Path.Combine(_root, "PlayerItems");
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            usersRoot: usersRoot,
            subProfInfo: "0#0#0#",
            playerItems: InventoryItem(itemId: 721_259, stackSize: 5));
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            MartialArtistNpcId,
            optionId: 6,
            CancellationToken.None);

        Assert.True(handled);
        var updated = AssertStoredSession(sessions, connection);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updated.Character.Attributes["SubProfInfo"], out var state));
        var learned = Assert.Single(state.Entries);
        Assert.Equal(1, learned.Id);
        Assert.Equal(1, learned.Level);
        Assert.Equal(1, learned.Phrase);
        var update = Assert.Single(ParseSubClassUpdates(sent));
        Assert.Equal(LegacySubClassAction.LearnSubClass, update.Action);
        Assert.Equal(1, update.SubClassId);

        var reloadedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.Empty(reloadedItems.InventoryItems);
        Assert.Contains("SubProfInfo=0#0#1#1#1#1#", File.ReadAllText(Path.Combine(usersRoot, "100.ini")));
    }

    [Fact]
    public async Task DuplicateAndInsufficientJoinsDoNotMutateSessionOrPersistence()
    {
        using var duplicateLog = new StringWriter();
        var duplicateSent = new List<byte[]>();
        var duplicateConnection = CreateSendingConnection(duplicateSent);
        var duplicateUsersRoot = CreateUsersRoot("800#0#1#2#1#1#", "10");
        var duplicateSessions = new WorldSessionStore();
        var duplicateSession = CreateSession(usersRoot: duplicateUsersRoot, subProfInfo: "800#0#1#2#1#1#");
        duplicateSessions.Set(duplicateConnection, duplicateSession);

        Assert.True(await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(duplicateConnection, duplicateLog, Session: duplicateSession, Sessions: duplicateSessions),
            duplicateSession,
            WarlockNpcId,
            optionId: 5,
            CancellationToken.None));
        Assert.Equal("800#0#1#2#1#1#", AssertStoredSession(duplicateSessions, duplicateConnection).Character.Attributes["SubProfInfo"]);
        Assert.Empty(ParseSubClassUpdates(duplicateSent));
        Assert.Contains("SubProfInfo=800#0#1#2#1#1#", File.ReadAllText(Path.Combine(duplicateUsersRoot, "100.ini")));

        using var insufficientLog = new StringWriter();
        var insufficientSent = new List<byte[]>();
        var insufficientConnection = CreateSendingConnection(insufficientSent);
        var insufficientUsersRoot = CreateUsersRoot("0#0#0#", "0");
        var insufficientItemsRoot = Path.Combine(_root, "InsufficientItems");
        var insufficientSessions = new WorldSessionStore();
        var insufficientSession = CreateSession(
            usersRoot: insufficientUsersRoot,
            subProfInfo: "0#0#0#",
            playerItems: InventoryItem(itemId: 721_259, stackSize: 4));
        insufficientSessions.Set(insufficientConnection, insufficientSession);

        Assert.True(await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(
                insufficientConnection,
                insufficientLog,
                Session: insufficientSession,
                Sessions: insufficientSessions,
                PlayerItemsRoot: insufficientItemsRoot),
            insufficientSession,
            MartialArtistNpcId,
            optionId: 6,
            CancellationToken.None));
        var unchanged = AssertStoredSession(insufficientSessions, insufficientConnection);
        Assert.Equal("0#0#0#", unchanged.Character.Attributes["SubProfInfo"]);
        Assert.Equal((ushort)4, Assert.Single(unchanged.PlayerItems.InventoryItems).StackSize);
        Assert.Empty(ParseSubClassUpdates(insufficientSent));
        Assert.False(File.Exists(Path.Combine(insufficientItemsRoot, CharacterUid + ".ini")));
    }

    [Fact]
    public async Task PerformerCardJoinConsumesOneCardAndLearnsPerformer()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = CreateUsersRoot("0#0#0#", "0");
        var playerItemsRoot = Path.Combine(_root, "PerformerItems");
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            usersRoot: usersRoot,
            subProfInfo: "0#0#0#",
            playerItems: InventoryItem(itemId: 711_679, stackSize: 1));
        sessions.Set(connection, session);

        Assert.True(await WorldPacketHandlers.TryHandleLegacySubclassMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            PerformerNpcId,
            optionId: 25,
            CancellationToken.None));

        var updated = AssertStoredSession(sessions, connection);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updated.Character.Attributes["SubProfInfo"], out var state));
        var learned = Assert.Single(state.Entries);
        Assert.Equal(6, learned.Id);
        Assert.Equal(1, learned.Level);
        Assert.Equal(1, learned.Phrase);
        var update = Assert.Single(ParseSubClassUpdates(sent));
        Assert.Equal(LegacySubClassAction.LearnSubClass, update.Action);
        Assert.Equal(6, update.SubClassId);
        Assert.Empty(new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid).InventoryItems);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string CreateUsersRoot(string subProfInfo, string subClassHashPoint)
    {
        var usersRoot = Path.Combine(_root, "Users", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, "100.ini"),
            $"""
            [Character]
            UID={CharacterUid}
            Name=SubclassJoinTest
            SubProfInfo={subProfInfo}
            SubClassHashPoint={subClassHashPoint}
            """);
        return usersRoot;
    }

    private static WorldSession CreateSession(
        string usersRoot,
        string subProfInfo,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId,
            Username: "subclass-join-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "SubclassJoinTest",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 70,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SubProfInfo"] = subProfInfo,
                    ["SubClassHashPoint"] = subProfInfo == "0#0#0#" ? "0" : "100",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot InventoryItem(uint itemId, ushort stackSize)
    {
        return new PlayerItemSnapshot(
            [new PlayerItemSnapshotItem { Uid = 900_001, ItemId = itemId, StackSize = stackSize }],
            [],
            [],
            [],
            []);
    }

    private static WorldSession AssertStoredSession(WorldSessionStore sessions, ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacySubClassGuiPacket> ParseSubClassGuiPackets(IEnumerable<byte[]> sent)
    {
        return sent
            .Select(ReadFrame)
            .Where(frame => LegacySubClassGuiPacket.TryParse(frame, out _))
            .Select(frame =>
            {
                Assert.True(LegacySubClassGuiPacket.TryParse(frame, out var packet));
                return packet;
            })
            .ToArray();
    }

    private static IReadOnlyList<LegacySubClassUpdatePacket> ParseSubClassUpdates(IEnumerable<byte[]> sent)
    {
        return sent
            .Select(ReadFrame)
            .Where(frame => LegacySubClassUpdatePacket.TryParse(frame, out _))
            .Select(frame =>
            {
                Assert.True(LegacySubClassUpdatePacket.TryParse(frame, out var packet));
                return packet;
            })
            .ToArray();
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
