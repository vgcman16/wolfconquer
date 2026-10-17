using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTopInHoursInvitationMessageBoxTests
{
    private const string PendingMessageBoxAttribute = "PendingMessageBox";
    private const string PendingTopInHoursMinute32MessageBox = "TopInHours:32";
    private const string PendingTopInHoursMessageBox = LegacyRuntimeMessageBoxPrompt.TopInHoursInvitationKey;
    private const string TopInHoursMinute32Text =
        "TopInHours [xx:32] has begun! Would you like to join?";

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public void TopInHoursMinute32DueInvitationExposesJoinMessageBoxMetadata()
    {
        var now = new DateTimeOffset(2026, 6, 5, 8, 32, 6, TimeSpan.Zero);

        Assert.True(LegacyTopInHoursInvitationScheduler.TryGetInvitation(now, out var invitation));

        Assert.Equal(32, invitation.Minute);
        Assert.Equal(6, invitation.Second);
        Assert.Equal(TopInHoursMinute32Text, invitation.Name);
        Assert.Equal((ushort)1002, invitation.MapId);
        Assert.Equal((ushort)363, invitation.X);
        Assert.Equal((ushort)304, invitation.Y);
        Assert.Equal((ushort)0, invitation.DynamicId);

        var messageBox = LegacyNpcReplyPacket.ForMessageBox(invitation.Name);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(messageBox.ToFrameBytes()), out var parsed));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, parsed.InteractType);
        Assert.Equal(ushort.MaxValue, parsed.InputMaxLength);
        Assert.Equal(0, parsed.OptionId);
        Assert.Equal(TopInHoursMinute32Text, parsed.Text);
    }

    [Fact]
    public void RuntimeTickerTopInHoursMinute32DueUpdateDoesNotAutoSendMessageBoxPrompt()
    {
        var start = new DateTimeOffset(2026, 6, 5, 8, 32, 5, TimeSpan.Zero);
        var ticker = new LegacyRuntimeUpdateTicker(
            CreateSnapshot(
                mapId: 1002,
                x: 705,
                y: 577,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            start,
            hasOverwhelmRune: false,
            hasRevelatorStartupEffect: false);

        _ = ticker.CreateDueUpdates(start);
        var update = Assert.Single(ticker.CreateDueUpdates(start.AddSeconds(1)));

        Assert.Null(update.MessageBoxPrompt);
    }

    [Theory]
    [InlineData(0, 1002, 363, 304)]
    [InlineData(255, 1015, 830, 584)]
    public async Task HandlerLegacyNpcReplyResolvesPendingTopInHoursMessageBox(
        byte optionId,
        long expectedMapId,
        long expectedX,
        long expectedY)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            CreateSnapshot(
                mapId: 1015,
                x: 830,
                y: 584,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [PendingMessageBoxAttribute] = PendingTopInHoursMinute32MessageBox,
                }));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(BuildLegacyNpcDialogReply(optionId, LegacyNpcDialogRequestPacket.MessageBoxInteractType)));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(expectedMapId, updated.Character.MapId);
        Assert.Equal(expectedX, updated.Character.X);
        Assert.Equal(expectedY, updated.Character.Y);
        AssertNoPendingMessageBoxAttributes(updated.Character);

        if (optionId == 0)
        {
            Assert.Equal(1015, updated.Character.PreviousMapId);
            Assert.Equal(830, updated.Character.PreviousMapX);
            Assert.Equal(584, updated.Character.PreviousMapY);
            Assert.Equal("0", updated.Character.Attributes["DynamicID"]);
            Assert.Equal("0", updated.Character.Attributes["PDinamycID"]);
            AssertLegacyTeleportTransition(
                sent,
                updated,
                checked((uint)expectedMapId),
                checked((uint)expectedX),
                checked((uint)expectedY));
        }
        else
        {
            Assert.Equal(1000, updated.Character.PreviousMapId);
            Assert.Equal(500, updated.Character.PreviousMapX);
            Assert.Equal(650, updated.Character.PreviousMapY);
            Assert.False(updated.Character.Attributes.ContainsKey("DynamicID"));
            Assert.False(updated.Character.Attributes.ContainsKey("PDinamycID"));
            Assert.Empty(sent);
            Assert.Contains("canceled TopInHours invitation message box", log.ToString());
        }
    }

    [Fact]
    public async Task HandlerLegacyNpcReplyAcceptsTopInHoursMessageBoxPromptAttributes()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(
            CreateSnapshot(
                mapId: 1015,
                x: 830,
                y: 584,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [LegacyRuntimeMessageBoxPrompt.PendingAttribute] = PendingTopInHoursMessageBox,
                    [LegacyRuntimeMessageBoxPrompt.DestinationMapIdAttribute] = "1002",
                    [LegacyRuntimeMessageBoxPrompt.DestinationXAttribute] = "363",
                    [LegacyRuntimeMessageBoxPrompt.DestinationYAttribute] = "304",
                    [LegacyRuntimeMessageBoxPrompt.DestinationDynamicIdAttribute] = "0",
                    [LegacyRuntimeMessageBoxPrompt.TimeoutSecondsAttribute] = "60",
                }));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(BuildLegacyNpcDialogReply(0, LegacyNpcDialogRequestPacket.MessageBoxInteractType)));

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1002, updated.Character.MapId);
        Assert.Equal(363, updated.Character.X);
        Assert.Equal(304, updated.Character.Y);
        Assert.Equal(1015, updated.Character.PreviousMapId);
        Assert.Equal(830, updated.Character.PreviousMapX);
        Assert.Equal(584, updated.Character.PreviousMapY);
        Assert.Equal("0", updated.Character.Attributes["DynamicID"]);
        Assert.Equal("0", updated.Character.Attributes["PDinamycID"]);
        AssertNoPendingMessageBoxAttributes(updated.Character);
        AssertLegacyTeleportTransition(sent, updated, expectedMapId: 1002, expectedX: 363, expectedY: 304);
    }

    private static void AssertLegacyTeleportTransition(
        IReadOnlyList<byte[]> sent,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        Assert.Equal(5, sent.Count);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var teleport));
        Assert.Equal((uint)session.Character.Uid, teleport.ObjId);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(expectedMapId, teleport.DwParam);
        Assert.Equal((long)expectedMapId, teleport.DwParam3);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var merchantStartup));
        Assert.Equal((uint)session.Character.Uid, merchantStartup.ObjId);
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)expectedMapId, merchantStartup.DwParam3);
        Assert.Equal(expectedX, merchantStartup.PositionX);
        Assert.Equal(expectedY, merchantStartup.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal((uint)session.Character.Uid, stopVending.ObjId);
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(expectedMapId, stopVending.DwParam);
        Assert.Equal((long)expectedMapId, stopVending.DwParam3);
        Assert.Equal(expectedX, stopVending.PositionX);
        Assert.Equal(expectedY, stopVending.PositionY);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal((uint)session.Character.Uid, color.ObjId);
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);
        Assert.Equal(expectedX, color.PositionX);
        Assert.Equal(expectedY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(expectedMapId, mapsById: null), mapStatus);
    }

    private static void AssertNoPendingMessageBoxAttributes(CharacterSnapshot character)
    {
        Assert.False(character.Attributes.ContainsKey(PendingMessageBoxAttribute));
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
