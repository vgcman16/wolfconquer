using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClaimPacketTests
{
    [Fact]
    public void LegacyBlessingInfoOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2258, WorldPacketOpcodes.LegacyBlessingInfo);
        Assert.Equal((ushort)2258, LegacyBlessingInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyBlessingInfoShowRoundTripsSourceShape()
    {
        var packet = new LegacyBlessingInfoPacket(
            LegacyBlessingInfoAction.Show,
            OnlineBlessingTraining: 10_300_000,
            HuntBlessing: 784_440);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(12, frame.Payload.Length);
        Assert.True(LegacyBlessingInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyClaimOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2143, WorldPacketOpcodes.LegacyClaim);
        Assert.Equal((ushort)2143, LegacyClaimPacket.Opcode);
    }

    [Fact]
    public void LegacyClaimShowRoundTripsSourceResponseShape()
    {
        var packet = LegacyClaimPacket.Show(
            mentorUid: 1_293_600,
            onlineBlessingTraining: 0,
            huntBlessing: 0,
            mentorExperience: 99_123,
            mentorHeavensBlessing: 12,
            plusStone: 34);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(36, frame.Payload.Length);
        Assert.True(LegacyClaimPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
        Assert.Equal(LegacyClaimAction.Show, parsed.Action);
    }

    [Fact]
    public void LegacyClaimParserAcceptsSourceRequestWithoutTrailingZero()
    {
        var writer = new PacketWriter(LegacyClaimPacket.Opcode, LegacyClaimPacket.RequestPayloadLength);
        writer.WriteUInt32(0, (uint)LegacyClaimAction.ClaimAmountExpBalls);
        writer.WriteUInt32(4, 1_293_600);
        writer.WriteUInt32(8, 123);
        writer.WriteUInt32(12, 456);
        writer.WriteUInt32(16, 0);
        writer.WriteUInt64(20, 789);
        writer.WriteUInt16(28, 2);
        writer.WriteUInt16(30, 3);
        var frame = ReadFrame(writer.ToArray());

        Assert.True(LegacyClaimPacket.TryParse(frame, out var parsed));
        Assert.Equal(LegacyClaimAction.ClaimAmountExpBalls, parsed.Action);
        Assert.Equal(789UL, parsed.MentorExperience);
        Assert.Equal(2, parsed.MentorHeavensBlessing);
        Assert.Equal(3, parsed.PlusStone);
    }

    [Fact]
    public async Task HandlerSendsBlessingAndClaimSummaryForShowRequest()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["OnlineTrainingPoints"] = "10300000",
            ["HuntingBlessing"] = "784440",
            ["Mentor_ExpBalls"] = "99123",
            ["Mentor_Blessing"] = "12",
            ["Mentor_Stones"] = "34",
        });

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(sent), log, session),
            ReadFrame(new LegacyBlessingInfoPacket(LegacyBlessingInfoAction.Show, 0, 0).ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyBlessingInfoPacket.TryParse(ReadFrame(sent[0]), out var blessing));
        Assert.Equal(new LegacyBlessingInfoPacket(LegacyBlessingInfoAction.Show, 10_300_000, 784_440), blessing);
        Assert.True(LegacyClaimPacket.TryParse(ReadFrame(sent[1]), out var claim));
        Assert.Equal(LegacyClaimAction.Show, claim.Action);
        Assert.Equal(1_293_600U, claim.MentorUid);
        Assert.Equal(99_123UL, claim.MentorExperience);
        Assert.Equal(12, claim.MentorHeavensBlessing);
        Assert.Equal(34, claim.PlusStone);
        Assert.Contains("WorldServer handled LegacyBlessingInfo", log.ToString());
    }

    [Fact]
    public async Task HandlerClaimsHuntBlessingAndSendsExperienceUpdate()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["HuntingBlessing"] = "2000000",
            ["HuntBlessing"] = "2000000",
        }) with
        {
            Character = CreateSnapshot(new Dictionary<string, string>
            {
                ["HuntingBlessing"] = "2000000",
                ["HuntBlessing"] = "2000000",
            }) with
            {
                Level = 139,
                Experience = 0,
            },
        };
        sessions.Set(connection, session);
        var request = new LegacyBlessingInfoPacket(LegacyBlessingInfoAction.ClaimHuntBlessing, 0, 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: CreateLevelExperienceCatalog(),
                Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(responseBytes), out var expUpdate));
        var update = Assert.Single(expUpdate.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Experience, update.Type);
        Assert.Equal(400UL, update.Value);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(139, updated!.Character.Level);
        Assert.Equal(400, updated.Character.Experience);
        Assert.Equal("0", updated.Character.Attributes["HuntingBlessing"]);
        Assert.Equal("0", updated.Character.Attributes["HuntBlessing"]);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyBlessingInfo hunt blessing claim", output);
        Assert.DoesNotContain("WorldServer deferred LegacyBlessingInfo", output);
        Assert.DoesNotContain("blessing claim mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerClaimsOnlineTrainingAndSendsExperienceUpdate()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["OnlineTrainingPoints"] = "2000000",
        }) with
        {
            Character = CreateSnapshot(new Dictionary<string, string>
            {
                ["OnlineTrainingPoints"] = "2000000",
            }) with
            {
                Level = 139,
                Experience = 0,
            },
        };
        sessions.Set(connection, session);
        var request = new LegacyBlessingInfoPacket(LegacyBlessingInfoAction.ClaimOnlineTraining, 0, 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: CreateLevelExperienceCatalog(),
                Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(responseBytes), out var expUpdate));
        var update = Assert.Single(expUpdate.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Experience, update.Type);
        Assert.Equal(400UL, update.Value);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(139, updated!.Character.Level);
        Assert.Equal(400, updated.Character.Experience);
        Assert.Equal("0", updated.Character.Attributes["OnlineTrainingPoints"]);

        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyBlessingInfo online training claim", output);
        Assert.DoesNotContain("WorldServer deferred LegacyBlessingInfo", output);
        Assert.DoesNotContain("blessing claim mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerTreatsUnsupportedBlessingActionAsSourceBackedNoOp()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["OnlineTrainingPoints"] = "10300000",
        });
        var request = new LegacyBlessingInfoPacket((LegacyBlessingInfoAction)99, 0, 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(sent), log, session),
            ReadFrame(request.ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer source-backed no-op LegacyBlessingInfo", output);
        Assert.Contains("action=99", output);
        Assert.Contains("source MsgClaim.MsgBlessingInfo switch has no default handler", output);
        Assert.DoesNotContain("WorldServer deferred LegacyBlessingInfo", output);
        Assert.DoesNotContain("blessing claim mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerSendsClaimSummaryForShowRequest()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["MentorExperience"] = "4567",
        });
        var request = LegacyClaimPacket.Show(
            mentorUid: 22,
            onlineBlessingTraining: 33,
            huntBlessing: 44,
            mentorExperience: 0,
            mentorHeavensBlessing: 0,
            plusStone: 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(sent), log, session),
            ReadFrame(request.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyClaimPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(LegacyClaimAction.Show, response.Action);
        Assert.Equal(22U, response.MentorUid);
        Assert.Equal(33U, response.OnlineBlessingTraining);
        Assert.Equal(44U, response.HuntBlessing);
        Assert.Equal(4_567UL, response.MentorExperience);
        Assert.Contains("WorldServer handled LegacyClaim", log.ToString());
    }

    [Fact]
    public async Task HandlerClaimsAllMentorExpBallsAndSendsSourceUpdates()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["Mentor_ExpBalls"] = "300",
            ["Mentor_Blessing"] = "12",
            ["Mentor_Stones"] = "34",
        }) with
        {
            Character = CreateSnapshot(new Dictionary<string, string>
            {
                ["Mentor_ExpBalls"] = "300",
                ["Mentor_Blessing"] = "12",
                ["Mentor_Stones"] = "34",
            }) with
            {
                Level = 139,
                Experience = 0,
            },
        };
        sessions.Set(connection, session);
        var request = new LegacyClaimPacket(
            (uint)LegacyClaimAction.ClaimExperience,
            MentorUid: 22,
            OnlineBlessingTraining: 33,
            HuntBlessing: 44,
            MentorExperience: 1,
            MentorHeavensBlessing: 0,
            PlusStone: 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: CreateLevelExperienceCatalog(),
                Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Equal(4, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[0]), out var message));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Equal("You have gained experience worth 0.5 exp ball(s).", message.Content[3]);

        Assert.True(LegacyStringPacket.TryParse(ReadFrame(sent[1]), out var effect));
        Assert.Equal(1_293_600U, effect.Uid);
        Assert.Equal(LegacyStringId.Effect, effect.Id);
        Assert.Equal(["angelwing"], effect.Strings);

        AssertSingleUpdate(sent[2], LegacyMsgUpdateDataType.Experience, 600);

        Assert.True(LegacyClaimPacket.TryParse(ReadFrame(sent[3]), out var response));
        Assert.Equal(1_293_600U, response.Mode);
        Assert.Equal(22U, response.MentorUid);
        Assert.Equal(33U, response.OnlineBlessingTraining);
        Assert.Equal(44U, response.HuntBlessing);
        Assert.Equal(0UL, response.MentorExperience);
        Assert.Equal(12, response.MentorHeavensBlessing);
        Assert.Equal(34, response.PlusStone);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(600, updated!.Character.Experience);
        Assert.Equal(139, updated.Character.Level);
        Assert.Equal("0", updated.Character.Attributes["Mentor_ExpBalls"]);
        Assert.Equal("0", updated.Character.Attributes["MentorExperience"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyClaim experience", output);
        Assert.Contains("claimed=300", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClaim", output);
        Assert.DoesNotContain("mentor reward mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerClaimExperienceAtMaxLevelClearsMentorPoolWithoutGainExpBallSideEffects()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["Mentor_ExpBalls"] = "300",
            ["Mentor_Blessing"] = "12",
            ["Mentor_Stones"] = "34",
        });
        sessions.Set(connection, session);
        var request = new LegacyClaimPacket(
            (uint)LegacyClaimAction.ClaimExperience,
            MentorUid: 22,
            OnlineBlessingTraining: 33,
            HuntBlessing: 44,
            MentorExperience: 1,
            MentorHeavensBlessing: 0,
            PlusStone: 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: CreateLevelExperienceCatalog(),
                Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyClaimPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(1_293_600U, response.Mode);
        Assert.Equal(22U, response.MentorUid);
        Assert.Equal(33U, response.OnlineBlessingTraining);
        Assert.Equal(44U, response.HuntBlessing);
        Assert.Equal(0UL, response.MentorExperience);
        Assert.Equal(12, response.MentorHeavensBlessing);
        Assert.Equal(34, response.PlusStone);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(140, updated!.Character.Level);
        Assert.Equal(0, updated.Character.Experience);
        Assert.Equal("0", updated.Character.Attributes["Mentor_ExpBalls"]);
        Assert.Equal("0", updated.Character.Attributes["MentorExperience"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyClaim experience", output);
        Assert.Contains("claimed=300", output);
        Assert.Contains("source GainExpBall returns without EXP mutation at level 140 or above", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClaim", output);
    }

    [Fact]
    public async Task HandlerClaimsRequestedMentorExpBallsAndSendsSourceShapedSummary()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["Mentor_ExpBalls"] = "1200",
            ["Mentor_Blessing"] = "12",
            ["Mentor_Stones"] = "34",
        }) with
        {
            Character = CreateSnapshot(new Dictionary<string, string>
            {
                ["Mentor_ExpBalls"] = "1200",
                ["Mentor_Blessing"] = "12",
                ["Mentor_Stones"] = "34",
            }) with
            {
                Level = 139,
                Experience = 0,
            },
        };
        sessions.Set(connection, session);
        var request = new LegacyClaimPacket(
            (uint)LegacyClaimAction.ClaimAmountExpBalls,
            MentorUid: 22,
            OnlineBlessingTraining: 33,
            HuntBlessing: 44,
            MentorExperience: 300,
            MentorHeavensBlessing: 0,
            PlusStone: 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: CreateLevelExperienceCatalog(),
                Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[0]), out var expUpdate));
        var update = Assert.Single(expUpdate.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.Experience, update.Type);
        Assert.Equal(600UL, update.Value);

        Assert.True(LegacyClaimPacket.TryParse(ReadFrame(sent[1]), out var response));
        Assert.Equal(1_293_600U, response.Mode);
        Assert.Equal(22U, response.MentorUid);
        Assert.Equal(33U, response.OnlineBlessingTraining);
        Assert.Equal(44U, response.HuntBlessing);
        Assert.Equal(900UL, response.MentorExperience);
        Assert.Equal(12, response.MentorHeavensBlessing);
        Assert.Equal(34, response.PlusStone);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(600, updated!.Character.Experience);
        Assert.Equal(139, updated.Character.Level);
        Assert.Equal("900", updated.Character.Attributes["Mentor_ExpBalls"]);
        Assert.Equal("900", updated.Character.Attributes["MentorExperience"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyClaim amount exp balls", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClaim", output);
        Assert.DoesNotContain("mentor reward mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerClaimsMentorHeavenBlessingAndSendsSourceUpdates()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["Mentor_ExpBalls"] = "1200",
            ["Mentor_Blessing"] = "7200",
            ["Mentor_Stones"] = "34",
            ["HeavenBlessing"] = "3600",
        });
        sessions.Set(connection, session);
        var request = new LegacyClaimPacket(
            (uint)LegacyClaimAction.ClaimHeavenBlessing,
            MentorUid: 22,
            OnlineBlessingTraining: 33,
            HuntBlessing: 44,
            MentorExperience: 0,
            MentorHeavensBlessing: 0,
            PlusStone: 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Equal(6, sent.Count);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(sent[0]), out var message));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, message.ChatType);
        Assert.Equal("You`ve received 2 hours` blessing time.", message.Content[3]);

        AssertSingleUpdate(sent[1], LegacyMsgUpdateDataType.HeavensBlessing, 10_800);
        AssertSingleUpdate(sent[2], LegacyMsgUpdateDataType.OnlineTraining, 0);

        var status = AssertSingleParsedUpdate(sent[3]);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, status.Type);
        Assert.NotNull(status.Values);
        Assert.True((status.Values![1] & 0b10UL) != 0);

        Assert.True(LegacyStringPacket.TryParse(ReadFrame(sent[4]), out var effect));
        Assert.Equal(1_293_600U, effect.Uid);
        Assert.Equal(LegacyStringId.Effect, effect.Id);
        Assert.Equal(["bless"], effect.Strings);

        Assert.True(LegacyClaimPacket.TryParse(ReadFrame(sent[5]), out var response));
        Assert.Equal(1_293_600U, response.Mode);
        Assert.Equal(22U, response.MentorUid);
        Assert.Equal(33U, response.OnlineBlessingTraining);
        Assert.Equal(44U, response.HuntBlessing);
        Assert.Equal(1_200UL, response.MentorExperience);
        Assert.Equal(0, response.MentorHeavensBlessing);
        Assert.Equal(34, response.PlusStone);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("0", updated!.Character.Attributes["Mentor_Blessing"]);
        Assert.Equal("0", updated.Character.Attributes["MentorBlessing"]);
        Assert.Equal("10800", updated.Character.Attributes["HeavenBlessing"]);
        Assert.Equal("10800", updated.Character.Attributes["HeavensBlessing"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyClaim heaven blessing", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClaim", output);
        Assert.DoesNotContain("mentor reward mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerClaimsMentorPlusStonesUntilInventorySpaceRunsOut()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var session = CreateSession(
            new Dictionary<string, string>
            {
                ["Mentor_ExpBalls"] = "1200",
                ["Mentor_Blessing"] = "12",
                ["Mentor_Stones"] = "350",
            },
            new PlayerItemSnapshot(
                Enumerable.Range(1, 39)
                    .Select(index => new PlayerItemSnapshotItem
                    {
                        Uid = (uint)index,
                        ItemId = 1_000_000,
                        Durability = 1,
                        MaximumDurability = 1,
                        StackSize = 1,
                    })
                    .ToArray(),
                [],
                [],
                [],
                []));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var request = new LegacyClaimPacket(
            (uint)LegacyClaimAction.ClaimPlus,
            MentorUid: 22,
            OnlineBlessingTraining: 33,
            HuntBlessing: 44,
            MentorExperience: 0,
            MentorHeavensBlessing: 0,
            PlusStone: 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: CreateLevelExperienceCatalog([CreateItemType(730_001)]),
                Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        Assert.Equal(2, sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(sent[0]), out var plusStone));
        Assert.Equal(40U, plusStone.Uid);
        Assert.Equal(730_001U, plusStone.ItemId);
        Assert.Equal(1, plusStone.Durability);
        Assert.Equal(1, plusStone.MaximumDurability);
        Assert.Equal(1, plusStone.StackSize);
        Assert.Equal(1, plusStone.Plus);
        Assert.Equal(0, plusStone.Bound);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, plusStone.Mode);

        Assert.True(LegacyClaimPacket.TryParse(ReadFrame(sent[1]), out var response));
        Assert.Equal(1_293_600U, response.Mode);
        Assert.Equal(22U, response.MentorUid);
        Assert.Equal(33U, response.OnlineBlessingTraining);
        Assert.Equal(44U, response.HuntBlessing);
        Assert.Equal(1_200UL, response.MentorExperience);
        Assert.Equal(12, response.MentorHeavensBlessing);
        Assert.Equal(250, response.PlusStone);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(40, updated!.PlayerItems.InventoryItems.Count);
        var reward = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == 730_001);
        Assert.Equal(1, reward.Plus);
        Assert.Equal(0, reward.Bound);
        Assert.Equal("250", updated.Character.Attributes["Mentor_Stones"]);
        Assert.Equal("250", updated.Character.Attributes["MentorStones"]);
        Assert.Equal("250", updated.Character.Attributes["MentorPlusStone"]);
        Assert.Equal("250", updated.Character.Attributes["PlusStone"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyClaim plus", output);
        Assert.Contains("granted=1", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClaim", output);
        Assert.DoesNotContain("mentor reward mutation is not modeled", output);
    }

    [Fact]
    public async Task HandlerClaimPlusBelowOneHundredStonesOnlySendsSourceSummary()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var connection = CreateConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(new Dictionary<string, string>
        {
            ["Mentor_ExpBalls"] = "1200",
            ["Mentor_Blessing"] = "12",
            ["Mentor_Stones"] = "99",
        });
        sessions.Set(connection, session);
        var request = new LegacyClaimPacket(
            (uint)LegacyClaimAction.ClaimPlus,
            MentorUid: 22,
            OnlineBlessingTraining: 33,
            HuntBlessing: 44,
            MentorExperience: 0,
            MentorHeavensBlessing: 0,
            PlusStone: 0);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, session, Sessions: sessions),
            ReadFrame(request.ToFrameBytes()));

        var responseBytes = Assert.Single(sent);
        Assert.True(LegacyClaimPacket.TryParse(ReadFrame(responseBytes), out var response));
        Assert.Equal(1_293_600U, response.Mode);
        Assert.Equal(1_200UL, response.MentorExperience);
        Assert.Equal(12, response.MentorHeavensBlessing);
        Assert.Equal(99, response.PlusStone);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Empty(updated!.PlayerItems.InventoryItems);
        Assert.Equal("99", updated.Character.Attributes["Mentor_Stones"]);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyClaim plus", output);
        Assert.Contains("granted=0", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClaim", output);
    }

    [Fact]
    public async Task HandlerTreatsUnsupportedClaimModeAsSourceBackedNoOp()
    {
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var session = CreateSession(new Dictionary<string, string>
        {
            ["Mentor_ExpBalls"] = "1200",
            ["Mentor_Blessing"] = "12",
            ["Mentor_Stones"] = "34",
        });
        var request = new LegacyClaimPacket(
            99,
            MentorUid: 22,
            OnlineBlessingTraining: 33,
            HuntBlessing: 44,
            MentorExperience: 55,
            MentorHeavensBlessing: 66,
            PlusStone: 77);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(CreateConnection(sent), log, session),
            ReadFrame(request.ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer source-backed no-op LegacyClaim", output);
        Assert.Contains("mode=99", output);
        Assert.Contains("source MsgClaim.HandlerMentorPrize returns immediately for unsupported modes", output);
        Assert.DoesNotContain("WorldServer deferred LegacyClaim", output);
        Assert.DoesNotContain("mentor reward mutation is not modeled", output);
    }

    private static WorldSession CreateSession(
        IReadOnlyDictionary<string, string> attributes,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "HeroOne",
            CreateSnapshot(attributes),
            playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5_000,
            Money: 98_602_439_956,
            ConquerPoints: 639_778_904,
            BoundConquerPoints: 456,
            BankConquerPoints: 123,
            PkPoints: 0,
            Online: 7,
            SourceFile: "1293600.ini",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static ConquerTcpConnection CreateConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static WorldDataCatalog CreateLevelExperienceCatalog(IReadOnlyList<ItemType>? items = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://legacy-claim",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items ?? [],
            itemAddBonuses: [],
            levelExperience:
            [
                new LevelExperience(
                    Type: 0,
                    Level: 139,
                    Experience: 1_200,
                    UpLevTime: 600,
                    MentorUpLevTime: 600,
                    SourceLine: 1,
                    RawLine: "0 139 1200 600 600",
                    Fields: ["0", "139", "1200", "600", "600"]),
            ],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static ItemType CreateItemType(int itemTypeId)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString();
        fields[1] = "LegacyClaimPlus";
        fields[ItemType.DurabilityFieldIndex] = "1";
        return new ItemType(itemTypeId, "LegacyClaimPlus", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static void AssertSingleUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
    {
        var update = AssertSingleParsedUpdate(bytes);
        Assert.Equal(type, update.Type);
        Assert.Equal(value, update.Value);
    }

    private static LegacyMsgUpdateEntry AssertSingleParsedUpdate(byte[] bytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        return Assert.Single(packet.Updates);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
