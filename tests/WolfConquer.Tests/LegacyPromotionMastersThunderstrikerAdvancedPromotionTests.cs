using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersThunderstrikerAdvancedPromotionTests : IDisposable
{
    private const uint ThunderstrikerPromotionNpcId = 23_599;
    private const long CharacterUid = 1_293_712;
    private const byte PromotionOption = 1;
    private const uint EmeraldItemId = 1_080_001;
    private const uint MeteorItemId = 1_088_001;
    private const uint MoonBoxItemId = 721_080;
    private const uint DragonBallItemId = 1_088_000;
    private const ushort FirstPromotionSpellId = 15_720;
    private const ushort SecondPromotionSpellId = 15_820;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersThunderstrikerAdvancedPromotionTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<long, long, string, string, ushort> LevelGateCases => new()
    {
        {
            9_000,
            14,
            "\tWhen you reach Level 15, you can get promoted to [Thunderstriker] and learn more thunder skills.",
            "I`ll~try~to~reach~Lv.15.",
            234
        },
        {
            9_001,
            39,
            "\tWhen you reach Level 40, you can get promoted to [Elite~T.striker] and learn more thunder skills.",
            "I`ll~try~to~reach~Lv.40.",
            234
        },
        {
            9_002,
            69,
            "There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level 70+]",
            "I~see.",
            6
        },
        {
            9_003,
            99,
            "There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level 100+]",
            "I~see.",
            6
        },
        {
            9_004,
            109,
            "There is much that you must learn before you can accept my training. Slaughter a legion of monsters first! [Level 110+]",
            "I~see.",
            6
        },
    };

    public static TheoryData<long, long, ushort, long> SpellPromotionCases => new()
    {
        { 9_000, 15, FirstPromotionSpellId, 9_001 },
        { 9_001, 40, SecondPromotionSpellId, 9_002 },
    };

    public static TheoryData<long, long, uint, string> MissingItemCases => new()
    {
        {
            9_002,
            70,
            EmeraldItemId,
            "I'm sorry but you need to have 1 Emerald at your inventory , to get one go and kill some HillMonsters ( Desert City ) to get one ."
        },
        { 9_003, 100, MeteorItemId, "I'm sorry but you need to have 1 Meteor at your inventory." },
        { 9_004, 110, MoonBoxItemId, "I'm sorry but you need to have 1 MoonBox at your inventory." },
    };

    public static TheoryData<long, long, uint, long, string> ItemPromotionCases => new()
    {
        {
            9_002,
            70,
            EmeraldItemId,
            9_003,
            "Excellent! You`ve learned ways of the Taoist. Do you feel a different energy in your body? That`s stronger power and battle prowess!"
        },
        {
            9_003,
            100,
            MeteorItemId,
            9_004,
            "Excellent! Do you feel a different energy in your body? That`s stronger power and battle prowess!"
        },
    };

    [Theory]
    [MemberData(nameof(LevelGateCases))]
    public async Task Option1RejectsLevelGatesWithoutMutation(
        long classCode,
        long level,
        string expectedText,
        string expectedOption,
        ushort expectedAvatar)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-level-" + classCode.ToString(CultureInfo.InvariantCulture));
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var session = CreateSession(
            classCode,
            level,
            usersRoot: usersRoot,
            playerItems: Inventory(
                Item(uid: 2_000, itemId: EmeraldItemId),
                Item(uid: 2_001, itemId: MeteorItemId),
                Item(uid: 2_002, itemId: MoonBoxItemId)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ThunderstrikerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Equal(
            [EmeraldItemId, MeteorItemId, MoonBoxItemId],
            updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Empty(ParseGameItemPackets(sent));

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == expectedText);
        Assert.Contains(
            replies,
            reply => reply.Text == expectedOption &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == expectedAvatar);
        Assert.Contains(
            "Class=" + classCode.ToString(CultureInfo.InvariantCulture),
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);
        Assert.Contains("source level gate rejected", log.ToString());
        Assert.DoesNotContain("PARITY-GAP", log.ToString());
    }

    [Theory]
    [MemberData(nameof(SpellPromotionCases))]
    public async Task Option1FirstTwoRanksLearnSpellPersistAndPromote(
        long classCode,
        long level,
        ushort spellId,
        long expectedClass)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-spell-" + classCode.ToString(CultureInfo.InvariantCulture));
        var spellsRoot = Path.Combine(_root, "spells-" + classCode.ToString(CultureInfo.InvariantCulture));
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var session = CreateSession(classCode, level, usersRoot: usersRoot);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            ThunderstrikerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(expectedClass, updated.Character.Class);
        Assert.Equal(new uint[] { spellId }, updated.PlayerSpells.SpellIds.ToArray());
        Assert.Equal(new uint[] { spellId }, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
        Assert.Equal([spellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());

        var replies = ParseNpcReplies(sent);
        Assert.Contains(replies, reply => reply.Text == "Congratulations! You have been promoted.");
        Assert.Contains(
            replies,
            reply => reply.Text == "Great,~thanks!" &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 234);
        Assert.Contains(
            "Class=" + expectedClass.ToString(CultureInfo.InvariantCulture),
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer handled LegacyNpc PromotionThunderstriker advanced promotion", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:102382-102525", logText);
        Assert.Contains("option=1", logText);
        Assert.Contains("spellId=" + spellId.ToString(CultureInfo.InvariantCulture), logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [MemberData(nameof(MissingItemCases))]
    public async Task Option1RejectsMissingPromotionItemsWithoutMutation(
        long classCode,
        long level,
        uint requiredItemId,
        string expectedText)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-missing-" + classCode.ToString(CultureInfo.InvariantCulture));
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var session = CreateSession(
            classCode,
            level,
            usersRoot: usersRoot,
            playerItems: Inventory(Item(uid: 2_000, itemId: 725_016)));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            ThunderstrikerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(classCode, updated.Character.Class);
        Assert.Equal([725_016U], updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.Empty(ParseItemUsagePackets(sent));
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == expectedText);
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "I~see." && reply.OptionId == byte.MaxValue);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
                reply.InputMaxLength == 6);
        Assert.Contains(
            "Class=" + classCode.ToString(CultureInfo.InvariantCulture),
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("source item gate rejected", logText);
        Assert.Contains("itemId=" + requiredItemId.ToString(CultureInfo.InvariantCulture), logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Theory]
    [MemberData(nameof(ItemPromotionCases))]
    public async Task Option1ConsumesRequiredItemPersistsAndPromotes(
        long classCode,
        long level,
        uint requiredItemId,
        long expectedClass,
        string expectedSuccessText)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-item-" + classCode.ToString(CultureInfo.InvariantCulture));
        var itemsRoot = Path.Combine(_root, "items-" + classCode.ToString(CultureInfo.InvariantCulture));
        CreateMinimalCharacterSnapshot(usersRoot, classCode);
        var requiredItem = Item(uid: 3_000, itemId: requiredItemId);
        var session = CreateSession(
            classCode,
            level,
            usersRoot: usersRoot,
            playerItems: Inventory(
                Item(uid: 2_000, itemId: 725_016),
                requiredItem));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            ThunderstrikerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(expectedClass, updated.Character.Class);
        Assert.Equal([725_016U], updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        Assert.Equal([725_016U], new PlayerItemSnapshotLoader(itemsRoot)
            .LoadByCharacterUid(CharacterUid)
            .InventoryItems
            .Select(item => item.ItemId)
            .ToArray());

        var removes = ParseItemUsagePackets(sent);
        var remove = Assert.Single(removes);
        Assert.Equal(requiredItem.Uid, remove.Packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, remove.Packet.Action);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == expectedSuccessText);
        Assert.Contains(
            "Class=" + expectedClass.ToString(CultureInfo.InvariantCulture),
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        var logText = log.ToString();
        Assert.Contains("WorldServer persisted LegacyNpc PromotionMaster item mutation", logText);
        Assert.Contains("WorldServer handled LegacyNpc PromotionThunderstriker advanced promotion", logText);
        Assert.Contains("consumedItemId=" + requiredItemId.ToString(CultureInfo.InvariantCulture), logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task Option1ConsumesMoonBoxGrantsDragonBallTalksAndPromotes()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "users-moonbox");
        var itemsRoot = Path.Combine(_root, "items-moonbox");
        CreateMinimalCharacterSnapshot(usersRoot, classCode: 9_004);
        var moonBox = Item(uid: 3_000, itemId: MoonBoxItemId);
        var session = CreateSession(
            classCode: 9_004,
            level: 110,
            usersRoot: usersRoot,
            playerItems: Inventory(
                Item(uid: 2_000, itemId: 725_016),
                moonBox));
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(DragonBallItemId, "DragonBall", durability: 77)),
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            ThunderstrikerPromotionNpcId,
            PromotionOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(9_005, updated.Character.Class);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        Assert.Equal([725_016U, DragonBallItemId], updated.PlayerItems.InventoryItems.Select(item => item.ItemId).ToArray());
        var dragonBall = Assert.Single(updated.PlayerItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Equal((ushort)77, dragonBall.Durability);
        Assert.Equal((ushort)77, dragonBall.MaximumDurability);

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == MoonBoxItemId);
        Assert.Contains(persistedItems.InventoryItems, item => item.ItemId == DragonBallItemId);
        Assert.Contains(
            "Class=9005",
            File.ReadAllText(Path.Combine(usersRoot, CharacterUid + ".ini")),
            StringComparison.Ordinal);

        var remove = Assert.Single(ParseItemUsagePackets(sent));
        Assert.Equal(moonBox.Uid, remove.Packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, remove.Packet.Action);
        var granted = Assert.Single(ParseGameItemPackets(sent));
        Assert.Equal(DragonBallItemId, granted.Packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, granted.Packet.Mode);
        Assert.Equal((ushort)77, granted.Packet.Durability);
        Assert.True(remove.Index < granted.Index);

        var talk = Assert.Single(ParseSystemTalkMessages(sent));
        Assert.Equal("You received DragonBall.", talk.Packet.Content[3]);
        Assert.True(granted.Index < talk.Index);
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Excellent!Do you feel a different energy in your body? That`s stronger power and battle prowess!");

        var logText = log.ToString();
        Assert.Contains("consumedItemId=721080", logText);
        Assert.Contains("grantedItemId=1088000", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        long classCode,
        long level,
        string? usersRoot = null,
        PlayerItemSnapshot? playerItems = null,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "thunderstriker-advanced-promotion-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "ThunderAdvanced",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: classCode,
                SecondClass: 0,
                Reborn: 0,
                Level: level,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot Inventory(params PlayerItemSnapshotItem[] items)
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems = items,
        };
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = 1,
        };
    }

    private static void CreateMinimalCharacterSnapshot(string usersRoot, long classCode)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid + ".ini"),
            "[Character]" + Environment.NewLine +
            "UID=" + CharacterUid + Environment.NewLine +
            "Class=" + classCode + Environment.NewLine);
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

    private static IReadOnlyList<LegacySpellPacket> ParseSpellPackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacySpellPacket>();
        foreach (var bytes in sent)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<(int Index, MsgItemUsagePacket Packet)> ParseItemUsagePackets(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, MsgItemUsagePacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add((index, packet));
            }
        }

        return packets;
    }

    private static IReadOnlyList<(int Index, LegacyGameItemPacket Packet)> ParseGameItemPackets(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, LegacyGameItemPacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyGameItemPacket.TryParse(ReadFrame(sent[index]), out var packet))
            {
                packets.Add((index, packet));
            }
        }

        return packets;
    }

    private static IReadOnlyList<(int Index, LegacyMsgMessagePacket Packet)> ParseSystemTalkMessages(
        IReadOnlyList<byte[]> sent)
    {
        var packets = new List<(int Index, LegacyMsgMessagePacket Packet)>();
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.ChatType == LegacyMsgMessagePacket.TalkChatType)
            {
                packets.Add((index, packet));
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://thunderstriker-advanced-promotion",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
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

    private static ItemType CreateItemType(uint itemTypeId, string name, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
