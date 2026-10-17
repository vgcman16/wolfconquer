using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAgatePrizeCenterNpcTests
{
    private const uint RefineryBadgeItemId = 720_148;
    private const string ForcedAgateRollAttribute = "RuntimeLegacyAgateTeleportRoll";
    private const string PrizeCenterCooldownAttribute = "RuntimeLegacyPrizeCenterTeleporterLastUnixSeconds";

    [Fact]
    public async Task HandlerSendsAgateIntroWithSourceOptionIds()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_771, x: 32, y: 7);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 1_882,
            optionId: 0);

        Assert.True(handled);
        Assert.Equal(6, sent.Count);
        Assert.Contains(
            "every 30 guardians",
            AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);

        var claim = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, claim.OptionId);
        Assert.Equal("Claim~Experience.", claim.Text);

        var withdraw = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option);
        Assert.Equal(10, withdraw.OptionId);
        Assert.Equal("Withdraw~to~Stage~2.", withdraw.Text);

        var quit = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Option);
        Assert.Equal(20, quit.OptionId);
        Assert.Equal("Quit~Arena.", quit.Text);

        Assert.Equal(31, AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Avatar).InputMaxLength);
        AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.Agate1", log.ToString());
    }

    [Fact]
    public async Task HandlerAppliesAndPersistsAgate1ExperienceReward()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, level: 139, experience: 0);
        var session = CreateSession(
            mapId: 1_771,
            x: 32,
            y: 7,
            attributes: new Dictionary<string, string>
            {
                ["DefeatedArenaGuardians"] = "35",
            },
            level: 139,
            experience: 0,
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(LevelExperienceRow(139, experience: 100_000, upLevTime: 10_000)),
                Sessions: sessions),
            session,
            npcId: 1_882,
            optionId: 2);

        Assert.True(handled);
        Assert.Equal(3, sent.Count);
        AssertSingleUpdate(sent[0], LegacyMsgUpdateDataType.Experience, 6_000);
        AssertAngelwingEffect(sent[1], session.Character.Uid);
        AssertSystemTalk(sent[2], "You have claimed experience worth an Exp Ball!");

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(139, updated.Character.Level);
        Assert.Equal(6_000, updated.Character.Experience);
        Assert.Equal("5", updated.Character.Attributes["DefeatedArenaGuardians"]);
        Assert.Equal("5", updated.Character.Attributes["PowerArenaDefeatedGuardians"]);

        var persisted = IniDocument.Parse(File.ReadAllText(snapshotPath)).GetSection("Character");
        Assert.Equal("139", persisted["Level"]);
        Assert.Equal("6000", persisted["Experience"]);
        Assert.Equal("5", persisted["DefeatedArenaGuardians"]);
        Assert.Equal("5", persisted["PowerArenaDefeatedGuardians"]);

        var output = log.ToString();
        Assert.Contains("WorldServer applied LegacyNpc Agate1 experience reward", output);
        Assert.Contains("guardians=35->5", output);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc Agate1 experience reward", output);
    }

    [Fact]
    public async Task HandlerAgate1ExperienceRewardAtMaxLevelConsumesGuardiansWithoutExperienceUpdate()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, level: 140, experience: 0);
        var session = CreateSession(
            mapId: 1_771,
            x: 32,
            y: 7,
            attributes: new Dictionary<string, string>
            {
                ["DefeatedArenaGuardians"] = "30",
            },
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 1_882,
            optionId: 2);

        Assert.True(handled);
        Assert.Equal(2, sent.Count);
        AssertAngelwingEffect(sent[0], session.Character.Uid);
        AssertSystemTalk(sent[1], "You have claimed experience worth an Exp Ball!");

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(140, updated.Character.Level);
        Assert.Equal(0, updated.Character.Experience);
        Assert.Equal("0", updated.Character.Attributes["DefeatedArenaGuardians"]);

        var persisted = IniDocument.Parse(File.ReadAllText(snapshotPath)).GetSection("Character");
        Assert.Equal("140", persisted["Level"]);
        Assert.Equal("0", persisted["Experience"]);
        Assert.Equal("0", persisted["DefeatedArenaGuardians"]);
        Assert.Contains(
            "source GainExpBall returns without EXP mutation at level 140 or above",
            log.ToString());
    }

    [Fact]
    public async Task HandlerAgate1Option3GrantsSourceStackAndFramesInSourceOrder()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        WriteCharacterSnapshot(usersRoot, level: 140, experience: 0, defeatedGuardians: 35);
        var session = CreateSession(
            mapId: 1_771,
            x: 32,
            y: 7,
            attributes: new Dictionary<string, string>
            {
                ["DefeatedArenaGuardians"] = "35",
            },
            usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalogWithItems(RefineryBadgeItemType()),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            npcId: 1_882,
            optionId: 3);

        Assert.True(handled);
        Assert.Equal(3, sent.Count);
        var itemFrame = AssertGameItem(sent[0]);
        Assert.Equal(RefineryBadgeItemId, itemFrame.ItemId);
        Assert.Equal(1, itemFrame.Durability);
        Assert.Equal(1, itemFrame.MaximumDurability);
        Assert.Equal(2, itemFrame.StackSize);
        Assert.Equal(0, itemFrame.Bound);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemFrame.Mode);
        AssertTopLeftSystem(sent[1], "You`ve received 2 Refinery Badges!");
        AssertAngelwingEffect(sent[2], session.Character.Uid);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal("5", updated.Character.Attributes["DefeatedArenaGuardians"]);
        Assert.Equal("5", updated.Character.Attributes["PowerArenaDefeatedGuardians"]);
        var badge = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(RefineryBadgeItemId, badge.ItemId);
        Assert.Equal(2, badge.StackSize);
        Assert.Equal(0, badge.Bound);
        Assert.Contains("WorldServer applied LegacyNpc Agate1 refinery badge reward", log.ToString());
        Assert.DoesNotContain("PARITY-GAP LegacyNpc Agate1 refinery badge reward", log.ToString());
    }

    [Fact]
    public async Task HandlerAgate1Option3PersistsAndReloadsGuardianAndMergedBadgeStack()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        WriteCharacterSnapshot(usersRoot, level: 140, experience: 0, defeatedGuardians: 62);
        var initialBadge = new PlayerItemSnapshotItem
        {
            Uid = 900,
            ItemId = RefineryBadgeItemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 3,
            Bound = 0,
        };
        var initialItems = PlayerItems(initialBadge);
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(1_293_600, initialItems);
        var session = CreateSession(
            mapId: 1_771,
            x: 32,
            y: 7,
            attributes: new Dictionary<string, string>
            {
                ["DefeatedArenaGuardians"] = "62",
            },
            usersRoot: usersRoot,
            playerItems: initialItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalogWithItems(RefineryBadgeItemType()),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            npcId: 1_882,
            optionId: 3);

        Assert.True(handled);
        Assert.Equal(3, sent.Count);
        var itemFrame = AssertGameItem(sent[0]);
        Assert.Equal(initialBadge.Uid, itemFrame.Uid);
        Assert.Equal(5, itemFrame.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, itemFrame.Mode);

        var reloadedCharacter = new CharacterSnapshotLoader(usersRoot, playerItemsRoot)
            .LoadByAccountId(session.AccountId);
        Assert.NotNull(reloadedCharacter);
        Assert.Equal("32", reloadedCharacter.Attributes["DefeatedArenaGuardians"]);
        Assert.Equal("32", reloadedCharacter.Attributes["PowerArenaDefeatedGuardians"]);
        var reloadedItems = new PlayerItemSnapshotLoader(playerItemsRoot)
            .LoadByCharacterUid(session.Character.Uid);
        var reloadedBadge = Assert.Single(reloadedItems.InventoryItems);
        Assert.Equal(initialBadge.Uid, reloadedBadge.Uid);
        Assert.Equal(RefineryBadgeItemId, reloadedBadge.ItemId);
        Assert.Equal(5, reloadedBadge.StackSize);
        Assert.Equal(0, reloadedBadge.Bound);
    }

    [Theory]
    [InlineData(29, 0)]
    [InlineData(30, 39)]
    public async Task HandlerAgate1Option3GuardPreservesExactNoMutationState(
        int defeatedGuardians,
        int inventoryCount)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(
            usersRoot,
            level: 140,
            experience: 0,
            defeatedGuardians: checked((ulong)defeatedGuardians));
        var initialItems = CreateInventory(inventoryCount);
        var itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
        itemLoader.SaveByCharacterUid(1_293_600, initialItems);
        var itemSnapshotPath = Path.Combine(playerItemsRoot, "1293600.bin");
        var characterBytes = File.ReadAllBytes(snapshotPath);
        var itemBytes = File.ReadAllBytes(itemSnapshotPath);
        var session = CreateSession(
            mapId: 1_771,
            x: 32,
            y: 7,
            attributes: new Dictionary<string, string>
            {
                ["DefeatedArenaGuardians"] = defeatedGuardians.ToString(CultureInfo.InvariantCulture),
            },
            usersRoot: usersRoot,
            playerItems: initialItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalogWithItems(RefineryBadgeItemType()),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            npcId: 1_882,
            optionId: 3);

        Assert.True(handled);
        Assert.Equal(4, sent.Count);
        Assert.Equal(
            "Your inventory is full! Please make 2 cells first before you claim the reward!",
            AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);
        var okay = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal("Okay.", okay.Text);
        Assert.Equal(byte.MaxValue, okay.OptionId);
        Assert.Equal(31, AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar).InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);

        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.Same(session, unchanged);
        Assert.Equal(characterBytes, File.ReadAllBytes(snapshotPath));
        Assert.Equal(itemBytes, File.ReadAllBytes(itemSnapshotPath));
    }

    [Fact]
    public async Task HandlerWithdrawsAgateThroughLegacyTeleportTransition()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_772,
            x: 50,
            y: 50,
            attributes: new Dictionary<string, string>
            {
                [ForcedAgateRollAttribute] = "0",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 1_884,
            optionId: 21);

        Assert.True(handled);
        Assert.Equal(5, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId: 1_773, expectedX: 39, expectedY: 66);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_773, updated.Character.MapId);
        Assert.Equal(39, updated.Character.X);
        Assert.Equal(66, updated.Character.Y);
        Assert.Equal(session.Character.MapId, updated.Character.PreviousMapId);
        Assert.Contains("source=NpcHandler.Agate3", log.ToString());
    }

    [Fact]
    public async Task HandlerSendsPrizeCenterTeleporterIntroDialog()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 3_071, x: 251, y: 392);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 9_619,
            optionId: 0);

        Assert.True(handled);
        Assert.Equal(7, sent.Count);
        Assert.Contains("Treasure~In~The~Blue", AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);
        Assert.Contains("where~do~you~want~to~go", AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Dialog).Text);
        Assert.Equal(1, AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Option).OptionId);
        Assert.Equal(2, AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Option).OptionId);
        Assert.Equal(byte.MaxValue, AssertNpcReply(sent[4], LegacyNpcReplyInteractType.Option).OptionId);
        Assert.Equal(14, AssertNpcReply(sent[5], LegacyNpcReplyInteractType.Avatar).InputMaxLength);
        AssertNpcReply(sent[6], LegacyNpcReplyInteractType.Finish);
        Assert.Contains("source=NpcHandler.PrizeCenterTeleporter1", log.ToString());
    }

    [Fact]
    public async Task HandlerPrizeCenterTeleporterUsesLegacyTeleportAndStoresCooldown()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 3_071, x: 72, y: 131);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 9_653,
            optionId: 1);

        Assert.True(handled);
        Assert.Equal(5, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId: 1_068, expectedX: 33, expectedY: 33);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_068, updated.Character.MapId);
        Assert.Equal(33, updated.Character.X);
        Assert.Equal(33, updated.Character.Y);
        Assert.True(updated.Character.Attributes.ContainsKey(PrizeCenterCooldownAttribute));
        Assert.Contains("source=NpcHandler.PrizeCenterTeleporter8", log.ToString());
    }

    [Fact]
    public async Task HandlerMammonEnvoyReturnUsesLegacyTeleport()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_068, x: 33, y: 33);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAgatePrizeCenterHandlerAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId: 9_623,
            optionId: 40);

        Assert.True(handled);
        Assert.Equal(5, sent.Count);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 0, session, expectedMapId: 1_002, expectedX: 410, expectedY: 354);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(410, updated.Character.X);
        Assert.Equal(354, updated.Character.Y);
        Assert.Contains("source=NpcHandler.MammonEnvoy", log.ToString());
    }

    private static async ValueTask<bool> InvokeAgatePrizeCenterHandlerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryHandleLegacyAgatePrizeCenterAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(
            null,
            [context, session, npcId, optionId, CancellationToken.None]);
        Assert.NotNull(result);
        return await (ValueTask<bool>)result;
    }

    private static WorldSession CreateSession(
        long mapId,
        long x,
        long y,
        IReadOnlyDictionary<string, string>? attributes = null,
        long level = 140,
        long experience = 0,
        string? usersRoot = null,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "agate-prize-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "PrizeSeeker",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: level,
                Experience: experience,
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
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: attributes ?? new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
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

    private static void AssertSingleUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(type, update.Type);
        Assert.Equal(value, update.Value);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertTopLeftSystem(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, packet.ChatType);
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal("SYSTEM", packet.Content[0]);
        Assert.Equal("ALL", packet.Content[1]);
        Assert.Equal(expectedText, packet.Content[3]);
    }

    private static void AssertAngelwingEffect(byte[] bytes, long characterUid)
    {
        Assert.True(LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(checked((uint)characterUid), packet.Uid);
        Assert.Equal(LegacyStringId.Effect, packet.Id);
        Assert.Equal(["angelwing"], packet.Strings);
    }

    private static void AssertSystemTalk(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        Assert.Equal(expectedText, packet.Content[3]);
    }

    private static string WriteCharacterSnapshot(
        string usersRoot,
        long level,
        long experience,
        ulong defeatedGuardians = 0)
    {
        var path = Path.Combine(usersRoot, "1293600.ini");
        File.WriteAllText(
            path,
            $"""
            [Character]
            UID=1293600
            Name=PrizeSeeker
            Body=1003
            Face=0
            Haire=0
            Class=7051
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level={level.ToString(CultureInfo.InvariantCulture)}
            Experience={experience.ToString(CultureInfo.InvariantCulture)}
            Map=1771
            X=32
            Y=7
            PMap=1771
            PMapX=32
            PMapY=7
            Strength=0
            Agility=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            Money=0
            ConquerPoints=0
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=0
            DefeatedArenaGuardians={defeatedGuardians.ToString(CultureInfo.InvariantCulture)}
            PowerArenaDefeatedGuardians={defeatedGuardians.ToString(CultureInfo.InvariantCulture)}
            """);
        return path;
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params LevelExperience[] levelExperience)
    {
        return CreateWorldDataCatalog(items: [], levelExperience);
    }

    private static WorldDataCatalog CreateWorldDataCatalogWithItems(params ItemType[] items)
    {
        return CreateWorldDataCatalog(items, levelExperience: []);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<LevelExperience> levelExperience)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://agate-prize-center",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
            levelExperience: levelExperience,
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
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, string.Empty, 0, []),
                [],
                "fixture://activity"),
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
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                string.Empty,
                new Dictionary<string, string>()),
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

    private static ItemType RefineryBadgeItemType()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = RefineryBadgeItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "RefineryBadge";
        fields[21] = "1";
        fields[46] = "10";
        fields[52] = "QuestItem";
        fields[53] = "It`s~used~to~exchange~for~refinery~packs.";
        return new ItemType(checked((int)RefineryBadgeItemId), fields[1], fields);
    }

    private static PlayerItemSnapshot PlayerItems(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshot CreateInventory(int count)
    {
        var items = Enumerable.Range(0, count)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(1_000 + index)),
                ItemId = checked((uint)(700_000 + index)),
                Durability = 1,
                MaximumDurability = 1,
                Position = 0,
                StackSize = 1,
            })
            .ToArray();
        return PlayerItems(items);
    }

    private static LevelExperience LevelExperienceRow(int level, long experience, int upLevTime)
    {
        return new LevelExperience(
            Type: 0,
            Level: level,
            Experience: experience,
            UpLevTime: upLevTime,
            MentorUpLevTime: upLevTime,
            SourceLine: 1,
            RawLine: string.Empty,
            Fields:
            [
                "0",
                level.ToString(CultureInfo.InvariantCulture),
                experience.ToString(CultureInfo.InvariantCulture),
                upLevTime.ToString(CultureInfo.InvariantCulture),
                upLevTime.ToString(CultureInfo.InvariantCulture),
            ]);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "wolf-agate-prize-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
