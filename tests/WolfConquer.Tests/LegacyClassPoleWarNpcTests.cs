using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassPoleWarNpcTests
{
    private const uint ClassPoleWarNpcId = 65_189;
    private const uint PlayerUid = 1_293_600;
    private const uint RivalPlayerUid = 1_293_601;
    private const long TunedClassPoleWarRewardConquerPoints = 300_000;

    [Fact]
    public async Task HandlerSendsTranslatedRootDialogWithSourceOptions()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyClassPoleWarNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ClassPoleWarNpcId,
            optionId: 0,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text.Contains("Class PK runs every day from 22:00 to 22:30"));
        Assert.Contains(replies, reply => reply.Text == "Enter the match." && reply.OptionId == 1);
        Assert.Contains(replies, reply => reply.Text == "Claim reward." && reply.OptionId == 50);
        Assert.Contains(replies, reply => reply.Text == "Just passing by." && reply.OptionId == byte.MaxValue);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 268);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
        Assert.Contains("source=NpcHandler.ClassPoleWar", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsClassRewardSelectionWithSourceOptionIds()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyClassPoleWarNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ClassPoleWarNpcId,
            optionId: 50,
            CancellationToken.None);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Choose the class reward you want to claim.");
        AssertOption(replies, "Archer.", 2);
        AssertOption(replies, "Warrior.", 3);
        AssertOption(replies, "Ninja.", 4);
        AssertOption(replies, "Monk.", 5);
        AssertOption(replies, "Trojan.", 6);
        AssertOption(replies, "Water Taoist.", 7);
        AssertOption(replies, "Fire Taoist.", 8);
        AssertOption(replies, "Pirate.", 9);
        AssertOption(replies, "Kung Fu King.", 10);
        AssertOption(replies, "Windwalker.", 11);
        AssertOption(replies, "Class Pole War.", 12);
        AssertOption(replies, "Thunder Striker.", 13);
        Assert.Contains(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar && reply.InputMaxLength == 269);
        Assert.All(replies.Where(reply => !string.IsNullOrEmpty(reply.Text)), reply => Assert.True(IsAscii(reply.Text), reply.Text));
    }

    [Fact]
    public async Task HandlerTeleportsToSourceClassPoleWarDestinationWhenStateApiIsAbsent()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_002, x: 430, y: 378, minHitPoints: 0);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyClassPoleWarNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ClassPoleWarNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(10_005U, teleport.DwParam);
        Assert.Equal(350U, teleport.PositionX);
        Assert.Equal(306U, teleport.PositionY);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(10_005, updated.Character.MapId);
        Assert.Equal(350, updated.Character.X);
        Assert.Equal(306, updated.Character.Y);
        Assert.Equal(1, updated.Character.MinHitPoints);
        Assert.Contains("source=NpcHandler.ClassPoleWar map=10005 x=350 y=306", log.ToString());
        Assert.Contains("PARITY-GAP LegacyNpc ClassPoleWar enter state unavailable", log.ToString());
    }

    [Fact]
    public async Task HandlerTeleportsToSourceClassPoleWarDestinationWhenAllEntryProcessesAreAlive()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            x: 430,
            y: 378,
            minHitPoints: 0,
            attributes: CreateClassPoleWarEntryProcessAttributes());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyClassPoleWarNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ClassPoleWarNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        var teleport = Assert.Single(ParseMsgActions(sent), action => action.Type == MsgActionType.Teleport);
        Assert.Equal(10_005U, teleport.DwParam);
        Assert.Equal(350U, teleport.PositionX);
        Assert.Equal(306U, teleport.PositionY);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(10_005, updated.Character.MapId);
        Assert.Equal(350, updated.Character.X);
        Assert.Equal(306, updated.Character.Y);
        Assert.Equal(1, updated.Character.MinHitPoints);
        Assert.Contains("WorldServer handled LegacyNpc ClassPoleWar enter", log.ToString());
        Assert.DoesNotContain("PARITY-GAP LegacyNpc ClassPoleWar enter state unavailable", log.ToString());
    }

    [Fact]
    public async Task HandlerDeniesClassPoleWarEntryWhenAnyModeledProcessIsNotAlive()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_002,
            x: 430,
            y: 378,
            minHitPoints: 1,
            attributes: CreateClassPoleWarEntryProcessAttributes(blockedScheduleName: "MsgArcherMaster", blockedProcess: "Dead"));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyClassPoleWarNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ClassPoleWarNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(handled);
        Assert.Empty(ParseMsgActions(sent));
        Assert.Contains(ParseMessages(sent), message => message.Content.Any(text => text.Contains("Class PK has not started yet")));
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(430, updated.Character.X);
        Assert.Equal(378, updated.Character.Y);
        Assert.Contains("WorldServer skipped LegacyNpc ClassPoleWar enter", log.ToString());
        Assert.Contains("schedule=MsgArcherMaster process=Dead is not Alive", log.ToString());
    }

    [Fact]
    public async Task ClassPoleWarRewardMissingWinnerIsDenied()
    {
        var result = await ClaimClassPoleWarRewardAsync(
            optionId: 2,
            attributes: CreateClassPoleWarRewardAttributes(optionId: 2, includeWinner: false),
            conquerPoints: 123);

        Assert.True(result.Handled);
        AssertClassPoleWarRewardNotGranted(result, expectedConquerPoints: 123);
    }

    [Fact]
    public async Task ClassPoleWarRewardActiveProcessIsDenied()
    {
        var result = await ClaimClassPoleWarRewardAsync(
            optionId: 2,
            attributes: CreateClassPoleWarRewardAttributes(optionId: 2, process: "Alive"),
            conquerPoints: 123);

        Assert.True(result.Handled);
        AssertClassPoleWarRewardNotGranted(result, expectedConquerPoints: 123);
    }

    [Fact]
    public async Task ClassPoleWarRewardWrongPlayerIsDenied()
    {
        var result = await ClaimClassPoleWarRewardAsync(
            optionId: 2,
            attributes: CreateClassPoleWarRewardAttributes(
                optionId: 2,
                winnerPlayerUid: RivalPlayerUid.ToString(CultureInfo.InvariantCulture),
                winnerName: "OtherArcher"),
            conquerPoints: 123);

        Assert.True(result.Handled);
        AssertClassPoleWarRewardNotGranted(result, expectedConquerPoints: 123);
    }

    [Fact]
    public async Task ClassPoleWarRewardEligibleArcherOptionPaysTunedConquerPointsAndCommonRewards()
    {
        var result = await ClaimClassPoleWarRewardAsync(
            optionId: 2,
            attributes: CreateClassPoleWarRewardAttributes(optionId: 2),
            conquerPoints: 123);

        Assert.True(result.Handled);
        AssertClassPoleWarRewardGranted(
            result.UpdatedSession,
            result.Sent,
            expectedConquerPoints: 123 + TunedClassPoleWarRewardConquerPoints,
            expectThunderStrikeExtraItem: false);
        AssertClassPoleWarRoundOwnerClearedOrClaimed(result.UpdatedSession, optionId: 2);
    }

    [Fact]
    public async Task ClassPoleWarRewardEligibleLocalFinishRoundOwnerDoesNotRequireExplicitDeadProcess()
    {
        var attributes = CreateClassPoleWarRewardAttributes(optionId: 2, includeProcess: false);

        var result = await ClaimClassPoleWarRewardAsync(
            optionId: 2,
            attributes: attributes,
            conquerPoints: 123);

        Assert.True(result.Handled);
        AssertClassPoleWarRewardGranted(
            result.UpdatedSession,
            result.Sent,
            expectedConquerPoints: 123 + TunedClassPoleWarRewardConquerPoints,
            expectThunderStrikeExtraItem: false);
        AssertClassPoleWarRoundOwnerClearedOrClaimed(result.UpdatedSession, optionId: 2);
    }

    [Fact]
    public async Task ClassPoleWarRewardEligibleThunderStrikeOptionPaysCommonRewardsAndExtraItem()
    {
        var result = await ClaimClassPoleWarRewardAsync(
            optionId: 13,
            attributes: CreateClassPoleWarRewardAttributes(optionId: 13),
            conquerPoints: 123);

        Assert.True(result.Handled);
        AssertClassPoleWarRewardGranted(
            result.UpdatedSession,
            result.Sent,
            expectedConquerPoints: 123 + TunedClassPoleWarRewardConquerPoints,
            expectThunderStrikeExtraItem: true);
        AssertClassPoleWarRoundOwnerClearedOrClaimed(result.UpdatedSession, optionId: 13);
    }

    [Fact]
    public async Task ClassPoleWarRewardDuplicateClaimDoesNotDoublePay()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            conquerPoints: 123,
            attributes: CreateClassPoleWarRewardAttributes(optionId: 2));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var firstHandled = await ClickClassPoleWarRewardAsync(connection, log, sessions, session, optionId: 2);

        Assert.True(firstHandled);
        Assert.True(sessions.TryGet(connection, out var firstUpdated));
        Assert.NotNull(firstUpdated);
        var expectedConquerPoints = 123 + TunedClassPoleWarRewardConquerPoints;
        AssertClassPoleWarRewardGranted(
            firstUpdated,
            sent,
            expectedConquerPoints,
            expectThunderStrikeExtraItem: false);
        AssertClassPoleWarRoundOwnerClearedOrClaimed(firstUpdated, optionId: 2);

        sent.Clear();
        var secondHandled = await ClickClassPoleWarRewardAsync(connection, log, sessions, firstUpdated, optionId: 2);

        Assert.True(secondHandled);
        Assert.True(sessions.TryGet(connection, out var secondUpdated));
        Assert.NotNull(secondUpdated);
        Assert.Equal(expectedConquerPoints, secondUpdated.Character.ConquerPoints);
        Assert.Equal(3, secondUpdated.PlayerItems.InventoryItems.Count);
        Assert.Empty(ParseGameItems(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry => IsConquerPointUpdate(entry)));
    }

    private static void AssertOption(IReadOnlyList<LegacyNpcReplyPacket> replies, string text, byte optionId)
    {
        Assert.Contains(replies, reply => reply.Text == text && reply.OptionId == optionId);
    }

    private static async Task<ClassPoleWarRewardInvokeResult> ClaimClassPoleWarRewardAsync(
        byte optionId,
        Dictionary<string, string>? attributes = null,
        long conquerPoints = 0)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            conquerPoints: conquerPoints,
            attributes: attributes ?? CreateClassPoleWarRewardAttributes(optionId));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await ClickClassPoleWarRewardAsync(connection, log, sessions, session, optionId);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new ClassPoleWarRewardInvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private static async Task<bool> ClickClassPoleWarRewardAsync(
        ConquerTcpConnection connection,
        StringWriter log,
        WorldSessionStore sessions,
        WorldSession session,
        byte optionId)
    {
        return await WorldPacketHandlers.TryHandleLegacyClassPoleWarNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ClassPoleWarNpcId,
            optionId,
            CancellationToken.None);
    }

    private static Dictionary<string, string> CreateClassPoleWarRewardAttributes(
        byte optionId,
        string process = "Dead",
        string? winnerPlayerUid = null,
        string winnerName = "ClassPoleHero",
        string winnerScore = "456789",
        bool includeProcess = true,
        bool includeWinner = true)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
        };

        var prefixes = ClassPoleWarRewardAttributePrefixes(optionId);
        if (includeProcess)
        {
            foreach (var prefix in prefixes)
            {
                attributes[prefix + "Process"] = process;
                attributes[prefix + "Proces"] = process;
            }
        }

        if (!includeWinner)
        {
            return attributes;
        }

        winnerPlayerUid ??= PlayerUid.ToString(CultureInfo.InvariantCulture);
        foreach (var prefix in prefixes)
        {
            attributes[prefix + "RoundOwnerPlayerUID"] = winnerPlayerUid;
            attributes[prefix + "RoundOwnerPlayerUid"] = winnerPlayerUid;
            attributes[prefix + "RoundOwnerUID"] = winnerPlayerUid;
            attributes[prefix + "RoundOwnerUid"] = winnerPlayerUid;
            attributes[prefix + "RoundOwnerName"] = winnerName;
            attributes[prefix + "RoundOwnerScore"] = winnerScore;
        }

        return attributes;
    }

    private static Dictionary<string, string> CreateClassPoleWarEntryProcessAttributes(
        string defaultProcess = "Alive",
        string? blockedScheduleName = null,
        string blockedProcess = "Dead")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DynamicID"] = "0",
        };

        foreach (var scheduleName in ClassPoleWarEntryScheduleNames())
        {
            var process = scheduleName.Equals(blockedScheduleName, StringComparison.Ordinal)
                ? blockedProcess
                : defaultProcess;
            foreach (var prefix in ClassPoleWarScheduleAttributePrefixes(scheduleName))
            {
                attributes[prefix + "Process"] = process;
                attributes[prefix + "Proces"] = process;
            }
        }

        return attributes;
    }

    private static IReadOnlyList<string> ClassPoleWarEntryScheduleNames()
    {
        return
        [
            "MsgArcherMaster",
            "MsgFireMaster",
            "MsgKungfuKing",
            "MsgMonkMaster",
            "MsgNinjaMaster",
            "MsgPirateMaster",
            "MsgTaoistMaster",
            "MsgTrojanMaster",
            "MsgWarriorGod",
            "MsgWindWalker",
            "MsgThnderStrike",
        ];
    }

    private static IReadOnlyList<string> ClassPoleWarRewardAttributePrefixes(byte optionId)
    {
        return ClassPoleWarScheduleAttributePrefixes(ClassPoleWarRewardScheduleName(optionId));
    }

    private static IReadOnlyList<string> ClassPoleWarScheduleAttributePrefixes(string scheduleName)
    {
        var withoutMsgPrefix = scheduleName.StartsWith("Msg", StringComparison.Ordinal)
            ? scheduleName[3..]
            : scheduleName;
        var aliases = new List<string>
        {
            scheduleName,
            withoutMsgPrefix,
        };

        if (scheduleName == "MsgArcherMaster")
        {
            aliases.Add("ArcherPole");
        }
        else if (scheduleName == "MsgClassPolaWar")
        {
            aliases.Add("MsgClassPoleWar");
            aliases.Add("ClassPoleWar");
        }
        else if (scheduleName == "MsgThnderStrike")
        {
            aliases.Add("MsgThunderStrike");
            aliases.Add("ThunderStrike");
        }

        return aliases
            .SelectMany(alias => new[] { "Legacy" + alias, alias, "Runtime" + alias })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ClassPoleWarRewardScheduleName(byte optionId)
    {
        return optionId switch
        {
            2 => "MsgArcherMaster",
            3 => "MsgWarriorGod",
            4 => "MsgNinjaMaster",
            5 => "MsgMonkMaster",
            6 => "MsgTrojanMaster",
            7 => "MsgTaoistMaster",
            8 => "MsgFireMaster",
            9 => "MsgPirateMaster",
            10 => "MsgKungfuKing",
            11 => "MsgWindWalker",
            12 => "MsgClassPolaWar",
            13 => "MsgThnderStrike",
            _ => throw new ArgumentOutOfRangeException(nameof(optionId), optionId, "Unknown ClassPoleWar reward option."),
        };
    }

    private static void AssertClassPoleWarRewardNotGranted(
        ClassPoleWarRewardInvokeResult result,
        long expectedConquerPoints)
    {
        Assert.Equal(expectedConquerPoints, result.UpdatedSession.Character.ConquerPoints);
        Assert.False(result.UpdatedSession.Character.Attributes.ContainsKey("SubProfInfo"));
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItems(result.Sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.Sent),
            packet => packet.Updates.Any(entry => IsConquerPointUpdate(entry)));
    }

    private static void AssertClassPoleWarRewardGranted(
        WorldSession updatedSession,
        IEnumerable<byte[]> sent,
        long expectedConquerPoints,
        bool expectThunderStrikeExtraItem)
    {
        Assert.Equal(expectedConquerPoints, updatedSession.Character.ConquerPoints);
        Assert.Equal("10000#0#0#", updatedSession.Character.Attributes["SubProfInfo"]);
        Assert.Equal(expectThunderStrikeExtraItem ? 4 : 3, updatedSession.PlayerItems.InventoryItems.Count);
        AssertRewardItem(updatedSession.PlayerItems.InventoryItems, 3_009_003, 1);
        AssertRewardItem(updatedSession.PlayerItems.InventoryItems, 4_050_001, 100);
        AssertRewardItem(updatedSession.PlayerItems.InventoryItems, 4_040_001, 100);

        var rewardItems = ParseGameItems(sent);
        AssertRewardItem(rewardItems, 3_009_003, 1);
        AssertRewardItem(rewardItems, 4_050_001, 100);
        AssertRewardItem(rewardItems, 4_040_001, 100);

        if (expectThunderStrikeExtraItem)
        {
            AssertRewardItem(updatedSession.PlayerItems.InventoryItems, 3_324_226, 5);
            AssertRewardItem(rewardItems, 3_324_226, 5);
        }
        else
        {
            Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_324_226);
            Assert.DoesNotContain(rewardItems, item => item.ItemId == 3_324_226);
        }

        Assert.Contains(
            ParseMsgUpdates(sent),
            packet => packet.Updates.Any(entry =>
                IsConquerPointUpdate(entry) &&
                entry.Value == checked((ulong)expectedConquerPoints)));
    }

    private static void AssertRewardItem(
        IEnumerable<PlayerItemSnapshotItem> items,
        uint itemId,
        ushort stackSize)
    {
        Assert.Contains(items, item => item.ItemId == itemId && item.StackSize == stackSize);
    }

    private static void AssertRewardItem(
        IEnumerable<LegacyGameItemPacket> items,
        uint itemId,
        ushort stackSize)
    {
        Assert.Contains(items, item => item.ItemId == itemId && item.StackSize == stackSize);
    }

    private static void AssertClassPoleWarRoundOwnerClearedOrClaimed(WorldSession updatedSession, byte optionId)
    {
        var attributes = updatedSession.Character.Attributes;
        var roundOwnerStillPresent = ClassPoleWarRoundOwnerAttributeNames(optionId)
            .Any(key => HasNonEmptyAttribute(attributes, key));
        var claimMarked = ClassPoleWarClaimAttributeNames(optionId)
            .Any(key => attributes.TryGetValue(key, out var value) && IsTruthyAttribute(value));

        Assert.True(
            !roundOwnerStillPresent || claimMarked,
            "Expected ClassPoleWar reward claim to clear RoundOwner attributes or set a claimed marker.");
    }

    private static IReadOnlyList<string> ClassPoleWarRoundOwnerAttributeNames(byte optionId)
    {
        var names = new List<string>();
        foreach (var prefix in ClassPoleWarRewardAttributePrefixes(optionId))
        {
            names.Add(prefix + "RoundOwnerPlayerUID");
            names.Add(prefix + "RoundOwnerPlayerUid");
            names.Add(prefix + "RoundOwnerUID");
            names.Add(prefix + "RoundOwnerUid");
            names.Add(prefix + "RoundOwnerName");
            names.Add(prefix + "RoundOwnerScore");
        }

        return names;
    }

    private static IReadOnlyList<string> ClassPoleWarClaimAttributeNames(byte optionId)
    {
        var names = new List<string>();
        foreach (var prefix in ClassPoleWarRewardAttributePrefixes(optionId))
        {
            names.Add(prefix + "RewardClaimed");
            names.Add(prefix + "WinnerCleared");
        }

        return names;
    }

    private static bool HasNonEmptyAttribute(IReadOnlyDictionary<string, string> attributes, string key)
    {
        return attributes.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value) &&
            !string.Equals(value, "0", StringComparison.Ordinal) &&
            !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTruthyAttribute(string value)
    {
        return bool.TryParse(value, out var parsed)
            ? parsed
            : string.Equals(value, "1", StringComparison.Ordinal);
    }

    private static bool IsConquerPointUpdate(LegacyMsgUpdateEntry entry)
    {
        return entry.Type is LegacyMsgUpdateDataType.ConquerPoints;
    }

    private static WorldSession CreateSession(
        long mapId = 1_002,
        long x = 430,
        long y = 378,
        long conquerPoints = 0,
        int minHitPoints = 1,
        Dictionary<string, string>? attributes = null,
        PlayerItemSnapshot? playerItems = null)
    {
        attributes = attributes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
        attributes.TryAdd("DynamicID", "0");

        return new WorldSession(
            AccountId: 100,
            Username: "class-pole-test",
            Character: new CharacterSnapshot(
                Uid: PlayerUid,
                Name: "ClassPoleHero",
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
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
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

    private static IReadOnlyList<LegacyMsgMessagePacket> ParseMessages(IEnumerable<byte[]> sent)
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

    private sealed record ClassPoleWarRewardInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession Session,
        WorldSession UpdatedSession);
}
