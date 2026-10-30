using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAdminUtilityLevelProficiencyParityTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;
    private const uint StuffNpcId = 77_839;
    private const uint Stuff12NpcId = 22_224;
    private const byte StuffLevel140Option = 60;
    private const byte Stuff12MaxProficiencyOption = 149;
    private const byte Stuff12Level140Option = 162;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyAdminUtilityLevelProficiencyParityTests-" + Guid.NewGuid().ToString("N"));
    private readonly string _usersRoot;
    private readonly string _playerProficienciesRoot;

    public LegacyAdminUtilityLevelProficiencyParityTests()
    {
        _usersRoot = Path.Combine(_root, "Users");
        _playerProficienciesRoot = Path.Combine(_root, "Proficiencies");
        Directory.CreateDirectory(_usersRoot);
        Directory.CreateDirectory(_playerProficienciesRoot);
    }

    [Theory]
    [InlineData(StuffNpcId, StuffLevel140Option)]
    [InlineData(Stuff12NpcId, Stuff12Level140Option)]
    [InlineData(Stuff12NpcId, Stuff12MaxProficiencyOption)]
    public async Task NormalCharactersCannotReachMutationBranches(uint npcId, byte optionId)
    {
        AssertUnchanged(await InvokeAsync(npcId, optionId, gmName: false,
            proficiencies: CreateMixedProficiencies()));
    }

    [Fact]
    public async Task StuffOption60DoesNotRaiseLevelStatsOrHitpointsForGmNickname()
    {
        AssertUnchanged(await InvokeAsync(StuffNpcId, StuffLevel140Option,
            level: 130, experience: 123_456, attributePoints: 7, minHitpoints: 5));
    }

    [Fact]
    public async Task StuffOption60At140DoesNotPublishAnAdminMessageBox()
    {
        AssertUnchanged(await InvokeAsync(StuffNpcId, StuffLevel140Option,
            level: 140, experience: 777, attributePoints: 9, includeMutationDependencies: false));
    }

    [Fact]
    public async Task Stuff12Option162DoesNotChangeBelowCutoffStatsForProjectManagerAttribute()
    {
        AssertUnchanged(await InvokeAsync(Stuff12NpcId, Stuff12Level140Option,
            level: 119, experience: 88, strength: 11, agility: 12, vitality: 100,
            spirit: 14, attributePoints: 7, gmName: false, projectManager: true));
    }

    [Fact]
    public async Task Stuff12Option162At140DoesNotPublishSuccessOrRejectionDialogs()
    {
        AssertUnchanged(await InvokeAsync(Stuff12NpcId, Stuff12Level140Option,
            level: 140, experience: 654, attributePoints: 19, includeMutationDependencies: false));
    }

    [Fact]
    public async Task DisabledLevelRouteDoesNotAttemptTheLockedCharacterWriter()
    {
        AssertUnchanged(await InvokeAsync(StuffNpcId, StuffLevel140Option,
            level: 130, experience: 55, attributePoints: 7, lockCharacterFile: true));
    }

    [Fact]
    public async Task Stuff12Option149PreservesEveryMixedProficiencyWithoutDisconnect()
    {
        AssertUnchanged(await InvokeAsync(Stuff12NpcId, Stuff12MaxProficiencyOption,
            proficiencies: CreateMixedProficiencies()));
    }

    [Fact]
    public async Task Stuff12Option149PreservesAlreadyMaximumRowsAndDoesNotDisconnect()
    {
        var proficiencies = new PlayerProficiencySnapshot(
        [
            new(410, Level: 20, PreviousLevel: 9, Experience: 0, NeededExperience: 456),
            new(999, Level: 20, PreviousLevel: 3, Experience: 0, NeededExperience: 88),
        ]);
        AssertUnchanged(await InvokeAsync(Stuff12NpcId, Stuff12MaxProficiencyOption,
            proficiencies: proficiencies));
    }

    [Fact]
    public async Task DisabledProficiencyRouteDoesNotAttemptTheLockedProficiencyWriter()
    {
        AssertUnchanged(await InvokeAsync(Stuff12NpcId, Stuff12MaxProficiencyOption,
            proficiencies: CreateMixedProficiencies(), lockProficiencyFile: true));
    }

    private static void AssertUnchanged(InvokeResult result)
    {
        Assert.True(result.Handled);
        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Empty(result.Sent);
        Assert.Equal(0, result.DisconnectCount);
        Assert.Null(result.PersistedCharacterAtCommit);
        Assert.Null(result.PersistedProficienciesAtCommit);
        Assert.Equal(result.OriginalCharacterText, result.PersistedCharacterText);
        Assert.Equal(result.OriginalProficiencyBytes, result.PersistedProficiencyBytes);
        Assert.Equal(result.InitialSession.Character.Level, result.ReloadedCharacter.Level);
        Assert.Equal(result.InitialSession.Character.Experience, result.ReloadedCharacter.Experience);
        Assert.Equal(result.InitialSession.Character.Strength, result.ReloadedCharacter.Strength);
        Assert.Equal(result.InitialSession.Character.Agility, result.ReloadedCharacter.Agility);
        Assert.Equal(result.InitialSession.Character.Vitality, result.ReloadedCharacter.Vitality);
        Assert.Equal(result.InitialSession.Character.Spirit, result.ReloadedCharacter.Spirit);
        Assert.Equal(result.InitialSession.Character.AttributePoints, result.ReloadedCharacter.AttributePoints);
        Assert.Equal(result.InitialSession.Character.MinHitPoints, result.ReloadedCharacter.MinHitPoints);
        Assert.Equal(result.InitialSession.PlayerProficiencies.Proficiencies, result.ReloadedProficiencies.Proficiencies);
        Assert.Contains("Sentinel=keep-me", result.PersistedCharacterText, StringComparison.Ordinal);
        Assert.Contains("Note=preserve-this-section", result.PersistedCharacterText, StringComparison.Ordinal);
        Assert.Contains("reason=ownedGmTransactionRequired", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
        Assert.DoesNotContain("persistence failed", result.Log, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        long level = 130,
        long experience = 123,
        long strength = 11,
        long agility = 12,
        long vitality = 100,
        long spirit = 14,
        long attributePoints = 7,
        long minHitpoints = 5,
        bool gmName = true,
        bool projectManager = false,
        PlayerProficiencySnapshot? proficiencies = null,
        bool includeMutationDependencies = true,
        bool lockCharacterFile = false,
        bool lockProficiencyFile = false)
    {
        WriteCharacterSnapshot(
            level,
            experience,
            strength,
            agility,
            vitality,
            spirit,
            attributePoints,
            minHitpoints,
            gmName,
            projectManager);
        proficiencies ??= PlayerProficiencySnapshot.Empty;
        var proficiencyLoader = new PlayerProficiencySnapshotLoader(_playerProficienciesRoot);
        proficiencyLoader.SaveByCharacterUid(CharacterUid, proficiencies);

        var characterPath = Path.Combine(_usersRoot, AccountId + ".ini");
        var proficiencyPath = Path.Combine(_playerProficienciesRoot, CharacterUid + ".bin");
        var originalCharacterText = File.ReadAllText(characterPath);
        var originalProficiencyBytes = File.ReadAllBytes(proficiencyPath);
        var originalFiles = Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Order().ToArray();
        var characterLoader = new CharacterSnapshotLoader(_usersRoot);
        var character = Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId));
        var session = new WorldSession(
            AccountId,
            "admin-utility-level-proficiency-test",
            character,
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            proficiencies,
            CombatGear: CombatGearSnapshot.Empty,
            UsersRoot: includeMutationDependencies ? _usersRoot : null);

        var sent = new List<byte[]>();
        var disconnectCount = 0;
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            },
            _ =>
            {
                disconnectCount++;
                return ValueTask.CompletedTask;
            });
        CharacterSnapshot? persistedCharacterAtCommit = null;
        PlayerProficiencySnapshot? persistedProficienciesAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is null)
            {
                return;
            }

            if (!ReferenceEquals(previous.Character, current.Character))
            {
                persistedCharacterAtCommit = characterLoader.LoadByAccountId(AccountId);
            }

            if (!ReferenceEquals(previous.PlayerProficiencies, current.PlayerProficiencies))
            {
                persistedProficienciesAtCommit = proficiencyLoader.LoadByCharacterUid(CharacterUid);
            }
        });
        sessions.Set(connection, session);
        var originalAttributes = session.Character.Attributes.ToArray();

        FileStream? characterLock = null;
        FileStream? proficiencyLock = null;
        try
        {
            if (lockCharacterFile)
            {
                characterLock = new FileStream(characterPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            if (lockProficiencyFile)
            {
                proficiencyLock = new FileStream(proficiencyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            using var log = new StringWriter();
            var handled = await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    Session: session,
                    WorldData: includeMutationDependencies ? CreateWorldDataCatalog() : null,
                    Sessions: sessions,
                    PlayerProficienciesRoot: includeMutationDependencies ? _playerProficienciesRoot : null),
                session,
                npcId,
                optionId,
                CancellationToken.None);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(originalAttributes, session.Character.Attributes.ToArray());
            Assert.Equal(originalFiles, Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Order().ToArray());
            return new InvokeResult(
                handled,
                session,
                updatedSession,
                persistedCharacterAtCommit,
                persistedProficienciesAtCommit,
                sent,
                disconnectCount,
                log.ToString(),
                originalCharacterText,
                File.ReadAllText(characterPath),
                originalProficiencyBytes,
                File.ReadAllBytes(proficiencyPath),
                Assert.IsType<CharacterSnapshot>(characterLoader.LoadByAccountId(AccountId)),
                proficiencyLoader.LoadByCharacterUid(CharacterUid));
        }
        finally
        {
            proficiencyLock?.Dispose();
            characterLock?.Dispose();
        }
    }

    private void WriteCharacterSnapshot(
        long level,
        long experience,
        long strength,
        long agility,
        long vitality,
        long spirit,
        long attributePoints,
        long minHitpoints,
        bool gmName,
        bool projectManager)
    {
        var privilegeLine = projectManager ? "ProjectManager=1" : "";
        File.WriteAllText(
            Path.Combine(_usersRoot, AccountId + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name={{(gmName ? "AdminUtilityTester[GM]" : "AdminUtilityTester")}}
            Class=1005
            Map=1002
            X=430
            Y=378
            FirstClass=1005
            SecoundeClass=0
            Reborn=0
            Level={{level}}
            Haire=0
            Experience={{experience}}
            PMap=1002
            PMapX=430
            PMapY=378
            Agility={{agility}}
            Strength={{strength}}
            Vitaliti={{vitality}}
            Spirit={{spirit}}
            Atributes={{attributePoints}}
            MinHitPoints={{minHitpoints}}
            MinMana=25
            MaxMana=25
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            Sentinel=keep-me
            {{privilegeLine}}

            [Other]
            Note=preserve-this-section
            """);
    }

    private static PlayerProficiencySnapshot CreateMixedProficiencies()
    {
        return new PlayerProficiencySnapshot(
        [
            new(410, Level: 4, PreviousLevel: 9, Experience: 123, NeededExperience: 456),
            new(999, Level: 20, PreviousLevel: 3, Experience: 77, NeededExperience: 88),
            new(420, Level: 20, PreviousLevel: 7, Experience: 0, NeededExperience: 999),
        ]);
    }

    private static WorldDataCatalog CreateWorldDataCatalog()
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-14T00:00:00Z"),
                "fixture://admin-utility-level-proficiency",
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
            superGuildWarInfo: EmptyGuildWarData("superguildwar"), classLevelStats: [], professionLevelUps: [],
            professionLevelBenefits: [], professionTitleBenefits: [],
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

    private sealed record InvokeResult(
        bool Handled,
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        CharacterSnapshot? PersistedCharacterAtCommit,
        PlayerProficiencySnapshot? PersistedProficienciesAtCommit,
        IReadOnlyList<byte[]> Sent,
        int DisconnectCount,
        string Log,
        string OriginalCharacterText,
        string PersistedCharacterText,
        byte[] OriginalProficiencyBytes,
        byte[] PersistedProficiencyBytes,
        CharacterSnapshot ReloadedCharacter,
        PlayerProficiencySnapshot ReloadedProficiencies);
}
