using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DirectGearReplacementSpellId = 17_460;
    private const ushort DirectGearReplacementPrioritySpellId = 17_490;
    private const ushort DirectGearReplacementPiratePassiveSpellId = 21_670;
    private const uint DirectGearReplacementItemId = 201;
    private const uint DirectGearReplacementPirateItemId = 701;
    private const int DirectGearReplacementNeptuneFlag = 0xDE;
    private const int DirectGearReplacementFlyFlag = 0x1B;
    private const int DirectGearReplacementFlashingNameFlag = 0;
    private const long DirectGearReplacementTargetHitpoints = 100_000;

    [Theory]
    [InlineData("equipped", 9, "replacement")]
    [InlineData("neptune", 9, "blocked")]
    [InlineData("fly", 2, "blocked")]
    [InlineData("peace", 2, "blocked")]
    [InlineData("out-of-gear-range", 10, "blocked")]
    [InlineData("unequipped", 2, "ordinary")]
    [InlineData("missing-catalog", 9, "unavailable")]
    [InlineData("missing-selected-row", 9, "unavailable")]
    [InlineData("catalog-only", 9, "unavailable")]
    [InlineData("unlearned-priority", 2, "replacement")]
    [InlineData("pirate-all-unlearned", 2, "ordinary")]
    public async Task DirectGearReplacementPhysicalRespectsOuterGatesBeforeReplacingOrdinaryHit(
        string scenario,
        int distance,
        string expectedOutcome)
    {
        // MsgAttackPacket.cs:635 rejects Neptune/range before Verified(null):640.
        // CheckCombatGear:642 then replaces Physical.OnPlayer:645, not vice versa.
        // Force the fixed Dragonhowl array index before looking up its row:
        // index0 selects17460; index1 selects the deliberately absent17470.
        // Filtering to resolvable rows first would incorrectly resample17460.
        var result = await DirectGearReplacementRunAsync(scenario, distance);

        foreach (var frames in new[] { result.CasterFrames, result.TargetFrames })
        {
            var ordinary = frames.Where(bytes =>
                MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.Physical &&
                attack.Query.Uid == PlayerUid && attack.Query.OpponentUid == TargetPlayerUid).ToArray();
            var animations = ParseSpellAnimations(frames);
            if (expectedOutcome == "replacement")
            {
                var animation = Assert.Single(animations);
                Assert.Equal(DirectGearReplacementSpellId, animation.SpellId);
                Assert.Equal(PlayerUid, animation.AttackerUid);
                Assert.Equal((830 + distance, 584), ((int)animation.X, (int)animation.Y));
                var target = Assert.Single(animation.Targets);
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.True(target.Hit);
                Assert.True(target.Damage > 0);
                var animationFrame = Assert.Single(frames, bytes =>
                    LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.AttackerUid == PlayerUid && packet.SpellId == DirectGearReplacementSpellId);
                Assert.Equal((ushort)2239, ReadFrame(animationFrame).Header.Opcode);
                Assert.Empty(ordinary);
            }
            else if (expectedOutcome == "ordinary")
            {
                Assert.Empty(animations);
                var incoming = Assert.Single(ordinary);
                Assert.Equal((ushort)2135, ReadFrame(incoming).Header.Opcode);
                Assert.True(MsgAttackPacket.TryParse(ReadFrame(incoming), out var attack));
                Assert.True(attack.Query.Damage > 0);
                Assert.Equal((120, 121), ((int)attack.Query.X, (int)attack.Query.Y));
            }
            else
            {
                Assert.Empty(animations);
                Assert.Empty(ordinary);
            }
        }

        var targetHitpointValues = ParseMsgUpdates(result.TargetFrames)
            .Where(update => update.Uid == TargetPlayerUid)
            .SelectMany(update => update.Updates)
            .Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints)
            .Select(entry => entry.Value).ToArray();
        if (expectedOutcome is "replacement" or "ordinary")
        {
            // Only establish a real, nonlethal receive and its publication here;
            // no production calculator supplies an expected damage oracle.
            Assert.InRange(result.Target.Character.MinHitPoints, 1, DirectGearReplacementTargetHitpoints - 1);
            Assert.Equal(checked((ulong)result.Target.Character.MinHitPoints), Assert.Single(targetHitpointValues));
        }
        else
        {
            Assert.Equal(DirectGearReplacementTargetHitpoints, result.Target.Character.MinHitPoints);
            Assert.Empty(targetHitpointValues);
        }

        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.Equal(500, result.Caster.Character.MinMana);
        Assert.Equal("100", result.Caster.Character.Attributes["Stamina"]);
        if (expectedOutcome != "blocked")
        {
            // Verified(null) adds this before CheckCombatGear, even when the
            // selected replacement cannot execute (CanAttackPlayer:320-323).
            Assert.True(LegacyPlayerDeathState.HasStatusFlag(
                result.Caster.Character.Attributes, DirectGearReplacementFlashingNameFlag));
        }
        if (scenario == "fly")
        {
            Assert.Equal(BuildStatusFlags(DirectGearReplacementFlyFlag),
                result.Target.Character.Attributes["StatusFlags"]);
        }
        if (expectedOutcome == "unavailable")
        {
            // GetSpells is a fixed array (CombatGear.cs:909-924), not a list
            // filtered by learned entries. CheckCombatGear:1395-1406 consumes a
            // nonzero selection even if ProcescMagic cannot execute its row.
            Assert.Contains("CombatGear", result.Output);
            Assert.Contains("unavailable", result.Output, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WorldServer handled legacy direct player physical", result.Output);
        }
    }

    private static async Task<DirectGearReplacementResult> DirectGearReplacementRunAsync(
        string scenario,
        int distance)
    {
        var hasCatalog = scenario != "missing-catalog";
        var pirate = scenario == "pirate-all-unlearned";
        var hasLearnedReplacement = scenario is not (
            "unequipped" or "missing-catalog" or "catalog-only" or "pirate-all-unlearned");
        var magicRows = hasCatalog
            // Supplied DBServer/magictype.txt:2854 is17460@@17460@@87.
            // Do not substitute the old local Magic wrapper's sort104 fixture.
            ? new List<MagicType> { CreateMagicRow(DirectGearReplacementSpellId, sort: 87, useMana: 0,
                damage: 120, experience: 1_000_000, range: 20, useStamina: 0,
                coolDown: 0, coldTime: 0) }
            : [];
        if (scenario == "unlearned-priority")
        {
            // Source MyCombatGear:1315 requires a learned17490 before its rate
            // override. Merely cataloguing a guaranteed-rate row must not beat
            // the fixed-array index0 selection of the learned17460.
            magicRows.Add(CreateMagicRow(DirectGearReplacementPrioritySpellId, sort: 99,
                useMana: 0, damage: 120, range: 4, coolDown: 0, coldTime: 0, gDamage: 100));
        }
        var worldData = pirate
            ? DirectGearReplacementCreatePirateCatalog()
            : CreateMagicCatalogCore(CreateMonsterFamily(), spawns: [], sobNpcs: [], magicRows: magicRows);
        var caster = CreateSession(mapId: 1_000, classCode: pirate ? 7_005 : 2_005, strength: 300, spirit: 0,
            minHitPoints: 100_000, minMana: 500, stamina: 100,
            spellIds: hasLearnedReplacement ? [DirectGearReplacementSpellId] : [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = scenario == "peace" ? "1" : "0",
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(scenario == "neptune"
                    ? [DirectGearReplacementNeptuneFlag] : []),
                ["LegacyCombatGearAttackSpellIndex"] = scenario == "missing-selected-row" ? "1" : "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1,
                Items =
                [
                    new CombatGearSnapshotItem(pirate ? DirectGearReplacementPirateItemId : DirectGearReplacementItemId,
                        Level: 1, Exp: 0,
                        Equip: scenario == "unequipped" ? 0U : 1U,
                        Transform: 0, MasteryScore: 0, Animas: [], BlackJades: []),
                ],
            },
        };
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectGearReplacementTarget",
            mapId: 1_000, x: 830 + distance, y: 584, strength: 0, spirit: 0,
            minHitPoints: DirectGearReplacementTargetHitpoints, classCode: 9_001, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = DirectGearReplacementTargetHitpoints.ToString(CultureInfo.InvariantCulture),
                ["StatusFlags"] = BuildStatusFlags(scenario == "fly" ? [DirectGearReplacementFlyFlag] : []),
            });
        if (hasLearnedReplacement)
        {
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(caster.PlayerSpells, worldData,
                DirectGearReplacementSpellId, out _, out var spell));
            Assert.True(spell.AttackInFly);
        }
        else
        {
            Assert.Empty(caster.PlayerSpells.Spells);
        }
        if (!hasCatalog)
        {
            Assert.False(LegacyMagicRuntime.TryResolveSpell(worldData, DirectGearReplacementSpellId, 0, out _));
        }
        if (scenario == "missing-selected-row")
        {
            Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, DirectGearReplacementSpellId, 0, out _));
            Assert.False(LegacyMagicRuntime.TryResolveSpell(worldData, 17_470, 0, out _));
        }
        if (scenario == "catalog-only")
        {
            Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, DirectGearReplacementSpellId, 0, out _));
            Assert.False(LegacyMagicRuntime.TryResolveLearnedSpell(caster.PlayerSpells, worldData,
                DirectGearReplacementSpellId, out _, out _));
        }
        if (scenario == "unlearned-priority")
        {
            Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, DirectGearReplacementPrioritySpellId, 0, out _));
            Assert.False(LegacyMagicRuntime.TryResolveLearnedSpell(caster.PlayerSpells, worldData,
                DirectGearReplacementPrioritySpellId, out _, out _));
            Assert.Equal("100", Assert.Single(magicRows, row => row.TypeId == DirectGearReplacementPrioritySpellId).Fields[12]);
            Assert.Equal(DirectGearReplacementSpellId, Assert.Single(caster.PlayerSpells.Spells).Id);
        }
        if (pirate)
        {
            Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, DirectGearReplacementPiratePassiveSpellId, 0, out _));
            var gear = worldData.CombatGearByGearTypeAndLevel[(checked((int)DirectGearReplacementPirateItemId), 1)];
            Assert.Equal(DirectGearReplacementPiratePassiveSpellId * 1_000, Assert.Single(gear.MagicTypeIds));
        }

        // GetSpell may select an unlearned fixed-array spell, but ProcescMagic
        // requires its learned entry (MsgAttackPacket:910) and consumes a miss.
        // Pirate ProcescMagic instead returnsfalse on each unlearned passive
        // (MyCombatGear:739,882); with no normal Pirate array it falls through.
        var sessions = new WorldSessionStore();
        var casterFrames = new ConcurrentQueue<byte[]>();
        var targetFrames = new ConcurrentQueue<byte[]>();
        var casterConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                casterFrames.Enqueue(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: (frame, _) =>
            {
                targetFrames.Enqueue(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: worldData, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var currentCaster));
        Assert.NotNull(currentCaster);
        Assert.True(sessions.TryGet(targetConnection, out var currentTarget));
        Assert.NotNull(currentTarget);
        return new DirectGearReplacementResult(currentCaster, currentTarget,
            casterFrames.ToArray(), targetFrames.ToArray(), log.ToString());
    }

    private static WorldDataCatalog DirectGearReplacementCreatePirateCatalog()
    {
        // Exact GET_Passive[0] is21670 (TQCombatGear.CombatGear.cs:476).
        // A configured gear skill makes it catalog-resolvable, but source
        // MyCombatGear.ProcescMagic:739 still requires a learned entry.
        var gear = new CombatGear(
            Id: checked((int)DirectGearReplacementPirateItemId),
            GearTypeId: checked((int)DirectGearReplacementPirateItemId),
            Level: 1, UpExperience: 0, Life: 0, Attack: 0, Defense: 0,
            UnknownStat0: 0, MagicDefense: 0,
            MagicTypeIds: [DirectGearReplacementPiratePassiveSpellId * 1_000],
            Score: 0, Unknown0: 0, Unknown1: 0, Unknown2: 0,
            SourceFile: "fixture://direct-physical-gear", LineNumber: 1, RawLine: "", Fields: []);
        return WorldDataCatalog.Create(
            new SeedManifest(DateTimeOffset.Parse("2026-10-15T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://direct-physical-gear", new Dictionary<string, int>()),
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
            guildWarInfo: EmptyGuildWarData("direct-gear-guildwar"),
            superGuildWarInfo: EmptyGuildWarData("direct-gear-superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [CreateMagicRow(DirectGearReplacementPiratePassiveSpellId, sort: 5,
                useMana: 0, damage: 120, maxTargets: 4, range: 9, coolDown: 0, coldTime: 0, gDamage: 100)],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [gear],
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

    private sealed record DirectGearReplacementResult(
        WorldSession Caster,
        WorldSession Target,
        byte[][] CasterFrames,
        byte[][] TargetFrames,
        string Output);
}
