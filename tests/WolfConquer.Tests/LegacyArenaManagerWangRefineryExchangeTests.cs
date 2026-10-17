using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyArenaManagerWangRefineryExchangeTests : IDisposable
{
    private const uint ArenaManagerWangNpcId = 1_881;
    private const uint RefineryBadgeItemId = 720_148;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyArenaManagerWangRefineryExchangeTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PrimaryPagesAndRewardsUseSourceOptionIds102Through117()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems();
        var worldData = CreateWorldDataCatalog();

        var pageOne = await InvokeAsync(11, originalItems, worldData, playerItemsRoot);
        Assert.Equal([102, 103, 104, 105, 106, 107, 108, 12], GetDialogOptionIds(pageOne.Sent));

        var pageTwo = await InvokeAsync(12, originalItems, worldData, playerItemsRoot);
        Assert.Equal([109, 110, 111, 112, 113, 114, 11, 13], GetDialogOptionIds(pageTwo.Sent));

        var pageThree = await InvokeAsync(13, originalItems, worldData, playerItemsRoot);
        Assert.Equal([115, 116, 117, 12, byte.MaxValue], GetDialogOptionIds(pageThree.Sent));

        var oldNormalizedOption = await InvokeAsync(100, originalItems, worldData, playerItemsRoot);
        Assert.False(oldNormalizedOption.Handled);
        Assert.Same(oldNormalizedOption.OriginalSession, oldNormalizedOption.UpdatedSession);
        Assert.Empty(oldNormalizedOption.Sent);
    }

    [Theory]
    [InlineData(102, 2, 724_154, "Primary", "Veneer", "1-handed, Critical Strike")]
    [InlineData(127, 6, 724_167, "Medium", "Bow Limb", "Bow, Breakthrough")]
    [InlineData(155, 12, 724_132, "Superior", "Bell", "Bracelet, Penetration")]
    public async Task ExchangeConsumesBadgesGrantsSourcePackAndPersistsReload(
        int optionId,
        int badgeCost,
        uint rewardItemId,
        string tierName,
        string packName,
        string equipmentAndEffect)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(
                uid: 10,
                itemId: RefineryBadgeItemId,
                stackSize: checked((ushort)(badgeCost + 1))));
        var result = await InvokeAsync(
            checked((byte)optionId),
            originalItems,
            CreateWorldDataCatalog(CreateItemType(rewardItemId, packName)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        var remainingBadge = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == RefineryBadgeItemId);
        Assert.Equal(1, remainingBadge.StackSize);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(0, reward.Position);
        Assert.Equal(0, reward.Bound);
        Assert.Equal(1, reward.StackSize);

        Assert.Equal(3, result.Sent.Count);
        var badgeUpdate = AssertGameItem(result.Sent[0]);
        Assert.Equal(RefineryBadgeItemId, badgeUpdate.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, badgeUpdate.Mode);
        Assert.Equal(1, badgeUpdate.StackSize);

        var grantedPacket = AssertGameItem(result.Sent[1]);
        Assert.Equal(rewardItemId, grantedPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, grantedPacket.Mode);
        Assert.Equal(0, grantedPacket.Bound);
        Assert.Equal(1, grantedPacket.StackSize);

        var successMessage = AssertSystemTalk(result.Sent[2]);
        Assert.Equal(
            $"You exchanged {badgeCost} Refinery Badges for a {tierName} " +
            $"{packName} Pack ({equipmentAndEffect})!",
            successMessage.Content[3]);
        Assert.DoesNotContain(result.Sent, IsNpcReply);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persisted);
        Assert.Contains($"option={optionId}", result.Log);
        Assert.Contains($"badgeCost={badgeCost}", result.Log);
        Assert.Contains($"rewardItemId={rewardItemId}", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CompetitionManagers refinery exchange", result.Log);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public async Task InsufficientOrBoundBadgesUseExactSourceDialogAndDoNotMutate(
        int badgeCount,
        byte bound)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(
                uid: 10,
                itemId: RefineryBadgeItemId,
                stackSize: checked((ushort)badgeCount),
                bound: bound));
        var result = await InvokeAsync(
            optionId: 102,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(724_154, "CriticalStrike(Primary)Pack")),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        AssertExactDialog(
            result.Sent,
            "You don`t have enough Refinery Badges! You should prepare 2 Refinery Badges if you want to get a primary refinery pack!",
            "I see.");
        Assert.DoesNotContain(result.Sent, IsGameItem);
        Assert.DoesNotContain(result.Sent, IsSystemTalk);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, persisted);
    }

    [Fact]
    public async Task FullInventoryUsesExactSourceDialogAndDoesNotConsumeBadges()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, RefineryBadgeItemId, stackSize: 12),
        };
        inventory.AddRange(
            Enumerable.Range(0, 39)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var result = await InvokeAsync(
            optionId: 155,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(724_132, "Penetration(Superior)Pack")),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        AssertExactDialog(
            result.Sent,
            "Your inventory is full! Please make 1 cell before you claim the pack!",
            "Okay.");
        Assert.DoesNotContain(result.Sent, IsGameItem);
        Assert.DoesNotContain(result.Sent, IsSystemTalk);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, persisted);
    }

    [Fact]
    public async Task MissingPackItemTypeIsMutationFree()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, RefineryBadgeItemId, stackSize: 6));
        var result = await InvokeAsync(
            optionId: 120,
            originalItems,
            CreateWorldDataCatalog(),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Contains("rewardItemId=724155", result.Log);
        Assert.Contains("reason=itemtype-missing", result.Log);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, persisted);
    }

    [Fact]
    public async Task MissingSessionStoreDoesNotPersistOrMutateExchange()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, RefineryBadgeItemId, stackSize: 2));
        var result = await InvokeAsync(
            optionId: 102,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(724_154, "CriticalStrike(Primary)Pack")),
            playerItemsRoot,
            includeSessionStore: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=session-store-missing", result.Log);
        Assert.Contains("no item persistence or badge mutation occurred", result.Log);

        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(originalItems, persisted);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotCommitSessionOrSendMutationPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, RefineryBadgeItemId, stackSize: 2));
        var result = await InvokeAsync(
            optionId: 102,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(724_154, "CriticalStrike(Primary)Pack")),
            blockedPlayerItemsRoot,
            seedPersistence: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Contains("refinery exchange persistence", result.Log);
        Assert.Contains("no session mutation was committed", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        byte optionId,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog worldData,
        string playerItemsRoot,
        bool seedPersistence = true,
        bool includeSessionStore = true)
    {
        if (seedPersistence)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(originalItems);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var handled = await WorldPacketHandlers.TryHandleLegacyCompetitionManagersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: playerItemsRoot),
            session,
            ArenaManagerWangNpcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "arena-manager-wang-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "RefineryHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DynamicID"] = "0",
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot CreatePlayerItems(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
        };
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string expectedText,
        string expectedOption)
    {
        Assert.Equal(4, sent.Count);
        var dialog = AssertNpcReply(sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(expectedText, dialog.Text);

        var option = AssertNpcReply(sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(expectedOption, option.Text);

        var avatar = AssertNpcReply(sent[2]);
        Assert.Equal(LegacyNpcReplyInteractType.Avatar, avatar.InteractType);
        Assert.Equal(31, avatar.InputMaxLength);

        var finish = AssertNpcReply(sent[3]);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static byte[] GetDialogOptionIds(IReadOnlyList<byte[]> sent)
    {
        return sent
            .Select(AssertNpcReply)
            .Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Option)
            .Select(packet => packet.OptionId)
            .ToArray();
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyMsgMessagePacket AssertSystemTalk(byte[] bytes)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        return packet;
    }

    private static bool IsNpcReply(byte[] bytes)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsGameItem(byte[] bytes)
    {
        return LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsSystemTalk(byte[] bytes)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.ChatType == LegacyMsgMessagePacket.TalkChatType;
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
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
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://arena-manager-wang-refinery-exchange",
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

    private static ItemType CreateItemType(uint itemTypeId, string name)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = "1";
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession);
}
