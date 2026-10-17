using System.Buffers;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTowerOfMysteryNpcTests
{
    private const uint TowerOfMysteryLayerChangeNpcId = 19_139;
    private const uint TowerofMysteryChallenge1NpcId = 200_020;
    private const uint TowerofMysteryConductorNpcId = 19_231;
    private const uint CloudSweeperNpcId = 19_128;
    private const uint UnrelatedNpcId = 999_001;

    [Fact]
    public async Task HandlerConductorRootDialogPreservesTowerOptionIds()
    {
        var result = await InvokeTowerHandlerAsync(TowerofMysteryConductorNpcId, optionId: 0);

        Assert.True(result.Handled);
        Assert.Equal(7, result.Sent.Count);
        var firstText = AssertNpcReply(result.Sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Tower of Mystery", firstText.Text);
        var enterOption = AssertNpcReply(result.Sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, enterOption.OptionId);
        Assert.Equal("Enter~the~Tower~of~Mystery.", enterOption.Text);
        var returnOption = AssertNpcReply(result.Sent[3], LegacyNpcReplyInteractType.Option);
        Assert.Equal(2, returnOption.OptionId);
        Assert.Equal("Return~to~Dragon~Island.", returnOption.Text);
        var closeOption = AssertNpcReply(result.Sent[4], LegacyNpcReplyInteractType.Option);
        Assert.Equal(byte.MaxValue, closeOption.OptionId);
        var avatar = AssertNpcReply(result.Sent[5], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(85, avatar.InputMaxLength);
        AssertNpcReply(result.Sent[6], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.TowerofMysteryConductor", result.Log);
    }

    [Theory]
    [InlineData(CloudSweeperNpcId, 1, "NpcHandler.CloudSweeper")]
    [InlineData(TowerOfMysteryLayerChangeNpcId, 2, "NpcHandler.TowerOfMysteryLayerChange")]
    public async Task HandlerSafeReturnOptionsTeleportToDragonIsland(uint npcId, byte optionId, string source)
    {
        var result = await InvokeTowerHandlerAsync(npcId, optionId);

        Assert.True(result.Handled);
        Assert.Equal(5, result.Sent.Count);
        AssertLegacyNpcTeleportTransition(result.Sent, result.Session, expectedMapId: 10_137, expectedX: 453, expectedY: 483);
        Assert.Equal(10_137, result.UpdatedSession.Character.MapId);
        Assert.Equal(453, result.UpdatedSession.Character.X);
        Assert.Equal(483, result.UpdatedSession.Character.Y);
        Assert.Contains($"source={source}", result.Log);
    }

    [Fact]
    public async Task HandlerChallengeStartLogsParityGapAndSendsSafeMessageBox()
    {
        var result = await InvokeTowerHandlerAsync(TowerofMysteryChallenge1NpcId, optionId: 1);

        Assert.True(result.Handled);
        var reply = Assert.Single(ParseNpcReplies(result.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal(
            "The Tower of Mystery challenge manager is not available yet. Your character state was not changed.",
            reply.Text);
        Assert.Equal(result.Session.Character.MapId, result.UpdatedSession.Character.MapId);
        Assert.Contains("PARITY-GAP LegacyNpc TowerOfMystery unsupported event-manager branch", result.Log);
        Assert.Contains("source=NpcHandler.TowerofMysteryChallenge1", result.Log);
        Assert.Contains("option=1", result.Log);
        Assert.Contains("NpcID.cs:199-208", result.Log);
    }

    [Fact]
    public async Task HandlerReturnsFalseForUnrelatedNpc()
    {
        var result = await InvokeTowerHandlerAsync(UnrelatedNpcId, optionId: 0);

        Assert.False(result.Handled);
        Assert.Empty(result.Sent);
        Assert.Same(result.Session, result.UpdatedSession);
    }

    private static async Task<TowerInvokeResult> InvokeTowerHandlerAsync(uint npcId, byte optionId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions);
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryHandleLegacyTowerOfMysteryAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var valueTask = Assert.IsType<ValueTask<bool>>(method.Invoke(
            null,
            [context, session, npcId, optionId, CancellationToken.None]));
        var handled = await valueTask.ConfigureAwait(false);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new TowerInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 100,
            Username: "tower-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "TowerSeeker",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 10_250,
                X: 1_014,
                Y: 1_282,
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

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertLegacyNpcTeleportTransition(
        IReadOnlyList<byte[]> sent,
        WorldSession session,
        uint expectedMapId,
        uint expectedX,
        uint expectedY)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[0]), out var action));
        Assert.Equal((uint)session.Character.Uid, action.ObjId);
        Assert.Equal(expectedMapId, action.DwParam);
        Assert.Equal(MsgActionType.Teleport, action.Type);
        Assert.Equal(expectedX, action.PositionX);
        Assert.Equal(expectedY, action.PositionY);
        Assert.Equal((long)expectedMapId, action.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[1]), out var merchantStartup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, merchantStartup.Type);
        Assert.Equal(2U, merchantStartup.DwParam);
        Assert.Equal((long)expectedMapId, merchantStartup.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(expectedMapId, stopVending.DwParam);
        Assert.Equal((long)expectedMapId, stopVending.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(0U, color.DwParam);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(expectedMapId, mapsById: null), mapStatus);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record TowerInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
