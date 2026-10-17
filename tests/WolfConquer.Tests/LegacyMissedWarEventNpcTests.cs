using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMissedWarEventNpcTests
{
    private const uint WarFightersNpcId = 22_213;
    private const uint EmperorWarNpcId = 43_434;
    private const uint PlayerUid = 1_293_602;
    private const uint RivalPlayerUid = 1_293_603;
    private const long LegacyWarFightersSourceConquerPoints = 5_000_000;
    private const long TunedWarFightersRewardConquerPoints = 300_000;
    private const long LegacyEmperorWarSourceConquerPoints = 5_000_000;
    private const long TunedEmperorWarRewardConquerPoints = 300_000;

    [Theory]
    [InlineData(WarFightersNpcId, "Hi, I manage the War Fighters event. How can I help you? It starts at 10:00.", "NpcHandler.WarFighters")]
    [InlineData(EmperorWarNpcId, "This is Emperor War. It runs from 16:00 to 16:30.", "NpcHandler.EmperorWar")]
    public async Task HandlerSendsTranslatedRootDialogWithSourceOptions(uint npcId, string expectedText, string sourceMethod)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();

        var handled = await WorldPacketHandlers.TryHandleLegacyMissedWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session),
            session,
            npcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == expectedText);
        Assert.Contains(replies, reply => reply.Text == "Enter" && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Reward." && reply.OptionId == 2);
        Assert.Contains(replies, reply => reply.Text == "Just passing by." && reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 63);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains($"source={sourceMethod}", log.ToString());
    }

    [Theory]
    [InlineData(WarFightersNpcId, 11_073U, 170U, 222U, "NpcHandler.WarFighters", "MsgWarFighters.Proces")]
    [InlineData(EmperorWarNpcId, 10_702U, 158U, 170U, "NpcHandler.EmperorWar", "MsgEmperorWar.Proces")]
    public async Task HandlerTeleportsToSourceDestinationAndRevivesWhenEventStateIsAbsent(
        uint npcId,
        uint mapId,
        uint x,
        uint y,
        string sourceMethod,
        string processName)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 430, y: 378, minHitPoints: 0);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyMissedWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(mapId, teleport.DwParam);
        Assert.Equal(x, teleport.PositionX);
        Assert.Equal(y, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(mapId, (uint)updated.Character.MapId);
        Assert.Equal(x, (uint)updated.Character.X);
        Assert.Equal(y, (uint)updated.Character.Y);
        Assert.Equal(1, updated.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains($"source={sourceMethod} map={mapId} x={x} y={y}", output);
        Assert.Contains("PARITY-GAP LegacyNpc MissedWarEvent", output);
        Assert.Contains("Source calls Player.Revive before entry", output);
        Assert.Contains(processName, output);
    }

    [Fact]
    public async Task WarFightersRewardMissingWinnerIsDeniedWithSafeDialog()
    {
        var result = await ClaimWarFightersRewardAsync(
            attributes: CreateWarFightersWinnerAttributes(winnerPlayerUid: "0", winnerName: string.Empty),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertWarFightersRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "There is no War Fighters reward available now.");
        Assert.Contains(replies, reply => reply.Text == "Okay." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.WarFighters option=2", result.Log);
    }

    [Fact]
    public async Task WarFightersRewardActiveProcessIsDeniedWithSafeDialog()
    {
        var result = await ClaimWarFightersRewardAsync(
            attributes: CreateWarFightersWinnerAttributes(process: "Alive"),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertWarFightersRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Please come back when War Fighters is finished.");
        Assert.Contains(replies, reply => reply.Text == "Okay." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.WarFighters option=2", result.Log);
    }

    [Fact]
    public async Task WarFightersRewardWrongPlayerIsDeniedWithSafeDialog()
    {
        var result = await ClaimWarFightersRewardAsync(
            attributes: CreateWarFightersWinnerAttributes(
                winnerPlayerUid: RivalPlayerUid.ToString(CultureInfo.InvariantCulture),
                winnerName: "RivalFighter"),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertWarFightersRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "You do not have any War Fighters reward to claim.");
        Assert.Contains(replies, reply => reply.Text == "Okay." && reply.OptionId == byte.MaxValue);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.WarFighters option=2", result.Log);
    }

    [Fact]
    public async Task WarFightersRewardEligibleWinnerClaimsTunedSourceBundleAndClearsRoundOwner()
    {
        var result = await ClaimWarFightersRewardAsync(
            attributes: CreateWarFightersWinnerAttributes(),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        var expectedConquerPoints = 100 + TunedWarFightersRewardConquerPoints;
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(200, result.UpdatedSession.Character.Money);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["ClassExperience"]);
        Assert.Equal("10000#0#0#", result.UpdatedSession.Character.Attributes["SubProfInfo"]);
        Assert.Equal(3, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_009_003 && item.StackSize == 3);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_050_001 && item.StackSize == 500);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_040_001 && item.StackSize == 500);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_324_226);
        var rewardItems = ParseGameItems(result.Sent);
        Assert.Contains(rewardItems, item => item.ItemId == 3_009_003 && item.StackSize == 3);
        Assert.Contains(rewardItems, item => item.ItemId == 4_050_001 && item.StackSize == 500);
        Assert.Contains(rewardItems, item => item.ItemId == 4_040_001 && item.StackSize == 500);
        Assert.DoesNotContain(rewardItems, item => item.ItemId == 3_324_226);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                IsConquerPointUpdate(entry) &&
                entry.Value == checked((ulong)expectedConquerPoints)));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.ClassExperience));
        AssertWarFightersRoundOwnerCleared(result.UpdatedSession);

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("300000", StringComparison.Ordinal));
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains(
            ParseMsgMessages(result.Sent),
            message =>
                message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                message.Content.Any(text =>
                    text.Contains("MissedWarHero", StringComparison.Ordinal) &&
                    text.Contains("300000", StringComparison.Ordinal) &&
                    text.Contains("WarFighters", StringComparison.Ordinal)));
        Assert.Contains("source=NpcHandler.WarFighters option=2", result.Log);
        Assert.Contains($"sourceConquerPoints={LegacyWarFightersSourceConquerPoints}", result.Log);
        Assert.Contains($"tunedConquerPoints={TunedWarFightersRewardConquerPoints}", result.Log);
        Assert.Contains("items=3009003x3,4050001x500,4040001x500", result.Log);
        Assert.DoesNotContain("3324226", result.Log);
        Assert.Contains("studyPoints=10000", result.Log);
        Assert.Contains("innerPowerPotency=10000", result.Log);
        Assert.Contains("references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs WarFighters option 2", result.Log);
    }

    [Fact]
    public async Task WarFightersRewardDuplicateClaimDoesNotDoublePay()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            conquerPoints: 100,
            money: 200,
            attributes: CreateWarFightersWinnerAttributes());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickWarFightersRewardAsync(connection, log, sessions, session);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        var expectedConquerPoints = 100 + TunedWarFightersRewardConquerPoints;
        Assert.Equal(expectedConquerPoints, firstUpdated.Character.ConquerPoints);

        sent.Clear();
        var secondHandled = await ClickWarFightersRewardAsync(connection, log, sessions, firstUpdated);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(expectedConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(200, secondUpdated.Character.Money);
        Assert.Equal(3, secondUpdated.PlayerItems.InventoryItems.Count);
        Assert.Empty(ParseGameItems(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                IsConquerPointUpdate(entry) ||
                entry.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.ClassExperience));
    }

    [Fact]
    public async Task EmperorWarRewardMissingWinnerIsDeniedWithSafeDialog()
    {
        var result = await ClaimEmperorWarRewardAsync(
            attributes: CreateEmperorWarWinnerAttributes(winnerPlayerUid: "0", winnerName: string.Empty),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertEmperorWarRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "There is no Emperor War reward available now.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task EmperorWarRewardActiveProcessIsDeniedWithSafeDialog()
    {
        var result = await ClaimEmperorWarRewardAsync(
            attributes: CreateEmperorWarWinnerAttributes(process: "Alive"),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertEmperorWarRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "Please come back when Emperor War is finished.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task EmperorWarRewardWrongPlayerIsDeniedWithSafeDialog()
    {
        var result = await ClaimEmperorWarRewardAsync(
            attributes: CreateEmperorWarWinnerAttributes(
                winnerPlayerUid: RivalPlayerUid.ToString(CultureInfo.InvariantCulture),
                winnerName: "RivalEmperor"),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        AssertEmperorWarRewardNotGranted(result, expectedConquerPoints: 100, expectedMoney: 200);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "You do not have any Emperor War reward to claim.");
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task EmperorWarRewardEligibleWinnerClaimsTunedSourceBundleAndClearsRoundOwner()
    {
        var result = await ClaimEmperorWarRewardAsync(
            attributes: CreateEmperorWarWinnerAttributes(),
            conquerPoints: 100,
            money: 200);

        Assert.True(result.Handled);
        var expectedConquerPoints = 100 + TunedEmperorWarRewardConquerPoints;
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(200, result.UpdatedSession.Character.Money);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["ClassExperience"]);
        Assert.Equal("10000#0#0#", result.UpdatedSession.Character.Attributes["SubProfInfo"]);
        Assert.Equal(4, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_009_003 && item.StackSize == 3);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_050_001 && item.StackSize == 500);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 4_040_001 && item.StackSize == 500);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_324_226 && item.StackSize == 5);
        var rewardItems = ParseGameItems(result.Sent);
        Assert.Contains(rewardItems, item => item.ItemId == 3_009_003 && item.StackSize == 3);
        Assert.Contains(rewardItems, item => item.ItemId == 4_050_001 && item.StackSize == 500);
        Assert.Contains(rewardItems, item => item.ItemId == 4_040_001 && item.StackSize == 500);
        Assert.Contains(rewardItems, item => item.ItemId == 3_324_226 && item.StackSize == 5);
        Assert.Contains(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                IsConquerPointUpdate(entry) &&
                entry.Value == checked((ulong)expectedConquerPoints)));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.ClassExperience));
        AssertEmperorWarRoundOwnerCleared(result.UpdatedSession);

        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("300000", StringComparison.Ordinal));
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.EmperorWar option=2", result.Log);
        Assert.Contains($"sourceConquerPoints={LegacyEmperorWarSourceConquerPoints}", result.Log);
        Assert.Contains($"tunedConquerPoints={TunedEmperorWarRewardConquerPoints}", result.Log);
        Assert.Contains("items=3009003x3,4050001x500,4040001x500,3324226x5", result.Log);
        Assert.Contains("studyPoints=10000", result.Log);
        Assert.Contains("innerPowerPotency=10000", result.Log);
        Assert.Contains("references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcHandler.cs EmperorWar option 2", result.Log);
        Assert.Contains("WorldServer deferred LegacyNpc EmperorWar InnerPower potency reward", result.Log);
    }

    [Fact]
    public async Task EmperorWarRewardDuplicateClaimDoesNotDoublePay()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            conquerPoints: 100,
            money: 200,
            attributes: CreateEmperorWarWinnerAttributes());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickEmperorWarRewardAsync(connection, log, sessions, session);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        var expectedConquerPoints = 100 + TunedEmperorWarRewardConquerPoints;
        Assert.Equal(expectedConquerPoints, firstUpdated.Character.ConquerPoints);

        sent.Clear();
        var secondHandled = await ClickEmperorWarRewardAsync(connection, log, sessions, firstUpdated);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(expectedConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(200, secondUpdated.Character.Money);
        Assert.Equal(4, secondUpdated.PlayerItems.InventoryItems.Count);
        Assert.Empty(ParseGameItems(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                IsConquerPointUpdate(entry) ||
                entry.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.ClassExperience));
    }

    private static async Task<MissedWarEventInvokeResult> ClaimWarFightersRewardAsync(
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0,
        long money = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            conquerPoints: conquerPoints,
            money: money,
            attributes: attributes ?? CreateWarFightersWinnerAttributes());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await ClickWarFightersRewardAsync(connection, log, sessions, session);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new MissedWarEventInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static async Task<bool> ClickWarFightersRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session)
    {
        return await WorldPacketHandlers.TryHandleLegacyMissedWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            WarFightersNpcId,
            optionId: 2,
            CancellationToken.None);
    }

    private static async Task<MissedWarEventInvokeResult> ClaimEmperorWarRewardAsync(
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0,
        long money = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            conquerPoints: conquerPoints,
            money: money,
            attributes: attributes ?? CreateEmperorWarWinnerAttributes());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await ClickEmperorWarRewardAsync(connection, log, sessions, session);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new MissedWarEventInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static async Task<bool> ClickEmperorWarRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session)
    {
        return await WorldPacketHandlers.TryHandleLegacyMissedWarEventNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EmperorWarNpcId,
            optionId: 2,
            CancellationToken.None);
    }

    private static WorldSession CreateSession(
        long mapId = 1_002,
        long x = 50,
        long y = 50,
        long conquerPoints = 0,
        long money = 0,
        int minHitPoints = 1,
        Dictionary<string, string>? attributes = null)
    {
        attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        attributes.TryAdd("DynamicID", "0");

        return new WorldSession(
            AccountId: 100,
            Username: "missed-war-test",
            Character: new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "MissedWarHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: mapId,
                X: x,
                Y: y,
                PreviousMapId: mapId,
                PreviousMapX: x,
                PreviousMapY: y,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: minHitPoints,
                MinMana: 0,
                Money: money,
                ConquerPoints: conquerPoints,
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

    private static Dictionary<string, string> CreateWarFightersWinnerAttributes(
        string process = "Dead",
        string? winnerPlayerUid = null,
        string winnerName = "MissedWarHero",
        string winnerScore = "456789")
    {
        winnerPlayerUid ??= PlayerUid.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
            ["ClassExperience"] = "0",
        };

        foreach (var processKey in WarFightersProcessAttributeNames())
        {
            attributes[processKey] = process;
        }

        SetWarFightersAlias(attributes, "RoundOwnerPlayerUID", winnerPlayerUid);
        SetWarFightersAlias(attributes, "RoundOwnerPlayerUid", winnerPlayerUid);
        SetWarFightersAlias(attributes, "RoundOwnerUID", winnerPlayerUid);
        SetWarFightersAlias(attributes, "RoundOwnerUid", winnerPlayerUid);
        SetWarFightersAlias(attributes, "RoundOwnerName", winnerName);
        SetWarFightersAlias(attributes, "RoundOwnerScore", winnerScore);
        return attributes;
    }

    private static void SetWarFightersAlias(IDictionary<string, string> attributes, string suffix, string value)
    {
        foreach (var prefix in WarFightersAttributePrefixes())
        {
            attributes[prefix + suffix] = value;
        }
    }

    private static IReadOnlyList<string> WarFightersAttributePrefixes()
    {
        return
        [
            "LegacyMsgWarFighters",
            "MsgWarFighters",
            "RuntimeMsgWarFighters",
            "LegacyWarFighters",
            "WarFighters",
            "RuntimeWarFighters",
        ];
    }

    private static IReadOnlyList<string> WarFightersProcessAttributeNames()
    {
        return
        [
            "LegacyMsgWarFightersProcess",
            "MsgWarFightersProcess",
            "RuntimeMsgWarFightersProcess",
            "LegacyWarFightersProcess",
            "WarFightersProcess",
            "RuntimeWarFightersProcess",
            "LegacyMsgWarFightersProces",
            "MsgWarFightersProces",
            "RuntimeMsgWarFightersProces",
            "LegacyWarFightersProces",
            "WarFightersProces",
            "RuntimeWarFightersProces",
        ];
    }

    private static IReadOnlyList<string> WarFightersRoundOwnerAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in WarFightersAttributePrefixes())
        {
            names.Add(prefix + "RoundOwnerPlayerUID");
            names.Add(prefix + "RoundOwnerPlayerUid");
            names.Add(prefix + "RoundOwnerUID");
            names.Add(prefix + "RoundOwnerUid");
            names.Add(prefix + "RoundOwnerName");
            names.Add(prefix + "RoundOwnerScore");
            names.Add(prefix + "PoleOwnerName");
            names.Add(prefix + "PoleReset");
        }

        return names;
    }

    private static Dictionary<string, string> CreateEmperorWarWinnerAttributes(
        string process = "Dead",
        string? winnerPlayerUid = null,
        string winnerName = "MissedWarHero",
        string winnerScore = "456789")
    {
        winnerPlayerUid ??= PlayerUid.ToString(CultureInfo.InvariantCulture);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
            ["ClassExperience"] = "0",
        };

        foreach (var processKey in EmperorWarProcessAttributeNames())
        {
            attributes[processKey] = process;
        }

        SetEmperorWarAlias(attributes, "RoundOwnerPlayerUID", winnerPlayerUid);
        SetEmperorWarAlias(attributes, "RoundOwnerPlayerUid", winnerPlayerUid);
        SetEmperorWarAlias(attributes, "RoundOwnerUID", winnerPlayerUid);
        SetEmperorWarAlias(attributes, "RoundOwnerUid", winnerPlayerUid);
        SetEmperorWarAlias(attributes, "WinnerPlayerUID", winnerPlayerUid);
        SetEmperorWarAlias(attributes, "WinnerPlayerUid", winnerPlayerUid);
        SetEmperorWarAlias(attributes, "WinnerUID", winnerPlayerUid);
        SetEmperorWarAlias(attributes, "WinnerUid", winnerPlayerUid);
        SetEmperorWarAlias(attributes, "RoundOwnerName", winnerName);
        SetEmperorWarAlias(attributes, "WinnerName", winnerName);
        SetEmperorWarAlias(attributes, "RoundOwnerScore", winnerScore);
        SetEmperorWarAlias(attributes, "WinnerScore", winnerScore);
        return attributes;
    }

    private static void SetEmperorWarAlias(IDictionary<string, string> attributes, string suffix, string value)
    {
        foreach (var prefix in EmperorWarAttributePrefixes())
        {
            attributes[prefix + suffix] = value;
        }
    }

    private static IReadOnlyList<string> EmperorWarAttributePrefixes()
    {
        return
        [
            "LegacyMsgEmperorWar",
            "MsgEmperorWar",
            "RuntimeMsgEmperorWar",
            "LegacyEmperorWar",
            "EmperorWar",
            "RuntimeEmperorWar",
        ];
    }

    private static IReadOnlyList<string> EmperorWarProcessAttributeNames()
    {
        return
        [
            "LegacyMsgEmperorWarProcess",
            "MsgEmperorWarProcess",
            "RuntimeMsgEmperorWarProcess",
            "LegacyEmperorWarProcess",
            "EmperorWarProcess",
            "RuntimeEmperorWarProcess",
            "LegacyMsgEmperorWarProces",
            "MsgEmperorWarProces",
            "RuntimeMsgEmperorWarProces",
            "LegacyEmperorWarProces",
            "EmperorWarProces",
            "RuntimeEmperorWarProces",
        ];
    }

    private static IReadOnlyList<string> EmperorWarRoundOwnerAttributeNames()
    {
        var names = new List<string>();
        foreach (var prefix in EmperorWarAttributePrefixes())
        {
            names.Add(prefix + "RoundOwnerPlayerUID");
            names.Add(prefix + "RoundOwnerPlayerUid");
            names.Add(prefix + "RoundOwnerUID");
            names.Add(prefix + "RoundOwnerUid");
            names.Add(prefix + "RoundOwnerName");
            names.Add(prefix + "RoundOwnerScore");
            names.Add(prefix + "WinnerPlayerUID");
            names.Add(prefix + "WinnerPlayerUid");
            names.Add(prefix + "WinnerUID");
            names.Add(prefix + "WinnerUid");
            names.Add(prefix + "WinnerName");
            names.Add(prefix + "WinnerScore");
        }

        return names;
    }

    private static void AssertWarFightersRewardNotGranted(
        MissedWarEventInvokeResult result,
        long expectedConquerPoints,
        long expectedMoney)
    {
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(expectedMoney, result.UpdatedSession.Character.Money);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["ClassExperience"]);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItems(result.Sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                IsConquerPointUpdate(entry) ||
                entry.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.ClassExperience));
    }

    private static void AssertWarFightersRoundOwnerCleared(WorldSession updatedSession)
    {
        foreach (var key in WarFightersRoundOwnerAttributeNames())
        {
            var value = updatedSession.Character.Attributes.TryGetValue(key, out var raw) ? raw : "<missing>";
            Assert.False(
                HasNonEmptyAttribute(updatedSession.Character.Attributes, key),
                $"Expected WarFighters reward claim to clear '{key}', but value was '{value}'.");
        }
    }

    private static void AssertEmperorWarRewardNotGranted(
        MissedWarEventInvokeResult result,
        long expectedConquerPoints,
        long expectedMoney)
    {
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(expectedMoney, result.UpdatedSession.Character.Money);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["ClassExperience"]);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItems(result.Sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry =>
                IsConquerPointUpdate(entry) ||
                entry.Type is LegacyMsgUpdateDataType.Money or LegacyMsgUpdateDataType.ClassExperience));
    }

    private static void AssertEmperorWarRoundOwnerCleared(WorldSession updatedSession)
    {
        foreach (var key in EmperorWarRoundOwnerAttributeNames())
        {
            var value = updatedSession.Character.Attributes.TryGetValue(key, out var raw) ? raw : "<missing>";
            Assert.False(
                HasNonEmptyAttribute(updatedSession.Character.Attributes, key),
                $"Expected EmperorWar reward claim to clear '{key}', but value was '{value}'.");
        }
    }

    private static bool HasNonEmptyAttribute(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return attributes.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value) &&
            !string.Equals(value, "0", StringComparison.Ordinal);
    }

    private static bool IsConquerPointUpdate(LegacyMsgUpdateEntry entry)
    {
        return entry.Type is LegacyMsgUpdateDataType.ConquerPoints;
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMsgMessages(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyGameItemPacket> ParseGameItems(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyGameItemPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet))
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

    private sealed record MissedWarEventInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
