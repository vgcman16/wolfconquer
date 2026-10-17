using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyQuestListPacketTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wolf-legacy-quest-list-" + Guid.NewGuid());

    [Fact]
    public void LegacyQuestListOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2120, WorldPacketOpcodes.LegacyQuestList);
        Assert.Equal(WorldPacketOpcodes.LegacyQuestList, LegacyQuestListPacket.Opcode);
    }

    [Fact]
    public void LegacyQuestListReviewAvailablePacketWritesSyntheticFixedLayout()
    {
        var bytes = LegacyQuestListPacket.CreateReviewAvailable(901).ToFrameBytes();
        var expected = new byte[36];
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(0, 2), 36);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2, 2), 2120);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4, 2), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(6, 2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(24, 4), 901);
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(28, 4), 2);

        Assert.Equal(expected, bytes);
        Assert.Equal(LegacyQuestListPacket.FrameLength, bytes.Length);

        var frame = ReadFrame(bytes);

        Assert.Equal(LegacyQuestListPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyQuestListPacket.PayloadLength, frame.Payload.Length);
    }

    [Fact]
    public void LegacyQuestListPacketRoundTripsFixedPayload()
    {
        var packet = LegacyQuestListPacket.CreateReviewAvailable(35_024);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyQuestListPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
        Assert.Equal(LegacyQuestListMode.Review, parsed.Mode);
        Assert.Equal((ushort)1, parsed.Count);
        Assert.Equal(35_024U, parsed.QuestId);
        Assert.Equal(LegacyQuestStatus.Available, parsed.Status);
        Assert.Equal(0U, parsed.Time);
    }

    [Fact]
    public void LegacyQuestListSingleQuestFrameWritesAcceptMode()
    {
        var bytes = LegacyQuestListPacket.CreateSingleQuestFrame(
            LegacyQuestListMode.AcceptQuest,
            new LegacyQuestListItem(650, LegacyQuestStatus.Accepted, 123));

        Assert.Equal(
            Convert.FromHexString("2400480801000100000000000000000000000000000000008A020000000000007B000000"),
            bytes);

        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(bytes), out var parsed));
        Assert.Equal(LegacyQuestListMode.AcceptQuest, parsed.Mode);
        Assert.Equal((ushort)1, parsed.Count);
        Assert.Equal(new LegacyQuestListItem(650, LegacyQuestStatus.Accepted, 123), Assert.Single(parsed.Items));
    }

    [Fact]
    public void LegacyQuestListReviewFramesFromPlayerQuestsWriteSavedQuestRows()
    {
        var snapshot = new PlayerQuestSnapshot(
            [
                new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 123_456, [7U, 9U]),
                new PlayerQuestSnapshotEntry(6_126, PlayerQuestStatus.Finished, 7, []),
            ]);

        var bytes = Assert.Single(LegacyQuestListPacket.CreateReviewFramesFromPlayerQuests(snapshot));

        Assert.Equal(
            Convert.FromHexString("300048080300020000000000000000000000000000000000D08800000000000040E20100EE1700000100000007000000"),
            bytes);

        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(bytes), out var parsed));
        Assert.Equal(LegacyQuestListMode.Review, parsed.Mode);
        Assert.Equal((ushort)2, parsed.Count);
        Assert.Equal(0UL, parsed.Reserved1);
        Assert.Equal(0UL, parsed.Reserved2);
        Assert.Equal(
            [
                new LegacyQuestListItem(35_024, LegacyQuestStatus.Accepted, 123_456),
                new LegacyQuestListItem(6_126, LegacyQuestStatus.Finished, 7),
            ],
            parsed.Items);
    }

    [Fact]
    public void LegacyQuestListReviewFramesFromPlayerQuestsChunkAtLegacyReviewLimit()
    {
        var snapshot = new PlayerQuestSnapshot(
            Enumerable.Range(0, LegacyQuestListPacket.MaxReviewItemsPerFrame + 1)
                .Select(index => new PlayerQuestSnapshotEntry((uint)(6_000 + index), PlayerQuestStatus.Available, 0, []))
                .ToArray());

        var frames = LegacyQuestListPacket.CreateReviewFramesFromPlayerQuests(snapshot);

        Assert.Equal(2, frames.Count);
        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(frames[0]), out var first));
        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(frames[1]), out var second));
        Assert.Equal((ushort)LegacyQuestListPacket.MaxReviewItemsPerFrame, first.Count);
        Assert.Equal((ushort)1, second.Count);
        Assert.Equal(6_000U, first.Items[0].QuestId);
        Assert.Equal(6_080U, Assert.Single(second.Items).QuestId);
    }

    [Fact]
    public void LegacyQuestListReviewFramesFromQuestCatalogOverlaySavedRowsAndAvailableRows()
    {
        var snapshot = new PlayerQuestSnapshot(
            [
                new PlayerQuestSnapshotEntry(650, PlayerQuestStatus.Accepted, 123, [1U]),
                new PlayerQuestSnapshotEntry(652, PlayerQuestStatus.Finished, 456, []),
                new PlayerQuestSnapshotEntry(999, PlayerQuestStatus.Accepted, 0, []),
            ]);
        var catalog = new[]
        {
            CreateQuestMission(650),
            CreateQuestMission(651),
            CreateQuestMission(652),
            CreateQuestMission(651),
        };

        var bytes = Assert.Single(LegacyQuestListPacket.CreateReviewFramesFromQuestCatalog(snapshot, catalog));

        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(bytes), out var parsed));
        Assert.Equal((ushort)3, parsed.Count);
        Assert.Equal(
            [
                new LegacyQuestListItem(650, LegacyQuestStatus.Accepted, 123),
                new LegacyQuestListItem(651, LegacyQuestStatus.Available, 0),
                new LegacyQuestListItem(652, LegacyQuestStatus.Finished, 456),
            ],
            parsed.Items);
    }

    [Fact]
    public void LegacyQuestListReviewStarterQuestMatchesSourceSendFullGui()
    {
        var normal = LegacyQuestListPacket.EnsureLegacyReviewStarterQuest(
            PlayerQuestSnapshot.Empty,
            characterClass: 7_051,
            out var normalStarterQuestId,
            out var normalAdded);

        Assert.True(normalAdded);
        Assert.Equal(LegacyQuestListPacket.LegacyDefaultStarterQuestId, normalStarterQuestId);
        var normalQuest = Assert.Single(normal.Quests);
        Assert.Equal(LegacyQuestListPacket.LegacyDefaultStarterQuestId, normalQuest.Uid);
        Assert.Equal(PlayerQuestStatus.Accepted, normalQuest.Status);
        Assert.Equal(0U, normalQuest.Time);
        Assert.Equal([0U], normalQuest.Intentions);

        var lee = LegacyQuestListPacket.EnsureLegacyReviewStarterQuest(
            PlayerQuestSnapshot.Empty,
            characterClass: 8_000,
            out var leeStarterQuestId,
            out var leeAdded);

        Assert.True(leeAdded);
        Assert.Equal(LegacyQuestListPacket.LegacyLeeStarterQuestId, leeStarterQuestId);
        Assert.Equal(LegacyQuestListPacket.LegacyLeeStarterQuestId, Assert.Single(lee.Quests).Uid);
    }

    [Fact]
    public void LegacyQuestListReviewStarterQuestDoesNotReplaceExistingQuest()
    {
        var snapshot = new PlayerQuestSnapshot(
            [
                new PlayerQuestSnapshotEntry(
                    LegacyQuestListPacket.LegacyDefaultStarterQuestId,
                    PlayerQuestStatus.Finished,
                    55,
                    []),
            ]);

        var updated = LegacyQuestListPacket.EnsureLegacyReviewStarterQuest(
            snapshot,
            characterClass: 7_051,
            out var starterQuestId,
            out var added);

        Assert.False(added);
        Assert.Equal(LegacyQuestListPacket.LegacyDefaultStarterQuestId, starterQuestId);
        Assert.Same(snapshot, updated);
    }

    [Fact]
    public async Task HandlerLegacyQuestListReviewSendsPlayerQuestSnapshotRows()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var questsRoot = Path.Combine(_root, "ReviewQuests");
        var session = CreateSession(
            new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(35_024, PlayerQuestStatus.Accepted, 123_456, [7U, 9U]),
                    new PlayerQuestSnapshotEntry(6_126, PlayerQuestStatus.Finished, 7, []),
                ]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                PlayerQuestsRoot: questsRoot),
            ReadFrame(CreateLegacyQuestListQuery()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(responseBytes), out var response));
        Assert.Equal((ushort)3, response.Count);
        Assert.Equal(
            [
                new LegacyQuestListItem(35_024, LegacyQuestStatus.Accepted, 123_456),
                new LegacyQuestListItem(6_126, LegacyQuestStatus.Finished, 7),
                new LegacyQuestListItem(LegacyQuestListPacket.LegacyDefaultStarterQuestId, LegacyQuestStatus.Accepted, 0),
            ],
            response.Items);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Contains(
            updatedSession!.Character.PlayerQuests.Quests,
            quest => quest.Uid == LegacyQuestListPacket.LegacyDefaultStarterQuestId &&
                quest.Status == PlayerQuestStatus.Accepted);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyQuestList", output);
        Assert.Contains("mode=Review count=0 responseCount=3 responseFrames=1.", output);
        Assert.Contains("source=player-snapshot", output);
        Assert.Contains("starterQuestId=804 starterAdded=True", output);

        var persisted = new PlayerQuestSnapshotLoader(questsRoot).LoadByCharacterUid(9_000_001);
        Assert.Contains(
            persisted.Quests,
            quest => quest.Uid == LegacyQuestListPacket.LegacyDefaultStarterQuestId &&
                quest.Status == PlayerQuestStatus.Accepted);
    }

    [Fact]
    public async Task HandlerLegacyQuestListReviewUsesQuestCatalogWhenLoaded()
    {
        var worldData = await TryLoadSeedWorldDataAsync();
        if (worldData is null ||
            !worldData.QuestMissionsByMissionId.ContainsKey(650) ||
            !worldData.QuestMissionsByMissionId.ContainsKey(651) ||
            !worldData.QuestMissionsByMissionId.ContainsKey((int)LegacyQuestListPacket.LegacyDefaultStarterQuestId))
        {
            return;
        }

        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(650, PlayerQuestStatus.Accepted, 123, [1U]),
                ]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, WorldData: worldData, Sessions: sessions),
            ReadFrame(CreateLegacyQuestListQuery()));

        Assert.True(sent.Count > 1);
        var rows = new List<LegacyQuestListItem>();
        foreach (var responseBytes in sent)
        {
            Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(responseBytes), out var response));
            rows.AddRange(response.Items);
        }

        Assert.Contains(new LegacyQuestListItem(650, LegacyQuestStatus.Accepted, 123), rows);
        Assert.Contains(new LegacyQuestListItem(651, LegacyQuestStatus.Available, 0), rows);
        Assert.Contains(
            new LegacyQuestListItem(
                LegacyQuestListPacket.LegacyDefaultStarterQuestId,
                LegacyQuestStatus.Accepted,
                0),
            rows);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Contains(
            updatedSession!.Character.PlayerQuests.Quests,
            quest => quest.Uid == LegacyQuestListPacket.LegacyDefaultStarterQuestId);
        var output = log.ToString();
        Assert.Contains("source=quest-catalog", output);
        Assert.Contains("starterQuestId=804 starterAdded=True", output);
        Assert.Contains($"responseCount={worldData.QuestMissions.Select(mission => mission.MissionId).Distinct().Count()}", output);
    }

    [Fact]
    public async Task HandlerLegacyQuestListAcceptAddsQuestAndSendsAcceptFrame()
    {
        var worldData = await TryLoadSeedWorldDataAsync();
        if (worldData is null ||
            !worldData.QuestMissionsByMissionId.TryGetValue(650, out var missions) ||
            missions.Count == 0)
        {
            return;
        }

        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var questsRoot = Path.Combine(_root, "AcceptQuests");
        var session = CreateSession(PlayerQuestSnapshot.Empty);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerQuestsRoot: questsRoot),
            ReadFrame(CreateLegacyQuestListQuery(LegacyQuestListMode.AcceptQuest, questId: 650, time: 123)));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacyQuestListMode.AcceptQuest, response.Mode);
        Assert.Equal(new LegacyQuestListItem(650, LegacyQuestStatus.Accepted, 123), Assert.Single(response.Items));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        var quest = Assert.Single(updatedSession!.Character.PlayerQuests.Quests);
        Assert.Equal(650U, quest.Uid);
        Assert.Equal(PlayerQuestStatus.Accepted, quest.Status);
        Assert.Equal(123U, quest.Time);
        Assert.Equal(Math.Max(0, missions[0].IntentAmount ?? missions[0].Intentions.Count), quest.Intentions.Count);
        var persistedQuest = Assert.Single(
            new PlayerQuestSnapshotLoader(questsRoot).LoadByCharacterUid(9_000_001).Quests);
        Assert.Equal(quest.Uid, persistedQuest.Uid);
        Assert.Equal(quest.Status, persistedQuest.Status);
        Assert.Equal(quest.Time, persistedQuest.Time);
        Assert.Equal(quest.Intentions, persistedQuest.Intentions);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyQuestList accept", output);
        Assert.DoesNotContain("WorldServer deferred LegacyQuestList accept", output);
    }

    [Fact]
    public async Task HandlerLegacyQuestListAcceptIgnoresWhenQuestCatalogEntryIsMissing()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(PlayerQuestSnapshot.Empty);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(CreateLegacyQuestListQuery(LegacyQuestListMode.AcceptQuest, questId: 650, time: 123)));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Same(session, updatedSession);
        var output = log.ToString();
        Assert.Contains("WorldServer ignored LegacyQuestList accept", output);
        Assert.Contains("source QuestInfo.AllQuests entry is not loaded", output);
        Assert.DoesNotContain("WorldServer deferred LegacyQuestList accept", output);
    }

    [Fact]
    public async Task HandlerLegacyQuestListAcceptIgnoresWhenLegacyAcceptedQuestLimitReached()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(
            new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(650, PlayerQuestStatus.Accepted, 1, []),
                    new PlayerQuestSnapshotEntry(651, PlayerQuestStatus.Accepted, 2, []),
                    new PlayerQuestSnapshotEntry(652, PlayerQuestStatus.Accepted, 3, []),
                    new PlayerQuestSnapshotEntry(653, PlayerQuestStatus.Accepted, 4, []),
                    new PlayerQuestSnapshotEntry(654, PlayerQuestStatus.Accepted, 5, []),
                ]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(CreateLegacyQuestListQuery(LegacyQuestListMode.AcceptQuest, questId: 655, time: 123)));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Same(session, updatedSession);
        var output = log.ToString();
        Assert.Contains("WorldServer ignored LegacyQuestList accept", output);
        Assert.Contains("accepted quest limit reached", output);
        Assert.DoesNotContain("WorldServer deferred LegacyQuestList accept", output);
    }

    [Fact]
    public async Task HandlerLegacyQuestListUnknownSourceModeIsNoOp()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var session = CreateSession(PlayerQuestSnapshot.Empty);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session),
            ReadFrame(CreateLegacyQuestListQuery((LegacyQuestListMode)9, questId: 650)));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer ignored LegacyQuestList", output);
        Assert.Contains("mode=9 questId=650; mode is not defined by source QuestMode", output);
        Assert.DoesNotContain("WorldServer deferred LegacyQuestList", output);
    }

    [Fact]
    public async Task HandlerLegacyQuestListQuitRemovesQuestAndSendsQuitThenAvailableReview()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var questsRoot = Path.Combine(_root, "QuitQuests");
        var session = CreateSession(
            new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(650, PlayerQuestStatus.Accepted, 123, [0U]),
                    new PlayerQuestSnapshotEntry(651, PlayerQuestStatus.Finished, 0, []),
                ]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                PlayerQuestsRoot: questsRoot),
            ReadFrame(CreateLegacyQuestListQuery(LegacyQuestListMode.QuitQuest, questId: 650)));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(sent[0]), out var quit));
        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(sent[1]), out var review));
        Assert.Equal(LegacyQuestListMode.QuitQuest, quit.Mode);
        Assert.Equal(new LegacyQuestListItem(650, LegacyQuestStatus.Accepted, 123), Assert.Single(quit.Items));
        Assert.Equal(LegacyQuestListMode.Review, review.Mode);
        Assert.Equal(new LegacyQuestListItem(650, LegacyQuestStatus.Available, 0), Assert.Single(review.Items));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal([651U], updatedSession!.Character.PlayerQuests.Quests.Select(quest => quest.Uid));
        var persisted = new PlayerQuestSnapshotLoader(questsRoot).LoadByCharacterUid(9_000_001);
        Assert.Equal([651U], persisted.Quests.Select(quest => quest.Uid));
        Assert.Contains("WorldServer handled LegacyQuestList quit", log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyQuestListFinishMarksQuestFinishedAndSendsFinishFrame()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateConnection(sent);
        var questsRoot = Path.Combine(_root, "FinishQuests");
        var session = CreateSession(
            new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(650, PlayerQuestStatus.Accepted, 123, [4U]),
                    new PlayerQuestSnapshotEntry(651, PlayerQuestStatus.Finished, 0, []),
                ]));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                PlayerQuestsRoot: questsRoot),
            ReadFrame(CreateLegacyQuestListQuery(LegacyQuestListMode.FinishQuest, questId: 650)));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyQuestListPacket.TryParseReviewFrame(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacyQuestListMode.FinishQuest, response.Mode);
        Assert.Equal(new LegacyQuestListItem(650, LegacyQuestStatus.Finished, 123), Assert.Single(response.Items));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        var quest = updatedSession!.Character.PlayerQuests.Quests.Single(entry => entry.Uid == 650);
        Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
        Assert.Empty(updatedSession.Character.PlayerQuests.AcceptedQuests);
        var persisted = new PlayerQuestSnapshotLoader(questsRoot).LoadByCharacterUid(9_000_001);
        Assert.Equal(
            [650U, 651U],
            persisted.Quests.Select(entry => entry.Uid));
        Assert.Equal(PlayerQuestStatus.Finished, persisted.Quests.Single(entry => entry.Uid == 650).Status);
        Assert.Contains("WorldServer handled LegacyQuestList finish", log.ToString());
    }

    [Fact]
    public void LegacyQuestListDailyResetSequenceMatchesProtocolQuestOrder()
    {
        var snapshot = CreateSnapshot(DateTime.Now.DayOfYear - 1);

        var initial = LegacyQuestListPacket.CreateDailyResetInitialReviews(snapshot, DateTime.Now);
        var followup = LegacyQuestListPacket.CreateDailyResetFollowupReviews(snapshot, DateTime.Now);

        Assert.Equal([6_126U], initial.Select(packet => packet.QuestId));
        Assert.Equal(
            [
                35_024U,
                35_007U,
                35_025U,
                35_028U,
                35_034U,
                6_390U,
                6_329U,
                6_245U,
                6_049U,
                6_366U,
                6_014U,
                2_375U,
                6_126U,
            ],
            followup.Select(packet => packet.QuestId));
    }

    [Fact]
    public void LegacyQuestListDailyResetDoesNotEmitWhenSavedDayMatchesToday()
    {
        var snapshot = CreateSnapshot(DateTime.Now.DayOfYear);

        Assert.Empty(LegacyQuestListPacket.CreateDailyResetInitialReviews(snapshot, DateTime.Now));
        Assert.Empty(LegacyQuestListPacket.CreateDailyResetFollowupReviews(snapshot, DateTime.Now));
    }

    [Fact]
    public void LegacyQuestListPacketRejectsWrongPayloadLength()
    {
        var writer = new PacketWriter(LegacyQuestListPacket.Opcode, payloadLength: 4);
        writer.WriteUInt32(0, 65_539);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyQuestListPacket.TryParse(frame, out _));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static byte[] CreateLegacyQuestListQuery(
        LegacyQuestListMode mode = LegacyQuestListMode.Review,
        uint questId = 0,
        uint time = 0)
    {
        var writer = new PacketWriter(LegacyQuestListPacket.Opcode, LegacyQuestListPacket.QueryPayloadLength);
        writer.WriteUInt16(0, (ushort)mode);
        writer.WriteUInt16(2, 0);
        writer.WriteUInt32(4, questId);
        writer.WriteUInt32(8, (uint)LegacyQuestStatus.Available);
        writer.WriteUInt32(12, time);
        return writer.ToArray();
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            new IPEndPoint(IPAddress.Loopback, 0),
            new IPEndPoint(IPAddress.Loopback, 0),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static WorldSession CreateSession(PlayerQuestSnapshot playerQuests)
    {
        return new WorldSession(
            AccountId: 9_000_001,
            Username: "FixtureHero",
            CreateSnapshot(DateTime.Now.DayOfYear) with { PlayerQuests = playerQuests },
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static async Task<WorldDataCatalog?> TryLoadSeedWorldDataAsync()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return null;
        }

        return await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static CharacterSnapshot CreateSnapshot(int day)
    {
        return new CharacterSnapshot(
            Uid: 9_000_001,
            Name: "FixtureHero",
            Body: 1008,
            Face: 1,
            Hair: 1,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 20,
            Y: 30,
            PreviousMapId: 1002,
            PreviousMapX: 20,
            PreviousMapY: 30,
            Strength: 10,
            Agility: 10,
            Vitality: 10,
            Spirit: 10,
            AttributePoints: 0,
            MinHitPoints: 100,
            MinMana: 50,
            Money: 1000,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "synthetic-character.ini",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Day"] = day.ToString(),
            },
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static QuestMission CreateQuestMission(int missionId)
    {
        return new QuestMission(
            SectionId: missionId,
            SourceLine: missionId,
            TypeId: 1,
            MissionId: missionId,
            Name: "FixtureHero",
            LevelMin: null,
            LevelMax: null,
            Auto: null,
            First: null,
            PrerequisiteRaw: null,
            PrerequisiteMissionIds: [],
            MapId: 1002,
            Profession: null,
            Sex: null,
            FinishTime: null,
            ActivityBeginTime: null,
            ActivityEndTime: null,
            CompleteFlag: null,
            Prize: null,
            IntentionDescription: null,
            IntentAmount: null,
            Content: null,
            BeginNpcAnchors: [],
            FinishNpcAnchors: [],
            Intentions: [],
            PrizeItems: [],
            Entries: []);
    }
}
