using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAgate2RewardMutationTests
{
    private const uint Agate2NpcId = 1_883;
    private const uint RefineryBadgeItemId = 720_148;
    private const long CharacterUid = 1_293_600;
    private const string ForcedAgateRollAttribute = "RuntimeLegacyAgateTeleportRoll";

    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    public async Task ExperienceOptionsPreserveSourceFortySixGuardianFailure(int optionId)
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: 44);
        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            usersRoot,
            defeatedGuardians: 44,
            attributes: new Dictionary<string, string>
            {
                [ForcedAgateRollAttribute] = "0",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(levelExperience: [LevelExperienceRow()]),
                Sessions: sessions),
            session,
            checked((byte)optionId));

        Assert.True(handled);
        Assert.Equal(3, sent.Count);
        Assert.Equal(
            "You need to defeat 46 guardians first to claim experience from the Agate.",
            AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog).Text);
        var okay = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal("Okay.", okay.Text);
        Assert.Equal(byte.MaxValue, okay.OptionId);
        AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Finish);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.Same(session, unchanged);
        Assert.Equal(snapshotBytes, File.ReadAllBytes(snapshotPath));
    }

    [Fact]
    public async Task OptionTwoAppliesAndPersistsExpBallWithoutAdvancing()
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: 50);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(usersRoot, defeatedGuardians: 50);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(levelExperience: [LevelExperienceRow()]),
                Sessions: sessions),
            session,
            optionId: 2);

        Assert.True(handled);
        Assert.Equal(3, sent.Count);
        AssertSingleUpdate(sent[0], LegacyMsgUpdateDataType.Experience, 6_000);
        AssertAngelwingEffect(sent[1]);
        AssertSystemTalk(sent[2], "You have claimed experience worth an Exp Ball!");

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_771, updated.Character.MapId);
        Assert.Equal(6_000, updated.Character.Experience);
        Assert.Equal("5", updated.Character.Attributes["DefeatedArenaGuardians"]);
        var persisted = IniDocument.Parse(File.ReadAllText(snapshotPath)).GetSection("Character");
        Assert.Equal("6000", persisted["Experience"]);
        Assert.Equal("5", persisted["DefeatedArenaGuardians"]);
        Assert.Equal("1771", persisted["Map"]);
        Assert.Contains("WorldServer applied LegacyNpc Agate2 experience reward", log.ToString());
    }

    [Fact]
    public async Task OptionTwelvePersistsRewardAndDestinationBeforeRewardThenAdvanceFrames()
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: 50);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            usersRoot,
            defeatedGuardians: 50,
            attributes: new Dictionary<string, string>
            {
                [ForcedAgateRollAttribute] = "0",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(levelExperience: [LevelExperienceRow()]),
                Sessions: sessions),
            session,
            optionId: 12);

        Assert.True(handled);
        Assert.Equal(8, sent.Count);
        AssertSingleUpdate(sent[0], LegacyMsgUpdateDataType.Experience, 6_000);
        AssertAngelwingEffect(sent[1]);
        AssertSystemTalk(sent[2], "You have claimed experience worth an Exp Ball!");
        AssertLegacyNpcTeleportTransition(sent, startIndex: 3, expectedX: 9, expectedY: 37);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_770, updated.Character.MapId);
        Assert.Equal(9, updated.Character.X);
        Assert.Equal(37, updated.Character.Y);
        Assert.Equal(1_771, updated.Character.PreviousMapId);
        Assert.Equal(6_000, updated.Character.Experience);
        Assert.Equal("5", updated.Character.Attributes["DefeatedArenaGuardians"]);

        var persisted = IniDocument.Parse(File.ReadAllText(snapshotPath)).GetSection("Character");
        Assert.Equal("6000", persisted["Experience"]);
        Assert.Equal("5", persisted["DefeatedArenaGuardians"]);
        Assert.Equal("1770", persisted["Map"]);
        Assert.Equal("9", persisted["X"]);
        Assert.Equal("37", persisted["Y"]);
        Assert.Equal("1771", persisted["PMap"]);
        Assert.Equal("0", persisted["DynamicID"]);
    }

    [Theory]
    [InlineData(3, 44, 0)]
    [InlineData(13, 44, 0)]
    [InlineData(3, 45, 39)]
    [InlineData(13, 45, 39)]
    public async Task BadgeOptionsPreserveCombinedSpaceAndGuardianGuard(
        int optionId,
        int defeatedGuardians,
        int inventoryCount)
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: checked((ulong)defeatedGuardians));
        var initialItems = CreateInventory(inventoryCount);
        var itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
        itemLoader.SaveByCharacterUid(CharacterUid, initialItems);
        var itemPath = Path.Combine(playerItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var itemBytes = File.ReadAllBytes(itemPath);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            usersRoot,
            defeatedGuardians: checked((ulong)defeatedGuardians),
            playerItems: initialItems,
            attributes: new Dictionary<string, string>
            {
                [ForcedAgateRollAttribute] = "0",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(items: [RefineryBadgeItemType()]),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            checked((byte)optionId));

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
        Assert.Equal(snapshotBytes, File.ReadAllBytes(snapshotPath));
        Assert.Equal(itemBytes, File.ReadAllBytes(itemPath));
    }

    [Fact]
    public async Task OptionThreeGrantsTwoUnboundBadgesAndPersistsWithoutAdvancing()
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: 50);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(usersRoot, defeatedGuardians: 50);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(items: [RefineryBadgeItemType()]),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            optionId: 3);

        Assert.True(handled);
        Assert.Equal(3, sent.Count);
        AssertBadgeItem(sent[0]);
        AssertTopLeftSystem(sent[1], "You`ve received 2 Refinery Badges!");
        AssertAngelwingEffect(sent[2]);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_771, updated.Character.MapId);
        Assert.Equal("5", updated.Character.Attributes["DefeatedArenaGuardians"]);
        var badge = Assert.Single(updated.PlayerItems.InventoryItems);
        Assert.Equal(RefineryBadgeItemId, badge.ItemId);
        Assert.Equal(2, badge.StackSize);
        Assert.Equal(0, badge.Bound);

        var persisted = IniDocument.Parse(File.ReadAllText(snapshotPath)).GetSection("Character");
        Assert.Equal("5", persisted["DefeatedArenaGuardians"]);
        var reloadedBadge = Assert.Single(new PlayerItemSnapshotLoader(playerItemsRoot)
            .LoadByCharacterUid(CharacterUid)
            .InventoryItems);
        Assert.Equal(2, reloadedBadge.StackSize);
        Assert.Equal(0, reloadedBadge.Bound);
        Assert.Contains("WorldServer applied LegacyNpc Agate2 refinery badge reward", log.ToString());
    }

    [Fact]
    public async Task OptionThirteenSendsBadgeFramesBeforePersistedAdvanceTransition()
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: 50);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            usersRoot,
            defeatedGuardians: 50,
            attributes: new Dictionary<string, string>
            {
                [ForcedAgateRollAttribute] = "7",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(items: [RefineryBadgeItemType()]),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            optionId: 13);

        Assert.True(handled);
        Assert.Equal(8, sent.Count);
        AssertBadgeItem(sent[0]);
        AssertTopLeftSystem(sent[1], "You`ve received 2 Refinery Badges!");
        AssertAngelwingEffect(sent[2]);
        AssertLegacyNpcTeleportTransition(sent, startIndex: 3, expectedX: 88, expectedY: 59);

        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_770, updated.Character.MapId);
        Assert.Equal(88, updated.Character.X);
        Assert.Equal(59, updated.Character.Y);
        Assert.Equal("5", updated.Character.Attributes["DefeatedArenaGuardians"]);
        Assert.Single(updated.PlayerItems.InventoryItems);

        var persisted = IniDocument.Parse(File.ReadAllText(snapshotPath)).GetSection("Character");
        Assert.Equal("5", persisted["DefeatedArenaGuardians"]);
        Assert.Equal("1770", persisted["Map"]);
        Assert.Equal("88", persisted["X"]);
        Assert.Equal("59", persisted["Y"]);
        Assert.Single(new PlayerItemSnapshotLoader(playerItemsRoot)
            .LoadByCharacterUid(CharacterUid)
            .InventoryItems);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    public async Task ExperienceOptionsFenceMissingLoadedLevelDataWithoutMutation(int optionId)
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: 45);
        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(usersRoot, defeatedGuardians: 45);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            checked((byte)optionId));

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.Same(session, unchanged);
        Assert.Equal(snapshotBytes, File.ReadAllBytes(snapshotPath));
        Assert.Contains("level experience data is not loaded", log.ToString());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(12)]
    public async Task ExperienceOptionsFenceCharacterPersistenceFailureWithoutMutation(int optionId)
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        Directory.CreateDirectory(usersRoot);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(usersRoot, defeatedGuardians: 45);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(levelExperience: [LevelExperienceRow()]),
                Sessions: sessions),
            session,
            checked((byte)optionId));

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.Same(session, unchanged);
        Assert.Contains("character reward persistence failed", log.ToString());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    public async Task BadgeOptionsFenceMissingLoadedItemDataWithoutMutation(int optionId)
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: 45);
        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(usersRoot, defeatedGuardians: 45);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            checked((byte)optionId));

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.Same(session, unchanged);
        Assert.Equal(snapshotBytes, File.ReadAllBytes(snapshotPath));
        Assert.False(Directory.Exists(playerItemsRoot));
        Assert.Contains("itemtype data for itemId=720148", log.ToString());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    public async Task BadgeOptionsFenceExhaustedItemUidWithoutMutation(int optionId)
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        var snapshotPath = WriteCharacterSnapshot(usersRoot, defeatedGuardians: 45);
        var initialItems = PlayerItems(new PlayerItemSnapshotItem
        {
            Uid = uint.MaxValue,
            ItemId = 700_001,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 1,
        });
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, initialItems);
        var itemPath = Path.Combine(playerItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var snapshotBytes = File.ReadAllBytes(snapshotPath);
        var itemBytes = File.ReadAllBytes(itemPath);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(usersRoot, defeatedGuardians: 45, playerItems: initialItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(items: [RefineryBadgeItemType()]),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            checked((byte)optionId));

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.Same(session, unchanged);
        Assert.Equal(snapshotBytes, File.ReadAllBytes(snapshotPath));
        Assert.Equal(itemBytes, File.ReadAllBytes(itemPath));
        Assert.Contains("inventory item UID space is exhausted", log.ToString());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    public async Task BadgeOptionsRollBackItemSidecarWhenCharacterPersistenceFails(int optionId)
    {
        using var log = new StringWriter();
        using var temp = new TemporaryDirectory();
        var usersRoot = Path.Combine(temp.Path, "Users");
        var playerItemsRoot = Path.Combine(temp.Path, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        var initialItems = PlayerItems();
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, initialItems);
        var itemPath = Path.Combine(playerItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var itemBytes = File.ReadAllBytes(itemPath);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            usersRoot,
            defeatedGuardians: 45,
            attributes: new Dictionary<string, string>
            {
                [ForcedAgateRollAttribute] = "0",
            });
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(items: [RefineryBadgeItemType()]),
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            checked((byte)optionId));

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchanged));
        Assert.Same(session, unchanged);
        Assert.Equal(itemBytes, File.ReadAllBytes(itemPath));
        Assert.Contains("character reward persistence failed", log.ToString());
    }

    private static async ValueTask<bool> InvokeAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryHandleLegacyAgatePrizeCenterAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, Agate2NpcId, optionId, CancellationToken.None]);
        Assert.NotNull(result);
        return await (ValueTask<bool>)result;
    }

    private static WorldSession CreateSession(
        string usersRoot,
        ulong defeatedGuardians,
        IReadOnlyDictionary<string, string>? attributes = null,
        PlayerItemSnapshot? playerItems = null)
    {
        var mergedAttributes = new Dictionary<string, string>(
            attributes ?? new Dictionary<string, string>(),
            StringComparer.OrdinalIgnoreCase)
        {
            ["DefeatedArenaGuardians"] = defeatedGuardians.ToString(CultureInfo.InvariantCulture),
            ["PowerArenaDefeatedGuardians"] = defeatedGuardians.ToString(CultureInfo.InvariantCulture),
        };
        return new WorldSession(
            AccountId: CharacterUid,
            Username: "agate2-reward-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PrizeSeeker",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 139,
                Experience: 0,
                MapId: 1_771,
                X: 32,
                Y: 7,
                PreviousMapId: 1_771,
                PreviousMapX: 32,
                PreviousMapY: 7,
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
                Attributes: mergedAttributes,
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

    private static string WriteCharacterSnapshot(string usersRoot, ulong defeatedGuardians)
    {
        var path = Path.Combine(usersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini");
        File.WriteAllText(
            path,
            $"""
            [Character]
            UID={CharacterUid.ToString(CultureInfo.InvariantCulture)}
            Name=PrizeSeeker
            Body=1003
            Face=0
            Haire=0
            Class=7051
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=139
            Experience=0
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

    private static void AssertBadgeItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item));
        Assert.Equal(RefineryBadgeItemId, item.ItemId);
        Assert.Equal(2, item.StackSize);
        Assert.Equal(0, item.Bound);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, item.Mode);
    }

    private static void AssertLegacyNpcTeleportTransition(
        IReadOnlyList<byte[]> sent,
        int startIndex,
        uint expectedX,
        uint expectedY)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex]), out var teleport));
        Assert.Equal((uint)CharacterUid, teleport.ObjId);
        Assert.Equal(1_770U, teleport.DwParam);
        Assert.Equal(MsgActionType.Teleport, teleport.Type);
        Assert.Equal(expectedX, teleport.PositionX);
        Assert.Equal(expectedY, teleport.PositionY);
        Assert.Equal(1_770L, teleport.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 1]), out var startup));
        Assert.Equal(MsgActionType.LegacyMerchantStartup, startup.Type);
        Assert.Equal(2U, startup.DwParam);
        Assert.Equal(1_770L, startup.DwParam3);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 2]), out var stopVending));
        Assert.Equal(MsgActionType.StopVending, stopVending.Type);
        Assert.Equal(1_770U, stopVending.DwParam);

        Assert.True(MsgActionPacket.TryParse(ReadFrame(sent[startIndex + 3]), out var color));
        Assert.Equal(MsgActionType.LegacyColor, color.Type);
        Assert.Equal(expectedX, color.PositionX);
        Assert.Equal(expectedY, color.PositionY);

        Assert.True(LegacyMapStatusPacket.TryParse(ReadFrame(sent[startIndex + 4]), out var mapStatus));
        Assert.Equal(LegacyMapStatusPacket.FromMap(1_770, mapsById: null), mapStatus);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static void AssertSingleUpdate(byte[] bytes, LegacyMsgUpdateDataType type, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(type, update.Type);
        Assert.Equal(value, update.Value);
    }

    private static void AssertAngelwingEffect(byte[] bytes)
    {
        Assert.True(LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal((uint)CharacterUid, packet.Uid);
        Assert.Equal(LegacyStringId.Effect, packet.Id);
        Assert.Equal(["angelwing"], packet.Strings);
    }

    private static void AssertTopLeftSystem(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RuneRankChatType, packet.ChatType);
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(expectedText, packet.Content[3]);
    }

    private static void AssertSystemTalk(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        Assert.Equal(expectedText, packet.Content[3]);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static PlayerItemSnapshot PlayerItems(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshot CreateInventory(int count)
    {
        return PlayerItems(Enumerable.Range(0, count)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = checked((uint)(1_000 + index)),
                ItemId = checked((uint)(700_000 + index)),
                Durability = 1,
                MaximumDurability = 1,
                Position = 0,
                StackSize = 1,
            })
            .ToArray());
    }

    private static ItemType RefineryBadgeItemType()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = RefineryBadgeItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "RefineryBadge";
        fields[21] = "1";
        fields[46] = "10";
        fields[52] = "QuestItem";
        return new ItemType(checked((int)RefineryBadgeItemId), fields[1], fields);
    }

    private static LevelExperience LevelExperienceRow()
    {
        return new LevelExperience(
            Type: 0,
            Level: 139,
            Experience: 100_000,
            UpLevTime: 10_000,
            MentorUpLevTime: 10_000,
            SourceLine: 1,
            RawLine: string.Empty,
            Fields: ["0", "139", "100000", "10000", "10000"]);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(
        IReadOnlyList<ItemType>? items = null,
        IReadOnlyList<LevelExperience>? levelExperience = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://agate2-reward",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items ?? [],
            itemAddBonuses: [],
            levelExperience: levelExperience ?? [],
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "wolf-agate2-reward-" + Guid.NewGuid().ToString("N"));
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
