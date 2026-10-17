using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClaimCupRewardTests : IDisposable
{
    private const uint ClaimCupNpcId = 54_613;
    private const uint RewardItemId = 2_100_075;
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyClaimCupRewardTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ClaimClearsGatePersistsCharacterAndItemThenSendsSourceOrderedPackets()
    {
        var initialItems = CreatePlayerItems(
            CreateInventoryItem(uid: 41, itemId: 300_001, durability: 9));
        var usersRoot = CreateUsersRoot(goldPrizeSave: 1);
        var playerItemsRoot = CreatePlayerItemsRoot(initialItems);
        var session = CreateSession(usersRoot, initialItems, goldPrizeSave: 1);

        var result = await InvokeAsync(
            session,
            CreateWorldDataCatalog(CreateItemType(RewardItemId, durability: 77)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Equal("1", result.OriginalSession.Character.Attributes["Goldprizesave"]);
        Assert.Equal("0", result.UpdatedSession.Character.Attributes["Goldprizesave"]);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == RewardItemId);
        Assert.Equal(42u, reward.Uid);
        Assert.Equal(77, reward.Durability);
        Assert.Equal(77, reward.MaximumDurability);
        Assert.Equal(0, reward.Position);
        Assert.Equal(1, reward.StackSize);
        Assert.Equal(0, reward.Bound);
        Assert.Equal(0, reward.Plus);
        Assert.Equal(0, reward.Bless);
        Assert.Equal(0, reward.Enchant);
        Assert.InRange(reward.Color, 3u, 8u);

        Assert.Equal(3, result.Sent.Count);
        var itemPacket = AssertGameItem(result.Sent[0]);
        Assert.Equal(reward.Uid, itemPacket.Uid);
        Assert.Equal(RewardItemId, itemPacket.ItemId);
        Assert.Equal(77, itemPacket.Durability);
        Assert.Equal(77, itemPacket.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Mode);
        Assert.Equal(0, itemPacket.Position);
        Assert.Equal(1, itemPacket.StackSize);
        Assert.Equal(0, itemPacket.Bound);

        var messageBox = AssertNpcReply(result.Sent[1]);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, messageBox.InteractType);
        Assert.Equal("Congratulations #52 .", messageBox.Text);

        var effect = AssertPlayerEffect(result.Sent[2]);
        Assert.Equal(checked((uint)CharacterUid), effect.Uid);
        Assert.Equal(LegacyStringId.Effect, effect.Id);
        Assert.Equal(["zf2-e128"], effect.Strings);

        Assert.Equal("0", ReadCharacterAttribute(usersRoot, "Goldprizesave"));
        var persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(result.UpdatedSession.PlayerItems, persistedItems);
        Assert.Contains("source=NpcHandler.CliamCup", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc CliamCup", result.Log);
    }

    [Fact]
    public async Task FullInventoryWinsBeforeGoldPrizeGateAndUsesSourceMessageBox()
    {
        var fullInventory = Enumerable.Range(1, 40)
            .Select(index => CreateInventoryItem(
                checked((uint)index),
                checked((uint)(300_000 + index)),
                durability: 1))
            .ToArray();
        var session = CreateSession(
            usersRoot: null,
            CreatePlayerItems(fullInventory),
            goldPrizeSave: 0);

        var result = await InvokeAsync(session, worldData: null, playerItemsRoot: null);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        var messageBox = Assert.Single(result.Sent);
        var reply = AssertNpcReply(messageBox);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("Please make 1 more space in your inventory.", reply.Text);
    }

    [Fact]
    public async Task UnregisteredGateUsesSourceMessageBoxAndDoesNotMutate()
    {
        var initialItems = CreatePlayerItems();
        var usersRoot = CreateUsersRoot(goldPrizeSave: 0);
        var playerItemsRoot = CreatePlayerItemsRoot(initialItems);
        var characterPath = GetCharacterPath(usersRoot);
        var itemPath = GetPlayerItemsPath(playerItemsRoot);
        var characterBefore = File.ReadAllBytes(characterPath);
        var itemsBefore = File.ReadAllBytes(itemPath);
        var session = CreateSession(usersRoot, initialItems, goldPrizeSave: 0);

        var result = await InvokeAsync(session, worldData: null, playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        var reply = AssertNpcReply(Assert.Single(result.Sent));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, reply.InteractType);
        Assert.Equal("You are not registered in the GoldPrize .", reply.Text);
        Assert.Equal(characterBefore, File.ReadAllBytes(characterPath));
        Assert.Equal(itemsBefore, File.ReadAllBytes(itemPath));
    }

    [Fact]
    public async Task MissingRewardItemTypeIsMutationFreeAndSendsNoPackets()
    {
        var initialItems = CreatePlayerItems();
        var usersRoot = CreateUsersRoot(goldPrizeSave: 1);
        var playerItemsRoot = CreatePlayerItemsRoot(initialItems);
        var characterPath = GetCharacterPath(usersRoot);
        var itemPath = GetPlayerItemsPath(playerItemsRoot);
        var characterBefore = File.ReadAllBytes(characterPath);
        var itemsBefore = File.ReadAllBytes(itemPath);
        var session = CreateSession(usersRoot, initialItems, goldPrizeSave: 1);

        var result = await InvokeAsync(session, CreateWorldDataCatalog(), playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Equal(characterBefore, File.ReadAllBytes(characterPath));
        Assert.Equal(itemsBefore, File.ReadAllBytes(itemPath));
        Assert.Contains("itemId=2100075", result.Log);
        Assert.Contains("reason=itemtypeMissing", result.Log);
    }

    [Fact]
    public async Task ItemPersistenceFailureRollsBackCharacterAndDoesNotCommitSessionOrPackets()
    {
        var initialItems = CreatePlayerItems();
        var usersRoot = CreateUsersRoot(goldPrizeSave: 1);
        var playerItemsRoot = Path.Combine(_root, "PlayersItems-blocked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(playerItemsRoot);
        Directory.CreateDirectory(GetPlayerItemsPath(playerItemsRoot));
        var characterPath = GetCharacterPath(usersRoot);
        var characterBefore = File.ReadAllBytes(characterPath);
        var session = CreateSession(usersRoot, initialItems, goldPrizeSave: 1);

        var result = await InvokeAsync(
            session,
            CreateWorldDataCatalog(CreateItemType(RewardItemId, durability: 77)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal("1", result.UpdatedSession.Character.Attributes["Goldprizesave"]);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Empty(result.Sent);
        Assert.Equal(characterBefore, File.ReadAllBytes(characterPath));
        Assert.Equal("1", ReadCharacterAttribute(usersRoot, "Goldprizesave"));
        Assert.True(Directory.Exists(GetPlayerItemsPath(playerItemsRoot)));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.claim-cup-*", SearchOption.AllDirectories));
        Assert.Contains("reason=transactionWriteFailed", result.Log);
        Assert.Contains("no session mutation or reward packets were committed", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        WorldSession session,
        WorldDataCatalog? worldData,
        string? playerItemsRoot)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyRewardServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            ClaimCupNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private string CreateUsersRoot(int goldPrizeSave)
    {
        var usersRoot = Path.Combine(_root, "Users-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        File.WriteAllLines(
            GetCharacterPath(usersRoot),
            [
                "[Character]",
                $"UID={CharacterUid.ToString(CultureInfo.InvariantCulture)}",
                "Name=ClaimCupTester",
                $"Goldprizesave={goldPrizeSave.ToString(CultureInfo.InvariantCulture)}",
                string.Empty,
                "[PaidNobility]",
            ]);
        return usersRoot;
    }

    private string CreatePlayerItemsRoot(PlayerItemSnapshot items)
    {
        var playerItemsRoot = Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
        new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, items);
        return playerItemsRoot;
    }

    private static string GetCharacterPath(string usersRoot)
    {
        return Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    }

    private static string GetPlayerItemsPath(string playerItemsRoot)
    {
        return Path.Combine(playerItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private static string ReadCharacterAttribute(string usersRoot, string attribute)
    {
        var prefix = attribute + "=";
        var line = File.ReadLines(GetCharacterPath(usersRoot))
            .Single(value => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line[prefix.Length..];
    }

    private static WorldSession CreateSession(
        string? usersRoot,
        PlayerItemSnapshot playerItems,
        int goldPrizeSave)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "legacy-claim-cup-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "ClaimCupTester",
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
                Y: 380,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 380,
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
                    ["Goldprizesave"] = goldPrizeSave.ToString(CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot CreatePlayerItems(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(
        uint uid,
        uint itemId,
        ushort durability)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = durability,
            MaximumDurability = durability,
            Position = 0,
            StackSize = 1,
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

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyStringPacket AssertPlayerEffect(byte[] bytes)
    {
        Assert.True(LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static ItemType CreateItemType(uint itemId, ushort durability)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "GoldPrize";
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), fields[1], fields);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://claim-cup-reward",
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData(
            "fixture://" + name,
            [],
            [],
            [],
            info: null,
            pole: null,
            conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession);
}
