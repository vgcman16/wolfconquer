using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySuperGuildWarGateNpcTests
{
    [Theory]
    [InlineData(516_076U, "NpcHandler.SuperGuildWar_CasteleLeftGate")]
    [InlineData(516_077U, "NpcHandler.SuperGuildWar_CasteleRightGate")]
    [InlineData(516_078U, "NpcHandler.SuperGuildWar_LeftGate")]
    [InlineData(516_079U, "NpcHandler.SuperGuildWar_MidleGate")]
    [InlineData(516_080U, "NpcHandler.SuperGuildWar_RightGate")]
    public async Task RootDialogUsesSourceBackedGateOptions(uint npcId, string sourceMethod)
    {
        var result = await InvokeGateHandlerAsync(npcId, optionId: 0);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Select the option you want to pursue.");
        Assert.Contains(replies, reply => reply.Text == "Open the gate" && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Close gate." && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.Text == "Get inside." && reply.OptionId == 3);
        Assert.Contains(replies, reply => reply.Text == "Not interested." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains($"source={sourceMethod}", result.Log);
    }

    [Theory]
    [InlineData(516_076U, "NpcHandler.SuperGuildWar_CasteleLeftGate")]
    [InlineData(516_077U, "NpcHandler.SuperGuildWar_CasteleRightGate")]
    [InlineData(516_078U, "NpcHandler.SuperGuildWar_LeftGate")]
    [InlineData(516_079U, "NpcHandler.SuperGuildWar_MidleGate")]
    [InlineData(516_080U, "NpcHandler.SuperGuildWar_RightGate")]
    public async Task StatefulGateBranchesAreFencedWithoutWinnerAndFurnitureRuntime(uint npcId, string sourceMethod)
    {
        foreach (var optionId in new byte[] { 1, 2, 3 })
        {
            var result = await InvokeGateHandlerAsync(npcId, optionId);

            Assert.True(result.Handled);
            Assert.Equal(1_002, result.UpdatedSession.Character.MapId);
            Assert.Equal(430, result.UpdatedSession.Character.X);
            Assert.Equal(378, result.UpdatedSession.Character.Y);
            Assert.DoesNotContain(ParseMsgActions(result.Sent), action => action.Type == MsgActionType.Teleport);

            var replies = ParseNpcReplies(result.Sent);
            Assert.Contains(replies, reply => reply.Text!.StartsWith("Super Guild War gate", StringComparison.Ordinal));
            Assert.Contains(replies, reply => reply.Text == "Okay." && reply.OptionId == byte.MaxValue);
            Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
            Assert.Contains("PARITY-GAP LegacyNpc SuperGuildWarGate", result.Log);
            Assert.Contains($"source={sourceMethod}", result.Log);
            Assert.Contains("MsgSchedules.SuperGuildWar", result.Log);
        }
    }

    [Fact]
    public async Task UnknownNpcIsNotHandled()
    {
        var result = await InvokeGateHandlerAsync(777_777, optionId: 0);

        Assert.False(result.Handled);
        Assert.Empty(result.Sent);
    }

    private static async Task<GateInvokeResult> InvokeGateHandlerAsync(uint npcId, byte optionId)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySuperGuildWarGateNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new GateInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession()
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
            ["GuildID"] = "77",
            ["GuildRank"] = "100",
        };

        return new WorldSession(
            AccountId: 100,
            Username: "super-guild-war-gate-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_700,
                Name: "GateHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
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
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
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

    private static IReadOnlyList<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in sent)
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static bool IsAscii(string value)
    {
        return value.All(static character => character <= 127);
    }

    private sealed record GateInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
