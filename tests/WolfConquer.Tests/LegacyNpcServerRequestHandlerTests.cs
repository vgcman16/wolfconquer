using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcServerRequestHandlerTests
{
    [Fact]
    public async Task HandlerStoresActiveNpcAndSendsFallbackDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(999_001, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Single(sent);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[0]), out var response));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, response.InteractType);
        Assert.Contains("legacy script is not modeled", response.Text);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(999_001U, updatedSession?.ActiveNpcId);

        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=npc severity=warning gap=clicked-npc", output);
        Assert.Contains("dedupe=npc:clicked-npc:npcId=999001:opcode=2084", output);
        Assert.Contains("WorldServer handled LegacyNpcServer", output);
        Assert.Contains("npcId=999001", output);
        Assert.Contains("WorldServer sent LegacyNpcServer fallback dialog", output);
        Assert.DoesNotContain("unknown world opcode", output);
    }

    [Fact]
    public async Task HandlerSendsBackTcDialogFromRuntimeCatalog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(10_032, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("\u8fd4\u56de\u53cc\u9f99\u57ce", dialog.Text);

        var returnOption = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(3, returnOption.OptionId);
        Assert.Equal("\u8bf7\u5e26\u6211\u56de\u53bb\u3002", returnOption.Text);

        var closeOption = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("\u554a\uff0c\u7b49\u7b49", closeOption.Text);

        var avatar = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(154, avatar.InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(10_032U, updatedSession?.ActiveNpcId);
        var output = log.ToString();
        Assert.Contains("source=NpcHandler.BackTC", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
    }

    [Fact]
    public async Task HandlerSendsSourceNamedFencedDialogForNianShouChest()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(4_503, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(4, sent.Count);
        var eventText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Nian Shou Chest", eventText.Text);

        var fencedText = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("AuntZhang", fencedText.Text);
        Assert.Contains("nothing was granted", fencedText.Text);

        var closeOption = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("I~understand.", closeOption.Text);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(4_503U, updatedSession?.ActiveNpcId);

        var output = log.ToString();
        Assert.Contains("source=NpcID.AuntZhang", output);
        Assert.Contains("client=NianShouChest", output);
        Assert.Contains("PARITY-GAP LegacyNpc SpringFortuneNianShouChest reward unavailable", output);
        Assert.Contains("PARITY-GAP category=npc severity=warning gap=clicked-npc", output);
        Assert.DoesNotContain("gap=missing-npc-script", output);
        Assert.DoesNotContain("WorldServer sent LegacyNpcServer fallback dialog", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForBackTcReplyTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(10_032, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 3, interactType: 1, npcId: 0)));

        Assert.Equal(10, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 5, session, expectedMapId: 1_002, expectedX: 410, expectedY: 354);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(1_002, teleportedSession.Character.MapId);
        Assert.Equal(410, teleportedSession.Character.X);
        Assert.Equal(354, teleportedSession.Character.Y);
        Assert.Equal(session.Character.MapId, teleportedSession.Character.PreviousMapId);
        Assert.Equal(session.Character.X, teleportedSession.Character.PreviousMapX);
        Assert.Equal(session.Character.Y, teleportedSession.Character.PreviousMapY);
        Assert.Contains("source=NpcHandler.BackTC", log.ToString());
    }

    [Theory]
    [InlineData(2_541U, "NpcHandler.NobilityDonor")]
    [InlineData(45_632U, "NpcHandler.AccountManager")]
    public async Task HandlerIgnoresSourceBackedZeroOptionNpcReplyForSpecialModeledNpcs(
        uint activeNpcId,
        string expectedSource)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession() with { ActiveNpcId = activeNpcId };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 0, interactType: 101, npcId: 0)));

        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(activeNpcId, updatedSession?.ActiveNpcId);

        var output = log.ToString();
        Assert.Contains("WorldServer ignored source-backed LegacyNpcReply zero option", output);
        Assert.Contains($"source={expectedSource}", output);
        Assert.Contains("option != byte.MaxValue && option != 0", output);
        Assert.DoesNotContain("WorldServer sent LegacyNpcServer fallback dialog", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    [Theory]
    [InlineData(LegacyNpcServerAction.PlaceFurniture)]
    [InlineData(LegacyNpcServerAction.Statue)]
    [InlineData(LegacyNpcServerAction.Cursor)]
    public async Task HandlerNoopsUnsupportedLegacyNpcServerActionsAsSourceBackedFallback(
        LegacyNpcServerAction action)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(new LegacyNpcServerRequestPacket(
                action,
                20_499,
                OptionId: 0,
                InteractType: 1,
                Input: string.Empty).ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpcServer", output);
        Assert.Contains($"npcId=20499 option=0 type=1 action={action}", output);
        Assert.Contains("PARITY-GAP source-backed no-op LegacyNpcServer", output);
        Assert.DoesNotContain("WorldServer sent LegacyNpcServer fallback dialog", output);
    }

    [Fact]
    public async Task HandlerSendsIronTongDialogForRayzoTestNpcClick()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_499, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(8, sent.Count);
        var firstText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Iron Tong", firstText.Text);

        var finalText = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("defend Dragon Island", finalText.Text);

        var enterOption = AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, enterOption.OptionId);
        Assert.Equal("Send~me~to~Dragon~Island.", enterOption.Text);

        var closeOption = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);

        var avatar = AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(55, avatar.InputMaxLength);
        AssertNpcReply(sent[7], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(20_499U, updatedSession?.ActiveNpcId);
        Assert.Contains("source=NpcHandler.IronTong", log.ToString());
    }

    [Fact]
    public async Task HandlerAcceptsNpcClickFromPreviousSameMapPositionAfterMovement()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                X = 597,
                Y = 606,
                PreviousMapId = 1002,
                PreviousMapX = 577,
                PreviousMapY = 586,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: CreateWorldData(new Npc(20_499, 2, 47_496, 1002, 577, 586, 1)),
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_499, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(8, sent.Count);
        var firstText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Iron Tong", firstText.Text);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.Equal(20_499U, updatedSession?.ActiveNpcId);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.IronTong", output);
        Assert.DoesNotContain("clicked NPC is not currently visible", output);
        Assert.DoesNotContain("PARITY-GAP skipped LegacyNpcServer", output);
    }

    [Fact]
    public async Task HandlerLogsPlayerPositionAndNearestNpcRowWhenClickIsNotVisible()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: CreateWorldData(new Npc(20_499, 2, 47_496, 1002, 620, 586, 1)),
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_499, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("PARITY-GAP skipped LegacyNpcServer", output);
        Assert.Contains("npcLookup=static=1/visible=0 sob=0/visible=0", output);
        Assert.Contains("playerMap=1002 playerX=577 playerY=586", output);
        Assert.Contains("staticNearestMap=1002 staticNearestX=620 staticNearestY=586", output);
        Assert.Contains("staticNearestDistance=43 staticNearestVisible=False", output);
        Assert.Contains("clicked NPC is not currently visible", output);
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForIronTongReplyTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_499, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(13, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 8, session, expectedMapId: 10_137, expectedX: 95, expectedY: 409);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(10_137, teleportedSession.Character.MapId);
        Assert.Equal(95, teleportedSession.Character.X);
        Assert.Equal(409, teleportedSession.Character.Y);
        Assert.Contains("source=NpcHandler.IronTong", log.ToString());
    }

    [Fact]
    public async Task HandlerIgnoresClosedLegacyNpcReplyForActiveNpc()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_499, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);
        var sentBeforeClose = sent.Count;

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(
                LegacyNpcDialogRequestPacket.ClosedOptionId,
                (byte)LegacyNpcReplyInteractType.Dialog,
                npcId: 0)));

        Assert.Equal(sentBeforeClose, sent.Count);
        var output = log.ToString();
        Assert.Contains("WorldServer ignored LegacyNpcReply closed option", output);
        Assert.Contains("npcId=20499", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    [Fact]
    public async Task HandlerSendsSourceBackedDragonIslandConductressDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_508, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(9, sent.Count);
        var firstText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Conductress", firstText.Text);

        var serviceText = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("decent teleport services", serviceText.Text);

        var twinCity = AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, twinCity.OptionId);
        Assert.Equal("Twin~City.", twinCity.Text);

        var market = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
        Assert.Equal(20, market.OptionId);
        Assert.Equal("Market.", market.Text);

        var ancient = AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Option);
        Assert.Equal(3, ancient.OptionId);
        Assert.Equal("Deityland.~(new~hunting~area)", ancient.Text);

        var closeOption = AssertNpcReply(sent[7], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("I`m~just~passing~by.", closeOption.Text);
        AssertNpcReply(sent[8], LegacyNpcReplyInteractType.Finish);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.DragonIslandConductress", output);
        Assert.Contains("avatar=none", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
    }

    [Theory]
    [InlineData(1, 1_002U, 410U, 354U)]
    [InlineData(20, 1_002U, 346U, 420U)]
    [InlineData(2, 1_002U, 345U, 441U)]
    [InlineData(3, 10_137U, 453U, 483U)]
    public async Task HandlerTeleportsDragonIslandConductressOptions(
        byte optionId,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_508, optionId, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId, expectedX, expectedY);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(expectedMapId, (uint)teleportedSession.Character.MapId);
        Assert.Equal(expectedX, (uint)teleportedSession.Character.X);
        Assert.Equal(expectedY, (uint)teleportedSession.Character.Y);
        Assert.Contains("source=NpcHandler.DragonIslandConductress", log.ToString());
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForDragonIslandConductressReplyTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_508, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 20, interactType: 1, npcId: 0)));

        Assert.Equal(14, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 9, session, expectedMapId: 1_002, expectedX: 346, expectedY: 420);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(1_002, teleportedSession.Character.MapId);
        Assert.Equal(346, teleportedSession.Character.X);
        Assert.Equal(420, teleportedSession.Character.Y);
        Assert.Equal(session.Character.MapId, teleportedSession.Character.PreviousMapId);
        Assert.Equal(session.Character.X, teleportedSession.Character.PreviousMapX);
        Assert.Equal(session.Character.Y, teleportedSession.Character.PreviousMapY);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.DragonIslandConductress", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    [Theory]
    [InlineData(20_571U, "NpcHandler.GlowtownPortal1")]
    [InlineData(20_572U, "NpcHandler.GlowtownPortal2")]
    [InlineData(20_573U, "NpcHandler.GlowtownPortal3")]
    public async Task HandlerSendsSourceBackedGlowtownPortalDialog(uint npcId, string expectedSource)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Are you sure you want to return to town?", dialog.Text);

        var yesOption = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, yesOption.OptionId);
        Assert.Equal("Yes.", yesOption.Text);

        var closeOption = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("I~will~look~around.", closeOption.Text);

        var avatar = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(156, avatar.InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);
        Assert.Contains($"source={expectedSource}", log.ToString());
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForGlowtownPortalReplyTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                X = 568,
                Y = 372,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_571, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(10, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 5, session, expectedMapId: 10_137, expectedX: 93, expectedY: 408);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(10_137, teleportedSession.Character.MapId);
        Assert.Equal(93, teleportedSession.Character.X);
        Assert.Equal(408, teleportedSession.Character.Y);
        Assert.Contains("source=NpcHandler.GlowtownPortal1", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForCobbWindDetailsDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                X = 104,
                Y = 406,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_500, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sent.Count is 8 or 10);
        var firstText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Cobb#Wind", firstText.Text);

        var detailsOption = AssertNpcReply(sent[^4], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, detailsOption.OptionId);
        Assert.Equal("Detailed~guest~information.", detailsOption.Text);

        var closeOption = AssertNpcReply(sent[^3], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("Thanks~for~the~warning.", closeOption.Text);

        var initialAvatar = AssertNpcReply(sent[^2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(85, initialAvatar.InputMaxLength);
        AssertNpcReply(sent[^1], LegacyNpcReplyInteractType.Finish);
        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        var countBeforeReply = sent.Count;
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(10, sent.Count - countBeforeReply);
        var heading = AssertNpcReply(sent[countBeforeReply + 1], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("[Demon]", heading.Text);

        var acknowledge = AssertNpcReply(sent[countBeforeReply + 7], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, acknowledge.OptionId);
        Assert.Equal("I~understand.", acknowledge.Text);

        var detailsAvatar = AssertNpcReply(sent[countBeforeReply + 8], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(85, detailsAvatar.InputMaxLength);
        AssertNpcReply(sent[countBeforeReply + 9], LegacyNpcReplyInteractType.Finish);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.CobbWind", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    [Fact]
    public async Task HandlerSendsTranslatedYuKoonDialogAndReturnsToDragonIsland()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                MapId = 10_250,
                X = 1_014,
                Y = 1_288,
                PreviousMapId = 10_250,
                PreviousMapX = 1_014,
                PreviousMapY = 1_288,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_824, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(7, sent.Count);
        var firstText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Yu#Koon", firstText.Text);

        var returnOption = AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, returnOption.OptionId);
        Assert.Equal("Return~to~the~Dragon~Island.", returnOption.Text);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(12, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 7, session, expectedMapId: 10_137, expectedX: 453, expectedY: 483);
        Assert.Contains("source=NpcHandler.YuKoon", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsDragonYuKoonMessageBoxAndAcceptTeleportsToDeityland()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_823, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(11, sent.Count);
        var firstText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Yu#Koon", firstText.Text);
        var enterOption = AssertNpcReply(sent[7], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, enterOption.OptionId);
        Assert.Equal("Send~me~to~the~Deityland.", enterOption.Text);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(12, sent.Count);
        var prompt = AssertNpcReply(sent[11], LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("Your kills in the Deity", prompt.Text);
        Assert.True(sessions.TryGet(connection, out var promptedSession));
        Assert.NotNull(promptedSession);
        Assert.Equal("DragonYuKoonDeityland", promptedSession.Character.Attributes["PendingMessageBox"]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: promptedSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(0, LegacyNpcDialogRequestPacket.MessageBoxInteractType, npcId: 0)));

        Assert.Equal(18, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 12, promptedSession, expectedMapId: 10_250, expectedX: 1_014, expectedY: 1_288);
        var notice = AssertNpcReply(sent[17], LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("Penitence Amulet", notice.Text);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.False(teleportedSession.Character.Attributes.ContainsKey("PendingMessageBox"));
        Assert.Equal(10_250, teleportedSession.Character.MapId);
        Assert.Equal(1_014, teleportedSession.Character.X);
        Assert.Equal(1_288, teleportedSession.Character.Y);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.DragonYuKoon", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    [Fact]
    public async Task HandlerUsesDragonYuKoonReplyToReturnToGlowtown()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with { ActiveNpcId = 20_823 };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 2, interactType: 1, npcId: 0)));

        Assert.Equal(5, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId: 10_137, expectedX: 102, expectedY: 408);
        Assert.Contains("source=NpcHandler.DragonYuKoon", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTranslatedDeityCobbWindDialogAndDetails()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                MapId = 10_250,
                X = 453,
                Y = 483,
                PreviousMapId = 10_250,
                PreviousMapX = 453,
                PreviousMapY = 483,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_825, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sent.Count is 17 or 18);
        var firstText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Cobb#Wind", firstText.Text);
        var initialReplies = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var reply))
            {
                initialReplies.Add(reply);
            }
        }

        Assert.Contains(
            initialReplies,
            reply => reply is { InteractType: LegacyNpcReplyInteractType.Option, OptionId: 2, Text: "Go~kill~the~Dragon~Wraith!" });
        Assert.Contains(
            initialReplies,
            reply => reply is { InteractType: LegacyNpcReplyInteractType.Option, OptionId: 1, Text: "Learn~more~about~these~beasts." });

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);
        var countBeforeDetails = sent.Count;

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(12, sent.Count - countBeforeDetails);
        var heading = AssertNpcReply(sent[countBeforeDetails + 1], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("[Beast]", heading.Text);
        var close = AssertNpcReply(sent[countBeforeDetails + 9], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Equal("I~see.", close.Text);
        Assert.Contains("source=NpcHandler.DeityCobbWind", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Theory]
    [InlineData(12, 10_250U, 366U, 653U)]
    [InlineData(13, 10_250U, 924U, 819U)]
    [InlineData(14, 10_250U, 471U, 295U)]
    [InlineData(15, 10_250U, 640U, 621U)]
    [InlineData(16, 10_250U, 422U, 408U)]
    public async Task HandlerTeleportsDeityCobbWindFreeAreas(
        byte optionId,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession() with { ActiveNpcId = 20_825 };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId, interactType: 1, npcId: 0)));

        Assert.Equal(5, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId, expectedX, expectedY);
        Assert.Contains("source=NpcHandler.DeityCobbWind", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerPaidDeityCobbWindBossTeleportSpendsConquerPoint()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            ActiveNpcId = 20_825,
            Character = baseSession.Character with { ConquerPoints = 2 },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 22, interactType: 1, npcId: 0)));

        Assert.Equal(6, sent.Count);
        AssertScalarUpdate(sent[0], LegacyMsgUpdateDataType.ConquerPoints, 1);
        var updatedSession = session with { Character = session.Character with { ConquerPoints = 1 } };
        AssertLegacyNpcTeleportTransition(sent, startIndex: 1, updatedSession, expectedMapId: 10_250, expectedX: 163, expectedY: 415);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(1, teleportedSession.Character.ConquerPoints);
        Assert.Equal(10_250, teleportedSession.Character.MapId);
        Assert.Equal(163, teleportedSession.Character.X);
        Assert.Equal(415, teleportedSession.Character.Y);
        Assert.Contains("cost=1", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerPaidDeityCobbWindBossTeleportRequiresConquerPoint()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession() with { ActiveNpcId = 20_825 };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 22, interactType: 1, npcId: 0)));

        Assert.Equal(4, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("You do not have enough conquer point.", dialog.Text);
        var close = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Equal("Aww!", close.Text);
        AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
        Assert.False(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[0]), out _));
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTranslatedNezhaDialogAndOpensRelicDecomposeWindow()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(22_568, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(7, sent.Count);
        var firstText = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Nezha", firstText.Text);
        Assert.DoesNotContain("npc script is not modeled", firstText.Text);
        var option = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, option.OptionId);
        Assert.Equal("Decompose~a~Relic.", option.Text);
        AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(8, sent.Count);
        AssertOpenClientWindowAction(sent[7], activeSession, windowId: 904);
        Assert.Contains("source=NpcHandler.Nezha", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTranslatedSaintTaiyiDialogAndRelicInfo()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(22_569, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(12, sent.Count);
        var intro = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Saint#Taiyi", intro.Text);
        var moneyCraft = AssertNpcReply(sent[8], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, moneyCraft.OptionId);
        Assert.Equal("Craft~a~Relic~Spirit.~(50,000~Silver)", moneyCraft.Text);
        var infoOption = AssertNpcReply(sent[10], LegacyNpcReplyInteractType.Option);
        Assert.Equal(3, infoOption.OptionId);
        Assert.Equal("How~to~obtain~the~Relics?", infoOption.Text);
        AssertNpcReply(sent[11], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);
        var countBeforeInfo = sent.Count;

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 3, interactType: 1, npcId: 0)));

        Assert.Equal(9, sent.Count - countBeforeInfo);
        var infoHeading = AssertNpcReply(sent[countBeforeInfo], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("How to Obtain Relics", infoHeading.Text);
        var attributes = AssertNpcReply(sent[countBeforeInfo + 6], LegacyNpcReplyInteractType.Option);
        Assert.Equal(4, attributes.OptionId);
        Assert.Equal("Attributes~of~Relics.", attributes.Text);
        var durability = AssertNpcReply(sent[countBeforeInfo + 7], LegacyNpcReplyInteractType.Option);
        Assert.Equal(5, durability.OptionId);
        Assert.Equal("Durability~of~Relics.", durability.Text);

        var countBeforeDurability = sent.Count;
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 5, interactType: 1, npcId: 0)));

        Assert.Equal(8, sent.Count - countBeforeDurability);
        var durabilityHeading = AssertNpcReply(sent[countBeforeDurability], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Durability of Relics", durabilityHeading.Text);
        var close = AssertNpcReply(sent[countBeforeDurability + 6], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Equal("I~see.", close.Text);
        Assert.Contains("source=NpcHandler.SaintTaiyi", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerCraftsSaintTaiyiRelicSpiritWithMoney()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            ActiveNpcId = 22_569,
            Character = baseSession.Character with { Money = 60_000 },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(3, sent.Count);
        AssertScalarUpdate(sent[0], LegacyMsgUpdateDataType.Money, 10_000);
        var item = AssertGameItem(sent[1], expectedItemId: 3_307_142);
        Assert.Equal(1, item.StackSize);
        AssertPlayerEffect(sent[2], checked((uint)session.Character.Uid), "zf2-e128");
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(10_000, updatedSession.Character.Money);
        Assert.Contains(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_307_142);
        Assert.Contains("source=NpcHandler.SaintTaiyi", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerRequiresMoneyForSaintTaiyiRelicSpiritCraft()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession() with { ActiveNpcId = 22_569 };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(3, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("You do not have enough Money point.", dialog.Text);
        var close = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Equal("Aww!", close.Text);
        AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Finish);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerCraftsSaintTaiyiRelicSpiritWithRelicCrystals()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var crystal = new PlayerItemSnapshotItem
        {
            Uid = 77,
            ItemId = 3_306_885,
            StackSize = 100,
        };
        var session = baseSession with
        {
            ActiveNpcId = 22_569,
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [crystal] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 2, interactType: 1, npcId: 0)));

        Assert.Equal(3, sent.Count);
        AssertRemoveInventory(sent[0], crystal.Uid);
        AssertGameItem(sent[1], expectedItemId: 3_307_142);
        AssertPlayerEffect(sent[2], checked((uint)session.Character.Uid), "zf2-e128");
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_306_885);
        Assert.Contains(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_307_142);
        Assert.Contains("crystals=100", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerRequiresRelicCrystalsForSaintTaiyiRelicSpiritCraft()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession() with { ActiveNpcId = 22_569 };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 2, interactType: 1, npcId: 0)));

        Assert.Equal(3, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("don`t have enough Relic Crystals", dialog.Text);
        var close = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Equal("I~see.", close.Text);
        AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Finish);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTranslatedDeitylandPortalDialogAndTeleportsToSupplyStation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                MapId = 10_250,
                X = 600,
                Y = 600,
                PreviousMapId = 10_250,
                PreviousMapX = 600,
                PreviousMapY = 600,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_861, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(4, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("This portal will send you to the supply station. Do you want to use the portal now?", dialog.Text);
        var yes = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, yes.OptionId);
        Assert.Equal("Yes.", yes.Text);
        var no = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, no.OptionId);
        Assert.Equal("No.", no.Text);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(9, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 4, activeSession, expectedMapId: 10_250, expectedX: 1_009, expectedY: 1_283);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(10_250, teleportedSession.Character.MapId);
        Assert.Equal(1_009, teleportedSession.Character.X);
        Assert.Equal(1_283, teleportedSession.Character.Y);
        Assert.Contains("source=NpcHandler.DeitylandPortals", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerUnlocksRareDeitylandChestWithSourceReward()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var key = new PlayerItemSnapshotItem
        {
            Uid = 90,
            ItemId = 3_303_973,
            StackSize = 1,
        };
        var session = baseSession with
        {
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [key] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(22_598, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(8, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Rare Deityland Chest", dialog.Text);
        var keyOption = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, keyOption.OptionId);
        Assert.Equal("I~own~the~key!", keyOption.Text);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(10, sent.Count);
        AssertRemoveInventory(sent[8], key.Uid);
        var reward = AssertGameItemIn(sent[9], [3_306_362U, 3_303_480U, 3_003_124U]);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == key.ItemId);
        Assert.Contains(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == reward.ItemId);
        Assert.Contains("source=NpcHandler.RareDeitylandChests", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerUnlocksEpicDeitylandChestWithEpicKey()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var epicDialogKey = new PlayerItemSnapshotItem
        {
            Uid = 91,
            ItemId = 3_303_974,
            StackSize = 1,
        };
        var sourceUnlockKey = new PlayerItemSnapshotItem
        {
            Uid = 92,
            ItemId = 3_303_973,
            StackSize = 1,
        };
        var session = baseSession with
        {
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [epicDialogKey, sourceUnlockKey] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(22_607, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(8, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Epic Deityland Chest", dialog.Text);
        var keyOption = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, keyOption.OptionId);
        Assert.Equal("I~own~the~key!", keyOption.Text);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(10, sent.Count);
        AssertRemoveInventory(sent[8], epicDialogKey.Uid);
        if (LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[9]), out var update))
        {
            Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.ConquerPoints, 200_000)], update.Updates);
        }
        else
        {
            AssertGameItemIn(sent[9], [4_200_005U, 3_007_269U, 3_004_247U]);
        }

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == epicDialogKey.ItemId);
        Assert.Contains(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == sourceUnlockKey.ItemId);
        Assert.Contains("source=NpcHandler.EpicDeitylandChests", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerUnlocksVictoryChestWhenDeitylandQuestObjectiveIsReady()
    {
        using var log = new StringWriter();
        var playerQuestsRoot = Path.Combine(Path.GetTempPath(), "wolf-victory-chest-quests-" + Guid.NewGuid());
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(3_944, PlayerQuestStatus.Accepted, 0, [100U]),
                ]),
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        try
        {
            await registry.HandleAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: session,
                    Sessions: sessions,
                    PlayerQuestsRoot: playerQuestsRoot),
                ReadFrame(LegacyNpcServerRequestPacket.Script(21_972, optionId: 0, interactType: 1).ToFrameBytes()));

            var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
            Assert.Contains("Victory Chest", dialog.Text);
            Assert.Contains(sent, frame =>
                LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
                packet.Text.Contains("Kill 100 devils in the Deityland"));
            Assert.Contains(sent, frame =>
                LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.InteractType == LegacyNpcReplyInteractType.Option &&
                packet.OptionId == 1 &&
                packet.Text == "Unlock~this~chest.");

            Assert.True(sessions.TryGet(connection, out var activeSession));
            Assert.NotNull(activeSession);

            await registry.HandleAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: activeSession,
                    Sessions: sessions,
                    PlayerQuestsRoot: playerQuestsRoot),
                ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

            Assert.Contains(sent, frame =>
                LegacyQuestListPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.Mode == LegacyQuestListMode.FinishQuest &&
                packet.QuestId == 3_944 &&
                packet.Status == LegacyQuestStatus.Finished);
            Assert.Contains(sent, frame =>
                LegacyGameItemPacket.TryParse(ReadFrame(frame), out var packet) &&
                new[] { 3_008_727U, 3_009_002U, 4_200_004U }.Contains(packet.ItemId));
            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            var quest = Assert.Single(updatedSession.Character.PlayerQuests.Quests, entry => entry.Uid == 3_944U);
            Assert.Equal(PlayerQuestStatus.Finished, quest.Status);
            Assert.Contains(updatedSession.PlayerItems.InventoryItems, item =>
                item.ItemId is 3_008_727U or 3_009_002U or 4_200_004U);
            var persistedQuest = Assert.Single(
                new PlayerQuestSnapshotLoader(playerQuestsRoot)
                    .LoadByCharacterUid(updatedSession.Character.Uid)
                    .Quests,
                entry => entry.Uid == 3_944U);
            Assert.Equal(PlayerQuestStatus.Finished, persistedQuest.Status);
            Assert.Equal([100U], persistedQuest.Intentions.ToArray());
            Assert.Contains("source=NpcHandler.VictoryChest", log.ToString());
            Assert.Contains("WorldServer persisted LegacyPlayerQuests", log.ToString());
            Assert.Contains("source=NpcHandler.VictoryChest", log.ToString());
            Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
        }
        finally
        {
            if (Directory.Exists(playerQuestsRoot))
            {
                Directory.Delete(playerQuestsRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerSendsVictoryChestAlreadyUnlockedDialogWhenQuestIsFinished()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(3_944, PlayerQuestStatus.Finished, 0, [100U]),
                ]),
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(22_577, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Contains(sent, frame =>
            LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet) &&
            packet.InteractType == LegacyNpcReplyInteractType.Option &&
            packet.OptionId == 1 &&
            packet.Text == "Unlock~this~chest.");
        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Contains(sent, frame =>
            LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet) &&
            packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
            packet.Text.Contains("already unlocked this chest today"));
        Assert.DoesNotContain(sent, frame => LegacyGameItemPacket.TryParse(ReadFrame(frame), out _));
        Assert.Contains("source=NpcHandler.VictoryChest", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsVictoryChestMissingKillsDialogWhenObjectiveIsNotReady()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                PlayerQuests = new PlayerQuestSnapshot(
                [
                    new PlayerQuestSnapshotEntry(3_944, PlayerQuestStatus.Accepted, 0, [99U]),
                ]),
            },
            ActiveNpcId = 21_972,
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Contains(sent, frame =>
            LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet) &&
            packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
            packet.Text.Contains("need to kill 100 devils"));
        Assert.DoesNotContain(sent, frame => LegacyGameItemPacket.TryParse(ReadFrame(frame), out _));
        Assert.Contains("source=NpcHandler.VictoryChest", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTranslatedDeityAltarDialogRulesAndTransformMenu()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateDragonIslandSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(22_560, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(9, sent.Count);
        var heading = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Deityland Sacrifice Ranking", heading.Text);
        var submit = AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, submit.OptionId);
        Assert.Equal("Submit~a~Jade~of~Faith.", submit.Text);
        var transform = AssertNpcReply(sent[7], LegacyNpcReplyInteractType.Option);
        Assert.Equal(2, transform.OptionId);
        Assert.Equal("Rune~transformation.", transform.Text);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);
        var countBeforeRules = sent.Count;

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(12, sent.Count - countBeforeRules);
        var rules = AssertNpcReply(sent[countBeforeRules], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Rules for Deityland Sacrifice", rules.Text);
        var submitTen = AssertNpcReply(sent[countBeforeRules + 9], LegacyNpcReplyInteractType.Option);
        Assert.Equal(6, submitTen.OptionId);
        Assert.Equal("Submit~10~Jades~of~Faith.", submitTen.Text);

        var countBeforeTransform = sent.Count;
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 2, interactType: 1, npcId: 0)));

        Assert.Equal(5, sent.Count - countBeforeTransform);
        var transformDialog = AssertNpcReply(sent[countBeforeTransform], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Rune transformation", transformDialog.Text);
        var witchery = AssertNpcReply(sent[countBeforeTransform + 2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(3, witchery.OptionId);
        Assert.Equal("Change~L1~Witchery~to~No~Mercy.", witchery.Text);
        Assert.Contains("source=NpcHandler.DeityAltar", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerTransformsDeityAltarRune()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var witchery = new PlayerItemSnapshotItem
        {
            Uid = 93,
            ItemId = 4_031_301,
            StackSize = 1,
        };
        var session = baseSession with
        {
            ActiveNpcId = 22_560,
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [witchery] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 3, interactType: 1, npcId: 0)));

        Assert.Equal(2, sent.Count);
        AssertRemoveInventory(sent[0], witchery.Uid);
        AssertGameItem(sent[1], expectedItemId: 4_031_001);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_031_301);
        Assert.Contains(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_031_001);
        Assert.Contains("source=NpcHandler.DeityAltar", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSacrificesDeityAltarJadesAndRedrawsRanking()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateDragonIslandSession();
        var jades = new PlayerItemSnapshotItem
        {
            Uid = 94,
            ItemId = 3_307_083,
            StackSize = 10,
        };
        var session = baseSession with
        {
            ActiveNpcId = 22_560,
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [jades] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 6, interactType: 1, npcId: 0)));

        Assert.Equal(10, sent.Count);
        AssertRemoveInventory(sent[0], jades.Uid);
        var heading = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Deityland Sacrifice Ranking", heading.Text);
        var totalLine = AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("sacrificed 10 Jades", totalLine.Text);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_307_083);
        Assert.Equal("10", updatedSession.Character.Attributes["DeitylandSacrificeJades"]);
        Assert.Contains("count=10", log.ToString());
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsSourceBackedTwinCityConductressDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(10_050, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(16, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Where do you want to go? I can teleport you for 100 silver.", dialog.Text);

        var shopCity = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(14, shopCity.OptionId);
        Assert.Equal("Market.", shopCity.Text);

        var dragonIsland = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(7, dragonIsland.OptionId);
        Assert.Equal("Dragon~Island.", dragonIsland.Text);

        var birdIsland = AssertNpcReply(sent[12], LegacyNpcReplyInteractType.Option);
        Assert.Equal(4, birdIsland.OptionId);
        Assert.Equal("Bird~Island.", birdIsland.Text);

        var closeOption = AssertNpcReply(sent[13], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("Never~mind.", closeOption.Text);

        var avatar = AssertNpcReply(sent[14], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(10, avatar.InputMaxLength);
        AssertNpcReply(sent[15], LegacyNpcReplyInteractType.Finish);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.TwinCityConductress", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForTwinCityConductressPaidTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateSession();
        var session = baseSession with
        {
            Character = baseSession.Character with { Money = 250 },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(10_050, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 14, interactType: 1, npcId: 0)));

        Assert.Equal(22, sent.Count);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[16]), out var moneyUpdate));
        Assert.Equal((uint)session.Character.Uid, moneyUpdate.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Money, 150)], moneyUpdate.Updates);

        AssertLegacyNpcTeleportTransition(sent, startIndex: 17, session, expectedMapId: 10_364, expectedX: 302, expectedY: 280);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(150, teleportedSession.Character.Money);
        Assert.Equal(10_364, teleportedSession.Character.MapId);
        Assert.Equal(302, teleportedSession.Character.X);
        Assert.Equal(280, teleportedSession.Character.Y);
        Assert.Equal(session.Character.MapId, teleportedSession.Character.PreviousMapId);
        Assert.Equal(session.Character.X, teleportedSession.Character.PreviousMapX);
        Assert.Equal(session.Character.Y, teleportedSession.Character.PreviousMapY);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.TwinCityConductress", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    [Fact]
    public async Task HandlerSendsTwinCityConductressSourceNoMoneyDialogWithoutTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(10_050, optionId: 14, interactType: 1).ToFrameBytes()));

        Assert.Equal(4, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("You do not have enough silver.", dialog.Text);

        var closeOption = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("All~right.", closeOption.Text);

        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(10, avatar.InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(session.Character.MapId, updatedSession.Character.MapId);
        Assert.Equal(session.Character.Money, updatedSession.Character.Money);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.TwinCityConductress", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
    }

    [Theory]
    [InlineData(10_052U, 1, 1_011U, 13U, 376U, "NpcHandler.PheonixCityConductress")]
    [InlineData(10_051U, 2, 1_000U, 82U, 321U, "NpcHandler.DesertCityConductress")]
    [InlineData(10_053U, 1, 1_020U, 378U, 13U, "NpcHandler.ApeCityConductress")]
    [InlineData(10_056U, 1, 1_015U, 1_015U, 710U, "NpcHandler.BirdCityConductress")]
    public async Task HandlerTeleportsRegionalConductressOptionsWithLegacyCost(
        uint npcId,
        byte optionId,
        uint expectedMapId,
        uint expectedX,
        uint expectedY,
        string expectedSource)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateSession();
        var session = baseSession with
        {
            Character = baseSession.Character with { Money = 250 },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId, interactType: 1).ToFrameBytes()));

        Assert.Equal(6, sent.Count);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[0]), out var moneyUpdate));
        Assert.Equal((uint)session.Character.Uid, moneyUpdate.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Money, 150)], moneyUpdate.Updates);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 1, session, expectedMapId, expectedX, expectedY);

        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(150, teleportedSession.Character.Money);
        Assert.Equal((int)expectedMapId, teleportedSession.Character.MapId);
        Assert.Equal((int)expectedX, teleportedSession.Character.X);
        Assert.Equal((int)expectedY, teleportedSession.Character.Y);

        var output = log.ToString();
        Assert.Contains($"source={expectedSource}", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
        Assert.DoesNotContain("PARITY-GAP unmodeled LegacyNpcReply", output);
    }

    [Fact]
    public async Task HandlerSendsRegionalConductressNoMoneyDialogWithoutTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(10_051, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.Equal(4, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("I~can~teleport~you~for~a~price~of~100~silver.", dialog.Text);
        var closeOption = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("Ah yes.", closeOption.Text);
        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(10, avatar.InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var unchangedSession));
        Assert.NotNull(unchangedSession);
        Assert.Equal(session.Character.MapId, unchangedSession.Character.MapId);
        Assert.Equal(session.Character.Money, unchangedSession.Character.Money);
        Assert.Contains("source=NpcHandler.DesertCityConductress", log.ToString());
    }

    [Theory]
    [InlineData(45U, 1_020, 1_020U, 565U, 562U, "NpcHandler.MarketConductress")]
    [InlineData(180U, 1_015, 1_015U, 717U, 571U, "NpcHandler.BoxerConductor")]
    public async Task HandlerReturnsMarketConductorsToPreviousTown(
        uint npcId,
        int previousMapId,
        uint expectedMapId,
        uint expectedX,
        uint expectedY,
        string expectedSource)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var baseSession = CreateSession();
        var session = baseSession with
        {
            Character = baseSession.Character with
            {
                MapId = 1_036,
                X = 217,
                Y = 198,
                PreviousMapId = previousMapId,
                PreviousMapX = 1,
                PreviousMapY = 1,
            },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId, expectedX, expectedY);
        Assert.True(sessions.TryGet(connection, out var returnedSession));
        Assert.NotNull(returnedSession);
        Assert.Equal((int)expectedMapId, returnedSession.Character.MapId);
        Assert.Equal((int)expectedX, returnedSession.Character.X);
        Assert.Equal((int)expectedY, returnedSession.Character.Y);

        var output = log.ToString();
        Assert.Contains($"source={expectedSource}", output);
        Assert.DoesNotContain("legacy script is not modeled", output);
    }

    [Theory]
    [InlineData(641_372U, "Do you want to enter the awakening map to improve your battle power?", "Yes.", "NpcHandler.joinArchaveMap")]
    [InlineData(641_374U, "Do you want to return to Twin City?", "Yes.", "NpcHandler.outArchaveMap")]
    [InlineData(43_433U, "Do you want to improve your strength? I can take you to the next training map.", "Take~me~there.", "NpcHandler.joinJopCentr")]
    public async Task HandlerSendsSourceBackedArchiveAndJobEntryDialogs(
        uint npcId,
        string expectedText,
        string expectedOption,
        string expectedSource)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(expectedText, dialog.Text);

        var enterOption = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, enterOption.OptionId);
        Assert.Equal(expectedOption, enterOption.Text);

        var closeOption = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);

        var avatar = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(286, avatar.InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);
        Assert.Contains($"source={expectedSource}", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsSourceBackedWarehouseDialogForNpc8Click()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(8, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[0]), out var dialog));
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Contains("most reliable man", dialog.Text);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[1]), out var openOption));
        Assert.Equal(LegacyNpcReplyInteractType.Option, openOption.InteractType);
        Assert.Equal(1, openOption.OptionId);
        Assert.Equal("Open~my~Warehouse.", openOption.Text);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[2]), out var closeOption));
        Assert.Equal(LegacyNpcReplyInteractType.Option, closeOption.InteractType);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[3]), out var avatar));
        Assert.Equal(LegacyNpcReplyInteractType.Avatar, avatar.InteractType);
        Assert.Equal(10, avatar.InputMaxLength);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[4]), out var finish));
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
        Assert.Contains("WorldServer sent LegacyWarehouse dialog", log.ToString());
    }

    [Fact]
    public async Task HandlerOpensWarehouseForSourceBackedWarehouseNpcClick()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(44, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Single(sent);
        AssertWarehouseOpenAction(sent[0], session);
        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyWarehouse open action", output);
        Assert.Contains("npcId=44", output);
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForWarehouseReplyOption()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(8, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: updatedSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(6, sent.Count);
        AssertWarehouseOpenAction(sent[^1], updatedSession);
        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyWarehouse dialog", output);
        Assert.Contains("WorldServer sent LegacyWarehouse open action", output);
        Assert.Contains("npcId=8", output);
    }

    [Fact]
    public async Task HandlerSendsTeleportallDialogFromLegacyScript()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(15_641, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(14, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("We made it easy the matter for you, from here you can move around the city.", dialog.Text);

        var guildWar = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, guildWar.OptionId);
        Assert.Equal("GuildWar.", guildWar.Text);

        var clanWar = AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Option);
        Assert.Equal(4, clanWar.OptionId);
        Assert.Equal("ClanWar[Choose-Map]", clanWar.Text);

        var close = AssertNpcReply(sent[11], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Equal("No Thx.", close.Text);

        var avatar = AssertNpcReply(sent[12], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(286, avatar.InputMaxLength);
        AssertNpcReply(sent[13], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.Teleportall", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsTeleportallCityMenuFromLegacyOption()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(15_641, optionId: 4, interactType: 1).ToFrameBytes()));

        Assert.Equal(9, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("You can choose the city you want to go to.", dialog.Text);

        var twinCity = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(15, twinCity.OptionId);
        Assert.Equal("T.C.", twinCity.Text);

        var birdIsland = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
        Assert.Equal(50, birdIsland.OptionId);
        Assert.Equal("BirdIsland", birdIsland.Text);

        Assert.Contains("source=NpcHandler.Teleportall", log.ToString());
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForTeleportallReplyOption()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(15_641, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 10, interactType: 1, npcId: 0)));

        Assert.Equal(19, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 14, session, expectedMapId: 1002, expectedX: 347, expectedY: 444);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(1002, teleportedSession.Character.MapId);
        Assert.Equal(347, teleportedSession.Character.X);
        Assert.Equal(444, teleportedSession.Character.Y);
        Assert.Equal(session.Character.MapId, teleportedSession.Character.PreviousMapId);
        Assert.Equal(session.Character.X, teleportedSession.Character.PreviousMapX);
        Assert.Equal(session.Character.Y, teleportedSession.Character.PreviousMapY);
        Assert.Contains("source=NpcHandler.Teleportall", log.ToString());
    }

    [Fact]
    public async Task HandlerOpensSourceBackedShopGui()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(5, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.Single(sent);
        AssertLegacyNpcOpenGuiAction(sent[0], session, npcId: 5, useMapIdField: false);
        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyNpc OpenGui action", output);
        Assert.Contains("source=NpcHandler.BlackSmith", output);
    }

    [Fact]
    public async Task HandlerSendsTranslatedExchangeShopGoodsDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(1_251, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(6, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Hello, I can help you exchange your items for gems and other goods.", dialog.Text);
        Assert.False(ContainsCjk(dialog.Text), dialog.Text);

        var gemShop = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, gemShop.OptionId);
        Assert.Equal("Open~gem~shop.", gemShop.Text);

        var otherShop = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(2, otherShop.OptionId);
        Assert.Equal("Open~other~shop.", otherShop.Text);

        var close = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Equal("No,~thanks.", close.Text);

        var avatar = AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(164, avatar.InputMaxLength);
        AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.Exchange_shop_Goods", log.ToString());
    }

    [Theory]
    [InlineData(1, 196U, 9_001U, 7U)]
    [InlineData(2, 174U, 9_002U, 3U)]
    public async Task HandlerOpensSourceBackedExchangeShopGoodsCatalog(
        byte optionId,
        uint expectedShopId,
        uint expectedIndex,
        uint expectedCount)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: CreateWorldData(
                new Npc(1_251, 2, 47_496, 1002, 577, 586, 1),
                [
                    new ExchangeShopRow(
                        "exchange_shop_goods.txt",
                        1,
                        ["9001", "3303526", "196", "7", "0", "3303483", "1", "0", "0", "0", "0", "0", "0"]),
                    new ExchangeShopRow(
                        "exchange_shop_goods.txt",
                        2,
                        ["9002", "3303527", "174", "3", "0", "3303483", "2", "0", "0", "0", "0", "0", "0"]),
                ]),
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(1_251, optionId, interactType: 1).ToFrameBytes()));

        var bytes = Assert.Single(sent);
        Assert.True(LegacyExchangeShopGoodsPacket.TryParse(ReadFrame(bytes), out var goods));
        Assert.Equal(21_875U, goods.NpcID);
        Assert.Equal(expectedShopId, goods.ShopID);
        Assert.Equal((uint)session.Character.Uid, goods.Uid);
        Assert.InRange(goods.TimeInSeconds, 1U, 86_400U);
        Assert.Equal(0U, goods.Unknown5);

        var item = Assert.Single(goods.Items);
        Assert.Equal(expectedIndex, item.Index);
        Assert.Equal(expectedCount, item.Count);
        Assert.Equal(0U, item.Unknown);

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyExchangeShopGoods", output);
        Assert.Contains("source=NpcHandler.Exchange_shop_Goods", output);
        Assert.Contains($"shopId={expectedShopId}", output);
    }

    [Theory]
    [InlineData(21_875U)]
    [InlineData(22_575U)]
    public async Task HandlerSendsTranslatedHauKanDialog(uint npcId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(7, sent.Count);
        var intro = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Hau Kan", intro.Text);
        Assert.False(ContainsCjk(intro.Text), intro.Text);

        var supplies = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, supplies.OptionId);
        Assert.Equal("Exchange~for~supplies.", supplies.Text);

        var winner = AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Option);
        Assert.Equal(2, winner.OptionId);
        Assert.Equal("Who~won~the~super~elimination~prize?", winner.Text);

        var close = AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.HauKan", log.ToString());
    }

    [Fact]
    public async Task HandlerOpensSourceBackedHauKanSupplyExchangeShop()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            WorldData: CreateWorldData(
                new Npc(21_875, 2, 47_496, 1002, 577, 586, 1),
                [
                    new ExchangeShopRow(
                        "exchange_shop_goods.txt",
                        1,
                        ["444", "3303526", "23", "1", "0", "3303483", "300", "3303485", "1", "0", "0", "0", "0"]),
                ]),
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(21_875, optionId: 1, interactType: 1).ToFrameBytes()));

        var bytes = Assert.Single(sent);
        Assert.True(LegacyExchangeShopGoodsPacket.TryParse(ReadFrame(bytes), out var goods));
        Assert.Equal(21_875U, goods.NpcID);
        Assert.Equal(23U, goods.ShopID);
        Assert.Equal((uint)session.Character.Uid, goods.Uid);

        var item = Assert.Single(goods.Items);
        Assert.Equal(444U, item.Index);
        Assert.Equal(1U, item.Count);
        Assert.Equal(0U, item.Unknown);

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyExchangeShopGoods", output);
        Assert.Contains("source=NpcHandler.HauKan", output);
        Assert.Contains("shopId=23", output);
    }

    [Fact]
    public async Task HandlerSendsTranslatedPointsDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(12_221, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(10, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Hi, I can exchange points and open special shops for you.", dialog.Text);
        Assert.False(ContainsCjk(dialog.Text), dialog.Text);

        var shops = AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Option);
        Assert.Equal(6, shops.OptionId);
        Assert.Equal("Shops.", shops.Text);

        var close = AssertNpcReply(sent[7], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        Assert.Equal("No.", close.Text);

        var avatar = AssertNpcReply(sent[8], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(164, avatar.InputMaxLength);
        AssertNpcReply(sent[9], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.Points", log.ToString());
    }

    [Fact]
    public async Task HandlerOpensSourceBackedPointsShopWindow()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(12_221, optionId: 6, interactType: 1).ToFrameBytes()));

        var bytes = Assert.Single(sent);
        AssertOpenClientWindowAction(bytes, session, windowId: 6_003);
        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyNpc OpenClientWindow action", output);
        Assert.Contains("source=NpcHandler.Points", output);
        Assert.Contains("window=6003", output);
    }

    [Fact]
    public async Task HandlerOpensSourceBackedMountRetailerShopGui()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(5_517, optionId: 1, interactType: 1).ToFrameBytes()));

        var bytes = Assert.Single(sent);
        AssertLegacyNpcOpenGuiAction(bytes, session, npcId: 5_517, useMapIdField: false);
        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyNpc OpenGui action", output);
        Assert.Contains("source=NpcHandler.MountRetailer", output);
    }

    [Fact]
    public async Task HandlerSendsTranslatedSteedRaceDialogAndOpensShopWindow()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(6_001, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(6, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("horse race", dialog.Text);
        Assert.False(ContainsCjk(dialog.Text), dialog.Text);

        var join = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, join.OptionId);
        Assert.Equal("Sign~me~up.", join.Text);

        var open = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(2, open.OptionId);
        Assert.Equal("Open~shop.", open.Text);

        var close = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        var avatar = AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(63, avatar.InputMaxLength);
        AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 2, interactType: 1, npcId: 0)));

        Assert.Equal(7, sent.Count);
        AssertOpenClientWindowAction(sent[^1], activeSession, windowId: 464);
        var output = log.ToString();
        Assert.Contains("source=NpcHandler.SteedRace", output);
        Assert.Contains("window=464", output);
    }

    [Fact]
    public async Task HandlerSendsTranslatedLotteryDialogAndOpensWindow()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(161_618, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Would you like to try your luck?", dialog.Text);
        Assert.False(ContainsCjk(dialog.Text), dialog.Text);

        var open = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, open.OptionId);
        Assert.Equal("Open~lottery~window.", open.Text);

        var close = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, close.OptionId);
        var avatar = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(286, avatar.InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(6, sent.Count);
        AssertOpenClientWindowAction(sent[^1], activeSession, windowId: 924);
        var output = log.ToString();
        Assert.Contains("source=NpcHandler.openlottery", output);
        Assert.Contains("window=924", output);
    }

    [Fact]
    public async Task HandlerSendsTranslatedEquipmentBlacksmithDialogAndOpensWindow()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(9_071, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(6, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("enhance your equipment", dialog.Text);
        Assert.False(ContainsCjk(dialog.Text), dialog.Text);

        var open = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, open.OptionId);
        Assert.Equal("Lower~Equipment~Level.", open.Text);

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(7, sent.Count);
        AssertOpenClientWindowAction(sent[^1], activeSession, windowId: 506);
        var output = log.ToString();
        Assert.Contains("source=NpcHandler.EquipmentBlacksmith", output);
        Assert.Contains("window=506", output);
    }

    [Theory]
    [InlineData(6_002U, 502U, "NpcHandler.GarmentShopKeeper")]
    [InlineData(10_020U, 741U, "NpcHandler.Beautician")]
    public async Task HandlerOpensSourceBackedDirectNpcClientWindows(
        uint npcId,
        uint windowId,
        string expectedSource)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 0, interactType: 1).ToFrameBytes()));

        var bytes = Assert.Single(sent);
        AssertOpenClientWindowAction(bytes, session, windowId);
        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacyNpc OpenClientWindow action", output);
        Assert.Contains($"source={expectedSource}", output);
        Assert.Contains($"window={windowId}", output);
    }

    [Fact]
    public async Task HandlerTeleportsSourceBackedJobCenterByClass()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(43_433, optionId: 1, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId: 10_451, expectedX: 82, expectedY: 64);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(10_451, teleportedSession.Character.MapId);
        Assert.Contains("source=NpcHandler.joinJopCentr", log.ToString());
    }

    [Theory]
    [InlineData(20_201U, "NpcHandler.leveHuntMap1")]
    [InlineData(20_202U, "NpcHandler.leveHuntMap2")]
    [InlineData(20_203U, "NpcHandler.leveHuntMap3")]
    [InlineData(20_204U, "NpcHandler.leveHuntMap4")]
    [InlineData(20_205U, "NpcHandler.leveHuntMap5")]
    [InlineData(20_206U, "NpcHandler.leveHuntMap6")]
    public async Task HandlerSendsSourceBackedLevelHuntExitDialog(uint npcId, string expectedSource)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(npcId, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Equal(5, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal("Hey Brothr HeroOne Do You Want Leve this map to Twin City", dialog.Text);

        var yesOption = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, yesOption.OptionId);
        Assert.Equal("Yes Plase", yesOption.Text);

        var closeOption = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        Assert.Equal("See~you~later.", closeOption.Text);

        var avatar = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(76, avatar.InputMaxLength);
        AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Finish);
        Assert.Contains($"source={expectedSource}", log.ToString());
    }

    [Fact]
    public async Task HandlerUsesActiveNpcForLevelHuntExitReplyTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_204, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(10, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 5, session, expectedMapId: 1_002, expectedX: 410, expectedY: 355);
        Assert.True(sessions.TryGet(connection, out var teleportedSession));
        Assert.NotNull(teleportedSession);
        Assert.Equal(1_002, teleportedSession.Character.MapId);
        Assert.Equal(410, teleportedSession.Character.X);
        Assert.Equal(355, teleportedSession.Character.Y);
        Assert.Equal(session.Character.MapId, teleportedSession.Character.PreviousMapId);
        Assert.Equal(session.Character.X, teleportedSession.Character.PreviousMapX);
        Assert.Equal(session.Character.Y, teleportedSession.Character.PreviousMapY);
        Assert.Contains("source=NpcHandler.leveHuntMap4", log.ToString());
    }

    [Fact]
    public async Task HandlerLogsKnownLegacyScriptForUnimplementedNpcOption()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            Session: session,
            Sessions: sessions);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(15_641, optionId: 99, interactType: 1).ToFrameBytes()));

        Assert.Single(sent);
        var fallback = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("legacy script is not modeled", fallback.Text);
        Assert.Contains("legacyScript=NpcHandler.Teleportall option=99", log.ToString());
        Assert.Contains("PARITY-GAP category=npc severity=warning gap=missing-npc-script", log.ToString());
        Assert.Contains("actionCategory=UnknownOption", log.ToString());
        Assert.Contains("unsupportedReason=MissingOption", log.ToString());
    }

    [Fact]
    public async Task HandlerSkipsLegacyNpcServerWithoutSession()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var context = new WorldPacketHandlerContext(connection, log);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            context,
            ReadFrame(LegacyNpcServerRequestPacket.Script(20_499, optionId: 0, interactType: 1).ToFrameBytes()));

        Assert.Empty(sent);
        var output = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpcServer session=none npcId=20499", output);
        Assert.Contains("no character session is available", output);
        Assert.DoesNotContain("unknown world opcode", output);
    }

    private static WorldSession CreateDragonIslandSession()
    {
        var session = CreateSession();
        return session with
        {
            Character = session.Character with
            {
                MapId = 10_137,
                X = 93,
                Y = 403,
                PreviousMapId = 10_137,
                PreviousMapX = 93,
                PreviousMapY = 403,
            },
        };
    }

    private static WorldDataCatalog CreateWorldData(
        Npc npc,
        IReadOnlyList<ExchangeShopRow>? exchangeShopGoods = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(DateTimeOffset.UnixEpoch, "fixture://legacy-npc-click", new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
            itemAddBonuses: [],
            levelExperience: [],
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
            npcs: [npc],
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
            exchangeShopGoods: exchangeShopGoods ?? [],
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }

    private static WorldSession CreateSession()
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
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static bool ContainsCjk(string value)
    {
        return value.Any(character => character is >= '\u3400' and <= '\u9FFF');
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static byte[] BuildLegacyNpcDialogReply(byte optionId, byte interactType, uint npcId)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
        writer.WriteUInt32(0, npcId);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = interactType;
        writer.Payload[8] = 0;
        writer.WriteUInt16(9, 0);
        return writer.ToArray();
    }

    private static void AssertWarehouseOpenAction(byte[] bytes, WorldSession session)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(4U, action.DwParam);
        Assert.Equal(MsgActionType.OpenClientWindow, action.Type);
        Assert.Equal((uint)session.Character.X, action.PositionX);
        Assert.Equal((uint)session.Character.Y, action.PositionY);
    }

    private static void AssertOpenClientWindowAction(byte[] bytes, WorldSession session, uint windowId)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(windowId, action.DwParam);
        Assert.Equal(MsgActionType.OpenClientWindow, action.Type);
        Assert.Equal((uint)session.Character.X, action.PositionX);
        Assert.Equal((uint)session.Character.Y, action.PositionY);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static void AssertScalarUpdate(byte[] bytes, LegacyMsgUpdateDataType dataType, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Single(packet.Updates);
        Assert.Equal(new LegacyMsgUpdateEntry(dataType, value), packet.Updates[0]);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes, uint expectedItemId)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedItemId, packet.ItemId);
        return packet;
    }

    private static LegacyGameItemPacket AssertGameItemIn(byte[] bytes, IReadOnlyCollection<uint> expectedItemIds)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Contains(packet.ItemId, expectedItemIds);
        return packet;
    }

    private static void AssertPlayerEffect(byte[] bytes, uint uid, string effectName)
    {
        Assert.True(LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(LegacyStringId.Effect, packet.Id);
        Assert.Equal([effectName], packet.Strings);
    }

    private static void AssertRemoveInventory(byte[] bytes, uint itemUid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(itemUid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static void AssertLegacyNpcOpenGuiAction(
        byte[] bytes,
        WorldSession session,
        uint npcId,
        bool useMapIdField)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(32U, action.DwParam);
        Assert.Equal(MsgActionType.OpenGuiNpc, action.Type);
        Assert.Equal((int)npcId, action.NpcId);
        Assert.Equal((uint)session.Character.X, action.PositionX);
        Assert.Equal((uint)session.Character.Y, action.PositionY);
        if (useMapIdField)
        {
            Assert.Equal((uint)session.Character.MapId, action.MapId);
            Assert.Equal(0L, action.DwParam3);
        }
        else
        {
            Assert.Equal(0U, action.MapId);
            Assert.Equal(session.Character.MapId, action.DwParam3);
        }
    }

    private static void AssertLegacyNpcTeleportAction(
        byte[] bytes,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var action));
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(expectedMapId, action.DwParam);
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(expectedX, action.PositionX);
        Assert.Equal(expectedY, action.PositionY);
        Assert.Equal((long)expectedMapId, action.DwParam3);
    }

    private static void AssertLegacyNpcTeleportTransition(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        AssertLegacyNpcTeleportAction(sent[startIndex], session, expectedMapId, expectedX, expectedY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 1]), out var merchantStartup));
        Assert.Equal((uint)session.Character.Uid, merchantStartup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)expectedMapId, merchantStartup.DwParam3);
        Assert.Equal(expectedX, merchantStartup.PositionX);
        Assert.Equal(expectedY, merchantStartup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 2]), out var stopVending));
        Assert.Equal((uint)session.Character.Uid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(expectedMapId, stopVending.DwParam);
        Assert.Equal((long)expectedMapId, stopVending.DwParam3);
        Assert.Equal(expectedX, stopVending.PositionX);
        Assert.Equal(expectedY, stopVending.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 3]), out var color));
        Assert.Equal((uint)session.Character.Uid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);
        Assert.Equal(expectedX, color.PositionX);
        Assert.Equal(expectedY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[startIndex + 4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(expectedMapId, mapsById: null), mapStatus);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
