using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonWarInvitationMessageBoxTests
{
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task HandlerLegacyNpcReplyAcceptsPendingDragonWarInvitationWhenIdle()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var dragonWarStates = new LegacyDragonWarRuntimeStateStore();
        var opened = dragonWarStates.Open(new DateTimeOffset(2026, 6, 28, 19, 0, 0, TimeSpan.Zero));
        Assert.NotNull(opened?.MessageBoxPrompt);
        var prompt = opened.MessageBoxPrompt;
        var session = CreateSession(
            CreateSnapshot(
                mapId: 1015,
                x: 830,
                y: 584,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DragonWarScore"] = "95",
                    ["DragonWarHits"] = "7",
                    [LegacyRuntimeMessageBoxPrompt.PendingAttribute] = LegacyRuntimeMessageBoxPrompt.DragonWarInvitationKey,
                    [LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute] = prompt.MapId.ToString(),
                    [LegacyRuntimeMessageBoxPrompt.DestinationXAttribute] = prompt.X.ToString(),
                    [LegacyRuntimeMessageBoxPrompt.DestinationYAttribute] = prompt.Y.ToString(),
                    [LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute] = prompt.DynamicId.ToString(),
                    [LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute] = prompt.Seconds.ToString(),
                }));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions,
            DragonWarStates: dragonWarStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(BuildLegacyNpcDialogReply(0, LegacyNpcDialogRequestPacket.MessageBoxInteractType)));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(LegacyDragonWarRuntimeStateStore.DragonWarMapId, updated.Character.MapId);
        Assert.Equal(1015, updated.Character.PreviousMapId);
        Assert.Equal(830, updated.Character.PreviousMapX);
        Assert.Equal(584, updated.Character.PreviousMapY);
        Assert.Equal("0", updated.Character.Attributes["DragonWarScore"]);
        Assert.Equal("0", updated.Character.Attributes["DragonWarHits"]);
        Assert.True(updated.Character.Attributes.TryGetValue("DynamicID", out var dynamicId));
        Assert.True(uint.TryParse(dynamicId, out var parsedDynamicId));
        Assert.NotEqual(0U, parsedDynamicId);
        AssertNoPendingMessageBoxAttributes(updated.Character);

        Assert.Equal(5, sent.Count);
        var expectedMapId = (uint)LegacyDragonWarRuntimeStateStore.DragonWarMapId;
        var expectedX = (uint)updated.Character.X;
        var expectedY = (uint)updated.Character.Y;

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal((uint)updated.Character.Uid, teleport.ObjId);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(expectedMapId, teleport.DwParam);
        Assert.Equal((long)expectedMapId, teleport.DwParam3);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var merchantStartup));
        Assert.Equal((uint)updated.Character.Uid, merchantStartup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)expectedMapId, merchantStartup.DwParam3);
        Assert.Equal(expectedX, merchantStartup.PositionX);
        Assert.Equal(expectedY, merchantStartup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal((uint)updated.Character.Uid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(expectedMapId, stopVending.DwParam);
        Assert.Equal((long)expectedMapId, stopVending.DwParam3);
        Assert.Equal(expectedX, stopVending.PositionX);
        Assert.Equal(expectedY, stopVending.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var mapColor));
        Assert.Equal((uint)updated.Character.Uid, mapColor.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, mapColor.Type);
        Assert.Equal(0U, mapColor.DwParam);
        Assert.Equal(expectedX, mapColor.PositionX);
        Assert.Equal(expectedY, mapColor.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(LegacyDragonWarRuntimeStateStore.DragonWarMapId, mapsById: null), mapStatus);
    }

    [Fact]
    public async Task HandlerLegacyNpcReplyCancelsPendingDragonWarInvitation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var dragonWarStates = new LegacyDragonWarRuntimeStateStore();
        var opened = dragonWarStates.Open(new DateTimeOffset(2026, 6, 28, 19, 0, 0, TimeSpan.Zero));
        Assert.NotNull(opened?.MessageBoxPrompt);
        var prompt = opened.MessageBoxPrompt;
        var session = CreateSession(
            CreateSnapshot(
                mapId: 1015,
                x: 830,
                y: 584,
                CreatePendingDragonWarAttributes(prompt)));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions,
            DragonWarStates: dragonWarStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(BuildLegacyNpcDialogReply(255, LegacyNpcDialogRequestPacket.MessageBoxInteractType)));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1015, updated.Character.MapId);
        Assert.Equal(830, updated.Character.X);
        Assert.Equal(584, updated.Character.Y);
        AssertNoPendingMessageBoxAttributes(updated.Character);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task HandlerLegacyNpcReplyAcceptSkipsPendingDragonWarInvitationWhenUnopened()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var dragonWarStates = new LegacyDragonWarRuntimeStateStore();
        var session = CreateSession(
            CreateSnapshot(
                mapId: 1015,
                x: 830,
                y: 584,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyRuntimeMessageBoxPrompt.PendingAttribute] = LegacyRuntimeMessageBoxPrompt.DragonWarInvitationKey,
                    [LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute] = "1002",
                    [LegacyRuntimeMessageBoxPrompt.DestinationXAttribute] = "319",
                    [LegacyRuntimeMessageBoxPrompt.DestinationYAttribute] = "256",
                    [LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute] = "0",
                    [LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute] = "60",
                }));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions,
            DragonWarStates: dragonWarStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(BuildLegacyNpcDialogReply(0, LegacyNpcDialogRequestPacket.MessageBoxInteractType)));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1015, updated.Character.MapId);
        Assert.Equal(830, updated.Character.X);
        Assert.Equal(584, updated.Character.Y);
        AssertNoPendingMessageBoxAttributes(updated.Character);
        Assert.DoesNotContain("DynamicID", updated.Character.Attributes.Keys);
        Assert.Contains("DragonWar", log.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task HandlerBlockedDragonWarAcceptanceClearsPendingCallbackWithoutTeleporting()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var dragonWarStates = new LegacyDragonWarRuntimeStateStore();
        var opened = dragonWarStates.Open(new DateTimeOffset(2026, 6, 28, 19, 0, 0, TimeSpan.Zero));
        Assert.NotNull(opened?.MessageBoxPrompt);
        var session = CreateSession(
            CreateSnapshot(
                mapId: 6_001,
                x: 50,
                y: 50,
                CreatePendingDragonWarAttributes(opened.MessageBoxPrompt)));
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                Sessions: sessions,
                DragonWarStates: dragonWarStates),
            ReadFrame(BuildLegacyNpcDialogReply(0, LegacyNpcDialogRequestPacket.MessageBoxInteractType)));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(6_001, updated.Character.MapId);
        Assert.Equal(50, updated.Character.X);
        Assert.Equal(50, updated.Character.Y);
        AssertNoPendingMessageBoxAttributes(updated.Character);
        Assert.Empty(sent);
        Assert.Contains("denied legacy GameClient.Teleport preflight", log.ToString());
    }

    private static Dictionary<string, string> CreatePendingDragonWarAttributes(LegacyRuntimeMessageBoxPrompt prompt)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [LegacyRuntimeMessageBoxPrompt.PendingAttribute] = LegacyRuntimeMessageBoxPrompt.DragonWarInvitationKey,
            [LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute] = prompt.MapId.ToString(),
            [LegacyRuntimeMessageBoxPrompt.DestinationXAttribute] = prompt.X.ToString(),
            [LegacyRuntimeMessageBoxPrompt.DestinationYAttribute] = prompt.Y.ToString(),
            [LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute] = prompt.DynamicId.ToString(),
            [LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute] = prompt.Seconds.ToString(),
        };
    }

    private static void AssertNoPendingMessageBoxAttributes(CharacterSnapshot character)
    {
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.PendingAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.DestinationXAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.DestinationYAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute));
        Assert.False(character.Attributes.ContainsKey(LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute));
    }

    private static WorldSession CreateSession(CharacterSnapshot snapshot)
    {
        return new WorldSession(
            AccountId: snapshot.Uid,
            Username: snapshot.Name,
            Character: snapshot,
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(
        long mapId,
        long x,
        long y,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 140,
            Experience: 0,
            MapId: mapId,
            X: x,
            Y: y,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 0,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5_000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 7,
            SourceFile: "",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static byte[] BuildLegacyNpcDialogReply(byte optionId, byte interactType)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
        writer.WriteUInt32(0, 0);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = interactType;
        writer.Payload[8] = 0;
        writer.WriteUInt16(9, 0);
        return writer.ToArray();
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
