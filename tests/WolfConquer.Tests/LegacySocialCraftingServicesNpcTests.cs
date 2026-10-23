using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySocialCraftingServicesNpcTests
{
    private const uint JiangHuNpcId = 15_745;
    private const uint ClanNpcId = 5_533;
    private const uint GuildCreatorNpcId = 10_003;
    private const uint LoveStoneNpcId = 390;
    private const uint LevelNpcId = 19_039;
    private const uint OfflineTgNpcId = 3_836;
    private const uint DragonFurnaceTwinNpcId = 23_736;
    private const uint DragonFurnaceForgingNpcId = 23_737;
    private const uint SolarFurnaceNpcId = 23_580;
    private const uint RuneFragmentItemId = 3_306_369;
    private const uint YellowRuneFragmentItemId = 3_306_370;
    private const uint AutumnNpcId = 19_317;
    private const uint ArtisanZhuRongNpcId = 22_559;
    private const uint TcTeleporterNpcId = 50;
    private const uint NobilityWarNpcId = 12_142;
    private const uint HuntingAreaNpcId = 50_060;

    [Theory]
    [InlineData(JiangHuNpcId, 0, "NpcHandler.JiangHuNpc", "Jiang Hu", 1)]
    [InlineData(ClanNpcId, 0, "NpcHandler.ClanNpc", "A Clan is a bond", 1)]
    [InlineData(GuildCreatorNpcId, 0, "NpcHandler.GuildCreator", "guild matters", 5)]
    [InlineData(LoveStoneNpcId, 0, "NpcHandler.LoveStone", "Fate can bring lovers", 2)]
    [InlineData(LevelNpcId, 0, "NpcHandler.LevelAbdo", "Level 140.", 1)]
    [InlineData(DragonFurnaceTwinNpcId, 0, "NpcHandler.DragonFurnaceTwin", "ancient furnace", 254)]
    [InlineData(DragonFurnaceForgingNpcId, 0, "NpcHandler.DragonFurnaceForging", "forge Anima", 1)]
    [InlineData(SolarFurnaceNpcId, 0, "NpcHandler.SolarFurnace", "solar furnace", 12)]
    [InlineData(RuneFragmentItemId, 0, "NpcHandler.RuneFragments", "combine 20 rune fragments", 1)]
    [InlineData(AutumnNpcId, 0, "NpcHandler.Autumn", "Perfection Ranking", 1)]
    [InlineData(NobilityWarNpcId, 0, "NpcHandler.NobilityWar", "Nobility Pole War", 1)]
    [InlineData(HuntingAreaNpcId, 0, "NpcHandler.HuntingArea", "hunting maps", 2)]
    public async Task RootDialogsAreAsciiAndPreserveSourceOptions(
        uint npcId,
        byte optionId,
        string source,
        string expectedText,
        byte expectedOption)
    {
        var result = await InvokeAsync(npcId, optionId);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains(expectedText, StringComparison.Ordinal));
        Assert.Contains(replies, reply => reply.OptionId == expectedOption);
        Assert.Contains("source=" + source, result.Log);
        AssertAsciiReplies(replies);
    }

    [Theory]
    [InlineData(ClanNpcId, 2, "My Clan Name...", 3, "NpcHandler.ClanNpc")]
    [InlineData(ClanNpcId, 31, "Name:", 32, "NpcHandler.ClanNpc")]
    [InlineData(GuildCreatorNpcId, 1, "Name", 3, "NpcHandler.GuildCreator")]
    [InlineData(GuildCreatorNpcId, 51, "Name:", 52, "NpcHandler.GuildCreator")]
    public async Task InputPromptsPreserveInputOptionIds(
        uint npcId,
        byte optionId,
        string inputLabel,
        byte inputOptionId,
        string source)
    {
        var result = await InvokeAsync(npcId, optionId);

        Assert.True(result.Handled);
        var input = Assert.Single(ParseNpcReplies(result.Sent), reply => reply.InteractType == LegacyNpcReplyInteractType.Input);
        Assert.Equal(inputLabel, input.Text);
        Assert.Equal(inputOptionId, input.OptionId);
        Assert.Equal(16, input.InputMaxLength);
        Assert.Contains("source=" + source, result.Log);
        AssertAsciiReplies(ParseNpcReplies(result.Sent));
    }

    [Theory]
    [InlineData(JiangHuNpcId, 1, "NpcHandler.JiangHuNpc", "52249-52316")]
    [InlineData(ClanNpcId, 3, "NpcHandler.ClanNpc", "52318-52674")]
    [InlineData(GuildCreatorNpcId, 52, "NpcHandler.GuildCreator", "52677-52910")]
    [InlineData(LoveStoneNpcId, 8, "NpcHandler.LoveStone", "52913-53035")]
    [InlineData(LevelNpcId, 3, "NpcHandler.LevelAbdo", "53533-53568")]
    [InlineData(OfflineTgNpcId, 0, "NpcHandler.OflineTGNpc", "53826-53852")]
    [InlineData(DragonFurnaceTwinNpcId, 101, "NpcHandler.DragonFurnaceTwin", "54735-54934")]
    [InlineData(SolarFurnaceNpcId, 12, "NpcHandler.SolarFurnace", "55017-55078")]
    [InlineData(ArtisanZhuRongNpcId, 1, "NpcHandler.ArtisanZhuRong", "57075-57112")]
    [InlineData(HuntingAreaNpcId, 1, "NpcHandler.HuntingArea", "57486-57552")]
    public async Task UnsafeBranchesSendUnavailableDialogAndSafeGapLog(
        uint npcId,
        byte optionId,
        string source,
        string sourceLines)
    {
        var result = await InvokeAsync(npcId, optionId, conquerPoints: 5_000_000, level: 90);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text.Contains("unavailable here", StringComparison.Ordinal));
        Assert.Equal(5_000_000, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(90, result.UpdatedSession.Character.Level);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Conquer));
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Level));

        Assert.Contains("PARITY-GAP LegacyNpc SocialCraftingServices", result.Log);
        Assert.Contains("source=" + source, result.Log);
        Assert.Contains("sourceLines=" + sourceLines, result.Log);
        Assert.Contains("npcId=" + npcId, result.Log);
        Assert.Contains("optionId=" + optionId, result.Log);
        AssertAsciiReplies(replies);
    }

    [Fact]
    public async Task NobilityWarClaimWithoutRoundOwnerPreservesItemsCpAndLevel()
    {
        var result = await InvokeAsync(NobilityWarNpcId, optionId: 2, conquerPoints: 5_000_000, level: 90);

        Assert.True(result.Handled);
        var replies = ParseNpcReplies(result.Sent);
        Assert.Contains(replies, reply => reply.Text == "There is no Nobility Pole War reward available now.");
        Assert.Equal(5_000_000, result.UpdatedSession.Character.ConquerPoints);
        Assert.Equal(90, result.UpdatedSession.Character.Level);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.DoesNotContain(ParseMsgUpdates(result.Sent), update =>
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Conquer or LegacyMsgUpdateDataType.Level));
        Assert.Contains("source=NpcHandler.NobilityWar option=2", result.Log);
        Assert.Contains("skipped missing MsgNobilityPole RoundOwner", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
        AssertAsciiReplies(replies);
    }

    [Fact]
    public async Task TcTeleporterUsesExactLocalMetadataIdsAndFixedDestinations()
    {
        var root = await InvokeAsync(TcTeleporterNpcId, optionId: 0);

        Assert.True(root.Handled);
        var rootReplies = ParseNpcReplies(root.Sent);
        Assert.Contains(rootReplies, reply => reply.Text == "Take me to the Game Manager." && reply.OptionId == 1);
        Assert.Contains(rootReplies, reply => reply.Text == "Twin City Conductress." && reply.OptionId == 7);

        var teleport = await InvokeAsync(TcTeleporterNpcId, optionId: 7, mapId: 1_002, x: 330, y: 429);

        Assert.True(teleport.Handled);
        Assert.True(teleport.Sessions.TryGet(teleport.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(1_002, updated.Character.MapId);
        Assert.Equal(427, updated.Character.X);
        Assert.Equal(357, updated.Character.Y);
        var action = Assert.Single(ParseMsgActions(teleport.Sent), packet => packet.Type == MsgActionType.Teleport);
        Assert.Equal(1_002U, action.DwParam);
        Assert.Equal(427U, action.PositionX);
        Assert.Equal(357U, action.PositionY);
        Assert.Contains("source=NpcHandler.TCTeleporter map=1002 x=427 y=357", teleport.Log);
    }

    [Fact]
    public async Task RuneFragmentDoubleFailureConsumesOneFragmentAndPersistsBoundStack()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-rune-fragments-" + Guid.NewGuid());
        try
        {
            var itemsRoot = Path.Combine(root, "PlayersItems");
            var initialItems = new PlayerItemSnapshot(
                [
                    CreateInventoryItem(uid: 2_001, itemId: RuneFragmentItemId, stackSize: 2, bound: 0),
                ],
                [],
                [],
                [],
                []);
            new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(CharacterUid, initialItems);

            var result = await InvokeAsync(
                RuneFragmentItemId,
                optionId: 2,
                playerItems: initialItems,
                characterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LegacyRuneFragmentDoubleFailRate"] = "1",
                },
                playerItemsRoot: itemsRoot);

            Assert.True(result.Handled);
            var liveFragment = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
            Assert.Equal(RuneFragmentItemId, liveFragment.ItemId);
            Assert.Equal((ushort)1, liveFragment.StackSize);
            Assert.Equal((byte)1, liveFragment.Bound);

            var persistedFragment = Assert.Single(new PlayerItemSnapshotLoader(itemsRoot)
                .LoadByCharacterUid(CharacterUid)
                .InventoryItems);
            Assert.Equal((ushort)1, persistedFragment.StackSize);
            Assert.Equal((byte)1, persistedFragment.Bound);
            Assert.Contains(ParseMsgMessages(result.Sent), packet => packet.Content.Any(
                content => content.Contains("Failed to combine", StringComparison.Ordinal)));
            Assert.DoesNotContain("PARITY-GAP LegacyNpc SocialCraftingServices", result.Log);
            Assert.Contains("Core.Rate(50)=true", result.Log);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuneFragmentDoubleSuccessAddsOneFragmentAndPersistsBoundStack()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-rune-fragments-" + Guid.NewGuid());
        try
        {
            var itemsRoot = Path.Combine(root, "PlayersItems");
            var initialItems = new PlayerItemSnapshot(
                [
                    CreateInventoryItem(uid: 2_001, itemId: RuneFragmentItemId, stackSize: 1, bound: 0),
                ],
                [],
                [],
                [],
                []);
            new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(CharacterUid, initialItems);

            var result = await InvokeAsync(
                RuneFragmentItemId,
                optionId: 2,
                playerItems: initialItems,
                characterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LegacyRuneFragmentDoubleFailRate"] = "0",
                },
                playerItemsRoot: itemsRoot);

            Assert.True(result.Handled);
            var liveFragment = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
            Assert.Equal(RuneFragmentItemId, liveFragment.ItemId);
            Assert.Equal((ushort)2, liveFragment.StackSize);
            Assert.Equal((byte)1, liveFragment.Bound);

            var persistedFragment = Assert.Single(new PlayerItemSnapshotLoader(itemsRoot)
                .LoadByCharacterUid(CharacterUid)
                .InventoryItems);
            Assert.Equal((ushort)2, persistedFragment.StackSize);
            Assert.Equal((byte)1, persistedFragment.Bound);
            Assert.Contains(ParseMsgMessages(result.Sent), packet => packet.Content.Any(
                content => content.Contains("successfully made 2", StringComparison.Ordinal)));
            Assert.DoesNotContain("PARITY-GAP LegacyNpc SocialCraftingServices", result.Log);
            Assert.Contains("Core.Rate(50)=false", result.Log);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RuneFragmentCombineTwentyConsumesFragmentsAndPersistsCompleteRune()
    {
        const uint expectedRuneItemId = 4_032_101;
        var root = Path.Combine(Path.GetTempPath(), "wolf-rune-fragments-" + Guid.NewGuid());
        try
        {
            var itemsRoot = Path.Combine(root, "PlayersItems");
            var initialItems = new PlayerItemSnapshot(
                [
                    CreateInventoryItem(uid: 2_001, itemId: YellowRuneFragmentItemId, stackSize: 20, bound: 0),
                ],
                [],
                [],
                [],
                []);
            new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(CharacterUid, initialItems);

            var result = await InvokeAsync(
                YellowRuneFragmentItemId,
                optionId: 1,
                playerItems: initialItems,
                characterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LegacyRuneFragmentCompleteRuneIndex"] = "1",
                },
                playerItemsRoot: itemsRoot,
                worldData: CreateWorldDataCatalog(
                    CreateItemType(YellowRuneFragmentItemId, "YellowRuneFragment", durability: 1),
                    CreateItemType(4_032_001, "YellowRuneA", durability: 700),
                    CreateItemType(expectedRuneItemId, "YellowRuneB", durability: 888),
                    CreateItemType(4_032_002, "YellowRuneWrongEnding", durability: 999),
                    CreateItemType(4_022_001, "BlueRune", durability: 999)));

            Assert.True(result.Handled);
            var liveRune = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
            Assert.Equal(expectedRuneItemId, liveRune.ItemId);
            Assert.Equal((ushort)888, liveRune.Durability);
            Assert.Equal((ushort)888, liveRune.MaximumDurability);
            Assert.Equal((byte)1, liveRune.Bound);
            Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == YellowRuneFragmentItemId);

            var persistedRune = Assert.Single(new PlayerItemSnapshotLoader(itemsRoot)
                .LoadByCharacterUid(CharacterUid)
                .InventoryItems);
            Assert.Equal(expectedRuneItemId, persistedRune.ItemId);
            Assert.Equal((ushort)888, persistedRune.Durability);
            Assert.Equal((byte)1, persistedRune.Bound);
            Assert.Contains(ParseMsgMessages(result.Sent), packet => packet.Content.Any(
                content => content.Contains("You have got 1 YellowRuneB(B).", StringComparison.Ordinal)));
            Assert.DoesNotContain("PARITY-GAP LegacyNpc RuneFragments", result.Log);
            Assert.Contains("rewardItemId=4032101", result.Log);
            Assert.Contains("poolIndex=1", result.Log);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UnknownNpcOrOptionIsNotHandled()
    {
        var unknownNpc = await InvokeAsync(npcId: 123_456, optionId: 0);
        Assert.False(unknownNpc.Handled);
        Assert.Empty(unknownNpc.Sent);

        var unknownOption = await InvokeAsync(JiangHuNpcId, optionId: 99);
        Assert.False(unknownOption.Handled);
        Assert.Empty(unknownOption.Sent);
    }

    private static async Task<LegacySocialCraftingServicesInvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        string input = "",
        long conquerPoints = 0,
        long level = 140,
        long mapId = 1_002,
        long x = 430,
        long y = 380,
        PlayerItemSnapshot? playerItems = null,
        IReadOnlyDictionary<string, string>? characterAttributes = null,
        string? playerItemsRoot = null,
        WorldDataCatalog? worldData = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var sessions = new WorldSessionStore();
        var session = CreateSession(conquerPoints, level, mapId, x, y, playerItems, characterAttributes);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacySocialCraftingServicesNpcAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: session,
                    Sessions: sessions,
                    WorldData: worldData,
                    PlayerItemsRoot: playerItemsRoot),
                session,
                npcId,
                optionId,
                input,
                CancellationToken.None)
            .ConfigureAwait(false);

        sessions.TryGet(connection, out var updatedSession);
        updatedSession ??= session;
        return new LegacySocialCraftingServicesInvokeResult(
            handled,
            sent,
            log.ToString(),
            updatedSession,
            sessions,
            connection);
    }

    private static WorldSession CreateSession(
        long conquerPoints,
        long level,
        long mapId,
        long x,
        long y,
        PlayerItemSnapshot? playerItems,
        IReadOnlyDictionary<string, string>? characterAttributes)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "legacy-social-crafting-services-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "SocialCrafter",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 2,
                Level: level,
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
                MinHitPoints: 1,
                MinMana: 0,
                Money: 5_000_000,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: characterAttributes is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(characterAttributes, StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private const long CharacterUid = 1_293_600;

    private static PlayerItemSnapshotItem CreateInventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize,
        byte bound)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
            Durability = 1,
            MaximumDurability = 1,
        };
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://social-crafting",
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertAsciiReplies(IEnumerable<LegacyNpcReplyPacket> replies)
    {
        foreach (var reply in replies)
        {
            Assert.All(reply.Text, character => Assert.True(character <= 127, reply.Text));
        }
    }

    private sealed record LegacySocialCraftingServicesInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession,
        WorldSessionStore Sessions,
        ConquerTcpConnection Connection);
}
