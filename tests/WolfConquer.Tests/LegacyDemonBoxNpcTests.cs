using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDemonBoxNpcTests : IDisposable
{
    private const uint DemonBoxNpcId = 4_294_967_259;
    private const uint AuroraDemonBoxNpcId = 4_294_967_260;
    private const uint HeavenDemonBoxNpcId = 4_294_967_263;
    private const uint DemonBoxItemId = 720_650;
    private const uint AuroraDemonBoxItemId = 720_674;
    private const uint HeavenDemonBoxItemId = 720_671;
    private const uint HeavenDemonMonsterId = 2_435;
    private const uint CharacterUid = 1_293_600;

    private readonly string _itemsRoot = Path.Combine(Path.GetTempPath(), "wolf-demon-box-items-" + Guid.NewGuid());

    [Fact]
    public async Task HandlerHeavenDemonBoxConsumesBoxAndSpawnsSourceMonsterInHouseInstance()
    {
        Directory.CreateDirectory(_itemsRoot);
        var initialItems = new PlayerItemSnapshot(
            [Item(uid: 50, HeavenDemonBoxItemId, stackSize: 1)],
            [],
            [],
            [],
            []);
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            initialItems.InventoryItems,
            dynamicId: CharacterUid.ToString(CultureInfo.InvariantCulture));
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        sessions.Set(connection, session);

        var handled = await InvokeDemonBoxHandlerAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                WorldData: CreateWorldDataCatalog(CreateMonster(HeavenDemonMonsterId, "HeavenDemon")),
                MonsterStates: monsterStates,
                PlayerItemsRoot: _itemsRoot),
            session,
            HeavenDemonBoxNpcId,
            optionId: 1);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(CharacterUid).InventoryItems);

        Assert.Contains(ParseItemRemovals(sent), packet => packet.Id == 50);
        var spawn = Assert.Single(ParseMonsterSpawns(sent), packet => packet.MonsterId == HeavenDemonMonsterId);
        Assert.Equal(session.Character.X, spawn.X);
        Assert.Equal(session.Character.Y, spawn.Y);
        Assert.Equal("HeavenDemon", spawn.Names[0]);
        var message = Assert.Single(ParseMessages(sent));
        Assert.Contains("You opened the Heaven Demon Box", message.Content[3], StringComparison.Ordinal);

        var dynamicMonsters = monsterStates.GetVisibleDynamicMonsters(
            checked((int)session.Character.MapId),
            CharacterUid,
            DateTimeOffset.UtcNow);
        var dynamicMonster = Assert.Single(dynamicMonsters, entry => entry.Instance.MonsterId == HeavenDemonMonsterId);
        Assert.True(dynamicMonster.Instance.RemoveOnDead);
        Assert.Contains("source=NpcHandler.HeavenDemonBox", log.ToString());
        Assert.DoesNotContain("PARITY-GAP LegacyNpc DemonBox dynamic spawn", log.ToString());
    }

    [Fact]
    public async Task HandlerDemonBoxMissingItemReturnsWithoutDialogOrMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeDemonBoxHandlerAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            DemonBoxNpcId,
            optionId: 0);

        Assert.True(handled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Contains("itemId=720650", log.ToString());
        Assert.Contains("source returns before dialog", log.ToString());
    }

    [Fact]
    public async Task HandlerAuroraDemonBoxIntroDialogUsesSourceOptionsAndEnglishText()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession([Item(uid: 60, AuroraDemonBoxItemId, stackSize: 1)]);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await InvokeDemonBoxHandlerAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            AuroraDemonBoxNpcId,
            optionId: 0);

        Assert.True(handled);
        var replies = ParseNpcReplies(sent);
        Assert.Equal(5, replies.Count);
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
                reply.Text.Contains("Pilgrim CP Pack (690000 CPs)", StringComparison.Ordinal));
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.OptionId == 1 &&
                reply.Text == "Yes!");
        Assert.Contains(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Option &&
                reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
                reply.Text == "Wait...");
        Assert.DoesNotContain(replies, reply => ContainsCjk(reply.Text));
        Assert.DoesNotContain(replies, reply => reply.Text.Contains(string.Concat((char)0x5C, 'u'), StringComparison.Ordinal));
        Assert.Contains("source=NpcHandler.AuroraDemonBox", log.ToString());
    }

    private static async ValueTask<bool> InvokeDemonBoxHandlerAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "TryHandleLegacyDemonBoxesAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(null, [context, session, npcId, optionId, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result).ConfigureAwait(false);
    }

    private static WorldSession CreateSession(
        IReadOnlyList<PlayerItemSnapshotItem> inventoryItems,
        string dynamicId = "0")
    {
        return new WorldSession(
            AccountId: 100,
            Username: "demon-box-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "HeroOne",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 10_098,
                X: 133,
                Y: 177,
                PreviousMapId: 10_098,
                PreviousMapX: 133,
                PreviousMapY: 177,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = dynamicId,
                },
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: new PlayerItemSnapshot(inventoryItems, [], [], [], []),
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshotItem Item(uint uid, uint itemId, ushort stackSize)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = 0,
            StackSize = stackSize,
        };
    }

    private static Monster CreateMonster(uint id, string name)
    {
        return new Monster(
            checked((int)id),
            name,
            new Dictionary<string, string>
            {
                ["life"] = "200000",
                ["lookface"] = "900",
                ["level"] = "138",
                ["defense"] = "0",
                ["defense2"] = "10000",
                ["attack_min"] = "1",
                ["attack_max"] = "1",
                ["attack_range"] = "0",
                ["Boss"] = "0",
            },
            []);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params Monster[] monsters)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-13T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-demon-box",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [],
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
            monsters: monsters,
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
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

    private static IReadOnlyList<MsgItemUsagePacket> ParseItemRemovals(IEnumerable<byte[]> sent)
    {
        var packets = new List<MsgItemUsagePacket>();
        foreach (var bytes in sent)
        {
            if (MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Action == MsgItemUsageAction.RemoveInventory)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMonsterSpawnPacket> ParseMonsterSpawns(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyMonsterSpawnPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyMonsterSpawnPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static bool ContainsCjk(string value)
    {
        return value.Any(character => character is >= (char)0x4E00 and <= (char)0x9FFF);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    public void Dispose()
    {
        if (Directory.Exists(_itemsRoot))
        {
            Directory.Delete(_itemsRoot, recursive: true);
        }
    }
}
