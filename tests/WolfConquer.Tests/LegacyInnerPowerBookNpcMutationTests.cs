using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyInnerPowerBookNpcMutationTests : IDisposable
{
    private const uint InnerPowerBookNpcId = 18_786;
    private const uint FirstBookItemId = 3_315_001;
    private const uint SecondBookItemId = 3_315_002;
    private const uint NextStageBookItemId = 3_315_003;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WolfConquer-LegacyInnerPowerBookNpc-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FirstBookGrantUsesStageOneSlotOneAndPersistsBeforeItsDialog()
    {
        var worldData = CreateCatalog(
            rows: [],
            secrets: [CreateSecret(1, 1)],
            types: [CreateType(1, 1, 11, FirstBookItemId)],
            items: [CreateItem(FirstBookItemId, "Universal Concept (A)")]);

        var result = await InvokeAsync(worldData, optionId: 2);

        var itemPacket = AssertGameItem(result.Sent[0]);
        Assert.Equal(FirstBookItemId, itemPacket.ItemId);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains("Universal Concept (A)", StringComparison.Ordinal));
        var item = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(FirstBookItemId, item.ItemId);
        var persisted = new PlayerItemSnapshotLoader(result.PlayerItemsRoot).LoadByCharacterUid(result.UpdatedSession.Character.Uid);
        Assert.Contains(persisted.InventoryItems, candidate => candidate.ItemId == FirstBookItemId);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task FirstBookGrantFencesWhenStageOneBookDataIsMissing()
    {
        var worldData = CreateCatalog(rows: [], secrets: [CreateSecret(1, 1)], types: [], items: []);

        var result = await InvokeAsync(worldData, optionId: 2);

        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.DoesNotContain(result.Sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Contains("PARITY-GAP LegacyNpc AdminUtility", result.Log);
        Assert.Contains("stage 1 slot 1", result.Log);
    }

    [Fact]
    public async Task NextBookGrantSelectsNextGongAndKeepsSourceMessageItemDialogOrder()
    {
        var worldData = CreateCatalog(
            rows: [CreateInnerPowerRow(firstComplete: true, secondUnlocked: false, secondComplete: false)],
            secrets: [CreateSecret(1, 2)],
            types:
            [
                CreateType(1, 1, 11, FirstBookItemId),
                CreateType(1, 2, 12, SecondBookItemId),
            ],
            items:
            [
                CreateItem(FirstBookItemId, "Universal Concept (A)"),
                CreateItem(SecondBookItemId, "Universal Concept (B)"),
            ]);

        var result = await InvokeAsync(worldData, optionId: 10);

        AssertMessage(result.Sent[0], "Universal Concept (B)");
        Assert.Equal(SecondBookItemId, AssertGameItem(result.Sent[1]).ItemId);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains("Universal Concept (B)", StringComparison.Ordinal));
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == SecondBookItemId);
        var persisted = new PlayerItemSnapshotLoader(result.PlayerItemsRoot).LoadByCharacterUid(result.UpdatedSession.Character.Uid);
        Assert.Contains(persisted.InventoryItems, item => item.ItemId == SecondBookItemId);
    }

    [Fact]
    public async Task NextBookGrantSelectsFirstGongOfNextCompletedStage()
    {
        var worldData = CreateCatalog(
            rows: [CreateInnerPowerRow(firstComplete: true, secondUnlocked: true, secondComplete: true)],
            secrets: [CreateSecret(1, 2), CreateSecret(2, 1)],
            types:
            [
                CreateType(1, 1, 11, FirstBookItemId),
                CreateType(1, 2, 12, SecondBookItemId),
                CreateType(2, 1, 21, NextStageBookItemId),
            ],
            items:
            [
                CreateItem(FirstBookItemId, "Universal Concept (A)"),
                CreateItem(SecondBookItemId, "Universal Concept (B)"),
                CreateItem(NextStageBookItemId, "Universal Concept (C)"),
            ]);

        var result = await InvokeAsync(worldData, optionId: 10);

        Assert.Equal(NextStageBookItemId, AssertGameItem(result.Sent[0]).ItemId);
        AssertMessage(result.Sent[1], "Universal Concept (C)");
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == NextStageBookItemId);
    }

    [Fact]
    public async Task NextBookShowsTrainingDialogForIncompleteLastGong()
    {
        var worldData = CreateCatalog(
            rows: [CreateInnerPowerRow(firstComplete: false, secondUnlocked: false, secondComplete: false)],
            secrets: [CreateSecret(1, 2)],
            types:
            [
                CreateType(1, 1, 11, FirstBookItemId),
                CreateType(1, 2, 12, SecondBookItemId),
            ],
            items:
            [
                CreateItem(FirstBookItemId, "Universal Concept (A)"),
                CreateItem(SecondBookItemId, "Universal Concept (B)"),
            ]);

        var result = await InvokeAsync(worldData, optionId: 10);

        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains("train the power of Universal Concept (A)", StringComparison.Ordinal));
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.DoesNotContain(result.Sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NextBookGrantRejectsDuplicateAndFullInventoryWithoutMutation(bool duplicate)
    {
        var worldData = CreateCatalog(
            rows: [CreateInnerPowerRow(firstComplete: true, secondUnlocked: false, secondComplete: false)],
            secrets: [CreateSecret(1, 2)],
            types:
            [
                CreateType(1, 1, 11, FirstBookItemId),
                CreateType(1, 2, 12, SecondBookItemId),
            ],
            items:
            [
                CreateItem(FirstBookItemId, "Universal Concept (A)"),
                CreateItem(SecondBookItemId, "Universal Concept (B)"),
            ]);
        var inventory = duplicate
            ? [CreateInventoryItem(1, SecondBookItemId)]
            : Enumerable.Range(1, 40).Select(index => CreateInventoryItem((uint)index, 7_000_000U + (uint)index)).ToArray();

        var result = await InvokeAsync(worldData, optionId: 10, inventory);

        Assert.DoesNotContain(result.Sent, bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) && packet.ItemId == SecondBookItemId);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems.Skip(inventory.Length), item => item.ItemId == SecondBookItemId);
        if (duplicate)
        {
            Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains("train the power of Universal Concept (B)", StringComparison.Ordinal));
        }
        else
        {
            AssertMessage(result.Sent[0], "Please make 1 more space in your inventory.");
        }
    }

    private async Task<InnerPowerInvokeResult> InvokeAsync(
        WorldDataCatalog worldData,
        byte optionId,
        IReadOnlyList<PlayerItemSnapshotItem>? inventory = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
        var playerItemsRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var session = CreateSession() with
        {
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = inventory ?? [] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            InnerPowerBookNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InnerPowerInvokeResult(handled, sent, log.ToString(), updatedSession, playerItemsRoot);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 100,
            Username: "inner-power-book-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "InnerPowerHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 2,
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
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static InnerPowerRankEntry CreateInnerPowerRow(bool firstComplete, bool secondUnlocked, bool secondComplete)
    {
        string[] fields =
        [
            "InnerPowerHero", "1293600", "0", "1", "1", "1", "2",
            "11", firstComplete ? "100" : "0", "1", "7", firstComplete ? "1" : "0",
            "12", secondComplete ? "100" : "0", secondUnlocked ? "1" : "0", secondUnlocked ? "7" : "0", secondComplete ? "1" : "0",
            "",
        ];
        return new InnerPowerRankEntry(fields[0], 1_293_600, fields.Skip(2).Take(fields.Length - 3).ToArray(), "InnerPower.txt", 2, string.Join('/', fields), fields);
    }

    private static NeiGongSecret CreateSecret(int id, int count)
    {
        return new NeiGongSecret(id, $"Stage{id}", "", count, [], [], "", "NeiGongInfo.ini", id, id.ToString(), new Dictionary<string, string>());
    }

    private static NeiGongType CreateType(int stageId, int slot, int typeId, uint itemId)
    {
        return new NeiGongType(stageId, slot, typeId, $"Gong{typeId}", "", 7, [], [], [1, 1, 1, 1, 1, 1, 1], "", 0, 0, 0, checked((int)itemId), "NeiGongInfo.ini", slot, $"{stageId}-{slot}", new Dictionary<string, string>());
    }

    private static ItemType CreateItem(uint itemId, string name)
    {
        var fields = Enumerable.Repeat("0", ItemType.DurabilityFieldIndex + 1).ToArray();
        fields[ItemType.DurabilityFieldIndex] = "100";
        return new ItemType(checked((int)itemId), name, fields);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(uint uid, uint itemId)
    {
        return new PlayerItemSnapshotItem { Uid = uid, ItemId = itemId, StackSize = 1 };
    }

    private static void AssertMessage(byte[] bytes, string expectedContent)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message));
        Assert.Contains(expectedContent, message.Content[3], StringComparison.Ordinal);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item));
        return item;
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

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<InnerPowerRankEntry> rows,
        IReadOnlyList<NeiGongSecret> secrets,
        IReadOnlyList<NeiGongType> types,
        IReadOnlyList<ItemType> items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(DateTimeOffset.Parse("2026-07-10T00:00:00Z"), "fixture://inner-power-book", new Dictionary<string, int>()),
            clientConfigs: [], items: items, itemAddBonuses: [], levelExperience: [], runeLevelExperience: [], runeStorageAttributes: [], itemRefineEffects: [], itemRefineEffectEx: [], itemRefineUpgrades: [], transformInfo: [], furniture: [], hairColorTypes: [], hairFaceStorageTypes: [], coatStorageTypes: [], coatStorageAttributes: [], coatColorRules: [], xuanbaoAdditionAttributes: [], xuanbaoComposeAttributeLimits: [], xuanbaoRandomAttributes: [], christmasNpcs: [], tutorTypes: [], tutorBattleLimitTypes: [], beastAttributes: [], fateExperience: [], spiritRates: [], hundredWeapons: [], gouyuTypes: [], gouyuImmortals: [], specialEffectAttributes: [], daoqiDictTypes: [], syndicateFormationTypes: [], syndicateSkillTypes: [], disdainRules: [], jiangHuAttributes: [], jiangHuCultivateStatuses: [], refineryBoxes: [], refinerySouls: [], refineryMaterials: [], stateCountFiles: [], binarySeedFiles: [], emptySeedFiles: [], backupSeedFiles: [], pokerTables: [], rouletteTables: [], npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"), meltingTypeRules: [], lotteryPrizes: [], arenaRecords: [], elitePkRecords: [], teamElitePkRecords: [], skillTeamPkRecords: [], classPkWarRecords: [], combatGearRanks: [], prestigeRanks: [], innerPowerRanks: rows, runeRankings: [], staticStatues: [], newPlayerRewards: [], votes: [], couplesPkRecords: [], socialAssociations: [], guildWarInfo: EmptyGuildWarData("guildwar"), superGuildWarInfo: EmptyGuildWarData("superguildwar"), classLevelStats: [], professionLevelUps: [], professionLevelBenefits: [], professionTitleBenefits: [], questInfo: new QuestInfoMetadata(null, null, null), questTaskTypes: [], questMissions: [], quizQuestions: [], magic: [], magicEffects: [], magicTypeOperations: [], titleTypes: [], combatGear: [], traps: [], npcs: [], sobNpcs: [], portals: [], maps: [], mapNames: [], monsters: [], monsterSpawns: [], lotteryDictionary: [], collectionRewardTiers: [], taskRewardTypes: [], taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(secrets.Count, [], "NeiGongInfo.ini", 1, "NeiGong", new Dictionary<string, string>()), neiGongSecrets: secrets, neiGongTypes: types, subProfessions: [], syndicateLevels: [], stageGoalInfo: new StageGoalInfo(0), stageGoalStages: [], stageGoalTasks: [], shops: [], shopItems: [], exchangeShopGoods: [], exchangeShopGoodsEx: [], exchangeShopLevels: [], honorShopItems: [], honorShopRecommendations: [], racePointShopItems: [], racePointShopRecommendations: [], goldenLeagueShopItems: [], goldenLeagueShopRecommendations: [], goldenLeagueShopConfig: new Dictionary<string, string>(), booths: [], boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed record InnerPowerInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession,
        string PlayerItemsRoot);
}
