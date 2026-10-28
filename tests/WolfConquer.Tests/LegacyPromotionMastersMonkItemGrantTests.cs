using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersMonkItemGrantTests : IDisposable
{
    private const uint MonkPromotionNpcId = 8_314;
    private const long CharacterUid = 1_293_600;
    private const byte WhirlwindKickOption = 71;
    private const ushort WhirlwindKickSpellId = 10_415;
    private const uint WhirlwindKickRewardItemId = 622_439;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersMonkItemGrantTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task WhirlwindKickOptionGrantsTwoSourceItemsPersistsAndStillLearnsSpell()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "spells-success");
        var itemsRoot = Path.Combine(_root, "items-success");
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession(playerItems: ExistingInventory());
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(WhirlwindKickRewardItemId, durability: 123)),
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot,
                PlayerItemsRoot: itemsRoot), ref session),
            session,
            MonkPromotionNpcId,
            WhirlwindKickOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([WhirlwindKickSpellId], updated.PlayerSpells.SpellIds.Select(id => (ushort)id).ToArray());

        var granted = updated.PlayerItems.InventoryItems
            .Where(item => item.ItemId == WhirlwindKickRewardItemId)
            .ToArray();
        Assert.True(granted.Length == 2, log.ToString());
        Assert.Equal([2_001U, 2_002U], granted.Select(item => item.Uid).ToArray());
        Assert.All(granted, item =>
        {
            Assert.Equal((ushort)123, item.Durability);
            Assert.Equal((ushort)123, item.MaximumDurability);
            Assert.Equal((ushort)0, item.Position);
            Assert.Equal((ushort)1, item.StackSize);
            Assert.Equal((byte)0, item.Bound);
            Assert.Equal((byte)0, item.Plus);
        });

        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.Equal(
            [2_000U, 2_001U, 2_002U],
            persistedItems.InventoryItems.Select(item => item.Uid).ToArray());
        Assert.Equal(
            [WhirlwindKickRewardItemId, WhirlwindKickRewardItemId],
            persistedItems.InventoryItems
                .Where(item => item.ItemId == WhirlwindKickRewardItemId)
                .Select(item => item.ItemId)
                .ToArray());
        Assert.Equal([WhirlwindKickSpellId], new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .Select(id => (ushort)id)
            .ToArray());

        var itemPackets = ParseGameItemPackets(sent);
        Assert.Equal(2, itemPackets.Count);
        Assert.Equal([2_001U, 2_002U], itemPackets.Select(entry => entry.Packet.Uid).ToArray());
        Assert.All(itemPackets, entry =>
        {
            Assert.Equal(WhirlwindKickRewardItemId, entry.Packet.ItemId);
            Assert.Equal(LegacyGameItemPacket.AddItemMode, entry.Packet.Mode);
            Assert.Equal((ushort)123, entry.Packet.Durability);
            Assert.Equal((ushort)123, entry.Packet.MaximumDurability);
        });
        var spellIndex = FindSpellPacketIndex(sent, WhirlwindKickSpellId);
        Assert.True(itemPackets.Max(entry => entry.Index) < spellIndex);
        Assert.Equal([WhirlwindKickSpellId], ParseSpellPackets(sent).Select(packet => packet.Id).ToArray());
        Assert.Contains(
            ParseNpcReplies(sent),
            reply => reply.Text == "Congratulation! You just have learned the skill WhirlwindKick");

        var logText = log.ToString();
        Assert.Contains("WorldServer persisted LegacyNpc PromotionMonk WhirlwindKick item grant", logText);
        Assert.Contains("WorldServer applied LegacyNpc PromotionMonk WhirlwindKick item grant", logText);
        Assert.Contains("source=NpcHandler.PromotionMonk", logText);
        Assert.Contains("sourceRange=NpcHandler.cs:43580-43601", logText);
        Assert.Contains("option=71", logText);
        Assert.Contains("itemId=622439", logText);
        Assert.Contains("itemCount=2", logText);
        Assert.Contains("WorldServer handled LegacyNpc PromotionMaster skill", logText);
        Assert.Contains("spellId=10415", logText);
        Assert.DoesNotContain("PARITY-GAP", logText);
    }

    [Fact]
    public async Task WhirlwindKickOptionWithoutRequiredItemsRootLeavesWholeGrantUnchanged()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "spells-missing-items-root");
        Directory.CreateDirectory(spellsRoot);
        var session = CreateSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(WhirlwindKickRewardItemId, durability: 87)),
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot), ref session),
            session,
            MonkPromotionNpcId,
            WhirlwindKickOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Same(session, updated);
        Assert.Empty(updated.PlayerSpells.Spells);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(CharacterUid).Spells);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Empty(sent);
        Assert.Contains("required PlayerItemsRoot unavailable", log.ToString());
    }

    [Fact]
    public async Task WhirlwindKickOptionAlreadyLearnedDoesNotGrantDuplicateItems()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(_root, "spells-already-learned");
        var itemsRoot = Path.Combine(_root, "items-already-learned");
        Directory.CreateDirectory(spellsRoot);
        Directory.CreateDirectory(itemsRoot);
        var learned = new PlayerSpellSnapshot([CreateSpellEntry(WhirlwindKickSpellId)]);
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, learned);
        var session = CreateSession(playerSpells: learned);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            LegacySpellProducerTestOwner.PrepareContext(new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(CreateItemType(WhirlwindKickRewardItemId, durability: 87)),
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot,
                PlayerItemsRoot: itemsRoot), ref session),
            session,
            MonkPromotionNpcId,
            WhirlwindKickOption,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.Empty(ParseGameItemPackets(sent));
        Assert.Empty(ParseSpellPackets(sent));
        Assert.Contains(ParseNpcReplies(sent), reply => reply.Text == "You`ve learned WhirlwindKick!");

        var logText = log.ToString();
        Assert.Contains("spellId=10415; already learned", logText);
        Assert.DoesNotContain("WhirlwindKick item grant", logText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot? playerItems = null,
        PlayerSpellSnapshot? playerSpells = null)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "monk-item-grant-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "MonkHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 6_004,
                FirstClass: 6_004,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells ?? PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot ExistingInventory()
    {
        return PlayerItemSnapshot.Empty with
        {
            InventoryItems =
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 2_000,
                    ItemId = 725_016,
                    Durability = 1,
                    MaximumDurability = 1,
                    Position = 0,
                    StackSize = 1,
                },
            ],
        };
    }

    private static PlayerSpellSnapshotEntry CreateSpellEntry(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
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

    private static int FindSpellPacketIndex(IReadOnlyList<byte[]> sent, ushort spellId)
    {
        for (var index = 0; index < sent.Count; index++)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(sent[index]), out var packet) &&
                packet.Id == spellId)
            {
                return index;
            }
        }

        return -1;
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

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://monk-item-grant",
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

    private static ItemType CreateItemType(uint itemTypeId, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "MonkEpicWeapon";
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), "MonkEpicWeapon", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
