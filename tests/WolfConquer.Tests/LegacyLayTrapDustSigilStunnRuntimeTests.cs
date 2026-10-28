using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLayTrapDustSigilStunnRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const uint TargetUid = 1_293_602;
    private const long MapId = 1_015;
    private const ushort CasterX = 830;
    private const ushort CasterY = 584;
    private const ushort TrapX = 840;
    private const ushort TrapY = 590;
    private const ushort TargetX = 842;
    private const ushort TargetY = 590;
    private const uint FloorUid = 7_700_001;
    private const ushort DustDetachmentSpellId = LegacyLayTrap.DustDetachmentSpellId;
    private const int DizzyStatusFlag = LegacyBossStatusEffect.DizzyFlag;
    private const int FixtureNinjaClass = 5_051;
    private const int DustSigilStunnGouyuType = 305;
    private const uint DustSigilStunnLevel = 9;
    private const string DustSigilStunnRollAttribute = "LegacyDustSigilStunnRoll";
    private const string DustSigilStunnExpiresAtUtcAttribute = "LegacyDustSigilStunnExpiresAtUtc";
    private const string DustSigilStunnRemoveOnDeadAttribute = "LegacyDustSigilStunnRemoveOnDead";
    private const string DustSigilStunnSentinelAttribute = "DustSigilStunnSentinel";
    private const int UnrelatedStatusFlag = 0x15;
    private const uint DustSigilStunnDurationSeconds = 2;

    private static readonly DateTimeOffset RepeatRuntimeNow =
        new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RepeatPulseSuccessfulDustSigilStunnAppliesTimedFlagAndOrderedD13FramesToVisiblePlayers()
    {
        Assert.Equal(0x3A, DizzyStatusFlag);
        var fixture = CreateRepeatPulseFixture(
            hasDustSigilStunn: true,
            forcedDustSigilStunnRoll: 0);

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            fixture.Context,
            fixture.RepeatAt,
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        AssertRepeatPulseHitTarget(fixture.OwnerSent);
        Assert.True(fixture.Sessions.TryGet(fixture.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.True(updatedTargetSession.Character.MinHitPoints > 0);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, DizzyStatusFlag));
        Assert.Equal(
            fixture.RepeatAt.AddSeconds(DustSigilStunnDurationSeconds),
            ReadDustSigilStunnExpiry(updatedTargetSession.Character.Attributes));
        Assert.Equal(
            bool.TrueString,
            updatedTargetSession.Character.Attributes[DustSigilStunnRemoveOnDeadAttribute]);

        var targetFrames = SnapshotFrames(fixture.TargetSent);
        var targetStatusIndex = AssertSingleDustSigilStunnStatusVector(
            targetFrames,
            TargetUid,
            expectedSet: true);
        var targetGameUpdateIndex = AssertSingleDustSigilStunnGameUpdate(
            targetFrames,
            TargetUid,
            expectedActive: true,
            expectedTime: DustSigilStunnDurationSeconds);
        Assert.True(targetStatusIndex < targetGameUpdateIndex);

        var viewerFrames = SnapshotFrames(fixture.OwnerSent);
        var viewerStatusIndex = AssertSingleDustSigilStunnStatusVector(
            viewerFrames,
            TargetUid,
            expectedSet: true);
        var viewerGameUpdateIndex = AssertSingleDustSigilStunnGameUpdate(
            viewerFrames,
            TargetUid,
            expectedActive: true,
            expectedTime: DustSigilStunnDurationSeconds);
        Assert.True(viewerStatusIndex < viewerGameUpdateIndex);
    }

    [Fact]
    public async Task RepeatPulseSuccessfulDustSigilStunnDoesNotRefreshOrResendAlreadyActiveFlag()
    {
        var originalExpiresAt = RepeatRuntimeNow.AddSeconds(30);
        var fixture = CreateRepeatPulseFixture(
            hasDustSigilStunn: true,
            forcedDustSigilStunnRoll: 0,
            targetAttributes: CreateDustSigilStunnAttributes(
                originalExpiresAt,
                includeDustStatusFlag: true));

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            fixture.Context,
            fixture.RepeatAt,
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        AssertRepeatPulseHitTarget(fixture.OwnerSent);
        Assert.True(fixture.Sessions.TryGet(fixture.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, DizzyStatusFlag));
        Assert.Equal(originalExpiresAt, ReadDustSigilStunnExpiry(updatedTargetSession.Character.Attributes));
        Assert.Equal(
            bool.TrueString,
            updatedTargetSession.Character.Attributes[DustSigilStunnRemoveOnDeadAttribute]);
        AssertNoDustSigilStunnLifecycleFrames(fixture.TargetSent, TargetUid);
        AssertNoDustSigilStunnLifecycleFrames(fixture.OwnerSent, TargetUid);
    }

    [Fact]
    public async Task DustSigilStunnExpiryHonorsExactBoundaryForOwnerAndVisibleViewer()
    {
        var start = new DateTimeOffset(2026, 7, 13, 13, 0, 0, TimeSpan.Zero);
        var expiresAt = start.AddSeconds(DustSigilStunnDurationSeconds);
        using var log = new StringWriter();
        var ownerSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        var ownerConnection = CreateSendingConnection(ownerSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled,
            log: log);
        var owner = CreateSession(
            TargetUid,
            "DustTarget",
            TargetX,
            TargetY,
            attributes: CreateDustSigilStunnAttributes(
                expiresAt,
                includeDustStatusFlag: true));
        var viewer = CreateSession(
            PlayerUid,
            "VisibleViewer",
            checked((ushort)(TargetX - 1)),
            TargetY);
        SetHandlerSession(handler, ownerConnection, owner);
        SetHandlerSession(handler, viewerConnection, viewer);

        await InvokeDustSigilStunnExpirationsAsync(handler, expiresAt.AddTicks(-1));

        var beforeExpiry = AssertHandlerSession(handler, ownerConnection);
        Assert.True(HasLegacyStatusFlag(beforeExpiry.Character.Attributes, DizzyStatusFlag));
        Assert.True(HasLegacyStatusFlag(beforeExpiry.Character.Attributes, UnrelatedStatusFlag));
        Assert.Equal(expiresAt, ReadDustSigilStunnExpiry(beforeExpiry.Character.Attributes));
        Assert.Empty(SnapshotFrames(ownerSent));
        Assert.Empty(SnapshotFrames(viewerSent));

        await InvokeDustSigilStunnExpirationsAsync(handler, expiresAt);

        var expired = AssertHandlerSession(handler, ownerConnection);
        Assert.False(HasLegacyStatusFlag(expired.Character.Attributes, DizzyStatusFlag));
        Assert.True(HasLegacyStatusFlag(expired.Character.Attributes, UnrelatedStatusFlag));
        Assert.False(expired.Character.Attributes.ContainsKey(DustSigilStunnExpiresAtUtcAttribute));
        Assert.False(expired.Character.Attributes.ContainsKey(DustSigilStunnRemoveOnDeadAttribute));
        Assert.Equal("preserve-me", expired.Character.Attributes[DustSigilStunnSentinelAttribute]);

        var ownerFrames = SnapshotFrames(ownerSent);
        Assert.Equal(2, ownerFrames.Count);
        var ownerStatusIndex = AssertSingleDustSigilStunnStatusVector(
            ownerFrames,
            TargetUid,
            expectedSet: false,
            expectedUnrelatedSet: true);
        var ownerGameUpdateIndex = AssertSingleDustSigilStunnGameUpdate(
            ownerFrames,
            TargetUid,
            expectedActive: false,
            expectedTime: 0);
        Assert.True(ownerStatusIndex < ownerGameUpdateIndex);

        var viewerFrames = SnapshotFrames(viewerSent);
        Assert.Single(viewerFrames);
        AssertSingleDustSigilStunnStatusVector(
            viewerFrames,
            TargetUid,
            expectedSet: false,
            expectedUnrelatedSet: true);
        Assert.Empty(ParseGameUpdates(viewerFrames));
        Assert.Contains("WorldServer expired legacy DustSigilStunn flags", log.ToString());
    }

    [Fact]
    public async Task DustSigilStunnMetadataOnlyExpirySendsNoLifecycleFrames()
    {
        var start = new DateTimeOffset(2026, 7, 13, 14, 0, 0, TimeSpan.Zero);
        var expiresAt = start.AddSeconds(1);
        using var log = new StringWriter();
        var ownerSent = new List<byte[]>();
        var viewerSent = new List<byte[]>();
        var ownerConnection = CreateSendingConnection(ownerSent);
        var viewerConnection = CreateSendingConnection(viewerSent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled,
            log: log);
        var owner = CreateSession(
            TargetUid,
            "DustTarget",
            TargetX,
            TargetY,
            attributes: CreateDustSigilStunnAttributes(
                expiresAt,
                includeDustStatusFlag: false));
        var viewer = CreateSession(
            PlayerUid,
            "VisibleViewer",
            checked((ushort)(TargetX - 1)),
            TargetY);
        SetHandlerSession(handler, ownerConnection, owner);
        SetHandlerSession(handler, viewerConnection, viewer);

        await InvokeDustSigilStunnExpirationsAsync(handler, expiresAt);

        var cleaned = AssertHandlerSession(handler, ownerConnection);
        Assert.False(HasLegacyStatusFlag(cleaned.Character.Attributes, DizzyStatusFlag));
        Assert.True(HasLegacyStatusFlag(cleaned.Character.Attributes, UnrelatedStatusFlag));
        Assert.False(cleaned.Character.Attributes.ContainsKey(DustSigilStunnExpiresAtUtcAttribute));
        Assert.False(cleaned.Character.Attributes.ContainsKey(DustSigilStunnRemoveOnDeadAttribute));
        Assert.Equal("preserve-me", cleaned.Character.Attributes[DustSigilStunnSentinelAttribute]);
        Assert.Empty(SnapshotFrames(ownerSent));
        Assert.Empty(SnapshotFrames(viewerSent));
        Assert.DoesNotContain("WorldServer expired legacy DustSigilStunn flags", log.ToString());
    }

    [Fact]
    public void PlayerDeathClearsDustSigilStunnFlagAndTimerMetadataWhileDeadRemains()
    {
        var now = new DateTimeOffset(2026, 7, 13, 15, 0, 0, TimeSpan.Zero);
        var session = CreateSession(
            TargetUid,
            "DustTarget",
            TargetX,
            TargetY,
            attributes: CreateDustSigilStunnAttributes(
                now.AddSeconds(DustSigilStunnDurationSeconds),
                includeDustStatusFlag: true));

        var death = LegacyPlayerDeathState.ApplyDeath(
            session.Character,
            now,
            TargetX,
            TargetY);

        Assert.True(HasLegacyStatusFlag(
            death.Character.Attributes,
            LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(death.Character.Attributes, DizzyStatusFlag));
        Assert.False(death.Character.Attributes.ContainsKey(DustSigilStunnExpiresAtUtcAttribute));
        Assert.False(death.Character.Attributes.ContainsKey(DustSigilStunnRemoveOnDeadAttribute));
        Assert.Equal("preserve-me", death.Character.Attributes[DustSigilStunnSentinelAttribute]);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 99)]
    public async Task RepeatPulseWithoutDustSigilStunnOrFailedRollDoesNotApplyDizzyStatus(
        bool hasDustSigilStunn,
        uint forcedDustSigilStunnRoll)
    {
        var fixture = CreateRepeatPulseFixture(
            hasDustSigilStunn,
            forcedDustSigilStunnRoll);

        var processedCount = await WorldPacketHandlers.SendDueLegacyLayTrapFloorSpellsAsync(
            fixture.Context,
            fixture.RepeatAt,
            CancellationToken.None);

        Assert.Equal(1, processedCount);
        AssertRepeatPulseHitTarget(fixture.OwnerSent);
        Assert.True(fixture.Sessions.TryGet(fixture.TargetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.True(updatedTargetSession.Character.MinHitPoints > 0);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, DizzyStatusFlag));
        Assert.False(updatedTargetSession.Character.Attributes.ContainsKey(DustSigilStunnExpiresAtUtcAttribute));
        Assert.False(updatedTargetSession.Character.Attributes.ContainsKey(DustSigilStunnRemoveOnDeadAttribute));
        AssertNoDustSigilStunnLifecycleFrames(fixture.TargetSent, TargetUid);
        AssertNoDustSigilStunnLifecycleFrames(fixture.OwnerSent, TargetUid);
    }

    private static RepeatPulseFixture CreateRepeatPulseFixture(
        bool hasDustSigilStunn,
        uint forcedDustSigilStunnRoll,
        Dictionary<string, string>? targetAttributes = null)
    {
        var worldData = CreateWorldData(CreateDustDetachmentMagicRow());
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var ownerSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var ownerConnection = CreateSendingConnection(ownerSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var ownerSession = CreateSession(
            PlayerUid,
            "DustOwner",
            CasterX,
            CasterY,
            attributes: CreateOwnerAttributes(hasDustSigilStunn, forcedDustSigilStunnRoll));
        var targetSession = CreateSession(
            TargetUid,
            "DustTarget",
            TargetX,
            TargetY,
            minHitPoints: 500_000,
            attributes: targetAttributes);
        sessions.Set(ownerConnection, ownerSession);
        sessions.Set(targetConnection, targetSession);

        var repeatAt = RepeatRuntimeNow;
        var effectCreatedAt = repeatAt.AddMilliseconds(-LegacyLayTrap.DustDetachmentRepeatMilliseconds);
        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            LegacyLayTrap.DustDetachmentFloorItemId,
            TrapX,
            TrapY,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            LegacyLayTrap.DustDetachmentFloorColor,
            ItemOwnerUid: 0,
            GuildId: 77,
            FlowerType: 0,
            Timer: 0,
            OwnerX: CasterX,
            OwnerY: CasterY,
            Name: "");
        var effect = new FloorItemRuntimeEffect(
            DustDetachmentSpellId,
            CasterX,
            CasterY,
            effectCreatedAt.AddMilliseconds(LegacyLayTrap.DustDetachmentInvokeMilliseconds),
            Distance: 4,
            HitRadius: 4.0,
            RemovalVisibilityRadius: LegacyLayTrap.DustDetachmentRemovalVisibilityRadius,
            RepeatMilliseconds: LegacyLayTrap.DustDetachmentRepeatMilliseconds,
            SpellLevel: 0,
            LevelHu: 0,
            RuntimeOwnerUid: PlayerUid,
            NextRepeatAt: effectCreatedAt);
        floorItems.AddVisible(
            FloorItemRuntimeEntry.ForEffect(MapId, floorPacket, effect),
            effectCreatedAt);

        var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            ownerConnection,
            log,
            ownerSession,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);
        return new RepeatPulseFixture(
            context,
            sessions,
            ownerSent,
            targetSent,
            targetConnection,
            repeatAt);
    }

    private static Dictionary<string, string> CreateDustSigilStunnAttributes(
        DateTimeOffset expiresAt,
        bool includeDustStatusFlag)
    {
        var flags = includeDustStatusFlag
            ? new[] { DizzyStatusFlag, UnrelatedStatusFlag }
            : new[] { UnrelatedStatusFlag };
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(flags),
            [DustSigilStunnExpiresAtUtcAttribute] = FormatTimestamp(expiresAt),
            [DustSigilStunnRemoveOnDeadAttribute] = bool.TrueString,
            [DustSigilStunnSentinelAttribute] = "preserve-me",
        };
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var values = new ulong[LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength];
        foreach (var flag in flags)
        {
            values[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', values.Select(value => value.ToString(CultureInfo.InvariantCulture))) + "/";
    }

    private static DateTimeOffset ReadDustSigilStunnExpiry(
        IReadOnlyDictionary<string, string> attributes)
    {
        Assert.True(attributes.TryGetValue(DustSigilStunnExpiresAtUtcAttribute, out var rawExpiresAt));
        Assert.True(DateTimeOffset.TryParse(
            rawExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var expiresAt));
        return expiresAt;
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static int AssertSingleDustSigilStunnStatusVector(
        IReadOnlyList<byte[]> frames,
        uint uid,
        bool expectedSet,
        bool? expectedUnrelatedSet = null)
    {
        var index = SingleFrameIndex(
            frames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == uid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var statusUpdate));
        Assert.Equal(uid, statusUpdate.Uid);
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.StatusFlag, statusEntry.Type);
        Assert.NotNull(statusEntry.Values);
        Assert.Equal(
            LegacyMsgUpdatePacket.RuntimeStatusFlagVectorLength,
            statusEntry.Values!.Count);
        Assert.Equal(expectedSet, IsLegacyStatusFlagSet(statusEntry.Values, DizzyStatusFlag));
        if (expectedUnrelatedSet is { } unrelatedSet)
        {
            Assert.Equal(unrelatedSet, IsLegacyStatusFlagSet(statusEntry.Values, UnrelatedStatusFlag));
        }

        return index;
    }

    private static int AssertSingleDustSigilStunnGameUpdate(
        IReadOnlyList<byte[]> frames,
        uint uid,
        bool expectedActive,
        uint expectedTime)
    {
        var index = SingleFrameIndex(
            frames,
            frame => LegacyGameUpdatePacket.TryParse(frame, out var update) && update.Uid == uid);
        Assert.True(LegacyGameUpdatePacket.TryParse(ReadFrame(frames[index]), out var gameUpdate));
        Assert.Equal(uid, gameUpdate.Uid);
        Assert.Equal(LegacyGameUpdateDataType.Stun, gameUpdate.Type);
        Assert.Equal(58U, (uint)gameUpdate.Type);
        Assert.Equal(expectedActive, gameUpdate.Active);
        Assert.Equal(0U, gameUpdate.ShownAmount);
        Assert.Equal(expectedTime, gameUpdate.Time);
        Assert.Equal(0U, gameUpdate.Amount);
        return index;
    }

    private static void AssertNoDustSigilStunnLifecycleFrames(
        IEnumerable<byte[]> frames,
        uint uid)
    {
        Assert.DoesNotContain(
            ParseMsgUpdates(frames),
            update => update.Uid == uid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.DoesNotContain(ParseGameUpdates(frames), update => update.Uid == uid);
    }

    private static Dictionary<string, string> CreateOwnerAttributes(
        bool hasDustSigilStunn,
        uint forcedDustSigilStunnRoll)
    {
        var attributes = new Dictionary<string, string>
        {
            ["GuildID"] = "77",
            [DustSigilStunnRollAttribute] = forcedDustSigilStunnRoll.ToString(CultureInfo.InvariantCulture),
        };
        if (hasDustSigilStunn)
        {
            attributes["MsgNinja"] = MsgNinjaWithEquippedItem(
                itemType: DustSigilStunnGouyuType,
                level: DustSigilStunnLevel,
                position: 1);
        }

        return attributes;
    }

    private static string MsgNinjaWithEquippedItem(uint itemType, uint level, uint position)
    {
        var fields = Enumerable.Repeat("0", 20)
            .Concat(
            [
                itemType.ToString(CultureInfo.InvariantCulture),
                level.ToString(CultureInfo.InvariantCulture),
                position.ToString(CultureInfo.InvariantCulture),
                "0",
                "0",
            ]);
        return string.Join('/', fields) + "/";
    }

    private static void AssertRepeatPulseHitTarget(IReadOnlyList<byte[]> ownerSent)
    {
        var repeatAnimation = Assert.Single(
            ParseSpellAnimations(ownerSent),
            packet => packet.AttackerUid == FloorUid &&
                packet.SpellId == DustDetachmentSpellId &&
                packet.BombFlag == 1);
        Assert.Contains(
            repeatAnimation.Targets,
            target => target.Uid == TargetUid &&
                target.Damage > 0 &&
                target.Hit);
    }

    private static WorldSession CreateSession(
        uint uid,
        string name,
        ushort x,
        ushort y,
        int minHitPoints = 500_000,
        Dictionary<string, string>? attributes = null)
    {
        attributes ??= new Dictionary<string, string>();
        return new WorldSession(
            AccountId: uid,
            Username: name.ToLowerInvariant(),
            new CharacterSnapshot(
                Uid: uid,
                Name: name,
                Body: 1_003,
                Face: 0,
                Hair: 0,
                Class: FixtureNinjaClass,
                FirstClass: FixtureNinjaClass,
                SecondClass: 0,
                Reborn: 0,
                Level: 120,
                Experience: 0,
                MapId: MapId,
                X: x,
                Y: y,
                PreviousMapId: MapId,
                PreviousMapX: x,
                PreviousMapY: y,
                Strength: 300,
                Agility: 0,
                Vitality: 300,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: minHitPoints,
                MinMana: 500,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 1,
                SourceFile: "fixture://laytrap-dust-sigil-stunn",
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>()),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(
            new PlayerSpellSnapshotEntry[]
            {
                new PlayerSpellSnapshotEntry(
                    DustDetachmentSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            }),
            PlayerProficiencySnapshot.Empty);
    }

    private static WorldDataCatalog CreateWorldData(params MagicType[] magicRows)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-23T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://laytrap-dust-sigil-stunn",
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
            magic: magicRows,
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

    private static MagicType CreateDustDetachmentMagicRow()
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = DustDetachmentSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = DustDetachmentSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = ((byte)LegacyMagicSort.LayTrap).ToString(CultureInfo.InvariantCulture);
        fields[3] = "DustDetachment";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "30200";
        fields[12] = "100";
        fields[13] = "3";
        fields[14] = "4";
        fields[15] = "15";
        fields[18] = "900000";
        fields[22] = "511616601";
        fields[24] = "1032";
        fields[35] = "5000";
        fields[36] = "6000";
        fields[44] = "2527";
        fields[47] = "10000";
        return new MagicType(DustDetachmentSpellId, DustDetachmentSpellId, "DustDetachment", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                lock (sent)
                {
                    sent.Add(frame.ToArray());
                }

                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static List<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyGameUpdatePacket> ParseGameUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyGameUpdatePacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyGameUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static int SingleFrameIndex(
        IReadOnlyList<byte[]> frames,
        Func<PacketFrame, bool> predicate)
    {
        var indexes = new List<int>();
        for (var index = 0; index < frames.Count; index++)
        {
            if (predicate(ReadFrame(frames[index])))
            {
                indexes.Add(index);
            }
        }

        return Assert.Single(indexes);
    }

    private static IReadOnlyList<byte[]> SnapshotFrames(IEnumerable<byte[]> frames)
    {
        lock (frames)
        {
            return frames.Select(frame => frame.ToArray()).ToArray();
        }
    }

    private static bool HasLegacyStatusFlag(
        IReadOnlyDictionary<string, string> attributes,
        int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            IsLegacyStatusFlagSet(
                raw
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                        ? value
                        : 0)
                    .ToArray(),
                flag);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static void SetHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        GetHandlerSessions(handler).Set(connection, session);
    }

    private static WorldSessionStore GetHandlerSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField(
            "_sessions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    private static WorldSession AssertHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection)
    {
        Assert.True(GetHandlerSessions(handler).TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static async ValueTask InvokeDustSigilStunnExpirationsAsync(
        WorldServerFrameHandler handler,
        DateTimeOffset now)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "SendDueLegacyDustSigilStunnExpirationsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var invocation = Assert.IsType<ValueTask>(
            method.Invoke(handler, [now, CancellationToken.None]));
        await invocation;
    }

    private sealed record RepeatPulseFixture(
        WorldPacketHandlerContext Context,
        WorldSessionStore Sessions,
        IReadOnlyList<byte[]> OwnerSent,
        IReadOnlyList<byte[]> TargetSent,
        ConquerTcpConnection TargetConnection,
        DateTimeOffset RepeatAt);
}
