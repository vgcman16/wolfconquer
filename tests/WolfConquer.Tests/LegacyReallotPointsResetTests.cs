using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyReallotPointsResetTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint ReallotPointsNpcId = 2_542;
    private const uint DragonBallItemId = 1_088_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyReallotPointsResetTests-" + Guid.NewGuid().ToString("N"));
    private readonly string _usersRoot;
    private readonly string _playerItemsRoot;

    public LegacyReallotPointsResetTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _playerItemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_playerItemsRoot);
    }

    [Fact]
    public async Task LevelBelowSeventyRejectsBeforeInventoryOrStatMutation()
    {
        var result = await InvokeAsync(new ResetCase(
            Level: 69,
            InventoryItems: [CreateDragonBall(4_001, bound: 0, stackSize: 1)]));

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.CharacterAtCommit);
        Assert.Null(result.ItemsAtCommit);
        Assert.Equal(result.OriginalPlayerItemsBytes, result.PersistedPlayerItemsBytes);
        AssertStats(result.ReloadedCharacter!, 11, 12, 13, 14, attributePoints: 77);
        Assert.Equal(4, result.Sent.Count);
        AssertDialog(
            result.Sent[0],
            "You cannot reallot your ability points unless you are reborn and above level 70.");
        Assert.Contains("reason=levelBelow70", result.Log);
    }

    [Fact]
    public async Task BoundDragonBallDoesNotSatisfySourceUnboundContainGate()
    {
        var result = await InvokeAsync(new ResetCase(
            InventoryItems: [CreateDragonBall(4_001, bound: 1, stackSize: 1)]));

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.CharacterAtCommit);
        Assert.Null(result.ItemsAtCommit);
        Assert.Equal(result.OriginalPlayerItemsBytes, result.PersistedPlayerItemsBytes);
        Assert.Equal(4, result.Sent.Count);
        AssertDialog(result.Sent[0], "Sorry, you do not have one DragonBall.");
        Assert.Contains("itemId=1088000 bound=0 reason=itemMissing", result.Log);
    }

    [Fact]
    public async Task NonRebornResetPersistsBeforeCommitConsumesUnboundStackAndOrdersRuntimeUpdates()
    {
        var result = await InvokeAsync(new ResetCase(
            Class: 1_005,
            Level: 140,
            Reborn: 0,
            Attributes: new Dictionary<string, string> { ["ExtraAtributes"] = "7" },
            InventoryItems:
            [
                CreateDragonBall(4_001, bound: 1, stackSize: 1),
                CreateDragonBall(4_002, bound: 0, stackSize: 2),
            ],
            ClassLevelStats: [CreateClassLevelStat("Trojan", 120, 101, 102, 103, 104)]));

        AssertStats(result.UpdatedSession.Character, 101, 102, 103, 104, attributePoints: 67);
        AssertStats(result.CharacterAtCommit!, 101, 102, 103, 104, attributePoints: 67);
        AssertStats(result.ReloadedCharacter!, 101, 102, 103, 104, attributePoints: 67);
        Assert.Equal("67", result.ReloadedCharacter!.Attributes["Atributes"]);
        Assert.Contains("Sentinel=keep-me", result.PersistedCharacterText, StringComparison.Ordinal);
        Assert.Contains("Note=preserve-this-section", result.PersistedCharacterText, StringComparison.Ordinal);

        Assert.NotNull(result.ItemsAtCommit);
        AssertDragonBallStacks(result.UpdatedSession.PlayerItems, boundStack: 1, unboundStack: 1);
        AssertDragonBallStacks(result.ItemsAtCommit!, boundStack: 1, unboundStack: 1);
        AssertDragonBallStacks(result.ReloadedItems, boundStack: 1, unboundStack: 1);

        Assert.Equal(11, result.Sent.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(result.Sent[0]), out var itemUpdate));
        Assert.Equal(4_002U, itemUpdate.Uid);
        Assert.Equal(DragonBallItemId, itemUpdate.ItemId);
        Assert.Equal((ushort)1, itemUpdate.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, itemUpdate.Mode);
        AssertMessageBox(result.Sent[1], "You have successfully reloaded your attribute points.");
        AssertScalarUpdate(result.Sent[2], LegacyMsgUpdateDataType.Strength, 101);
        AssertScalarUpdate(result.Sent[3], LegacyMsgUpdateDataType.Agility, 102);
        AssertScalarUpdate(result.Sent[4], LegacyMsgUpdateDataType.Spirit, 104);
        AssertScalarUpdate(result.Sent[5], LegacyMsgUpdateDataType.Vitality, 103);
        AssertScalarUpdate(result.Sent[6], LegacyMsgUpdateDataType.Atributes, 67);
        AssertEquipmentRuntimeRefresh(result.Sent, startIndex: 7);
        Assert.DoesNotContain(
            result.Sent,
            bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));

        Assert.Contains("sourceRange=NpcHandler.cs:7509-7564", result.Log);
        Assert.Contains("itemId=1088000 itemBound=0 consumedCount=1", result.Log);
        Assert.Contains("updateOrder=Strength,Agility,Spirit,Vitality,Atributes", result.Log);
        Assert.Contains("equipmentRuntimeRefresh=true callItems=false", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task NonRebornWaterUsesLevelOneHundredTenFreePointGate()
    {
        var result = await InvokeAsync(new ResetCase(
            Class: 13_005,
            Level: 111,
            Reborn: 0,
            Attributes: new Dictionary<string, string> { ["ExtraAtributes"] = "4" },
            InventoryItems: [CreateDragonBall(4_001, bound: 0, stackSize: 1)],
            ClassLevelStats: [CreateClassLevelStat("Taoist", 111, 21, 22, 23, 24)]));

        AssertStats(result.ReloadedCharacter!, 21, 22, 23, 24, attributePoints: 7);
        AssertScalarUpdate(result.Sent[6], LegacyMsgUpdateDataType.Atributes, 7);
    }

    [Theory]
    [InlineData(1, 1_005, 0, 331, false)]
    [InlineData(2, 1_005, 13_005, 386, true)]
    public async Task RebornResetUsesExactExtraPointTablesAndSecondLevelFallback(
        long reborn,
        long firstClass,
        long secondClass,
        long expectedAttributePoints,
        bool expectsSecondLevelFallback)
    {
        var result = await InvokeAsync(new ResetCase(
            Class: 1_005,
            FirstClass: firstClass,
            SecondClass: secondClass,
            Level: 100,
            Reborn: reborn,
            Attributes: new Dictionary<string, string>
            {
                ["ExtraAtributes"] = "9",
                ["FRL"] = "125",
                ["SRL"] = "0",
            },
            InventoryItems: [CreateDragonBall(4_001, bound: 0, stackSize: 1)]));

        AssertStats(result.ReloadedCharacter!, 0, 0, 1, 0, expectedAttributePoints);
        AssertStats(result.CharacterAtCommit!, 0, 0, 1, 0, expectedAttributePoints);
        Assert.Equal(
            expectsSecondLevelFallback ? "True" : "False",
            ExtractLogValue(result.Log, "secondRebornLevelDefaulted"));
        if (expectsSecondLevelFallback)
        {
            Assert.Equal("130", result.ReloadedCharacter!.Attributes["SRL"]);
            Assert.Contains("SRL=130", result.PersistedCharacterText, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal("0", result.ReloadedCharacter!.Attributes["SRL"]);
        }
    }

    [Fact]
    public async Task MissingNonRebornSourceStatsFencesBeforeDragonBallOrCharacterMutation()
    {
        var result = await InvokeAsync(new ResetCase(
            Class: 1_005,
            Level: 140,
            Reborn: 0,
            InventoryItems: [CreateDragonBall(4_001, bound: 0, stackSize: 1)]));

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.CharacterAtCommit);
        Assert.Null(result.ItemsAtCommit);
        Assert.Equal(result.OriginalPlayerItemsBytes, result.PersistedPlayerItemsBytes);
        AssertStats(result.ReloadedCharacter!, 11, 12, 13, 14, attributePoints: 77);
        Assert.Equal(4, result.Sent.Count);
        AssertDialog(
            result.Sent[0],
            "Attribute reset is unavailable here because it must consume one Dragon Ball and rewrite your stats safely.");
        Assert.Contains("sourceStatsMissing:Trojan:120", result.Log);
        Assert.Contains("PARITY-GAP LegacyNpc ReallotPoints", result.Log);
    }

    [Fact]
    public async Task CharacterPersistenceFailureRollsBackItemFileAndDoesNotCommitOrSend()
    {
        var result = await InvokeAsync(
            new ResetCase(
                Class: 1_005,
                Level: 140,
                Reborn: 0,
                InventoryItems: [CreateDragonBall(4_001, bound: 0, stackSize: 1)],
                ClassLevelStats: [CreateClassLevelStat("Trojan", 120, 101, 102, 103, 104)]),
            removeCharacterBeforeHandle: true);

        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Null(result.CharacterAtCommit);
        Assert.Null(result.ItemsAtCommit);
        Assert.Null(result.ReloadedCharacter);
        Assert.Equal(result.OriginalPlayerItemsBytes, result.PersistedPlayerItemsBytes);
        Assert.Single(result.ReloadedItems.InventoryItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=snapshotMissing", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        ResetCase resetCase,
        bool removeCharacterBeforeHandle = false)
    {
        WriteCharacter(resetCase);
        var characterPath = Path.Combine(_usersRoot, AccountId + ".ini");
        var originalCharacterText = File.ReadAllText(characterPath);
        var characterLoader = new CharacterSnapshotLoader(_usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId));

        var originalItems = new PlayerItemSnapshot(resetCase.InventoryItems, [], [], [], []);
        var itemLoader = new PlayerItemSnapshotLoader(_playerItemsRoot);
        itemLoader.SaveByCharacterUid(CharacterUid, originalItems);
        var playerItemsPath = Path.Combine(_playerItemsRoot, CharacterUid + ".bin");
        var originalPlayerItemsBytes = File.ReadAllBytes(playerItemsPath);
        var session = new WorldSession(
            AccountId,
            "reallot-reset-test",
            character,
            originalItems,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            UsersRoot: _usersRoot);

        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        CharacterSnapshot? characterAtCommit = null;
        PlayerItemSnapshot? itemsAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is null || ReferenceEquals(previous, current))
            {
                return;
            }

            characterAtCommit = characterLoader.LoadByAccountId(AccountId);
            itemsAtCommit = itemLoader.LoadByCharacterUid(CharacterUid);
        });
        sessions.Set(connection, session);
        if (removeCharacterBeforeHandle)
        {
            File.Delete(characterPath);
        }

        using var log = new StringWriter();
        var handled = await WorldPacketHandlers.TryHandleLegacyUtilityNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: CreateWorldDataCatalog(resetCase.ClassLevelStats),
                Sessions: sessions,
                PlayerItemsRoot: _playerItemsRoot),
            session,
            ReallotPointsNpcId,
            optionId: 9,
            input: "",
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            session,
            updatedSession,
            characterAtCommit,
            itemsAtCommit,
            characterLoader.LoadByAccountId(AccountId),
            itemLoader.LoadByCharacterUid(CharacterUid),
            sent,
            log.ToString(),
            originalCharacterText,
            File.Exists(characterPath) ? File.ReadAllText(characterPath) : "",
            originalPlayerItemsBytes,
            File.ReadAllBytes(playerItemsPath));
    }

    private void WriteCharacter(ResetCase resetCase)
    {
        var extraAttributes = string.Join(
            Environment.NewLine,
            resetCase.Attributes.Select(pair => pair.Key + "=" + pair.Value));
        File.WriteAllText(
            Path.Combine(_usersRoot, AccountId + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=ReallotResetTester
            Class={{resetCase.Class}}
            Map=1002
            X=430
            Y=378
            FirstClass={{resetCase.FirstClass}}
            SecoundeClass={{resetCase.SecondClass}}
            Reborn={{resetCase.Reborn}}
            Level={{resetCase.Level}}
            Haire=0
            Experience=0
            PMap=1002
            PMapX=430
            PMapY=378
            Agility=12
            Strength=11
            Vitaliti=13
            Spirit=14
            Atributes=77
            MinHitPoints=1
            MinMana=0
            MaxMana=0
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            Sentinel=keep-me
            {{extraAttributes}}

            [Other]
            Note=preserve-this-section
            """);
    }

    private static PlayerItemSnapshotItem CreateDragonBall(uint uid, byte bound, ushort stackSize)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = DragonBallItemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            Bound = bound,
            StackSize = stackSize,
        };
    }

    private static ClassLevelStat CreateClassLevelStat(
        string className,
        int level,
        int strength,
        int agility,
        int vitality,
        int spirit)
    {
        var rawValue = $"{strength},{vitality},{agility},{spirit}";
        return new ClassLevelStat(
            className,
            level,
            strength,
            agility,
            vitality,
            spirit,
            "fixture://Stats.ini",
            1,
            $"{className}[{level}]",
            rawValue,
            $"{className}[{level}]={rawValue}",
            [strength.ToString(), vitality.ToString(), agility.ToString(), spirit.ToString()]);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(IReadOnlyList<ClassLevelStat> classLevelStats)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z"),
                "fixture://reallot-points-reset",
                new Dictionary<string, int>()),
            clientConfigs: [], items: [], itemAddBonuses: [], levelExperience: [], runeLevelExperience: [],
            runeStorageAttributes: [], itemRefineEffects: [], itemRefineEffectEx: [], itemRefineUpgrades: [],
            transformInfo: [], furniture: [], hairColorTypes: [], hairFaceStorageTypes: [], coatStorageTypes: [],
            coatStorageAttributes: [], coatColorRules: [], xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [], xuanbaoRandomAttributes: [], christmasNpcs: [], tutorTypes: [],
            tutorBattleLimitTypes: [], beastAttributes: [], fateExperience: [], spiritRates: [], hundredWeapons: [],
            gouyuTypes: [], gouyuImmortals: [], specialEffectAttributes: [], daoqiDictTypes: [],
            syndicateFormationTypes: [], syndicateSkillTypes: [], disdainRules: [], jiangHuAttributes: [],
            jiangHuCultivateStatuses: [], refineryBoxes: [], refinerySouls: [], refineryMaterials: [],
            stateCountFiles: [], binarySeedFiles: [], emptySeedFiles: [], backupSeedFiles: [], pokerTables: [],
            rouletteTables: [], npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, "", 0, []),
                [],
                "fixture://activity"),
            meltingTypeRules: [], lotteryPrizes: [], arenaRecords: [], elitePkRecords: [], teamElitePkRecords: [],
            skillTeamPkRecords: [], classPkWarRecords: [], combatGearRanks: [], prestigeRanks: [],
            innerPowerRanks: [], runeRankings: [], staticStatues: [], newPlayerRewards: [], votes: [],
            couplesPkRecords: [], socialAssociations: [], guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"), classLevelStats: classLevelStats,
            professionLevelUps: [], professionLevelBenefits: [], professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [], questMissions: [], quizQuestions: [], magic: [], magicEffects: [],
            magicTypeOperations: [], titleTypes: [], combatGear: [], traps: [], npcs: [], sobNpcs: [], portals: [],
            maps: [], mapNames: [], monsters: [], monsterSpawns: [], lotteryDictionary: [], collectionRewardTiers: [],
            taskRewardTypes: [], taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                "",
                new Dictionary<string, string>()),
            neiGongSecrets: [], neiGongTypes: [], subProfessions: [], syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0), stageGoalStages: [], stageGoalTasks: [], shops: [], shopItems: [],
            exchangeShopGoods: [], exchangeShopGoodsEx: [], exchangeShopLevels: [], honorShopItems: [],
            honorShopRecommendations: [], racePointShopItems: [], racePointShopRecommendations: [],
            goldenLeagueShopItems: [], goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(), booths: [], boothItems: []);
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

    private static void AssertStats(
        CharacterSnapshot snapshot,
        long strength,
        long agility,
        long vitality,
        long spirit,
        long attributePoints)
    {
        Assert.Equal(strength, snapshot.Strength);
        Assert.Equal(agility, snapshot.Agility);
        Assert.Equal(vitality, snapshot.Vitality);
        Assert.Equal(spirit, snapshot.Spirit);
        Assert.Equal(attributePoints, snapshot.AttributePoints);
    }

    private static void AssertDragonBallStacks(
        PlayerItemSnapshot items,
        ushort boundStack,
        ushort unboundStack)
    {
        Assert.Equal(boundStack, Assert.Single(items.InventoryItems, item => item.Bound != 0).StackSize);
        Assert.Equal(unboundStack, Assert.Single(items.InventoryItems, item => item.Bound == 0).StackSize);
    }

    private static void AssertDialog(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, packet.InteractType);
        Assert.Equal(expectedText, packet.Text);
    }

    private static void AssertMessageBox(byte[] bytes, string expectedText)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, packet.InteractType);
        Assert.Equal(expectedText, packet.Text);
    }

    private static void AssertScalarUpdate(
        byte[] bytes,
        LegacyMsgUpdateDataType expectedType,
        ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(CharacterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(expectedType, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static void AssertEquipmentRuntimeRefresh(IReadOnlyList<byte[]> sent, int startIndex)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[startIndex]), out var maxHitpointsOnly));
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, Assert.Single(maxHitpointsOnly.Updates).Type);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[startIndex + 1]), out var hitpointPair));
        Assert.Collection(
            hitpointPair.Updates,
            update => Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, update.Type),
            update => Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, update.Type));
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(sent[startIndex + 2]), out var status));
        Assert.Equal(CharacterUid, status.Uid);
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadFrame(sent[startIndex + 3]), out var refine));
        Assert.Equal(CharacterUid, refine.Uid);
    }

    private static string ExtractLogValue(string log, string key)
    {
        var marker = key + "=";
        var start = log.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += marker.Length;
        var end = log.IndexOfAny([' ', '.', '\r', '\n'], start);
        return end < 0 ? log[start..] : log[start..end];
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record ResetCase(
        long Class = 1_005,
        long FirstClass = 1_005,
        long SecondClass = 0,
        long Reborn = 0,
        long Level = 140,
        IReadOnlyDictionary<string, string>? Attributes = null,
        IReadOnlyList<PlayerItemSnapshotItem>? InventoryItems = null,
        IReadOnlyList<ClassLevelStat>? ClassLevelStats = null)
    {
        public IReadOnlyDictionary<string, string> Attributes { get; init; } =
            Attributes ?? new Dictionary<string, string>();

        public IReadOnlyList<PlayerItemSnapshotItem> InventoryItems { get; init; } = InventoryItems ?? [];

        public IReadOnlyList<ClassLevelStat> ClassLevelStats { get; init; } = ClassLevelStats ?? [];
    }

    private sealed record InvokeResult(
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        CharacterSnapshot? CharacterAtCommit,
        PlayerItemSnapshot? ItemsAtCommit,
        CharacterSnapshot? ReloadedCharacter,
        PlayerItemSnapshot ReloadedItems,
        IReadOnlyList<byte[]> Sent,
        string Log,
        string OriginalCharacterText,
        string PersistedCharacterText,
        byte[] OriginalPlayerItemsBytes,
        byte[] PersistedPlayerItemsBytes);
}
