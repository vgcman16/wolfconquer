using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAdminUtilityNpcTests
{
    private const uint Stuff12NpcId = 22_224;
    private const uint StuffNpcId = 77_839;
    private const uint InnerPowerBookNpcId = 18_786;

    [Theory]
    [InlineData(StuffNpcId, "NpcHandler.Stuff")]
    [InlineData(Stuff12NpcId, "NpcHandler.Stuff12")]
    public async Task AdminStuffNpcsAreHiddenFromNormalPlayers(uint npcId, string sourceMethod)
    {
        var result = await InvokeAsync(npcId, optionId: 0);

        Assert.True(result.Handled);
        Assert.Empty(result.Sent);
        Assert.Contains("ignored LegacyNpc admin utility for non-privileged character", result.Log);
        Assert.Contains(sourceMethod, result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc AdminUtility", result.Log);
    }

    [Theory]
    [InlineData(StuffNpcId, "NpcHandler.Stuff", "Legacy admin equipment selector", "Level 140.", 60)]
    [InlineData(Stuff12NpcId, "NpcHandler.Stuff12", "Legacy advanced admin equipment selector", "Max weapon proficiency.", 149)]
    public async Task AdminStuffRootDialogsAreEnglishAndExposeLegacyOptions(
        uint npcId,
        string sourceMethod,
        string expectedText,
        string expectedOptionText,
        byte expectedOptionId)
    {
        var result = await InvokeAsync(npcId, optionId: 0, gmName: true);

        Assert.True(result.Handled);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == expectedOptionText && reply.OptionId == expectedOptionId);
        Assert.Contains(sourceMethod, result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc AdminUtility", result.Log);
        AssertAsciiReplies(result.Sent);
    }

    [Theory]
    [InlineData(StuffNpcId, 60, "Level 140 admin service is disabled.", "NpcHandler.Stuff")]
    [InlineData(StuffNpcId, 89, "Admin item grants are disabled.", "NpcHandler.Stuff")]
    [InlineData(Stuff12NpcId, 149, "Max weapon proficiency admin service is disabled.", "NpcHandler.Stuff12")]
    [InlineData(Stuff12NpcId, 162, "Level 140 admin service is disabled.", "NpcHandler.Stuff12")]
    public async Task AdminStuffGrantLevelAndProficiencyBranchesAreFenced(
        uint npcId,
        byte optionId,
        string expectedText,
        string sourceMethod)
    {
        var result = await InvokeAsync(npcId, optionId, gmName: true);

        Assert.True(result.Handled);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text == expectedText);
        Assert.Contains("PARITY-GAP LegacyNpc AdminUtility", result.Log);
        Assert.Contains(sourceMethod, result.Log);
        Assert.Equal(100, result.UpdatedSession.Character.Level);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
    }

    [Fact]
    public async Task AdminStuffRootDialogAllowsProjectManagerAttribute()
    {
        var result = await InvokeAsync(StuffNpcId, optionId: 0, projectManager: true);

        Assert.True(result.Handled);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains("admin equipment selector", StringComparison.Ordinal));
        Assert.DoesNotContain("non-privileged", result.Log);
    }

    [Fact]
    public async Task InnerPowerBookNpcRejectsNonSecondReborn()
    {
        var result = await InvokeAsync(InnerPowerBookNpcId, optionId: 0, reborn: 1);

        Assert.True(result.Handled);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains("second rebirth", StringComparison.Ordinal));
        Assert.Contains("source=NpcHandler.InnerPowerNpc", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc AdminUtility", result.Log);
    }

    [Fact]
    public async Task InnerPowerBookNpcShowsTutorialAndReportsMissingBookDataWithoutMutation()
    {
        var root = await InvokeAsync(InnerPowerBookNpcId, optionId: 0, reborn: 2);

        Assert.True(root.Handled);
        Assert.Contains(ParseNpcReplies(root.Sent), reply => reply.Text.Contains("essence of Inner Power", StringComparison.Ordinal));
        Assert.Contains(ParseNpcReplies(root.Sent), reply => reply.Text == "How to acquire Inner Power?" && reply.OptionId == 1);

        var info = await InvokeAsync(InnerPowerBookNpcId, optionId: 1, reborn: 2);
        Assert.True(info.Handled);
        Assert.Contains(ParseNpcReplies(info.Sent), reply => reply.Text.Contains("Potency Points", StringComparison.Ordinal));
        Assert.DoesNotContain("PARITY-GAP LegacyNpc AdminUtility", info.Log);

        var grant = await InvokeAsync(InnerPowerBookNpcId, optionId: 2, reborn: 2);
        Assert.True(grant.Handled);
        Assert.Contains(ParseNpcReplies(grant.Sent), reply => reply.Text == "The first Inner Power book is not available in this runtime.");
        Assert.Contains("PARITY-GAP LegacyNpc AdminUtility", grant.Log);
        Assert.Contains("NpcHandler.InnerPowerNpc", grant.Log);
        Assert.Empty(grant.UpdatedSession.PlayerItems.InventoryItems);
    }

    private static async Task<AdminUtilityInvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        int reborn = 0,
        bool gmName = false,
        bool projectManager = false)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(reborn, gmName, projectManager);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new AdminUtilityInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(int reborn, bool gmName, bool projectManager)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (projectManager)
        {
            attributes["ProjectManager"] = "1";
        }

        return new WorldSession(
            AccountId: 100,
            Username: "admin-utility-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: gmName ? "AdminUtilityHero[GM]" : "AdminUtilityHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: reborn,
                Level: 100,
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertAsciiReplies(IEnumerable<byte[]> sent)
    {
        foreach (var reply in ParseNpcReplies(sent))
        {
            if (!string.IsNullOrEmpty(reply.Text))
            {
                Assert.True(reply.Text.All(static character => character <= 127), reply.Text);
            }
        }
    }

    private sealed record AdminUtilityInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
