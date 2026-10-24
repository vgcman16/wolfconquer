using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("LegacySobNpcEventRuntime")]
public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const uint TargetPlayerUid = 1_293_601;
    private const ushort ThunderSpellId = 1_000;
    private const ushort DevouringStrikeSpellId = 15_700;
    private const ushort ChillingSnowSpellId = 12_960;
    private const ushort LightningBombSpellId = 1_010;
    private const ushort SupermanSpellId = 1_025;
    private const ushort FastBladeSpellId = 1_045;
    private const ushort ScentSwordSpellId = 1_046;
    private const ushort MagicShieldSpellId = 1_090;
    private const ushort StarOfAccuracySpellId = 1_085;
    private const ushort CycloneSpellId = 1_110;
    private const ushort SpiritHealingSpellId = 1_190;
    private const ushort AddManaSpellId = 1_195;
    private const ushort SuperCycloneSpellId = 11_970;
    private const ushort WideStrikeSectorSpellId = 1_250;
    private const ushort CannonBarrageSpellId = 11_050;
    private const ushort PirateXpSkillSpellId = 11_060;
    private const ushort ScurvyBombSpellId = 11_040;
    private const ushort GaleBombSpellId = 11_070;
    private const ushort AddBlackSpotSpellId = 11_100;
    private const ushort MoveLineSpellId = 11_110;
    private const ushort BlackSpotSpellId = 11_120;
    private const ushort WindstormSpellId = 11_140;
    private const ushort PhoenixSpellId = 5_030;
    private const ushort KylinSigilSpellId = 16_330;
    private const ushort TideTrapSpellId = 14_250;
    private const ushort BloomOfDeathSpellId = 14_720;
    private const ushort FatalCrossSpellId = 11_980;
    private const ushort MonsterHunterSpellId = 15_950;
    private const ushort HellVortexSpellId = 16_370;
    private const ushort LavaNutSpellId = 22_040;
    private const ushort ThunderNutSpellId = 22_050;
    private const ushort FrozenNutSpellId = 22_060;
    private const ushort DragonhowlCombatGearSpellId = 17_460;
    private const uint DragonhowlCombatGearItemId = 0xC9;
    private const string RuntimeLegacyAttackStampUtcAttribute = "LegacyAttackStampUtc";
    private const string RuntimeLegacyProtectExpiresAtUtcAttribute = "LegacyProtectExpiresAtUtc";
    private const string RuntimeLegacyCycloneExpiresAtUtcAttribute = "LegacyCycloneExpiresAtUtc";
    private const string RuntimeLegacySupermanExpiresAtUtcAttribute = "LegacySupermanExpiresAtUtc";
    private const string RuntimeLegacySuperCycloneExpiresAtUtcAttribute = "LegacySuperCycloneExpiresAtUtc";
    private const string RuntimeLegacyCannonBarrageExpiresAtUtcAttribute = "LegacyCannonBarrageExpiresAtUtc";
    private const string RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute = "LegacyBlackbeardsRageExpiresAtUtc";
    private const string RuntimeLegacyFrightenedExpiresAtUtcAttribute = "LegacyFrightenedExpiresAtUtc";
    private const string RuntimeLegacyFrightenedRemoveOnDeadAttribute = "LegacyFrightenedRemoveOnDead";
    private const string RuntimeLegacyDustSigilStunnExpiresAtUtcAttribute = "LegacyDustSigilStunnExpiresAtUtc";
    private const string RuntimeLegacyDustSigilStunnRemoveOnDeadAttribute = "LegacyDustSigilStunnRemoveOnDead";
    private const string RuntimeLegacyCombatGearDamageRollAttribute = "LegacyCombatGearDamageRoll";
    private const string RuntimeLegacyOverwhelmRollAttribute = "LegacyOverwhelmRoll";
    private const string RuntimeLegacySolidnessRollAttribute = "LegacySolidnessRoll";
    private const string RuntimeLegacyNinjaHellVortexEquippedAttribute = "LegacyNinjaHellVortexEquipped";
    private const string RuntimeLegacyHellVortexRollAttribute = "LegacyHellVortexRoll";
    private const string RuntimeLegacyThundercloudSightAttribute = "ThundercloudSight";
    private const string RuntimeLegacyAttackHitAttribute = "AttackHit";
    private const int LegacyMagicShieldFlag = 0x08;
    private const int LegacyCombatGearEarthTop4WeeklyFlag = 0x10E | 0x40;
    private const int LegacyCombatGearLightningMagicShieldTop4WeeklyFlag = 0x110 | LegacyMagicShieldFlag | 0x40;
    private const int LegacyStarOfAccuracyFlag = 0x07;
    private const int LegacyXpListFlag = 0x04;
    private const int LegacyCycloneFlag = 0x17;
    private const int LegacySupermanFlag = 0x12;
    private const int LegacyFlashingNameFlag = 0x00;
    private const int LegacyRedNameFlag = 0x0E;
    private const int LegacyBlackNameFlag = 0x0F;
    private const int LegacyFrightenedFlag = 0x36;
    private const int LegacyDizzyFlag = 0x3A;
    private const int LegacyScurvyBombFlag = 0x60;
    private const int LegacyCannonBarrageFlag = 0x78;
    private const int LegacyBlackbeardsRageFlag = 0x79;
    private const int LegacySuperCycloneFlag = 0x96;
    private const int LegacyHeavenBlessingFlag = 0x21;
    private const int LegacyFocusedFlag = 0x15;
    private const int LegacyOblivionFlag = 0x70;
    private const int LegacySlayerFlag = 0xDA;
    private const int LegacyNeptuneCurseFlag = 0xDE;
    private const int LegacyImmortalForceFlag = 0xDC;
    private const int LegacyRiseOfTaoismFlag = 0xE3;
    private const int LegacyHookMoonCasterFlag = 0x104;
    private const int LegacyCleanSweepFlag = 0xFE;
    private const int ChillingSnowFlag = 0xC5;
    private const int XChillingSnowFlag = 0xC6;
    private const int LegacyPkModeCapture = 3;
    private const int LegacyPkModeJiang = 6;
    private const int LegacyPkModeCs = 7;
    private const int LegacyPkModeUnion = 11;
    private const int LegacyJiangAttackFlagNotHitFriends = 1;
    private const int LegacyJiangAttackFlagNotHitClanMembers = 2;
    private const int LegacyJiangAttackFlagNotHitGuildMembers = 4;
    private const int LegacyJiangAttackFlagNotHitAlliedGuild = 8;
    private const int LegacyJiangAttackFlagNoHitAlliesClan = 16;
    private const uint ExpectedThunderDamage = 1_805;
    private const uint ExpectedLegacyPhysicalLineSectorDamage = 800;
    private const uint ExpectedPlayerThunderDamage = 421;
    private const uint SourceDefaultExpRateProf = 1_000;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private readonly record struct BossSpellRetaliationResult(
        ushort SpellId,
        uint Damage,
        uint RemainingHitpoints,
        IReadOnlyDictionary<string, string> Attributes,
        IReadOnlyList<LegacyGameUpdatePacket> GameUpdates,
        string Output);

    [Fact]
    public void RuntimeSpellResolutionRequiresExactLearnedLevel()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, level: 0));
        var playerSpells = new PlayerSpellSnapshot(
        [
            new PlayerSpellSnapshotEntry(
                ThunderSpellId,
                Level: 2,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0),
        ]);

        Assert.False(LegacyMagicRuntime.TryResolveLearnedSpell(
            playerSpells,
            worldData,
            ThunderSpellId,
            out _,
            out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerMagicAttackAgainstVisibleMonsterSendsSpellAnimationAppliesDamageAndSpendsMana(
        bool legacyEncodedQuery)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery)).ToFrameBytes()));

        var spellFrameIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(sent[index]), out _));
        Assert.True(LegacySpellAnimationPacket.TryParse(ReadFrame(sent[spellFrameIndex]), out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(monster.X, spell.X);
        Assert.Equal(monster.Y, spell.Y);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        Assert.Equal(0, spell.LevelHu);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(ExpectedThunderDamage, target.Damage);
        Assert.True(target.Hit);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - ExpectedThunderDamage, damagedMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal(500, updatedSession.Character.MaxMana);

        var manaUpdateIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update) &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana);
        Assert.True(manaUpdateIndex < spellFrameIndex);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[manaUpdateIndex]), out var manaUpdate));
        Assert.Equal(PlayerUid, manaUpdate.Uid);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)], manaUpdate.Updates);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy player magic", output);
        Assert.Contains("spellId=1000", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("totalDamage=1805", output);
        Assert.Contains("totalAppliedDamage=1805", output);
        Assert.Contains($"targetDamageAudit={monster.Uid}:1805/1805/", output);
        Assert.Contains("source=Magic.OnMonster/Physical.OnMonster damage audit", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisibleMonsterUpdatesAndPersistsWeaponProficiencyExperience()
    {
        var profsRoot = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-LegacyMagicMonsterWeaponProficiency-" + Guid.NewGuid().ToString("N"));
        try
        {
            var monsterFamily = CreateMonsterFamily();
            var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
            var worldData = CreateMagicCatalog(monsterFamily, spawn);
            var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
            var startingProficiency = new PlayerProficiencySnapshotEntry(
                421,
                Level: 5,
                PreviousLevel: 0,
                Experience: 123,
                NeededExperience: LegacyProficiencyPacket.GetNeededExperience(5));
            var playerItems = new PlayerItemSnapshot(
                InventoryItems: [],
                RelicFuseItems: [],
                MythsoulBagItems: [],
                EquipmentItems:
                [
                    new PlayerItemSnapshotItem
                    {
                        ItemId = 421_003,
                        Durability = 100,
                        Position = 4,
                    },
                ],
                RuneItems: []);
            var session = CreateSession(
                playerItems: playerItems,
                playerProficiencies: new PlayerProficiencySnapshot([startingProficiency]));
            var expectedProficiency = LegacyProficiencyPacket.ApplyExperience(
                startingProficiency,
                checked(ExpectedThunderDamage * SourceDefaultExpRateProf)).Proficiency;
            var monsterStates = new MonsterRuntimeStateStore();
            var sessions = new WorldSessionStore();
            var sent = new List<byte[]>();
            using var log = new StringWriter();
            var connection = CreateSendingConnection(sent);
            sessions.Set(connection, session);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                PlayerProficienciesRoot: profsRoot);

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

            var packet = Assert.Single(ParseProficiencyPackets(sent));
            Assert.Equal(LegacyProficiencyPacket.FromSnapshot(expectedProficiency), packet);
            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal([expectedProficiency], updatedSession.PlayerProficiencies.Proficiencies);

            var persisted = new PlayerProficiencySnapshotLoader(profsRoot).LoadByCharacterUid(PlayerUid);
            Assert.Equal([expectedProficiency], persisted.Proficiencies);
            Assert.Contains("legacy player-monster magic weapon proficiency experience", log.ToString());
        }
        finally
        {
            if (Directory.Exists(profsRoot))
            {
                Directory.Delete(profsRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerGenericMagicShapeMissAfterVerifiedStillSpendsMana()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                WideStrikeSectorSpellId,
                sort: 4,
                useMana: 7,
                range: 4,
                coldTime: 0));
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [WideStrikeSectorSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: WideStrikeSectorSpellId,
                x: 900,
                y: 900)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        var manaUpdate = Assert.Single(
            ParseMsgUpdates(sent),
            update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)], manaUpdate.Updates);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);

        var output = log.ToString();
        Assert.Contains("no visible monsters matched shape", output);
        Assert.Contains("spellId=1250", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstDamagedMonsterUsesLegacyFinalDamageForExperience()
    {
        var monsterAttributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["level"] = "120",
        };
        var monsterFamily = CreateMonsterFamily() with { Attributes = monsterAttributes };
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, level: 0, experience: 10_000, coldTime: 0),
            CreateMagicRow(ThunderSpellId, sort: 1, level: 1, experience: 20_000, coldTime: 0));
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var preDamage = monster.MaxLife - 50;
        var preHit = monsterStates.ApplyDamage(monster, preDamage, DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.False(preHit.Killed);
        Assert.Equal(50U, preHit.RemainingHitpoints);

        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-spell-xp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            "PlayersSpells");
        var session = CreateSession();
        var statusFields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0);
        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, ThunderSpellId, 0, out var runtimeSpell));
        var expectedDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData,
            monsterFamily,
            runtimeSpell,
            statusFields).Damage;
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            PlayerSpellsRoot: spellsRoot);

        try
        {
            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

            var spell = Assert.Single(ParseSpellAnimations(sent));
            var target = Assert.Single(spell.Targets);
            Assert.Equal(monster.Uid, target.Uid);
            Assert.True(expectedDamage > preHit.RemainingHitpoints);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.False(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
            var death = Assert.Single(ParseDeathAttacks(sent), attack => attack.Query.OpponentUid == monster.Uid);
            Assert.Equal(1, death.Query.KilledMonster);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal((long)expectedDamage, updatedSession.Character.Experience);
            var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
            Assert.Equal((int)expectedDamage, updatedSpell.Experience);
            var persistedSpell = Assert.Single(new PlayerSpellSnapshotLoader(spellsRoot)
                .LoadByCharacterUid(updatedSession.Character.Uid)
                .Spells);
            Assert.Equal(updatedSpell, persistedSpell);

            var output = log.ToString();
            Assert.Contains("WorldServer sent legacy monster experience", output);
            Assert.Contains($"baseGain={expectedDamage}", output);
            Assert.Contains($"totalDamage={expectedDamage}", output);
        }
        finally
        {
            var tempRoot = Path.GetDirectoryName(spellsRoot);
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisibleMonsterAppliesMagicCriticalStrikeEffectFromGear()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var item = CreateStatusItemType(421_003, "SourceClueBacksword", magicAttack: 1_000, skillCriticalStrike: 1_500);
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [spawn],
            [item],
            CreateMagicRow(ThunderSpellId, sort: 1, damage: 0));
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = checked((uint)item.Id),
                    Durability = 100,
                    Position = 4,
                },
            ],
            RuneItems: []);
        var session = CreateSession(
            spirit: 0,
            playerItems: playerItems,
            attributes: new Dictionary<string, string>
            {
                ["LegacyMagicLuckyStrikeRoll"] = "99",
                ["LegacyMagicRefineryRoll"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0);
        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, ThunderSpellId, 0, out var runtimeSpell));
        var expected = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData,
            monsterFamily,
            runtimeSpell,
            fields,
            luckyStrikeRoll: 99,
            magicCriticalStrikeRoll: 0);

        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(expected.Damage, target.Damage);
        Assert.Equal(MsgAttackEffect.CriticalStrike, target.Effect);
        Assert.Equal(expected.Effect, target.Effect);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - expected.Damage, damagedMonster.Hitpoints);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstBossAppliesBossKillerButNotMonsterHunter()
    {
        var monsterAttributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["Boss"] = "1",
        };
        var monsterFamily = CreateMonsterFamily() with { Attributes = monsterAttributes };
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, damage: 40, coldTime: 0),
            CreateMagicRow(MonsterHunterSpellId, sort: 1, useMana: 0, damage: 30_300, coldTime: 0));
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        Assert.True(monster.Boss);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellEntries:
            [
                CreatePlayerSpell(ThunderSpellId),
                CreatePlayerSpell(MonsterHunterSpellId),
            ],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["BossKillerLevel"] = "1",
                ["LegacyMagicLuckyStrikeRoll"] = "99",
                ["LegacyMagicRefineryRoll"] = "99",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, ThunderSpellId, 0, out var runtimeSpell));
        var fields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0);
        var expected = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData,
            monsterFamily,
            runtimeSpell,
            fields,
            luckyStrikeRoll: 99,
            magicCriticalStrikeRoll: 99,
            monsterDamageBonusPercent: 220);
        var incorrectMonsterHunterLeak = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData,
            monsterFamily,
            runtimeSpell,
            fields,
            luckyStrikeRoll: 99,
            magicCriticalStrikeRoll: 99,
            monsterDamageBonusPercent: 520);

        // Source: Magic.OnMonster has BossKiller, while MonsterHunter is only in Physical/Range.OnMonster.
        Assert.Equal(expected.Damage, target.Damage);
        Assert.NotEqual(incorrectMonsterHunterLeak.Damage, target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - expected.Damage, damagedMonster.Hitpoints);
    }

    [Fact]
    public void CalculatePlayerMonsterDamageAppliesMagicPercentBeforeSpellFlatDamage()
    {
        var attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["level"] = "120",
            ["defense"] = "0",
            ["defense2"] = "10000",
        };
        var monsterFamily = CreateMonsterFamily() with { Attributes = attributes };
        var session = CreateSession(spirit: 0);
        var statusFields = new uint[LegacyPlayerStatusPacket.FieldCount];
        statusFields[LegacyPlayerStatusPacket.MagicAttackIndex] = 100;
        statusFields[LegacyPlayerStatusPacket.MagicPercentIndex] = 50;
        var spell = new LegacyMagicRuntimeSpell(
            ThunderSpellId,
            LegacyMagicSort.Attack,
            "Spell1000",
            Level: 0,
            UseMana: 0,
            Damage: 40,
            Experience: 0,
            MaxTargets: 0,
            Range: 9,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);

        var result = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData: null,
            monsterFamily,
            spell,
            statusFields,
            luckyStrikeRoll: 99,
            magicCriticalStrikeRoll: 99);

        Assert.Equal(410U, result.Damage);
        Assert.NotEqual(453U, result.Damage);
    }

    [Fact]
    public void CalculatePlayerMonsterDamageSubtractsMonsterDefenseInsideLevelScaling()
    {
        var attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["level"] = "120",
            ["defense"] = "50",
            ["defence"] = "50",
            ["defense2"] = "10000",
        };
        var monsterFamily = CreateMonsterFamily() with { Attributes = attributes };
        var session = CreateSession(spirit: 0);
        var statusFields = new uint[LegacyPlayerStatusPacket.FieldCount];
        statusFields[LegacyPlayerStatusPacket.MagicAttackIndex] = 100;
        var spell = new LegacyMagicRuntimeSpell(
            ThunderSpellId,
            LegacyMagicSort.Attack,
            "Spell1000",
            Level: 0,
            UseMana: 0,
            Damage: 40,
            Experience: 0,
            MaxTargets: 0,
            Range: 9,
            NeedLevel: 0,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 0,
            ColdTime: 0);

        var result = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData: null,
            monsterFamily,
            spell,
            statusFields,
            luckyStrikeRoll: 99,
            magicCriticalStrikeRoll: 99);

        Assert.Equal(192U, result.Damage);
        Assert.NotEqual(242U, result.Damage);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstDefense2ZeroMonsterUsesLegacyOneDamageClamp()
    {
        var baseFamily = CreateMonsterFamily();
        var attributes = new Dictionary<string, string>(baseFamily.Attributes)
        {
            ["defense2"] = "0",
        };
        var monsterFamily = baseFamily with { Attributes = attributes };
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count == 1, log.ToString());
        var spell = Assert.Single(spells);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(1U, target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - 1U, damagedMonster.Hitpoints);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisibleMonsterConsumesSlayerAndCapsDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacySlayerFlag),
                ["SlayerPercent"] = "1000",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(monster.MaxLife - 1U, target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(1U, damagedMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacySlayerFlag));
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, LegacySlayerFlag)));
        Assert.Contains("WorldServer consumed legacy Slayer monster-hit damage flag", log.ToString());
    }

    [Theory]
    [InlineData(20_211, "TrainingMonster")]
    [InlineData(9_001, "WarDevil")]
    [InlineData(9_001, "TrainingSprite")]
    public async Task HandlerMagicAttackAgainstLegacyOneDamageMonsterUsesLegacyFamilyAndNameClamp(
        int monsterTypeId,
        string monsterName)
    {
        var baseFamily = CreateMonsterFamily();
        var monsterFamily = baseFamily with
        {
            Id = monsterTypeId,
            Name = monsterName,
        };
        var spawn = CreateMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterTypeId,
            sourceMonsterName: monsterName);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count == 1, log.ToString());
        var spell = Assert.Single(spells);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(1U, target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - 1U, damagedMonster.Hitpoints);
    }

    [Fact]
    public async Task HandlerPhysicalAttackAgainstLegacyOneDamageMonsterOverridesClientDamage()
    {
        var monsterFamily = CreateMonsterFamily() with { Name = "TrainingSprite" };
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584, sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 50_000,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var attacks = new List<MsgAttackPacket>();
        foreach (var frame in sent)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(frame), out var attack))
            {
                attacks.Add(attack);
            }
        }

        var response = Assert.Single(attacks, attack => attack.Query is
        {
            Uid: PlayerUid,
            OpponentUid: var opponentUid,
            AttackType: MsgAttackType.Physical,
        } && opponentUid == monster.Uid);
        Assert.Equal(1, response.Query.Damage);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - 1U, damagedMonster.Hitpoints);
        Assert.DoesNotContain(ParseDeathAttacks(sent), attack => attack.Query.OpponentUid == monster.Uid);
    }

    [Fact]
    public async Task HandlerPhysicalAttackAgainstDamagedMonsterDisplaysRequestedDamageAndKillsWithAppliedDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var preDamage = monster.MaxLife - 50;
        var preHit = monsterStates.ApplyDamage(monster, preDamage, DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.False(preHit.Killed);
        Assert.Equal(50U, preHit.RemainingHitpoints);

        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 20_000);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 1,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var response = AssertSinglePhysicalAttack(sent, monster.Uid);
        Assert.True(response.Query.Damage > checked((int)preHit.RemainingHitpoints));
        Assert.False(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        var death = Assert.Single(ParseDeathAttacks(sent), attack => attack.Query.OpponentUid == monster.Uid);
        Assert.Equal(1, death.Query.KilledMonster);
        var output = log.ToString();
        Assert.Contains("requestedDamage=", output);
        Assert.Contains("damageApplied=50", output);
        Assert.Contains("source=Physical.OnMonster damage audit", output);
    }

    [Theory]
    [InlineData(LavaNutSpellId)]
    [InlineData(ThunderNutSpellId)]
    [InlineData(FrozenNutSpellId)]
    public async Task HandlerSourceSilentNutSummonMagicAgainstVisibleMonsterDoesNotEmitUnsupportedGapOrSideEffects(
        ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(spellId, sort: 107, useMana: 11, damage: 9_999, coldTime: 10_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellEntries:
        [
            new PlayerSpellSnapshotEntry(
                spellId,
                Level: 0,
                Experience: 33,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0)
        ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false, spellId)).ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.True(updatedSession.MagicCooldowns is null || !updatedSession.MagicCooldowns.ContainsKey(spellId));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(spellId, updatedSpell.Id);
        Assert.Equal(33, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer skipped source-silent legacy player magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("sort=107", output);
        Assert.Contains("status=sourceSilentNoOp", output);
        Assert.DoesNotContain("PARITY-GAP", output);
        Assert.DoesNotContain("unsupported", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstBossCreditsNonLethalHarmToAttackerSessions()
    {
        var baseFamily = CreateMonsterFamily();
        var bossAttributes = new Dictionary<string, string>(baseFamily.Attributes)
        {
            ["Boss"] = "1",
            ["life"] = "10000",
        };
        var bossFamily = baseFamily with
        {
            Name = "TrainingBoss",
            Attributes = bossAttributes,
        };
        var spawn = CreateMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: bossFamily.Id,
            sourceMonsterName: bossFamily.Name);
        var worldData = CreateMagicCatalog(
            bossFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, coldTime: 0));
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            bossFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var clock = new ManualRuntimeTimeProvider(DateTimeOffset.UtcNow);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var rayzoSent = new List<byte[]>();
        var ninjaSent = new List<byte[]>();
        using var log = new StringWriter();
        var rayzoConnection = CreateSendingConnection(rayzoSent);
        var ninjaConnection = CreateSendingConnection(ninjaSent);
        var rayzoSession = CreateSession(name: "TestSrv");
        var ninjaSession = CreateSession(uid: TargetPlayerUid, name: "Ninja");
        sessions.Set(rayzoConnection, rayzoSession);
        sessions.Set(ninjaConnection, ninjaSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rayzoConnection,
                log,
                rayzoSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                TimeProvider: clock),
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                ninjaConnection,
                log,
                ninjaSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                TimeProvider: clock),
            ReadFrame(new MsgAttackPacket(
                CreateMagicQuery(monster, legacyEncodedQuery: false) with { Uid = TargetPlayerUid }).ToFrameBytes()));

        Assert.True(sessions.TryGet(ninjaConnection, out var updatedNinjaSession));
        Assert.NotNull(updatedNinjaSession);
        clock.Advance(TimeSpan.FromMilliseconds(1_000)); // The declared row retains its native1000ms CoolDown.
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                ninjaConnection,
                log,
                updatedNinjaSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                TimeProvider: clock),
            ReadFrame(new MsgAttackPacket(
                CreateMagicQuery(monster, legacyEncodedQuery: false) with { Uid = TargetPlayerUid }).ToFrameBytes()));

        clock.Advance(TimeSpan.FromSeconds(1)); // Declare a later physical input; do not edit either stamp.
        Assert.True(sessions.TryGet(rayzoConnection, out var updatedRayzoSession));
        Assert.NotNull(updatedRayzoSession);
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                rayzoConnection,
                log,
                updatedRayzoSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                TimeProvider: clock),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 10_000,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var ranking = monsterStates.GetBossHarmRanking(monster, clock.GetUtcNow());
        var physicalHit = AssertSinglePhysicalAttack(rayzoSent, monster.Uid);
        Assert.True(physicalHit.Query.Damage > 0);
        Assert.Collection(
            ranking,
            first =>
            {
                Assert.Equal(1, first.Rank);
                Assert.Equal(TargetPlayerUid, first.PlayerUid);
                Assert.Equal("Ninja", first.PlayerName);
                Assert.Equal(ExpectedThunderDamage * 2UL, first.Damage);
            },
            second =>
            {
                Assert.Equal(2, second.Rank);
                Assert.Equal(PlayerUid, second.PlayerUid);
                Assert.Equal("TestSrv", second.PlayerName);
                Assert.Equal(ExpectedThunderDamage + checked((ulong)physicalHit.Query.Damage), second.Damage);
            });
    }

    [Fact]
    public async Task HandlerBossDeathCreatesHarmRankRewardMailForOnlineHunters()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-boss-harm-mail-" + Guid.NewGuid());
        try
        {
            var baseFamily = CreateMonsterFamily();
            var bossAttributes = new Dictionary<string, string>(baseFamily.Attributes)
            {
                ["Boss"] = "1",
                ["life"] = "10000",
            };
            var bossFamily = baseFamily with
            {
                Name = "TrainingBoss",
                Attributes = bossAttributes,
            };
            var spawn = CreateMonsterSpawn(
                boundX: 832,
                boundY: 584,
                monsterTypeId: bossFamily.Id,
                sourceMonsterName: bossFamily.Name);
            var worldData = CreateMagicCatalog(
                bossFamily,
                [spawn],
                CreateMagicRow(ThunderSpellId, sort: 1, coldTime: 0));
            var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                bossFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
            var clock = new ManualRuntimeTimeProvider(DateTimeOffset.UtcNow);
            var monsterStates = new MonsterRuntimeStateStore();
            var mailboxes = new LegacyMailboxStore(root);
            var sessions = new WorldSessionStore();
            var rayzoSent = new List<byte[]>();
            var ninjaSent = new List<byte[]>();
            using var log = new StringWriter();
            var rayzoConnection = CreateSendingConnection(rayzoSent);
            var ninjaConnection = CreateSendingConnection(ninjaSent);
            var rayzoSession = CreateSession(name: "TestSrv");
            var ninjaSession = CreateSession(uid: TargetPlayerUid, name: "Ninja");
            sessions.Set(rayzoConnection, rayzoSession);
            sessions.Set(ninjaConnection, ninjaSession);

            await PacketHandlers.HandleAsync(
                new WorldPacketHandlerContext(
                    rayzoConnection,
                    log,
                    rayzoSession,
                    WorldData: worldData,
                    Sessions: sessions,
                    MonsterStates: monsterStates,
                    Mailboxes: mailboxes,
                    TimeProvider: clock),
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

            await PacketHandlers.HandleAsync(
                new WorldPacketHandlerContext(
                    ninjaConnection,
                    log,
                    ninjaSession,
                    WorldData: worldData,
                    Sessions: sessions,
                    MonsterStates: monsterStates,
                    Mailboxes: mailboxes,
                    TimeProvider: clock),
                ReadFrame(new MsgAttackPacket(
                    CreateMagicQuery(monster, legacyEncodedQuery: false) with { Uid = TargetPlayerUid }).ToFrameBytes()));

            Assert.True(sessions.TryGet(ninjaConnection, out var updatedNinjaSession));
            Assert.NotNull(updatedNinjaSession);
            clock.Advance(TimeSpan.FromMilliseconds(1_000)); // The declared row retains its native1000ms CoolDown.
            await PacketHandlers.HandleAsync(
                new WorldPacketHandlerContext(
                    ninjaConnection,
                    log,
                    updatedNinjaSession,
                    WorldData: worldData,
                    Sessions: sessions,
                    MonsterStates: monsterStates,
                    Mailboxes: mailboxes,
                    TimeProvider: clock),
                ReadFrame(new MsgAttackPacket(
                    CreateMagicQuery(monster, legacyEncodedQuery: false) with { Uid = TargetPlayerUid }).ToFrameBytes()));

            var preDeathRanking = monsterStates.GetBossHarmRanking(monster, clock.GetUtcNow());
            Assert.Equal([TargetPlayerUid, PlayerUid], preDeathRanking.Select(entry => entry.PlayerUid).ToArray());
            Assert.Equal([1, 2], preDeathRanking.Select(entry => entry.Rank).ToArray());
            PrimeMonsterForServerAuthoritativeFinalHit(monsterStates, monster);

            clock.Advance(TimeSpan.FromSeconds(1)); // Declare a later physical input; do not edit either stamp.
            Assert.True(sessions.TryGet(rayzoConnection, out var updatedRayzoSession));
            Assert.NotNull(updatedRayzoSession);
            await PacketHandlers.HandleAsync(
                new WorldPacketHandlerContext(
                    rayzoConnection,
                    log,
                    updatedRayzoSession,
                    WorldData: worldData,
                    Sessions: sessions,
                    MonsterStates: monsterStates,
                    Mailboxes: mailboxes,
                    TimeProvider: clock),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid,
                    OpponentUid: monster.Uid,
                    X: monster.X,
                    Y: monster.Y,
                    AttackType: MsgAttackType.Physical,
                    Damage: 10_000,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0)).ToFrameBytes()));

            Assert.Empty(monsterStates.GetBossHarmRanking(monster, clock.GetUtcNow()));

            var ninjaMail = Assert.Single(new LegacyMailboxStore(root).Load(TargetPlayerUid));
            Assert.Equal(1U, ninjaMail.Id);
            Assert.Equal(100_000UL, ninjaMail.Money);
            Assert.Equal(1_000U, ninjaMail.EMoney);
            Assert.Equal(1U, ninjaMail.Flag);
            Assert.Equal(1U, ninjaMail.EmoneyRecordType);
            Assert.Equal("STR_SYSTEM_NAME@@", ninjaMail.Title);
            Assert.Equal("STR_SYSTEM_NAME@@", ninjaMail.SenderName);
            Assert.Equal("STR_SYSTEM_NAME@@", ninjaMail.Content);

            var rayzoMail = Assert.Single(new LegacyMailboxStore(root).Load(PlayerUid));
            Assert.Equal(1U, rayzoMail.Id);
            Assert.Equal(100_000UL, rayzoMail.Money);
            Assert.Equal(800U, rayzoMail.EMoney);
            Assert.Equal(2U, rayzoMail.Flag);
            Assert.Equal(1U, rayzoMail.EmoneyRecordType);

            Assert.Contains(
                ninjaSent,
                bytes => LegacyMailListPacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.Type == LegacyMailListType.NewMail &&
                    packet.Mails.Count == 1 &&
                    packet.Mails[0].EMoney == 1_000UL &&
                    packet.Mails[0].Flag == 1UL);
            Assert.Contains(
                ninjaSent,
                bytes => LegacyMailNotifyPacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.Mode == LegacyMailNotifyMode.HasMail);
            Assert.Contains(
                ParseLegacyMessages(ninjaSent),
                message => message.Content[3].Contains(
                    "being 1st hunter while defeating TrainingBoss",
                    StringComparison.Ordinal));

            Assert.Contains(
                rayzoSent,
                bytes => LegacyMailListPacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.Type == LegacyMailListType.NewMail &&
                    packet.Mails.Count == 1 &&
                    packet.Mails[0].EMoney == 800UL &&
                    packet.Mails[0].Flag == 2UL);
            Assert.Contains(
                rayzoSent,
                bytes => LegacyMailNotifyPacket.TryParse(ReadFrame(bytes), out var packet) &&
                    packet.Mode == LegacyMailNotifyMode.HasMail);
            Assert.Contains(
                ParseLegacyMessages(rayzoSent),
                message => message.Content[3].Contains(
                    "being 2st hunter while defeating TrainingBoss",
                    StringComparison.Ordinal));

            var output = log.ToString();
            Assert.Contains("WorldServer applied legacy boss harm-rank mail rewards", output);
            Assert.Contains("ranking=2", output);
            Assert.Contains("created=2", output);
            Assert.Contains("skippedOffline=0", output);
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
    public async Task HandlerMagicAttackAgainstVisiblePlayerSendsSpellAnimationAppliesDamageAndSpendsMana()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateNativeOrdinaryMagic421Catalog(
            monsterFamily,
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        casterSession = CreateNativeOrdinaryMagic421Input(casterSession);
        targetSession = CreateNativeOrdinaryMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        context = await PrepareNativeOrdinaryMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            Assert.Equal(checked((ushort)targetSession.Character.X), spell.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), spell.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 5_000 - ExpectedPlayerThunderDamage));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerDirectPhysicalAgainstVisiblePlayerUsesServerCalculatedDamage()
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), []);
        var sessions = new WorldSessionStore();
        var attackerSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var attackerConnection = CreateSendingConnection(attackerSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var attackerSession = CreateSession(strength: 1_000, spellIds: []);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584);
        sessions.Set(attackerConnection, attackerSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            attackerConnection,
            log,
            attackerSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Physical,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var attackerAttack = AssertSinglePhysicalAttack(attackerSent, TargetPlayerUid);
        var targetAttack = AssertSinglePhysicalAttack(targetSent, TargetPlayerUid);
        Assert.Equal(attackerAttack.Query.Damage, targetAttack.Query.Damage);
        Assert.True(attackerAttack.Query.Damage > 0);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.True(updatedTargetSession.Character.MinHitPoints < targetSession.Character.MinHitPoints);
        Assert.Contains(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == checked((uint)updatedTargetSession.Character.MinHitPoints)));
        Assert.Contains("WorldServer handled legacy direct player physical", log.ToString());
    }

    [Fact]
    public async Task HandlerDirectPhysicalAgainstVisiblePlayerUsesFocusedAjustAttackAndClearsMarker()
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), []);
        var baselineSessions = new WorldSessionStore();
        var focusedSessions = new WorldSessionStore();
        var baselineAttackerSent = new List<byte[]>();
        var baselineTargetSent = new List<byte[]>();
        var focusedAttackerSent = new List<byte[]>();
        var focusedTargetSent = new List<byte[]>();
        using var baselineLog = new StringWriter();
        using var focusedLog = new StringWriter();
        var baselineAttackerConnection = CreateSendingConnection(baselineAttackerSent);
        var baselineTargetConnection = CreateSendingConnection(baselineTargetSent);
        var focusedAttackerConnection = CreateSendingConnection(focusedAttackerSent);
        var focusedTargetConnection = CreateSendingConnection(focusedTargetSent);
        var baselineAttackerSession = CreateSession(strength: 1_000, spellIds: []);
        var focusedAttackerSession = CreateSession(
            strength: 1_000,
            spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyFocusedFlag),
                ["IntensifyDamage"] = "200",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584);
        baselineSessions.Set(baselineAttackerConnection, baselineAttackerSession);
        baselineSessions.Set(baselineTargetConnection, targetSession);
        focusedSessions.Set(focusedAttackerConnection, focusedAttackerSession);
        focusedSessions.Set(focusedTargetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                baselineAttackerConnection,
                baselineLog,
                baselineAttackerSession,
                WorldData: worldData,
                Sessions: baselineSessions),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Physical,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                focusedAttackerConnection,
                focusedLog,
                focusedAttackerSession,
                WorldData: worldData,
                Sessions: focusedSessions),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Physical,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var baselineAttack = AssertSinglePhysicalAttack(baselineAttackerSent, TargetPlayerUid);
        var focusedAttack = AssertSinglePhysicalAttack(focusedAttackerSent, TargetPlayerUid);
        Assert.True(focusedAttack.Query.Damage > baselineAttack.Query.Damage);

        Assert.True(focusedSessions.TryGet(focusedAttackerConnection, out var updatedFocusedAttacker));
        Assert.NotNull(updatedFocusedAttacker);
        Assert.False(HasLegacyStatusFlag(updatedFocusedAttacker.Character.Attributes, LegacyFocusedFlag));
        Assert.False(updatedFocusedAttacker.Character.Attributes.ContainsKey("doFocus"));
        Assert.Contains("WorldServer consumed legacy Focused attack", focusedLog.ToString());
        Assert.Contains("WorldServer cleared legacy Focused attack marker", focusedLog.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerUpdatesPersistsRightWeaponProficiency()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateNativePlayerOnlyProficiencyInputCatalog(
            monsterFamily,
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var proficienciesRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-player-magic-prof-xp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            "PlayersProfs");
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var initialProficiency = new PlayerProficiencySnapshotEntry(
            Id: 421,
            Level: 0,
            PreviousLevel: 0,
            Experience: 100,
            NeededExperience: LegacyProficiencyPacket.GetNeededExperience(0));
        var casterItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    ItemId = 421_003,
                    Durability = 100,
                    Position = 4,
                },
            ],
            RuneItems: []);
        var casterSession = CreateSession(
            playerItems: casterItems,
            playerProficiencies: new PlayerProficiencySnapshot([initialProficiency]));
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        casterSession = CreateNativeOrdinaryMagic421Input(casterSession);
        targetSession = CreateNativeOrdinaryMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            PlayerProficienciesRoot: proficienciesRoot);

        try
        {
            // Declared initial disk snapshot, never a desired post-cast progress outcome.
            var proficiencyLoader = new PlayerProficiencySnapshotLoader(proficienciesRoot);
            proficiencyLoader.SaveByCharacterUid(PlayerUid, casterSession.PlayerProficiencies);
            Assert.Equal(initialProficiency,
                Assert.Single(proficiencyLoader.LoadByCharacterUid(PlayerUid).Proficiencies));
            context = await PrepareNativeOrdinaryMagic421QueriesAsync(
                context, targetConnection, casterSent, targetSent);
            Assert.Equal(initialProficiency,
                Assert.Single(proficiencyLoader.LoadByCharacterUid(PlayerUid).Proficiencies));

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid,
                    OpponentUid: TargetPlayerUid,
                    X: checked((ushort)targetSession.Character.X),
                    Y: checked((ushort)targetSession.Character.Y),
                    AttackType: MsgAttackType.Magic,
                    Damage: 0,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0,
                    SpellId: ThunderSpellId)).ToFrameBytes()));

            // Native Attack1000 player branch leaves num3 zero; UpdateSpell returns before proficiency work.
            var expectedExperience = initialProficiency.Experience;
            Assert.DoesNotContain(casterSent,
                bytes => LegacyProficiencyPacket.TryParse(ReadFrame(bytes), out _));
            Assert.DoesNotContain(casterSent,
                bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(sessions.TryGet(targetConnection, out var hitTarget));
            Assert.Equal(5_000L - ExpectedPlayerThunderDamage,
                Assert.IsType<WorldSession>(hitTarget).Character.MinHitPoints);
            Assert.Equal(ExpectedPlayerThunderDamage,
                Assert.Single(Assert.Single(ParseSpellAnimations(casterSent)).Targets).Damage);

            Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
            Assert.NotNull(updatedCasterSession);
            var updatedProficiency = Assert.Single(updatedCasterSession.PlayerProficiencies.Proficiencies);
            Assert.Equal(421U, updatedProficiency.Id);
            Assert.Equal(expectedExperience, updatedProficiency.Experience);
            Assert.Equal(initialProficiency, updatedProficiency);

            var persistedProficiency = Assert.Single(new PlayerProficiencySnapshotLoader(proficienciesRoot)
                .LoadByCharacterUid(PlayerUid)
                .Proficiencies);
            Assert.Equal(updatedProficiency, persistedProficiency);
            Assert.Equal(initialProficiency, persistedProficiency);
            Assert.DoesNotContain("WorldServer sent legacy player magic weapon proficiency experience", log.ToString());
        }
        finally
        {
            var tempRoot = Path.GetDirectoryName(proficienciesRoot);
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(LegacyCombatGearEarthTop4WeeklyFlag, 99, true, "Earth|Top4Weekly")]
    [InlineData(LegacyCombatGearLightningMagicShieldTop4WeeklyFlag, 9, true, "Lightning|MagicShield|Top4Weekly")]
    [InlineData(LegacyCombatGearLightningMagicShieldTop4WeeklyFlag, 10, false, "Lightning|MagicShield|Top4Weekly")]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesTargetCombatGearCheckDamageGate(
        int targetStatusFlag,
        uint damageRoll,
        bool expectBlocked,
        string expectedReason)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(targetStatusFlag),
                [RuntimeLegacyCombatGearDamageRollAttribute] = damageRoll.ToString(CultureInfo.InvariantCulture),
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        if (expectBlocked)
        {
            Assert.Equal(493, updatedCasterSession.Character.MinMana);
            Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
            Assert.Empty(ParseSpellAnimations(casterSent));
            Assert.Empty(ParseSpellAnimations(targetSent));
            Assert.Contains(ParseMsgUpdates(casterSent), update =>
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Mana &&
                    entry.Value == 493));
            Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));

            var output = log.ToString();
            Assert.Contains(expectedReason, output);
            Assert.Contains(
                "source=CanAttackPlayer.Verified attacked.Owner.CombatGear.CheckDamage",
                output);
            Assert.DoesNotContain("safe zone", output);
            return;
        }

        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
        Assert.DoesNotContain("attacked.Owner.CombatGear.CheckDamage", log.ToString());
    }

    [Theory]
    [InlineData(181325, 181325, true)]
    [InlineData(181325, 182525, false)]
    public async Task HandlerMagicAttackAgainstVisiblePlayerOnMap1082AppliesSameGarmentGate(
        uint casterGarmentId,
        uint targetGarmentId,
        bool expectBlocked)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(), CreateStatusItemType(checked((int)casterGarmentId), "DeclaredCasterGarment"), CreateStatusItemType(checked((int)targetGarmentId), "DeclaredTargetGarment")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_082,
            playerItems: CreateEquipmentPlayerItems(casterGarmentId));
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 1_082,
            playerItems: CreateEquipmentPlayerItems(targetGarmentId));
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        if (expectBlocked)
        {
            Assert.Equal(493, updatedCasterSession.Character.MinMana);
            Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
            Assert.Empty(ParseSpellAnimations(casterSent));
            Assert.Empty(ParseSpellAnimations(targetSent));
            Assert.Contains(
                "source CanAttackPlayer.Verified map=1082 rejected same garmentId=181325",
                log.ToString());
            return;
        }

        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
        Assert.DoesNotContain("map=1082 rejected same garment", log.ToString());
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesCaptureTheFlagGate(
        bool targetAttackable,
        bool expectBlocked)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(mapId: 2_057);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 2_057,
            attributes: new Dictionary<string, string>
            {
                ["CaptureTheFlagAttackable"] = targetAttackable ? "1" : "0",
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        if (expectBlocked)
        {
            Assert.Equal(493, updatedCasterSession.Character.MinMana);
            Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
            Assert.Empty(ParseSpellAnimations(casterSent));
            Assert.Empty(ParseSpellAnimations(targetSent));
            Assert.Contains(ParseMsgUpdates(casterSent), update =>
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Mana &&
                    entry.Value == 493));
            Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
            Assert.Contains(
                "source CanAttackPlayer.Verified map=2057 rejected CaptureTheFlag.Attackable",
                log.ToString());
            return;
        }

        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
        Assert.DoesNotContain("rejected CaptureTheFlag.Attackable", log.ToString());
    }

    [Theory]
    [InlineData(ThunderSpellId, false, 99, 0, true)]
    [InlineData(ThunderSpellId, true, 70, 0, true)]
    [InlineData(ThunderSpellId, true, 69, 0, false)]
    [InlineData(DevouringStrikeSpellId, false, 99, 0, false)]
    [InlineData(ThunderSpellId, false, 99, LegacyRiseOfTaoismFlag, false)]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesImmortalForceOverwhelmGate(
        ushort spellId,
        bool hasOverwhelmRune,
        uint overwhelmRoll,
        int casterStatusFlag,
        bool expectBlocked)
    {
        const uint overwhelmRuneItemId = 300_009;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            spellId == ThunderSpellId
                ? [CreateRetainedNativeMagic421Helmet(), CreateItemType((int)overwhelmRuneItemId, "Overwhelm(+9)")]
                : [CreateItemType((int)overwhelmRuneItemId, "Overwhelm(+9)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0),
            CreateMagicRow(DevouringStrikeSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterAttributes = new Dictionary<string, string>
        {
            [RuntimeLegacyOverwhelmRollAttribute] = overwhelmRoll.ToString(CultureInfo.InvariantCulture),
        };
        if (casterStatusFlag != 0)
        {
            casterAttributes["StatusFlags"] = BuildStatusFlags(casterStatusFlag);
        }

        var casterSession = CreateSession(
            attributes: casterAttributes,
            spellIds: [spellId],
            playerItems: hasOverwhelmRune
                ? CreatePlayerItems(new PlayerItemSnapshotItem
                {
                    ItemId = overwhelmRuneItemId,
                    Position = 101,
                })
                : CreatePlayerItems());
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyImmortalForceFlag),
            });
        if (spellId == ThunderSpellId)
        {
            casterSession = CreateRetainedNativeMagic421Input(casterSession);
            targetSession = CreateRetainedNativeMagic421Input(targetSession);
        }
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        if (spellId == ThunderSpellId)
        {
            context = await PrepareRetainedNativeMagic421QueriesAsync(
                context, targetConnection, casterSent, targetSent);
        }

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: spellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        var casterUpdates = ParseMsgUpdates(casterSent);
        if (expectBlocked)
        {
            Assert.Equal(493, updatedCasterSession.Character.MinMana);
            Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
            Assert.Empty(ParseSpellAnimations(casterSent));
            Assert.Empty(ParseSpellAnimations(targetSent));
            Assert.Contains(
                casterUpdates,
                update => update.Uid == PlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.ForbiddenPirate &&
                        entry.Value == 1));
            Assert.Contains(
                "source CanAttackPlayer.Verified ImmortalForce rejected target because Overwhelm did not bypass",
                log.ToString());
            return;
        }

        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal(casterStatusFlag == LegacyRiseOfTaoismFlag ? 0 : 5_000 - ExpectedPlayerThunderDamage,
            updatedTargetSession.Character.MinHitPoints);
        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
        if (casterStatusFlag == LegacyRiseOfTaoismFlag)
        {
            // This retained gate row supplies only flag227, not the real15440 producer.
            // Native query: raw Spirit300 + MA0 +1 +13000 =13301. ExtraMDamage stays0.
            // Attack1000 soul0 /2 gives6921; Receive killsHP5000 without clamping wire damage.
            foreach (var animation in new[] { casterAnimation, targetAnimation })
            {
                Assert.Equal(PlayerUid, animation.AttackerUid);
                Assert.Equal(spellId, animation.SpellId);
                var hit = Assert.Single(animation.Targets);
                Assert.Equal(TargetPlayerUid, hit.Uid);
                Assert.Equal(6_921U, hit.Damage);
                Assert.True(hit.Hit);
                Assert.Equal(MsgAttackEffect.None, hit.Effect);
            }
        }
        Assert.DoesNotContain(
            casterUpdates,
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ForbiddenPirate));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Theory]
    [InlineData(15_940, 300_108U, 84, true)]
    [InlineData(15_940, 300_108U, 85, false)]
    [InlineData(16_550, 300_109U, 99, true)]
    [InlineData(ThunderSpellId, 300_109U, 0, false)]
    [InlineData(15_940, 0U, 0, false)]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesSolidnessGate(
        ushort spellId,
        uint solidnessRuneItemId,
        uint solidnessRoll,
        bool expectBlocked)
    {
        var itemTypes = solidnessRuneItemId == 0
            ? Array.Empty<ItemType>()
            : [CreateItemType((int)solidnessRuneItemId, $"Solidness(+{solidnessRuneItemId % 100})")];
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            spellId == ThunderSpellId ? [CreateRetainedNativeMagic421Helmet(), .. itemTypes] : itemTypes,
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0),
            CreateMagicRow(15_940, sort: 1, useMana: 7, damage: 120, coldTime: 0),
            CreateMagicRow(16_550, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        // Native15940 is always Passive; this downstream Solidness gate fixture
        // declares the pending source invocation token, not its acquisition producer.
        var casterSession = CreateSession(spellIds: [spellId],
            legacyRandomSpell: spellId == 15_940 ? spellId : (ushort)0);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                [RuntimeLegacySolidnessRollAttribute] = solidnessRoll.ToString(CultureInfo.InvariantCulture),
            },
            playerItems: solidnessRuneItemId == 0
                ? CreatePlayerItems()
                : CreatePlayerItems(new PlayerItemSnapshotItem
                {
                    ItemId = solidnessRuneItemId,
                    Position = 101,
                }));
        if (spellId == ThunderSpellId)
        {
            casterSession = CreateRetainedNativeMagic421Input(casterSession);
            targetSession = CreateRetainedNativeMagic421Input(targetSession);
        }
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        if (spellId == ThunderSpellId)
        {
            context = await PrepareRetainedNativeMagic421QueriesAsync(
                context, targetConnection, casterSent, targetSent);
        }

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: spellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        if (expectBlocked)
        {
            Assert.Equal(493, updatedCasterSession.Character.MinMana);
            Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
            Assert.Empty(ParseSpellAnimations(casterSent));
            Assert.Empty(ParseSpellAnimations(targetSent));
            Assert.Contains("source CanAttackPlayer.Verified Solidness rejected", log.ToString());
            return;
        }

        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
        Assert.DoesNotContain("Solidness rejected", log.ToString());
    }

    [Fact]
    public void CalculateHellVortexPlayerTargetUsesDecompiledRowDamageFallback()
    {
        const ushort fixedPercentSpellId = 31_023;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [HellVortexSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0, damage3: 30_030));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0, damage3: 30_080));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(fixedPercentSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0, damage3: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            HellVortexSpellId,
            out var baselinePlayerSpell,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            HellVortexSpellId,
            out var boostedPlayerSpell,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(HellVortexSpellId, baselinePlayerSpell.Id);
        Assert.Equal(HellVortexSpellId, boostedPlayerSpell.Id);
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(baselineSpell.Damage, fixedPercentSpell.Damage);
        Assert.Equal(30U, baselineSpell.Damage3 % 1_000);
        Assert.Equal(80U, boostedSpell.Damage3 % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Fact]
    public async Task HandlerHellVortexMonsterSelectedCastFansOutOnlyMonstersAroundSelectedMonster()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var casterNearSpawn = CreateMonsterSpawn(generatorId: 11, boundX: 831, boundY: 584);
        var selectedSpawn = CreateMonsterSpawn(generatorId: 12, boundX: 840, boundY: 584);
        var boundarySpawn = CreateMonsterSpawn(generatorId: 13, boundX: 846, boundY: 584);
        var outsideSpawn = CreateMonsterSpawn(generatorId: 14, boundX: 847, boundY: 584);
        var spawns = new[] { casterNearSpawn, selectedSpawn, boundarySpawn, outsideSpawn };
        var sobNpc = CreateTrainingSobNpc() with
        {
            MapId = 1_015,
            X = 840,
            Y = 584,
            Life = 10_000,
            MaxLife = 10_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns,
            [sobNpc],
            [
                CreateMagicRow(
                    HellVortexSpellId,
                    sort: 98,
                    useMana: 7,
                    damage: 200,
                    experience: 100_000,
                    coldTime: 0,
                    damage3: 30_030),
                CreateMagicRow(
                    HellVortexSpellId,
                    sort: 98,
                    level: 1,
                    useMana: 7,
                    damage: 200,
                    experience: 200_000,
                    coldTime: 0,
                    damage3: 30_030),
            ]);
        var casterNearMonster = CreateRuntimeMonsterInstance(worldData, casterNearSpawn, monsterFamily);
        var selectedMonster = CreateRuntimeMonsterInstance(worldData, selectedSpawn, monsterFamily);
        var boundaryMonster = CreateRuntimeMonsterInstance(worldData, boundarySpawn, monsterFamily);
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var playerSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var playerConnection = CreateSendingConnection(playerSent);
        var session = CreateSession(strength: 300, spellIds: [HellVortexSpellId]);
        var sameCenterPlayer = CreateSession(
            uid: TargetPlayerUid,
            name: "SameCenterPlayer",
            x: selectedMonster.X,
            y: selectedMonster.Y,
            minHitPoints: 50_000);
        sessions.Set(connection, session);
        sessions.Set(playerConnection, sameCenterPlayer);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: selectedMonster.Uid,
                X: 900,
                Y: 901,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: HellVortexSpellId)).ToFrameBytes()));

        Assert.True(monsterStates.TryGetVisibleInstance(
            casterNearMonster,
            DateTimeOffset.UtcNow,
            out var visibleCasterNearMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(
            selectedMonster,
            DateTimeOffset.UtcNow,
            out var visibleSelectedMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(
            boundaryMonster,
            DateTimeOffset.UtcNow,
            out var visibleBoundaryMonster));
        Assert.True(monsterStates.TryGetVisibleInstance(
            outsideMonster,
            DateTimeOffset.UtcNow,
            out var visibleOutsideMonster));
        Assert.Equal(casterNearMonster.MaxLife, visibleCasterNearMonster.Hitpoints);
        Assert.True(visibleSelectedMonster.Hitpoints < selectedMonster.MaxLife);
        Assert.True(visibleBoundaryMonster.Hitpoints < boundaryMonster.MaxLife);
        Assert.Equal(outsideMonster.MaxLife, visibleOutsideMonster.Hitpoints);
        Assert.Equal((selectedMonster.X, selectedMonster.Y), (visibleSelectedMonster.X, visibleSelectedMonster.Y));
        Assert.Equal((boundaryMonster.X, boundaryMonster.Y), (visibleBoundaryMonster.X, visibleBoundaryMonster.Y));
        Assert.Equal(10_000, sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(playerConnection, out var updatedSameCenterPlayer));
        Assert.NotNull(updatedSameCenterPlayer);
        Assert.Equal(50_000, updatedSameCenterPlayer.Character.MinHitPoints);

        var animationFrameIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(sent[index]), out _));
        Assert.Equal(sent.Count - 1, animationFrameIndex);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(900, animation.X);
        Assert.Equal(901, animation.Y);
        Assert.Equal(HellVortexSpellId, animation.SpellId);
        Assert.Equal(0, animation.BombFlag);
        Assert.Equal(2, animation.Targets.Count);
        Assert.Contains(animation.Targets, target => target.Uid == selectedMonster.Uid && target.Damage > 0);
        Assert.Contains(animation.Targets, target => target.Uid == boundaryMonster.Uid && target.Damage > 0);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == casterNearMonster.Uid);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideMonster.Uid);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == TargetPlayerUid);
        Assert.All(animation.Targets, target =>
        {
            Assert.Equal(0, target.NewX);
            Assert.Equal(0, target.NewY);
        });

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal(0, updatedSession.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedSession.PlayerSpells.Spells).Experience);
        Assert.DoesNotContain(sent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Empty(ParseProficiencyPackets(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.Experience);
        Assert.Contains("monsterTargets=2 playerTargets=0", log.ToString());
        Assert.Contains("increaseExperienceUp=False updateSpellCheckUpdate=False", log.ToString());
    }

    [Fact]
    public async Task HandlerHellVortexPlayerSelectedCastFansOutOnlyPlayersAroundSelectedPlayerAndScalesDamage()
    {
        const uint casterNearPlayerUid = 1_293_602;
        const uint boundaryPlayerUid = 1_293_603;
        const uint outsidePlayerUid = 1_293_604;

        var monsterFamily = CreateHighLifeMonsterFamily();
        var monsterSpawn = CreateMonsterSpawn(generatorId: 21, boundX: 840, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [monsterSpawn],
            CreateMagicRow(
                HellVortexSpellId,
                sort: 98,
                useMana: 7,
                damage: 200,
                experience: 100_000,
                coldTime: 0,
                damage3: 30_030),
            CreateMagicRow(
                HellVortexSpellId,
                sort: 98,
                level: 1,
                useMana: 7,
                damage: 200,
                experience: 200_000,
                coldTime: 0,
                damage3: 30_030));
        var sameCenterMonster = CreateRuntimeMonsterInstance(worldData, monsterSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var selectedSent = new List<byte[]>();
        var casterNearSent = new List<byte[]>();
        var boundarySent = new List<byte[]>();
        var outsideSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var selectedConnection = CreateSendingConnection(selectedSent);
        var casterNearConnection = CreateSendingConnection(casterNearSent);
        var boundaryConnection = CreateSendingConnection(boundarySent);
        var outsideConnection = CreateSendingConnection(outsideSent);
        var session = CreateSession(strength: 300, spellIds: [HellVortexSpellId]);
        var selectedPlayer = CreateSession(
            uid: TargetPlayerUid,
            name: "Selected",
            x: 840,
            y: 584,
            minHitPoints: 50_000);
        var casterNearPlayer = CreateSession(
            uid: casterNearPlayerUid,
            name: "CasterNear",
            x: 831,
            y: 584,
            minHitPoints: 50_000);
        var boundaryPlayer = CreateSession(
            uid: boundaryPlayerUid,
            name: "Boundary",
            x: 846,
            y: 584,
            minHitPoints: 50_000);
        var outsidePlayer = CreateSession(
            uid: outsidePlayerUid,
            name: "Outside",
            x: 847,
            y: 584,
            minHitPoints: 50_000);
        sessions.Set(connection, session);
        sessions.Set(selectedConnection, selectedPlayer);
        sessions.Set(casterNearConnection, casterNearPlayer);
        sessions.Set(boundaryConnection, boundaryPlayer);
        sessions.Set(outsideConnection, outsidePlayer);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            HellVortexSpellId,
            out _,
            out var hellVortexSpell));
        var selectedPhysicalDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            session,
            selectedPlayer,
            hellVortexSpell,
            worldData,
            1U);
        var boundaryPhysicalDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            session,
            boundaryPlayer,
            hellVortexSpell,
            worldData,
            1U);
        // Physical.OnPlayer adds its nonpenetrating 50% before HellVortex's 12/28 scaling.
        var expectedSelectedDamage = (selectedPhysicalDamage + selectedPhysicalDamage * 50 / 100) * 12 / 28;
        var expectedBoundaryDamage = (boundaryPhysicalDamage + boundaryPhysicalDamage * 50 / 100) * 12 / 28;
        Assert.True(expectedSelectedDamage > 0);
        Assert.True(expectedBoundaryDamage > 0);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 900,
                Y: 901,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: HellVortexSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(selectedConnection, out var updatedSelectedPlayer));
        Assert.NotNull(updatedSelectedPlayer);
        Assert.Equal(50_000 - (long)expectedSelectedDamage, updatedSelectedPlayer.Character.MinHitPoints);
        Assert.Equal((840L, 584L), (updatedSelectedPlayer.Character.X, updatedSelectedPlayer.Character.Y));
        Assert.False(HasLegacyStatusFlag(updatedSelectedPlayer.Character.Attributes, LegacyScurvyBombFlag));
        Assert.True(sessions.TryGet(boundaryConnection, out var updatedBoundaryPlayer));
        Assert.NotNull(updatedBoundaryPlayer);
        Assert.Equal(50_000 - (long)expectedBoundaryDamage, updatedBoundaryPlayer.Character.MinHitPoints);
        Assert.Equal((846L, 584L), (updatedBoundaryPlayer.Character.X, updatedBoundaryPlayer.Character.Y));
        Assert.True(sessions.TryGet(casterNearConnection, out var updatedCasterNearPlayer));
        Assert.NotNull(updatedCasterNearPlayer);
        Assert.Equal(50_000, updatedCasterNearPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(outsideConnection, out var updatedOutsidePlayer));
        Assert.NotNull(updatedOutsidePlayer);
        Assert.Equal(50_000, updatedOutsidePlayer.Character.MinHitPoints);
        Assert.True(monsterStates.TryGetVisibleInstance(
            sameCenterMonster,
            DateTimeOffset.UtcNow,
            out var visibleSameCenterMonster));
        Assert.Equal(sameCenterMonster.MaxLife, visibleSameCenterMonster.Hitpoints);

        var animationFrameIndex = Assert.Single(
            Enumerable.Range(0, sent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(sent[index]), out _));
        Assert.Equal(sent.Count - 1, animationFrameIndex);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(900, animation.X);
        Assert.Equal(901, animation.Y);
        Assert.Equal(0, animation.BombFlag);
        Assert.Equal(2, animation.Targets.Count);
        var selectedTarget = Assert.Single(animation.Targets, target => target.Uid == TargetPlayerUid);
        Assert.Equal(expectedSelectedDamage, selectedTarget.Damage);
        Assert.Equal(0, selectedTarget.NewX);
        Assert.Equal(0, selectedTarget.NewY);
        var boundaryTarget = Assert.Single(animation.Targets, target => target.Uid == boundaryPlayerUid);
        Assert.Equal(expectedBoundaryDamage, boundaryTarget.Damage);
        Assert.Equal(0, boundaryTarget.NewX);
        Assert.Equal(0, boundaryTarget.NewY);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == casterNearPlayerUid);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == outsidePlayerUid);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == sameCenterMonster.Uid);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal(0, updatedSession.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedSession.PlayerSpells.Spells).Experience);
        Assert.DoesNotContain(sent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.Empty(ParseProficiencyPackets(sent));
        Assert.DoesNotContain(
            ParseMsgUpdates(sent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.Experience);
        Assert.Contains("monsterTargets=0 playerTargets=2", log.ToString());
        Assert.Contains("mixedTargetCategories=False pushback=False bombFlag=0", log.ToString());
    }

    [Fact]
    public async Task HandlerHellVortexSortSiblingSpendsResourcesBeforeExactIdNoOp()
    {
        const ushort sortSiblingSpellId = 31_023;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                sortSiblingSpellId,
                sort: 98,
                useMana: 7,
                damage: 200,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [sortSiblingSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 900,
                Y: 901,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: sortSiblingSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Contains("after resource preparation", log.ToString());
        Assert.Contains($"exact matching handler id is {HellVortexSpellId}", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerTriggersHellVortexCounter()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0),
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0, damage3: 30_030));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, spellIds: [ThunderSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            strength: 300,
            spellIds: [HellVortexSpellId],
            attributes: new Dictionary<string, string>
            {
                [RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1",
                [RuntimeLegacyHellVortexRollAttribute] = "0",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.True(updatedCasterSession.Character.MinHitPoints < 5_000);

        var casterAnimations = ParseSpellAnimations(casterSent);
        var targetAnimations = ParseSpellAnimations(targetSent);
        var casterAnimation = Assert.Single(casterAnimations);
        var targetAnimation = Assert.Single(targetAnimations);
        foreach (var animation in new[] { casterAnimation, targetAnimation })
        {
            Assert.Equal(TargetPlayerUid, animation.AttackerUid);
            Assert.Equal(HellVortexSpellId, animation.SpellId);
            Assert.Equal(0, animation.X);
            Assert.Equal(0, animation.Y);
            Assert.Equal(0, animation.BombFlag);
            var animationTarget = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, animationTarget.Uid);
            Assert.Equal(5_000 - (uint)updatedCasterSession.Character.MinHitPoints, animationTarget.Damage);
            Assert.True(animationTarget.Hit);
            Assert.Equal(0, animationTarget.NewX);
            Assert.Equal(0, animationTarget.NewY);
        }

        Assert.Contains("source CanAttackPlayer.Verified HellVortex counter rejected incoming player magic", log.ToString());
        Assert.Contains("WorldServer sent legacy player HellVortex counter", log.ToString());
        Assert.DoesNotContain("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerHellVortexCounterFansOutAroundSelectedAttacker()
    {
        const uint adjacentPlayerUid = 1_293_602;

        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0),
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0, damage3: 30_030));
        var sessions = new WorldSessionStore();
        var attackerSent = new List<byte[]>();
        var counterCasterSent = new List<byte[]>();
        var adjacentSent = new List<byte[]>();
        using var log = new StringWriter();
        var attackerConnection = CreateSendingConnection(attackerSent);
        var counterCasterConnection = CreateSendingConnection(counterCasterSent);
        var adjacentConnection = CreateSendingConnection(adjacentSent);
        var attackerSession = CreateSession(strength: 300, spellIds: [ThunderSpellId]);
        var counterCasterSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 839,
            y: 584,
            strength: 300,
            spellIds: [HellVortexSpellId],
            attributes: new Dictionary<string, string>
            {
                [RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1",
                [RuntimeLegacyHellVortexRollAttribute] = "0",
            });
        var adjacentSession = CreateSession(
            uid: adjacentPlayerUid,
            name: "Adjacent",
            x: 824,
            y: 584,
            strength: 300);
        sessions.Set(attackerConnection, attackerSession);
        sessions.Set(counterCasterConnection, counterCasterSession);
        sessions.Set(adjacentConnection, adjacentSession);
        var context = new WorldPacketHandlerContext(
            attackerConnection,
            log,
            attackerSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)counterCasterSession.Character.X),
                Y: checked((ushort)counterCasterSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(attackerConnection, out var updatedAttackerSession));
        Assert.NotNull(updatedAttackerSession);
        Assert.True(sessions.TryGet(counterCasterConnection, out var updatedCounterCasterSession));
        Assert.NotNull(updatedCounterCasterSession);
        Assert.True(sessions.TryGet(adjacentConnection, out var updatedAdjacentSession));
        Assert.NotNull(updatedAdjacentSession);

        Assert.Equal(493, updatedAttackerSession.Character.MinMana);
        Assert.Equal(5_000, updatedCounterCasterSession.Character.MinHitPoints);
        Assert.True(updatedAttackerSession.Character.MinHitPoints < 5_000);
        Assert.True(updatedAdjacentSession.Character.MinHitPoints < 5_000);
        Assert.Equal((830L, 584L), (updatedAttackerSession.Character.X, updatedAttackerSession.Character.Y));
        Assert.Equal((824L, 584L), (updatedAdjacentSession.Character.X, updatedAdjacentSession.Character.Y));

        var expectedTargetDamageByUid = new Dictionary<uint, uint>
        {
            [PlayerUid] = 5_000U - checked((uint)updatedAttackerSession.Character.MinHitPoints),
            [adjacentPlayerUid] = 5_000U - checked((uint)updatedAdjacentSession.Character.MinHitPoints),
        };

        var attackerAnimation = Assert.Single(ParseSpellAnimations(attackerSent));
        var counterCasterAnimation = Assert.Single(ParseSpellAnimations(counterCasterSent));
        var adjacentAnimation = Assert.Single(ParseSpellAnimations(adjacentSent));
        foreach (var animation in new[] { attackerAnimation, counterCasterAnimation, adjacentAnimation })
        {
            Assert.Equal(TargetPlayerUid, animation.AttackerUid);
            Assert.Equal(HellVortexSpellId, animation.SpellId);
            Assert.Equal(0, animation.X);
            Assert.Equal(0, animation.Y);
            Assert.Equal(0, animation.BombFlag);
            Assert.Equal(2, animation.Targets.Count);
            foreach (var target in animation.Targets)
            {
                Assert.True(expectedTargetDamageByUid.TryGetValue(target.Uid, out var expectedDamage));
                Assert.Equal(expectedDamage, target.Damage);
                Assert.True(target.Hit);
                Assert.Equal(0, target.NewX);
                Assert.Equal(0, target.NewY);
            }
        }

        Assert.Contains("source CanAttackPlayer.Verified HellVortex counter rejected incoming player magic", log.ToString());
        Assert.Contains("WorldServer sent legacy player HellVortex counter", log.ToString());
        Assert.DoesNotContain("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerHellVortexCounterCanTriggerBoundedNestedCounterFanout()
    {
        const uint adjacentPlayerUid = 1_293_602;
        const uint nestedCounterUid = 1_293_603;

        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0),
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0, damage3: 30_030));
        var sessions = new WorldSessionStore();
        var attackerSent = new List<byte[]>();
        var counterCasterSent = new List<byte[]>();
        var adjacentSent = new List<byte[]>();
        var nestedCounterSent = new List<byte[]>();
        using var log = new StringWriter();
        var attackerConnection = CreateSendingConnection(attackerSent);
        var counterCasterConnection = CreateSendingConnection(counterCasterSent);
        var adjacentConnection = CreateSendingConnection(adjacentSent);
        var nestedCounterConnection = CreateSendingConnection(nestedCounterSent);
        var attackerSession = CreateSession(strength: 300, spellIds: [ThunderSpellId]);
        var counterCasterSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            strength: 300,
            spellIds: [HellVortexSpellId],
            attributes: new Dictionary<string, string>
            {
                [RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1",
                [RuntimeLegacyHellVortexRollAttribute] = "0",
            });
        var adjacentSession = CreateSession(
            uid: adjacentPlayerUid,
            name: "Adjacent",
            x: 832,
            y: 586,
            strength: 300);
        var nestedCounterSession = CreateSession(
            uid: nestedCounterUid,
            name: "NestedCounter",
            x: 831,
            y: 585,
            strength: 300,
            spellIds: [HellVortexSpellId],
            attributes: new Dictionary<string, string>
            {
                [RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1",
                [RuntimeLegacyHellVortexRollAttribute] = "0",
            });
        sessions.Set(attackerConnection, attackerSession);
        sessions.Set(counterCasterConnection, counterCasterSession);
        sessions.Set(adjacentConnection, adjacentSession);
        sessions.Set(nestedCounterConnection, nestedCounterSession);
        var context = new WorldPacketHandlerContext(
            attackerConnection,
            log,
            attackerSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)counterCasterSession.Character.X),
                Y: checked((ushort)counterCasterSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(attackerConnection, out var updatedAttackerSession));
        Assert.NotNull(updatedAttackerSession);
        Assert.True(sessions.TryGet(counterCasterConnection, out var updatedCounterCasterSession));
        Assert.NotNull(updatedCounterCasterSession);
        Assert.True(sessions.TryGet(adjacentConnection, out var updatedAdjacentSession));
        Assert.NotNull(updatedAdjacentSession);
        Assert.True(sessions.TryGet(nestedCounterConnection, out var updatedNestedCounterSession));
        Assert.NotNull(updatedNestedCounterSession);

        Assert.Equal(493, updatedAttackerSession.Character.MinMana);
        Assert.True(updatedAttackerSession.Character.MinHitPoints < 5_000);
        Assert.Equal(5_000, updatedCounterCasterSession.Character.MinHitPoints);
        Assert.True(updatedAdjacentSession.Character.MinHitPoints < 5_000);
        Assert.Equal(5_000, updatedNestedCounterSession.Character.MinHitPoints);

        var attackerAnimations = ParseSpellAnimations(attackerSent);
        Assert.Equal(2, attackerAnimations.Count);
        var counterAnimation = Assert.Single(attackerAnimations, animation => animation.AttackerUid == TargetPlayerUid);
        var nestedAnimation = Assert.Single(attackerAnimations, animation => animation.AttackerUid == nestedCounterUid);
        foreach (var animation in new[] { counterAnimation, nestedAnimation })
        {
            Assert.Equal(HellVortexSpellId, animation.SpellId);
            Assert.Contains(animation.Targets, target => target.Uid == PlayerUid && target.Damage > 0);
            Assert.Contains(animation.Targets, target => target.Uid == adjacentPlayerUid && target.Damage > 0);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == TargetPlayerUid);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == nestedCounterUid);
        }

        Assert.Equal(2, ParseSpellAnimations(counterCasterSent).Count);
        Assert.Equal(2, ParseSpellAnimations(adjacentSent).Count);
        Assert.Equal(2, ParseSpellAnimations(nestedCounterSent).Count);

        var output = log.ToString();
        Assert.Contains("source CanAttackPlayer.Verified HellVortex counter rejected incoming player magic", output);
        Assert.Contains("WorldServer sent legacy player HellVortex counter", output);
        Assert.Contains("WorldServer handled legacy HellVortex magic", output);
        Assert.DoesNotContain("cascade limit", output);
        Assert.DoesNotContain("WorldServer handled legacy player-player magic", output);
    }

    [Fact]
    public async Task HandlerHellVortexCounterAgainstWildPhoenixAttackerConsumesDefenderChargeAndKeepsZeroDamageTarget()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0),
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 0, damage: 200, coldTime: 0, damage3: 30_030),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [ThunderSpellId, WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            strength: 300,
            spellIds: [HellVortexSpellId],
            attributes: new Dictionary<string, string>
            {
                [RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1",
                [RuntimeLegacyHellVortexRollAttribute] = "0",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var expectedHellVortexDamage = 1U * 12 / 28;
        Assert.Equal(0U, expectedHellVortexDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(5_000, updatedCasterSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedCasterSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyWildPhoenixFlag));

        var casterAnimations = ParseSpellAnimations(casterSent);
        var targetAnimations = ParseSpellAnimations(targetSent);
        var casterAnimation = Assert.Single(casterAnimations);
        var targetAnimation = Assert.Single(targetAnimations);
        foreach (var animation in new[] { casterAnimation, targetAnimation })
        {
            Assert.Equal(TargetPlayerUid, animation.AttackerUid);
            Assert.Equal(HellVortexSpellId, animation.SpellId);
            var animationTarget = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, animationTarget.Uid);
            Assert.Equal(expectedHellVortexDamage, animationTarget.Damage);
            Assert.True(animationTarget.Hit);
        }

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(casterSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("source CanAttackPlayer.Verified HellVortex counter rejected incoming player magic", output);
        Assert.Contains("WorldServer sent legacy player HellVortex counter", output);
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.DoesNotContain("WorldServer handled legacy player-player magic", output);
    }

    [Theory]
    [InlineData("Waiting", true)]
    [InlineData("Alive", false)]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesTournamentProcessGate(
        string tournamentProcess,
        bool expectBlocked)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateNativeOrdinaryMagic421Catalog(
            monsterFamily,
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            spellIds: [ThunderSpellId],
            attributes: new Dictionary<string, string>
            {
                ["LegacyTournamentInTournament"] = "1",
                ["LegacyTournamentType"] = "LastManStand",
                ["LegacyTournamentProcess"] = tournamentProcess,
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        casterSession = CreateNativeOrdinaryMagic421Input(casterSession);
        targetSession = CreateNativeOrdinaryMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        // Retained LastManStand attributes are compatibility gate inputs,
        // not a claim of a genuinely earned typed tournament registration.
        context = await PrepareNativeOrdinaryMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        if (expectBlocked)
        {
            Assert.Equal(493, updatedCasterSession.Character.MinMana);
            Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
            Assert.Empty(ParseSpellAnimations(casterSent));
            Assert.Empty(ParseSpellAnimations(targetSent));
            Assert.Contains(
                "source CanAttackPlayer.Verified tournament type=LastManStand process=Waiting rejected non-alive caster",
                log.ToString());
            return;
        }

        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
        Assert.DoesNotContain("tournament type=LastManStand", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerWithChillingSnowExtendsTargetColdTimeAndSendsMagicColdTime()
    {
        const uint chillingSnowDurationSeconds = 7;
        var initialRemaining = TimeSpan.FromSeconds(10);
        var initialReadyAt = DateTimeOffset.UtcNow.Add(initialRemaining);
        var expectedReadyAt = initialReadyAt.AddSeconds(chillingSnowDurationSeconds);
        var expectedExtendedRemainingMilliseconds = checked((int)(initialRemaining +
            TimeSpan.FromSeconds(chillingSnowDurationSeconds)).TotalMilliseconds);

        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0),
            CreateMagicRow(ChillingSnowSpellId, sort: 1, useMana: 0, damage: chillingSnowDurationSeconds, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_005,
            spellEntries:
            [
                CreatePlayerSpell(ThunderSpellId),
                CreatePlayerSpell(ChillingSnowSpellId),
            ],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(ChillingSnowFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 1_005) with
        {
            MagicCooldowns = new Dictionary<ushort, DateTimeOffset>
            {
                [ThunderSpellId] = initialReadyAt,
            },
        };
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, XChillingSnowFlag));
        Assert.NotNull(updatedTargetSession.MagicCooldowns);
        Assert.True(updatedTargetSession.MagicCooldowns.TryGetValue(ThunderSpellId, out var updatedReadyAt));
        Assert.Equal(expectedReadyAt, updatedReadyAt);

        Assert.Contains(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, XChillingSnowFlag)));

        var coldTime = Assert.Single(ParseMagicColdTimePackets(targetSent));
        var coldTimeSpell = Assert.Single(coldTime.Spells);
        Assert.Equal(ThunderSpellId, coldTimeSpell.SpellId);
        Assert.InRange(
            coldTimeSpell.Time,
            checked((int)initialRemaining.TotalMilliseconds) + 1,
            expectedExtendedRemainingMilliseconds);
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerWithChillingSnowSkipsColdTimeWhenTargetAlreadyHasXChillingSnow()
    {
        const uint chillingSnowDurationSeconds = 7;
        var initialReadyAt = DateTimeOffset.UtcNow.AddSeconds(10);
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0),
            CreateMagicRow(ChillingSnowSpellId, sort: 1, useMana: 0, damage: chillingSnowDurationSeconds, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_005,
            spellEntries:
            [
                CreatePlayerSpell(ThunderSpellId),
                CreatePlayerSpell(ChillingSnowSpellId),
            ],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(ChillingSnowFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 1_005,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(XChillingSnowFlag),
            }) with
        {
            MagicCooldowns = new Dictionary<ushort, DateTimeOffset>
            {
                [ThunderSpellId] = initialReadyAt,
            },
        };
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, XChillingSnowFlag));
        Assert.NotNull(updatedTargetSession.MagicCooldowns);
        Assert.True(updatedTargetSession.MagicCooldowns.TryGetValue(ThunderSpellId, out var updatedReadyAt));
        Assert.Equal(initialReadyAt, updatedReadyAt);

        foreach (var sentFrames in new[] { casterSent, targetSent })
        {
            Assert.DoesNotContain(
                ParseMsgUpdates(sentFrames),
                update => update.Uid == TargetPlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        IsLegacyStatusFlagSet(entry.Values, XChillingSnowFlag)));
        }

        Assert.Empty(ParseMagicColdTimePackets(targetSent));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstNormalVisiblePlayerInCapturePkModeSpendsManaThenSkipsDamageOrAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeCapture.ToString(CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(ParseMsgUpdates(casterSent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Mana &&
                entry.Value == 493));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        Assert.Contains("target is not FlashingName or BlackName", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstFlashingNameVisiblePlayerInCapturePkModeAppliesDamageAndSpendsMana()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeCapture.ToString(CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyFlashingNameFlag),
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            Assert.Equal(checked((ushort)targetSession.Character.X), spell.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), spell.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstNormalVisiblePlayerInPkModeAppliesCasterFlashingName()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_000,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584, mapId: 1_000);
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyFlashingNameFlag));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            Assert.Equal(checked((ushort)targetSession.Character.X), spell.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), spell.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyFlashingNameFlag)));

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy player-target magic flashing-name", output);
        Assert.Contains("WorldServer handled legacy player-player magic", output);
        Assert.Contains(
            "source=CanAttackPlayer.Verified flashingname AddFlag(FlashingName, 30, RemoveOnDead: true)",
            output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstFlashingNameVisiblePlayerInCapturePkModeDoesNotApplyCasterFlashingName()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_000,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeCapture.ToString(CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 1_000,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyFlashingNameFlag),
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyFlashingNameFlag));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.DoesNotContain(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyFlashingNameFlag)));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Theory]
    [InlineData(LegacyRedNameFlag)]
    [InlineData(LegacyBlackNameFlag)]
    public async Task HandlerMagicAttackAgainstColoredVisiblePlayerInPkModeDoesNotApplyCasterFlashingName(
        int targetStatusFlag)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_000,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 1_000,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(targetStatusFlag),
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyFlashingNameFlag));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.DoesNotContain(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyFlashingNameFlag)));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackTeamPkAgainstTeammateSpendsManaThenSkipsDamageOrAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "2",
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        teamStates.CreateTeam(casterConnection, PlayerUid);
        Assert.True(teamStates.TryAddMember(casterConnection, targetConnection, TargetPlayerUid));
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains("Team PK rejected target because they are on the caster's team", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackTeamQualifierMap700AgainstTeammateSpendsManaThenSkipsDamageOrAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 700,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "42",
                ["InTeamQualifier"] = "true",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 700,
            attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = "42",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        teamStates.CreateTeam(casterConnection, PlayerUid);
        Assert.True(teamStates.TryAddMember(casterConnection, targetConnection, TargetPlayerUid));
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(
            $"source CanAttackPlayer.Verified TeamQualifier map=700 rejected same-team targetUid={TargetPlayerUid}",
            log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackTeamPkAgainstSameGuildPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "2",
                ["GuildID"] = "77",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains("Team PK rejected target because guildId=77 matches caster GuildID", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackGuildPkAgainstSameGuildPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "5",
                ["GuildID"] = "77",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains("Guild PK rejected target because guildId=77 matches caster GuildID", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackTeamPkAgainstFriendPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = "2",
                ["FriendUIDs"] = TargetPlayerUid.ToString(CultureInfo.InvariantCulture),
            },
            new Dictionary<string, string>(),
            $"Team PK rejected target because targetUid={TargetPlayerUid} is in caster Associate friends");
    }

    [Fact]
    public async Task HandlerMagicAttackTeamPkAgainstGuildAllyPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = "2",
                ["GuildID"] = "77",
                ["GuildAllyIDs"] = "88/90/",
            },
            new Dictionary<string, string>
            {
                ["GuildID"] = "88",
            },
            "Team PK rejected target because guildId=88 is allied with caster GuildID");
    }

    [Fact]
    public async Task HandlerMagicAttackTeamPkAgainstSameClanPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = "2",
                ["ClanUID"] = "501",
            },
            new Dictionary<string, string>
            {
                ["ClanUID"] = "501",
            },
            "Team PK rejected target because clanId=501 matches caster ClanUID");
    }

    [Fact]
    public async Task HandlerMagicAttackTeamPkAgainstClanAllyPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = "2",
                ["ClanUID"] = "501",
                ["ClanAllyUIDs"] = "601,602",
            },
            new Dictionary<string, string>
            {
                ["ClanUID"] = "602",
            },
            "Team PK rejected target because clanId=602 is allied with caster ClanUID");
    }

    [Fact]
    public async Task HandlerMagicAttackGuildPkAgainstGuildAllyPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = "5",
                ["GuildID"] = "77",
                ["AllyGuildIds"] = "88|89",
            },
            new Dictionary<string, string>
            {
                ["GuildID"] = "89",
            },
            "Guild PK rejected target because guildId=89 is allied with caster GuildID");
    }

    [Fact]
    public async Task HandlerMagicAttackCsPkAgainstSameServerPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeCs.ToString(CultureInfo.InvariantCulture),
                ["ServerID"] = "401",
            },
            new Dictionary<string, string>
            {
                ["ServerID"] = "401",
            },
            "source CanAttackPlayer.Verified CS PK rejected target because serverId=401 matches caster ServerID");
    }

    [Fact]
    public async Task HandlerMagicAttackUnionPkAgainstSameUnionPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeUnion.ToString(CultureInfo.InvariantCulture),
                ["UnionUID"] = "1370",
            },
            new Dictionary<string, string>
            {
                ["UnionUID"] = "1370",
            },
            "source CanAttackPlayer.Verified Union PK rejected target because unionId=1370 matches caster MyUnion.UID");
    }

    [Fact]
    public async Task HandlerMagicAttackUnionPkAgainstDifferentUnionPlayerAppliesDamageAndSpendsMana()
    {
        await AssertPlayerMagicRelationGateAllowsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeUnion.ToString(CultureInfo.InvariantCulture),
                ["UnionUID"] = "1370",
                ["InUnion"] = "1",
            },
            new Dictionary<string, string>
            {
                ["UnionUID"] = "1371",
                ["InUnion"] = "1",
            },
            unexpectedLog: "Union PK rejected target");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkAgainstInactiveJiangTargetSpendsManaThenSkipsDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
            },
            new Dictionary<string, string>(),
            "source CanAttackPlayer.Verified Jiang PK rejected target because JiangHuActive is not active");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkNotHitFriendsSpendsManaThenSkipsFriendTargetDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["JiangPkFlag"] = LegacyJiangAttackFlagNotHitFriends.ToString(CultureInfo.InvariantCulture),
                ["FriendUIDs"] = TargetPlayerUid.ToString(CultureInfo.InvariantCulture),
            },
            new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
            },
            $"source CanAttackPlayer.Verified Jiang PK NotHitFriends rejected targetUid={TargetPlayerUid}");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkFlagPriorityAllowsSameGuildWhenNotHitFriendsConfiguredAndTargetIsNotFriend()
    {
        await AssertPlayerMagicRelationGateAllowsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["JiangPkFlags"] = (LegacyJiangAttackFlagNotHitFriends | LegacyJiangAttackFlagNotHitGuildMembers)
                    .ToString(CultureInfo.InvariantCulture),
                ["GuildID"] = "77",
            },
            new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
                ["GuildID"] = "77",
            },
            unexpectedLog: "Jiang PK NotHitGuildMembers rejected");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkNoHitAlliesClanSpendsManaThenSkipsClanAllyTargetDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["JiangPkFlag"] = LegacyJiangAttackFlagNoHitAlliesClan.ToString(CultureInfo.InvariantCulture),
                ["ClanUID"] = "501",
                ["ClanAllyUIDs"] = "601,602",
            },
            new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
                ["ClanUID"] = "602",
            },
            "source CanAttackPlayer.Verified Jiang PK NoHitAlliesClan rejected clanId=602");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkNotHitAlliedGuildSpendsManaThenSkipsGuildAllyTargetDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["JiangPkFlag"] = LegacyJiangAttackFlagNotHitAlliedGuild.ToString(CultureInfo.InvariantCulture),
                ["GuildID"] = "77",
                ["AllyGuildIds"] = "88|89",
            },
            new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
                ["GuildID"] = "89",
            },
            "source CanAttackPlayer.Verified Jiang PK NotHitAlliedGuild rejected guildId=89");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkNotHitClanMembersSpendsManaThenSkipsSameClanTargetDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["JiangPkFlag"] = LegacyJiangAttackFlagNotHitClanMembers.ToString(CultureInfo.InvariantCulture),
                ["ClanUID"] = "501",
            },
            new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
                ["ClanUID"] = "501",
            },
            "source CanAttackPlayer.Verified Jiang PK NotHitClanMembers rejected clanId=501");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkNotHitClanMembersWithNoClanIdsAppliesDamageAndSpendsMana()
    {
        await AssertPlayerMagicRelationGateAllowsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["JiangPkFlag"] = LegacyJiangAttackFlagNotHitClanMembers.ToString(CultureInfo.InvariantCulture),
            },
            new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
            },
            unexpectedLog: "Jiang PK NotHitClanMembers rejected");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkNotHitGuildMembersSpendsManaThenSkipsSameGuildTargetDamageOrAnimation()
    {
        await AssertPlayerMagicRelationGateSkipsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["JiangPkFlag"] = LegacyJiangAttackFlagNotHitGuildMembers.ToString(CultureInfo.InvariantCulture),
                ["GuildID"] = "77",
            },
            new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
                ["GuildID"] = "77",
            },
            "source CanAttackPlayer.Verified Jiang PK NotHitGuildMembers rejected guildId=77");
    }

    [Fact]
    public async Task HandlerMagicAttackJiangPkNotHitGuildMembersWithNoGuildIdsAppliesDamageAndSpendsMana()
    {
        await AssertPlayerMagicRelationGateAllowsAsync(
            new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["JiangPkFlag"] = LegacyJiangAttackFlagNotHitGuildMembers.ToString(CultureInfo.InvariantCulture),
            },
            new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
            },
            unexpectedLog: "Jiang PK NotHitGuildMembers rejected");
    }

    [Fact]
    public async Task HandlerMagicAttackUnionPkWithExplicitNotInUnionAppliesDamageAndSpendsMana()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeUnion.ToString(CultureInfo.InvariantCulture),
                ["UnionUID"] = "1370",
                ["InUnion"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["UnionUID"] = "1370",
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
        Assert.DoesNotContain("Union PK rejected target", log.ToString());
    }

    private static async Task AssertPlayerMagicRelationGateSkipsAsync(
        IReadOnlyDictionary<string, string> casterAttributes,
        IReadOnlyDictionary<string, string> targetAttributes,
        string expectedLog)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(attributes: casterAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: targetAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(ParseMsgUpdates(casterSent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Mana &&
                entry.Value == 493));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        Assert.Contains(expectedLog, log.ToString());
    }

    private static async Task AssertPlayerMagicRelationGateAllowsAsync(
        IReadOnlyDictionary<string, string> casterAttributes,
        IReadOnlyDictionary<string, string> targetAttributes,
        string unexpectedLog)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(attributes: casterAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: targetAttributes);
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
        Assert.DoesNotContain(unexpectedLog, log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstMap700StaticWorldPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(mapId: 700);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584, mapId: 700);
        Assert.False(targetSession.Character.Attributes.ContainsKey("DynamicID"));
        Assert.False(targetSession.Character.Attributes.ContainsKey("DynamicId"));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        var casterBefore = System.Text.Json.JsonSerializer.Serialize(casterSession);
        var targetBefore = System.Text.Json.JsonSerializer.Serialize(targetSession);
        var nativeBefore = Assert.IsType<LegacyNativeMagicRow>(worldData.FindNativeMagicRow(ThunderSpellId, 0));
        var nativeCadenceBefore = nativeBefore.CoolDown;
        var nativeDamageBefore = nativeBefore.Damage;
        var nativeFieldsBefore = nativeBefore.Source.Fields.ToArray();

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(500, updatedCasterSession.Character.MinMana);
        Assert.Same(casterSession, updatedCasterSession);
        Assert.Equal(casterBefore, System.Text.Json.JsonSerializer.Serialize(updatedCasterSession));
        Assert.Same(casterSession.Character.Attributes, updatedCasterSession.Character.Attributes);
        Assert.Same(casterSession.PlayerItems, updatedCasterSession.PlayerItems);
        Assert.Same(casterSession.PlayerSpells, updatedCasterSession.PlayerSpells);
        Assert.Same(casterSession.MagicCooldowns, updatedCasterSession.MagicCooldowns);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Same(targetSession, updatedTargetSession);
        Assert.Equal(targetBefore, System.Text.Json.JsonSerializer.Serialize(updatedTargetSession));
        Assert.Same(nativeBefore, worldData.FindNativeMagicRow(ThunderSpellId, 0));
        Assert.Equal(nativeCadenceBefore, nativeBefore.CoolDown);
        Assert.Equal(nativeDamageBefore, nativeBefore.Damage);
        Assert.Equal(nativeFieldsBefore, nativeBefore.Source.Fields);
        Assert.Empty(casterSent);
        Assert.Empty(targetSent);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains("target map=700 dynamicId=0 blocks player-target magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackJiangMap1002NotOnOwnServerSpendsManaThenSkipsDamageOrAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_002,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
                ["OnMyOwnServer"] = "false",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 1_002,
            attributes: new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(ParseMsgUpdates(casterSent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Mana &&
                entry.Value == 493));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        Assert.Contains(
            "source CanAttackPlayer.Verified map=1002 rejected caster because OnMyOwnServer is false",
            log.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public async Task HandlerMagicAttackJiangMap1002OnOwnServerTrueOrAbsentFallsThroughToExistingMapGate(
        string? onMyOwnServer)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterAttributes = new Dictionary<string, string>
        {
            ["PkMode"] = LegacyPkModeJiang.ToString(CultureInfo.InvariantCulture),
        };
        if (onMyOwnServer is not null)
        {
            casterAttributes["OnMyOwnServer"] = onMyOwnServer;
        }

        var casterSession = CreateSession(mapId: 1_002, attributes: casterAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: 1_002,
            attributes: new Dictionary<string, string>
            {
                ["JiangHuActive"] = "1",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(ParseMsgUpdates(casterSent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Mana &&
                entry.Value == 493));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        Assert.Contains("source CanAttackPlayer.Verified Constants.BlockAttackMap", log.ToString());
        Assert.DoesNotContain("OnMyOwnServer is false", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstBlockAttackMapPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        const long blockAttackMapId = 3_825;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: blockAttackMapId,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: blockAttackMapId);
        Assert.False(casterSession.Character.Attributes.ContainsKey("DynamicID"));
        Assert.False(casterSession.Character.Attributes.ContainsKey("DynamicId"));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(ParseMsgUpdates(casterSent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Mana &&
                entry.Value == 493));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));

        var output = log.ToString();
        Assert.Contains("Constants.BlockAttackMap", output);
        Assert.Contains("map=3825", output);
        Assert.Contains("dynamicId=0", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstBlockAttackMapDynamicIdPlayerSpendsManaThenSkipsDamageOrAnimation()
    {
        const long blockAttackMapId = 3_825;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: 1_000,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
                ["DynamicID"] = blockAttackMapId.ToString(CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584, mapId: 1_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(ParseMsgUpdates(casterSent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Mana &&
                entry.Value == 493));
        Assert.DoesNotContain(ParseMsgUpdates(targetSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));

        var output = log.ToString();
        Assert.Contains("Constants.BlockAttackMap", output);
        Assert.Contains("map=1000", output);
        Assert.Contains("dynamicId=3825", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstBlockAttackMapFlashingNamePlayerInCapturePkModeAppliesDamageAndSpendsMana()
    {
        const long blockAttackMapId = 3_825;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: blockAttackMapId,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = LegacyPkModeCapture.ToString(CultureInfo.InvariantCulture),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            mapId: blockAttackMapId,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyFlashingNameFlag),
            });
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, updatedTargetSession.Character.MinHitPoints);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            Assert.Equal(checked((ushort)targetSession.Character.X), spell.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), spell.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy player-player magic", output);
        Assert.DoesNotContain("Constants.BlockAttackMap", output);
    }

    [Theory]
    [InlineData("target", "Invisible", "1", "attacked.Invisible")]
    [InlineData("target", "ArenaWatching", "yes", "attacked.Owner.IsWatching()")]
    [InlineData("caster", "RuntimeWatching", "true", "client.IsWatching()")]
    public async Task HandlerMagicAttackAgainstVisiblePlayerSkipsAttributeBackedCanAttackPlayerInvisibleOrWatchingSpendsManaThenSkipsDamageOrAnimation(
        string attributeOwner,
        string attributeName,
        string attributeValue,
        string expectedDiagnostic)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterAttributes = attributeOwner == "caster"
            ? new Dictionary<string, string> { [attributeName] = attributeValue }
            : null;
        var targetAttributes = attributeOwner == "target"
            ? new Dictionary<string, string> { [attributeName] = attributeValue }
            : null;
        var casterSession = CreateSession(attributes: casterAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: targetAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        var casterBefore = System.Text.Json.JsonSerializer.Serialize(casterSession);
        var targetBefore = System.Text.Json.JsonSerializer.Serialize(targetSession);
        var nativeBefore = Assert.IsType<LegacyNativeMagicRow>(worldData.FindNativeMagicRow(ThunderSpellId, 0));
        var nativeCadenceBefore = nativeBefore.CoolDown;
        var nativeDamageBefore = nativeBefore.Damage;
        var nativeFieldsBefore = nativeBefore.Source.Fields.ToArray();

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(500, updatedCasterSession.Character.MinMana);
        Assert.Same(casterSession, updatedCasterSession);
        Assert.Equal(casterBefore, System.Text.Json.JsonSerializer.Serialize(updatedCasterSession));
        Assert.Same(casterSession.Character.Attributes, updatedCasterSession.Character.Attributes);
        Assert.Same(casterSession.PlayerItems, updatedCasterSession.PlayerItems);
        Assert.Same(casterSession.PlayerSpells, updatedCasterSession.PlayerSpells);
        Assert.Same(casterSession.MagicCooldowns, updatedCasterSession.MagicCooldowns);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Same(targetSession, updatedTargetSession);
        Assert.Equal(targetBefore, System.Text.Json.JsonSerializer.Serialize(updatedTargetSession));
        Assert.Same(nativeBefore, worldData.FindNativeMagicRow(ThunderSpellId, 0));
        Assert.Equal(nativeCadenceBefore, nativeBefore.CoolDown);
        Assert.Equal(nativeDamageBefore, nativeBefore.Damage);
        Assert.Equal(nativeFieldsBefore, nativeBefore.Source.Fields);
        Assert.Empty(casterSent);
        Assert.Empty(targetSent);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains(expectedDiagnostic, log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerSkipsTargetInsideLegacyProtectWindow()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var protectExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                [RuntimeLegacyProtectExpiresAtUtcAttribute] =
                    protectExpiresAt.ToString("O", CultureInfo.InvariantCulture),
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        var casterBefore = System.Text.Json.JsonSerializer.Serialize(casterSession);
        var targetBefore = System.Text.Json.JsonSerializer.Serialize(targetSession);
        var nativeBefore = Assert.IsType<LegacyNativeMagicRow>(worldData.FindNativeMagicRow(ThunderSpellId, 0));
        var nativeCadenceBefore = nativeBefore.CoolDown;
        var nativeDamageBefore = nativeBefore.Damage;
        var nativeFieldsBefore = nativeBefore.Source.Fields.ToArray();

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(500, updatedCasterSession.Character.MinMana);
        Assert.Same(casterSession, updatedCasterSession);
        Assert.Equal(casterBefore, System.Text.Json.JsonSerializer.Serialize(updatedCasterSession));
        Assert.Same(casterSession.Character.Attributes, updatedCasterSession.Character.Attributes);
        Assert.Same(casterSession.PlayerItems, updatedCasterSession.PlayerItems);
        Assert.Same(casterSession.PlayerSpells, updatedCasterSession.PlayerSpells);
        Assert.Same(casterSession.MagicCooldowns, updatedCasterSession.MagicCooldowns);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Same(targetSession, updatedTargetSession);
        Assert.Equal(targetBefore, System.Text.Json.JsonSerializer.Serialize(updatedTargetSession));
        Assert.Same(nativeBefore, worldData.FindNativeMagicRow(ThunderSpellId, 0));
        Assert.Equal(nativeCadenceBefore, nativeBefore.CoolDown);
        Assert.Equal(nativeDamageBefore, nativeBefore.Damage);
        Assert.Equal(nativeFieldsBefore, nativeBefore.Source.Fields);
        Assert.Empty(casterSent);
        Assert.Empty(targetSent);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains("target legacy Protect window is active", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerSkipsTargetWithLegacyNeptuneCurse()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyNeptuneCurseFlag),
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        var casterBefore = System.Text.Json.JsonSerializer.Serialize(casterSession);
        var targetBefore = System.Text.Json.JsonSerializer.Serialize(targetSession);
        var nativeBefore = Assert.IsType<LegacyNativeMagicRow>(worldData.FindNativeMagicRow(ThunderSpellId, 0));
        var nativeCadenceBefore = nativeBefore.CoolDown;
        var nativeDamageBefore = nativeBefore.Damage;
        var nativeFieldsBefore = nativeBefore.Source.Fields.ToArray();

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(500, updatedCasterSession.Character.MinMana);
        Assert.Same(casterSession, updatedCasterSession);
        Assert.Equal(casterBefore, System.Text.Json.JsonSerializer.Serialize(updatedCasterSession));
        Assert.Same(casterSession.Character.Attributes, updatedCasterSession.Character.Attributes);
        Assert.Same(casterSession.PlayerItems, updatedCasterSession.PlayerItems);
        Assert.Same(casterSession.PlayerSpells, updatedCasterSession.PlayerSpells);
        Assert.Same(casterSession.MagicCooldowns, updatedCasterSession.MagicCooldowns);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Same(targetSession, updatedTargetSession);
        Assert.Equal(targetBefore, System.Text.Json.JsonSerializer.Serialize(updatedTargetSession));
        Assert.Same(nativeBefore, worldData.FindNativeMagicRow(ThunderSpellId, 0));
        Assert.Equal(nativeCadenceBefore, nativeBefore.CoolDown);
        Assert.Equal(nativeDamageBefore, nativeBefore.Damage);
        Assert.Equal(nativeFieldsBefore, nativeBefore.Source.Fields);
        Assert.Empty(casterSent);
        Assert.Empty(targetSent);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Contains("target has NeptuneCurse", log.ToString());
    }

    [Theory]
    [InlineData(10_137, 143, 584, 144, 584, "DragonIsland")]
    [InlineData(10_250, 1_098, 1_168, 1_099, 1_168, "DeityLand")]
    public async Task HandlerMagicAttackAgainstVisiblePlayerSkipsTargetInsideLegacySafeZone(
        long mapId,
        long casterX,
        long casterY,
        long targetX,
        long targetY,
        string safeZone)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(x: casterX, y: casterY, mapId: mapId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: targetX,
            y: targetY,
            mapId: mapId);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        var casterBefore = System.Text.Json.JsonSerializer.Serialize(casterSession);
        var targetBefore = System.Text.Json.JsonSerializer.Serialize(targetSession);
        var nativeBefore = Assert.IsType<LegacyNativeMagicRow>(worldData.FindNativeMagicRow(ThunderSpellId, 0));
        var nativeCadenceBefore = nativeBefore.CoolDown;
        var nativeDamageBefore = nativeBefore.Damage;
        var nativeFieldsBefore = nativeBefore.Source.Fields.ToArray();

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(500, updatedCasterSession.Character.MinMana);
        Assert.Same(casterSession, updatedCasterSession);
        Assert.Equal(casterBefore, System.Text.Json.JsonSerializer.Serialize(updatedCasterSession));
        Assert.Same(casterSession.Character.Attributes, updatedCasterSession.Character.Attributes);
        Assert.Same(casterSession.PlayerItems, updatedCasterSession.PlayerItems);
        Assert.Same(casterSession.PlayerSpells, updatedCasterSession.PlayerSpells);
        Assert.Same(casterSession.MagicCooldowns, updatedCasterSession.MagicCooldowns);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Same(targetSession, updatedTargetSession);
        Assert.Equal(targetBefore, System.Text.Json.JsonSerializer.Serialize(updatedTargetSession));
        Assert.Same(nativeBefore, worldData.FindNativeMagicRow(ThunderSpellId, 0));
        Assert.Equal(nativeCadenceBefore, nativeBefore.CoolDown);
        Assert.Equal(nativeDamageBefore, nativeBefore.Damage);
        Assert.Equal(nativeFieldsBefore, nativeBefore.Source.Fields);
        Assert.Empty(casterSent);
        Assert.Empty(targetSent);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));

        var output = log.ToString();
        Assert.Contains($"source CanAttackPlayer.Verified rejected {safeZone} safe zone", output);
        Assert.Contains($"map={mapId}", output);
        Assert.Contains("You can't PK in the safe zone.", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerDeathSetsDeathFlagsAndSendsDeathFrame()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: 100);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        // Earn native Status.MA through Data251 before the lethal public cast.
        // No gear, flag, cached result or raw character input is assigned here.
        var queryStart = casterSent.Count;
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgActionPacket { ObjId = PlayerUid,
                Type = (MsgActionType)251 }.ToFrameBytes()));
        var queryBytes = Assert.Single(casterSent.Skip(queryStart),
            bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(queryBytes), out var queryStatus));
        Assert.Equal(PlayerUid, queryStatus.Uid);
        Assert.Equal(301U, queryStatus.MagicAttack); // Raw Spirit300 + native Equip1540 increment.
        Assert.Equal(900U, queryStatus.MaxHP);      // Class142 generic Spirit300 *3, no gear.
        Assert.Equal(1_500U, queryStatus.MaxMana);  // Class142 generic Spirit300 *5.
        Assert.True(sessions.TryGet(casterConnection, out var queriedCasterSession));
        Assert.NotNull(queriedCasterSession);
        Assert.Equal(900L, queriedCasterSession.Character.MinHitPoints); // Native query clamps rawHP5000.
        Assert.Equal(500L, queriedCasterSession.Character.MinMana);     // Query does not fill current MP.
        context = context with { Session = queriedCasterSession };
        casterSent.Clear();
        targetSent.Clear();

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(0, updatedTargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Equal(
            PlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedTargetSession.Character.Attributes[LegacyPlayerDeathState.DeadKillerUidAttribute]);
        Assert.Equal("Caster", updatedTargetSession.Character.Attributes[LegacyPlayerDeathState.DeadKillerNameAttribute]);

        Assert.Contains(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 0));

        var deathHp = AssertExistingNormalDeathCombatPhases(targetSent, TargetPlayerUid,
            (900, 100), (900, 100), (900, 100), (900, 0));
        foreach (var updates in new[] { ParseMsgUpdates(casterSent), ParseMsgUpdates(targetSent) })
        {
            var statusUpdates = updates.Where(update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag)).ToArray();
            Assert.Equal(2, statusUpdates.Length);
            for (var phase = 0; phase < statusUpdates.Length; phase++)
            {
                var statusEntry = Assert.Single(statusUpdates[phase].Updates);
                Assert.NotNull(statusEntry.Values);
                Assert.Equal(phase == 1,
                    IsLegacyStatusFlagSet(statusEntry.Values!, LegacyPlayerDeathState.DeadStatusFlag));
            }
        }
        var deathIndex = SingleFrameIndex(targetSent, frame =>
            MsgAttackPacket.TryParse(frame, out var death) && death.Query.AttackType == MsgAttackType.Death);
        Assert.All(deathHp, index => Assert.True(index < deathIndex));

        foreach (var death in new[] { Assert.Single(ParseDeathAttacks(casterSent)), Assert.Single(ParseDeathAttacks(targetSent)) })
        {
            Assert.Equal(PlayerUid, death.Query.Uid);
            Assert.Equal(TargetPlayerUid, death.Query.OpponentUid);
            Assert.Equal(MsgAttackType.Death, death.Query.AttackType);
            Assert.Equal(0, death.Query.Damage);
        }

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            // Native Attack1000 keeps (queriedMA301 + raw row120) /2 =210 on the wire.
            // Receive kills HP100 without replacing the same SpellObj.Damage by HP loss.
            Assert.Equal(210U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisibleTrainingSobNpcSendsSpellAnimationAndMutatesRuntimeHitpoints()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_039, x: 84, y: 70);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.Equal(
            sobNpc.Id.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
        Assert.True(bool.Parse(updatedSession.Character.Attributes[RuntimeLegacyAttackHitAttribute]));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(10_000 - (int)ExpectedPlayerThunderDamage, visibleSobNpc.Life);
        Assert.Equal(10_000, visibleSobNpc.MaxLife);

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count > 0, log.ToString());
        var spell = Assert.Single(spells);
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.DoesNotContain(ParseDeathAttacks(sent), attack => attack.Query.OpponentUid == checked((uint)sobNpc.Id));
        Assert.Contains("WorldServer handled legacy player-SOB magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisibleTrainingSobNpcAppliesReceiveDamageModifiers()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_039,
            x: 84,
            y: 70,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyHookMoonCasterFlag, LegacyCleanSweepFlag),
                ["HookMoonPower"] = "140",
                ["CleanSweepPower"] = "25",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        var expectedModifiedDamage = (ExpectedPlayerThunderDamage * 140 / 100) +
            ((ExpectedPlayerThunderDamage * 140 / 100) * 25 / 100);
        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(10_000 - (int)expectedModifiedDamage, visibleSobNpc.Life);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(
            sobNpc.Id.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
        Assert.True(bool.Parse(updatedSession.Character.Attributes[RuntimeLegacyAttackHitAttribute]));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(expectedModifiedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Contains("WorldServer handled legacy player-SOB magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisibleTrainingSobNpcUpdatesAndPersistsWeaponProficiencyExperience()
    {
        var profsRoot = Path.Combine(
            Path.GetTempPath(),
            "WolfConquer-LegacyMagicSobWeaponProficiency-" + Guid.NewGuid().ToString("N"));
        try
        {
            var monsterFamily = CreateMonsterFamily();
            var sobNpc = CreateTrainingSobNpc();
            var worldData = CreateMagicCatalogWithSobNpcs(
                monsterFamily,
                [sobNpc],
                CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
            var startingProficiency = new PlayerProficiencySnapshotEntry(
                480,
                Level: 5,
                PreviousLevel: 0,
                Experience: 123,
                NeededExperience: LegacyProficiencyPacket.GetNeededExperience(5));
            var playerItems = new PlayerItemSnapshot(
                InventoryItems: [],
                RelicFuseItems: [],
                MythsoulBagItems: [],
                EquipmentItems:
                [
                    new PlayerItemSnapshotItem
                    {
                        ItemId = 480_003,
                        Durability = 100,
                        Position = 4,
                    },
                ],
                RuneItems: []);
            var session = CreateSession(
                mapId: 1_039,
                x: 84,
                y: 70,
                playerItems: playerItems) with
            {
                PlayerProficiencies = new PlayerProficiencySnapshot([startingProficiency]),
            };
            var expectedProficiency = LegacyProficiencyPacket.ApplyExperience(
                startingProficiency,
                checked(ExpectedPlayerThunderDamage * SourceDefaultExpRateProf)).Proficiency;
            var sessions = new WorldSessionStore();
            var sobNpcStates = new SobNpcRuntimeStateStore();
            var sent = new List<byte[]>();
            using var log = new StringWriter();
            var connection = CreateSendingConnection(sent);
            sessions.Set(connection, session);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates,
                PlayerProficienciesRoot: profsRoot);

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid,
                    OpponentUid: checked((uint)sobNpc.Id),
                    X: checked((ushort)sobNpc.X),
                    Y: checked((ushort)sobNpc.Y),
                    AttackType: MsgAttackType.Magic,
                    Damage: 0,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0,
                    SpellId: ThunderSpellId)).ToFrameBytes()));

            var packet = Assert.Single(ParseProficiencyPackets(sent));
            Assert.Equal(LegacyProficiencyPacket.FromSnapshot(expectedProficiency), packet);
            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal([expectedProficiency], updatedSession.PlayerProficiencies.Proficiencies);

            var persisted = new PlayerProficiencySnapshotLoader(profsRoot).LoadByCharacterUid(PlayerUid);
            Assert.Equal([expectedProficiency], persisted.Proficiencies);
            Assert.Equal(10_000 - (int)ExpectedPlayerThunderDamage, sobNpcStates.GetVisible(sobNpc).Life);
            Assert.Contains("legacy player-SOB magic weapon proficiency experience", log.ToString());
            Assert.Contains("WorldServer handled legacy player-SOB magic", log.ToString());
        }
        finally
        {
            if (Directory.Exists(profsRoot))
            {
                Directory.Delete(profsRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormAgainstVisibleMonsterAppliesThreePhysicalHits()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateWindstormRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellIds: [WindstormSpellId],
            legacyRandomSpell: WindstormSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false, WindstormSpellId)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count > 0, log.ToString());
        var spell = Assert.Single(spells);
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(WindstormSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellEffect);
        Assert.Equal(3, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(monster.Uid, target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        var totalDamage = spell.Targets.Aggregate(0UL, (total, target) => total + target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - (uint)totalDamage, damagedMonster.Hitpoints);
        Assert.Contains("WorldServer handled legacy PhysicalSpells monster magic", log.ToString());
        Assert.DoesNotContain("unsupported monster magic sort", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormAgainstGuardMonsterInPeaceModeSpendsResourcesThenSkipsDamage()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(
            guardFamily,
            [spawn],
            CreateWindstormRow(useMana: 7, useStamina: 5));
        var guard = CreateRuntimeMonsterInstance(worldData, spawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [WindstormSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "1",
            },
            legacyRandomSpell: WindstormSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(guard, legacyEncodedQuery: false, WindstormSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(WindstormSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleGuard));
        Assert.Equal(guard.MaxLife, visibleGuard.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("source CanAttackMonster.Verified rejected Guard monster family because pkMode=1", output);
        Assert.Contains("WorldServer handled legacy PhysicalSpells magic with no modeled target hits", output);
        Assert.DoesNotContain("WorldServer handled legacy PhysicalSpells monster magic", output);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormAgainstGuardMonsterInPkModeAppliesDamage()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(guardFamily, [spawn], CreateWindstormRow());
        var guard = CreateRuntimeMonsterInstance(worldData, spawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellIds: [WindstormSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            },
            legacyRandomSpell: WindstormSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(guard, legacyEncodedQuery: false, WindstormSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(WindstormSpellId, spell.SpellId);
        Assert.Equal(3, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(guard.Uid, target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
        });

        var totalDamage = spell.Targets.Aggregate(0UL, (total, target) => total + target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var damagedGuard));
        Assert.Equal(guard.MaxLife - (uint)totalDamage, damagedGuard.Hitpoints);
        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);
        Assert.Contains("WorldServer handled legacy PhysicalSpells monster magic", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalSpellsKylinSigilCyclesSourceDamageBonusAgainstMonster()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateKylinSigilRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [KylinSigilSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false, KylinSigilSpellId)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count > 0, log.ToString());
        var spell = Assert.Single(spells);
        Assert.Equal(KylinSigilSpellId, spell.SpellId);
        Assert.Equal(3, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(monster.Uid, target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
        });
        Assert.True(spell.Targets[1].Damage > spell.Targets[0].Damage);
        Assert.True(spell.Targets[2].Damage > spell.Targets[1].Damage);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("3", updatedSession.Character.Attributes["KylinSigil"]);

        var totalDamage = spell.Targets.Aggregate(0UL, (total, target) => total + target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - (uint)totalDamage, damagedMonster.Hitpoints);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsKylinSigilCyclesSourceDamageBonusAgainstVisiblePlayer()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateKylinSigilRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 301, spellIds: [KylinSigilSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: KylinSigilSpellId)).ToFrameBytes()));

        var expectedDamage = new uint[] { 225, 240, 263 };
        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(KylinSigilSpellId, spell.SpellId);
            Assert.Equal(expectedDamage, spell.Targets.Select(target => target.Damage));
            Assert.All(spell.Targets, target =>
            {
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.True(target.Hit);
            });
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("3", updatedCasterSession.Character.Attributes["KylinSigil"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - expectedDamage.Sum(damage => (long)damage), updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("WorldServer handled legacy PhysicalSpells player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalSpellsKylinSigilEnqueuesAllThreeZeroDamagePlayerHits()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateKylinSigilRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(spellIds: [KylinSigilSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: KylinSigilSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(3, spell.Targets.Count);
            Assert.All(spell.Targets, target =>
            {
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.Equal(0U, target.Damage);
                Assert.True(target.Hit);
            });
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("3", updatedCasterSession.Character.Attributes["KylinSigil"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("targets=3 totalDamage=0", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalSpellsAssassinRuneAutoHuntAddsSourceFlatDamageAgainstMonster()
    {
        const uint assassinRuneItemId = 4_010_301;

        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var baselineWorldData = CreateMagicCatalog(monsterFamily, [spawn], CreateWindstormRow());
        var bonusWorldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [spawn],
            [CreateItemType((int)assassinRuneItemId, "Assassin")],
            CreateWindstormRow());
        var baselineTargets = await RunPhysicalSpellMonsterTargetsAsync(
            baselineWorldData,
            monsterFamily,
            spawn,
            CreateSession(
                strength: 300,
                spellIds: [WindstormSpellId],
                attributes: new Dictionary<string, string> { ["OnAutoHunt"] = "1" },
                reborn: 2,
                legacyRandomSpell: WindstormSpellId));
        var bonusTargets = await RunPhysicalSpellMonsterTargetsAsync(
            bonusWorldData,
            monsterFamily,
            spawn,
            CreateSession(
                strength: 300,
                spellIds: [WindstormSpellId],
                attributes: new Dictionary<string, string> { ["OnAutoHunt"] = "1" },
                reborn: 2,
                playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
                {
                    ItemId = assassinRuneItemId,
                    Position = 101,
                }),
                legacyRandomSpell: WindstormSpellId));

        Assert.Equal(3, baselineTargets.Count);
        Assert.Equal(3, bonusTargets.Count);
        for (var index = 0; index < bonusTargets.Count; index++)
        {
            Assert.Equal(baselineTargets[index].Damage + 38_000UL, bonusTargets[index].Damage);
        }
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormAgainstLegacyOneDamageMonsterAppliesOneDamagePerHit()
    {
        var monsterFamily = CreateMonsterFamily() with { Name = "TrainingSprite" };
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584, sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateWindstormRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellIds: [WindstormSpellId],
            legacyRandomSpell: WindstormSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false, WindstormSpellId)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count > 0, log.ToString());
        var spell = Assert.Single(spells);
        Assert.Equal(WindstormSpellId, spell.SpellId);
        Assert.Equal(3, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(monster.Uid, target.Uid);
            Assert.Equal(1U, target.Damage);
            Assert.True(target.Hit);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - 3U, damagedMonster.Hitpoints);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormAgainstVisiblePlayerAppliesThreePhysicalHits()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateWindstormRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [WindstormSpellId],
            legacyRandomSpell: WindstormSpellId);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: WindstormSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(WindstormSpellId, spell.SpellId);
            Assert.Equal(3, spell.Targets.Count);
            Assert.All(spell.Targets, target =>
            {
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.True(target.Damage > 0);
                Assert.True(target.Hit);
            });
        }

        var totalDamage = casterSpell.Targets.Aggregate(0UL, (total, target) => total + target.Damage);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)totalDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("WorldServer handled legacy PhysicalSpells player magic", log.ToString());
        Assert.DoesNotContain("unsupported player magic sort", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormAgainstTrainingSobNpcAppliesThreePhysicalHits()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(monsterFamily, [sobNpc], CreateWindstormRow());
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_039,
            x: 84,
            y: 70,
            strength: 300,
            spellIds: [WindstormSpellId],
            legacyRandomSpell: WindstormSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: WindstormSpellId)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count > 0, log.ToString());
        var spell = Assert.Single(spells);
        Assert.Equal(WindstormSpellId, spell.SpellId);
        Assert.Equal(3, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
        });

        var totalDamage = spell.Targets.Aggregate(0UL, (total, target) => total + target.Damage);
        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(10_000 - (int)totalDamage, visibleSobNpc.Life);
        Assert.Contains("WorldServer handled legacy PhysicalSpells SOB magic", log.ToString());
        Assert.DoesNotContain("unsupported SOB magic sort", log.ToString());
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormSpendsResourcesOnceForThreeHits()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateWindstormRow(useMana: 7, useStamina: 5));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [WindstormSpellId],
            legacyRandomSpell: WindstormSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false, WindstormSpellId)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count > 0, log.ToString());
        var spell = Assert.Single(spells);
        Assert.Equal(3, spell.Targets.Count);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
    }

    [Fact]
    public async Task HandlerPhysicalSpellsWindstormChainsBloomOfDeathWhenLearned()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateWindstormRow(),
            CreateBloomOfDeathRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellIds: [WindstormSpellId, BloomOfDeathSpellId],
            legacyRandomSpell: WindstormSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false, WindstormSpellId)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.Equal(2, spells.Count);

        Assert.Equal(WindstormSpellId, spells[0].SpellId);
        Assert.Equal(3, spells[0].Targets.Count);
        Assert.Equal(BloomOfDeathSpellId, spells[1].SpellId);
        Assert.Equal(1, spells[1].SpellEffect);
        Assert.NotEmpty(spells[1].Targets);
        Assert.Contains(spells[1].Targets, target => target.Uid == monster.Uid && target.Damage > 0 && target.Hit);
        Assert.Contains("WorldServer handled legacy BloomOfDeath follow-up", log.ToString());
    }

    [Fact]
    public async Task HandlerLayTrapBloomOfDeathDirectHitsVisibleMonstersWithinCasterRadius()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584);
        var outsideRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 837, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [inRangeSpawn, outsideRangeSpawn],
            CreateBloomOfDeathRow());
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
        var outsideRangeMonster = CreateRuntimeMonsterInstance(worldData, outsideRangeSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [BloomOfDeathSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                inRangeMonster,
                legacyEncodedQuery: false,
                spellId: BloomOfDeathSpellId,
                x: inRangeMonster.X,
                y: inRangeMonster.Y)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(inRangeMonster.X, spell.X);
        Assert.Equal(inRangeMonster.Y, spell.Y);
        Assert.Equal(BloomOfDeathSpellId, spell.SpellId);
        Assert.Equal(1, spell.SpellEffect);

        var target = Assert.Single(spell.Targets);
        Assert.Equal(inRangeMonster.Uid, target.Uid);
        Assert.True(target.Hit);
        Assert.True(target.Damage > 0);

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < inRangeMonster.MaxLife);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideRangeMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideRangeMonster.MaxLife, untouchedMonster.Hitpoints);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy LayTrap magic", output);
        Assert.Contains("spellId=14720", output);
        Assert.DoesNotContain("unsupported LayTrap spell id", output);
        Assert.DoesNotContain("WorldServer skipped legacy LayTrap magic", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisibleTrainingSobNpcDeathResetsHitpointsAndSendsDeathUpdates()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 10_499, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_039, x: 84, y: 70, spirit: 9_500);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(10_000, visibleSobNpc.Life);
        Assert.Equal(10_000, visibleSobNpc.MaxLife);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(10_000U, target.Damage);
        Assert.True(target.Hit);

        var death = Assert.Single(ParseDeathAttacks(sent));
        Assert.Equal(PlayerUid, death.Query.Uid);
        Assert.Equal(checked((uint)sobNpc.Id), death.Query.OpponentUid);
        Assert.Equal(MsgAttackType.Death, death.Query.AttackType);

        Assert.Contains(
            ParseMsgUpdates(sent),
            update => update.Uid == checked((uint)sobNpc.Id) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 10_000UL));
        Assert.Contains(
            ParseMsgUpdates(sent),
            update => update.Uid == checked((uint)sobNpc.Id) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.MaxHitpoints &&
                    entry.Value == 10_000UL));
        Assert.Contains("WorldServer handled legacy player-SOB magic", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisibleEventSobNpcSendsSpellAnimationAndLogsSideEffectGap()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateEventSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_038, x: 84, y: 70);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(50_000_000 - (int)ExpectedPlayerThunderDamage, visibleSobNpc.Life);
        Assert.Equal(50_000_000, visibleSobNpc.MaxLife);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.DoesNotContain(ParseDeathAttacks(sent), attack => attack.Query.OpponentUid == checked((uint)sobNpc.Id));

        var output = log.ToString();
        Assert.Contains("PARITY-GAP category=magic", output);
        Assert.Contains("gap=sob-side-effects", output);
        Assert.Contains("WorldServer handled legacy player-SOB event damage with deferred side effects", output);
        Assert.Contains("WorldServer handled legacy player-SOB magic", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstDeadEventSobNpcSpendsManaThenSkipsAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateEventSobNpc() with { Life = 0 };
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: sobNpc.MapId, x: 84, y: 70);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Mana &&
                entry.Value == 493));

        var output = log.ToString();
        Assert.Contains("source CanAttackNpc.Verified rejected dead SOB NPC", output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstMap3844NeutralSobNpcSendsSpellAnimationAndMutatesRuntimeHitpoints()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateNeutralSobNpc() with
        {
            MapId = LegacySobNpcEventRules.CanAttackNpcAlwaysAllowedMapId,
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: sobNpc.MapId, x: 84, y: 70);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(50_000 - (int)ExpectedPlayerThunderDamage, visibleSobNpc.Life);
        Assert.Equal(50_000, visibleSobNpc.MaxLife);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Contains("WorldServer handled legacy player-SOB magic", log.ToString());
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarLeftGateUid,
        "LeftGate",
        1_008,
        LegacySobNpcEventRules.GuildWarLeftGateDefaultDemolitionMesh)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarRightGateUid,
        "RightGate",
        1_007,
        LegacySobNpcEventRules.GuildWarRightGateDefaultDemolitionMesh)]
    public async Task HandlerMagicAttackAgainstInactiveGuildWarGateDeathSendsDemolitionSobNpcUpdateWithoutNewSynWar(
        int sobNpcId,
        string sobNpcName,
        int lookface,
        int expectedDemolitionMesh)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateGuildWarGateSobNpc(sobNpcId, sobNpcName, lookface);
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 10_499, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var peerConnection = CreateSendingConnection(peerSent);
        var guildAttributes = new Dictionary<string, string>
        {
            ["GuildID"] = "77",
            ["GuildName"] = "SourceGuild",
            ["GuildRank"] = "100",
        };
        var casterSession = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 84,
            y: 70,
            spirit: 9_500,
            attributes: guildAttributes);
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 86,
            y: 70,
            attributes: guildAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(peerConnection, peerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates,
            GuildWarStates: new LegacyGuildWarRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Equal(expectedDemolitionMesh, visibleSobNpc.Lookface);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(peerSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
            Assert.Equal((uint)sobNpc.MaxLife, target.Damage);
            Assert.True(target.Hit);
        }

        foreach (var spawn in new[] { Assert.Single(ParseSobNpcSpawns(casterSent)), Assert.Single(ParseSobNpcSpawns(peerSent)) })
        {
            Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
            Assert.Equal((uint)sobNpc.MaxLife, spawn.Hitpoints);
            Assert.Equal((uint)sobNpc.MaxLife, spawn.MaxHitpoints);
            Assert.Equal(expectedDemolitionMesh, spawn.Mesh);
            Assert.Equal(LegacySobNpcEventRules.GateKind, spawn.Type);
        }

        Assert.DoesNotContain(ParseDeathAttacks(casterSent), attack => attack.Query.OpponentUid == checked((uint)sobNpc.Id));
        Assert.DoesNotContain(casterSent.Concat(peerSent), bytes => LegacyNewSynWarPacket.TryParse(ReadFrame(bytes), out _));

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacySobNpcSpawn GuildWar gate demolition", output);
        Assert.Contains("gap=sob-side-effects", output);
        Assert.Contains("WorldServer handled legacy player-SOB magic", output);
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarLeftGateUid,
        "LeftGate",
        1_008,
        LegacySobNpcEventRules.GuildWarLeftGateDefaultDemolitionMesh,
        LegacyNewSynWarPacket.LeftGateDestroyedDwParam)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarRightGateUid,
        "RightGate",
        1_007,
        LegacySobNpcEventRules.GuildWarRightGateDefaultDemolitionMesh,
        LegacyNewSynWarPacket.RightGateDestroyedDwParam)]
    public async Task HandlerMagicAttackAgainstActiveGuildWarGateDeathSendsNewSynWarWithSobOwnerAndDemolitionSobNpcUpdate(
        int sobNpcId,
        string sobNpcName,
        int lookface,
        int expectedDemolitionMesh,
        uint expectedDestroyedDwParam)
    {
        var clock = new ManualRuntimeTimeProvider(DateTimeOffset.UtcNow);
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateGuildWarGateSobNpc(sobNpcId, sobNpcName, lookface);
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 10_499, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var guildWarStates = new LegacyGuildWarRuntimeStateStore();
        const uint gateOwnerGuildId = 88;
        sobNpcStates.SetOwnerGuildId(sobNpc, gateOwnerGuildId);
        guildWarStates.Start();
        var casterSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var peerConnection = CreateSendingConnection(peerSent);
        var guildAttributes = new Dictionary<string, string>
        {
            ["GuildID"] = "77",
            ["GuildName"] = "SourceGuild",
            ["GuildRank"] = "100",
        };
        var casterSession = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 84,
            y: 70,
            spirit: 9_500,
            attributes: guildAttributes);
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 86,
            y: 70,
            attributes: guildAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(peerConnection, peerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates,
            GuildWarStates: guildWarStates,
            TimeProvider: clock);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Equal(expectedDemolitionMesh, visibleSobNpc.Lookface);
        Assert.Equal(gateOwnerGuildId, sobNpcStates.GetOwnerGuildId(sobNpc));

        foreach (var spawn in new[] { Assert.Single(ParseSobNpcSpawns(casterSent)), Assert.Single(ParseSobNpcSpawns(peerSent)) })
        {
            Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
            Assert.Equal((uint)sobNpc.MaxLife, spawn.Hitpoints);
            Assert.Equal((uint)sobNpc.MaxLife, spawn.MaxHitpoints);
            Assert.Equal(expectedDemolitionMesh, spawn.Mesh);
            Assert.Equal(LegacySobNpcEventRules.GateKind, spawn.Type);
        }

        foreach (var packet in new[] { Assert.Single(ParseNewSynWarPackets(casterSent)), Assert.Single(ParseNewSynWarPackets(peerSent)) })
        {
            Assert.Equal(LegacyNewSynWarPacket.DestroyedType, packet.Type);
            Assert.Equal(expectedDestroyedDwParam, packet.DwParam);
            Assert.Equal(gateOwnerGuildId, packet.SyndicateId);
            Assert.True(packet.IsDestroyed);
            Assert.False(packet.IsOwnershipChanged);
        }

        Assert.DoesNotContain(ParseDeathAttacks(casterSent), attack => attack.Query.OpponentUid == checked((uint)sobNpc.Id));

        // Existing row omits coolDown, so CreateMagicRow's native1000ms cadence must elapse.
        clock.Advance(TimeSpan.FromMilliseconds(1_000));

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.Equal(2, ParseSobNpcSpawns(casterSent).Count);
        Assert.Equal(2, ParseSobNpcSpawns(peerSent).Count);
        Assert.Single(ParseNewSynWarPackets(casterSent));
        Assert.Single(ParseNewSynWarPackets(peerSent));

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacySobNpcSpawn GuildWar gate demolition", output);
        Assert.Contains("WorldServer handled legacy player-SOB magic", output);
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarExorcimPillarUid,
        "ExorcimPillar")]
    [InlineData(
        LegacySobNpcEventRules.GuildWarTenacityPillarUid,
        "TenacityPillar")]
    public async Task HandlerMagicAttackAgainstInactiveGuildWarCapturePillarIsRejectedByNewGuildWarGate(
        int sobNpcId,
        string sobNpcName)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateGuildWarCapturePillarSobNpc(sobNpcId, sobNpcName);
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 10_499, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var peerConnection = CreateSendingConnection(peerSent);
        var guildAttributes = new Dictionary<string, string>
        {
            ["GuildID"] = "77",
            ["GuildName"] = "SourceGuild",
            ["GuildRank"] = "100",
        };
        var casterSession = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 84,
            y: 70,
            spirit: 9_500,
            attributes: guildAttributes);
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 86,
            y: 70,
            attributes: guildAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(peerConnection, peerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates,
                GuildWarStates: new LegacyGuildWarRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(0U, sobNpcStates.GetOwnerGuildId(sobNpc));
        Assert.Equal(sobNpc.Name, visibleSobNpc.Name);
        Assert.Equal(sobNpc.Life, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Equal(sobNpc.Lookface, visibleSobNpc.Lookface);
        Assert.Empty(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(peerSent));
        Assert.Empty(ParseSobNpcSpawns(casterSent));
        Assert.Empty(ParseSobNpcSpawns(peerSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.DoesNotContain(ParseDeathAttacks(casterSent), attack => attack.Query.OpponentUid == checked((uint)sobNpc.Id));
        Assert.DoesNotContain(casterSent.Concat(peerSent), bytes => LegacyNewSynWarPacket.TryParse(ReadFrame(bytes), out _));

        var output = log.ToString();
        Assert.Contains("source GUILDWAR.Verified rejected", output);
        Assert.Contains($"NEWGUILDWAR pillar uid={sobNpc.Id}", output);
        Assert.DoesNotContain("WorldServer sent LegacySobNpcSpawn GuildWar pillar capture", output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", output);
    }

    [Theory]
    [InlineData(
        LegacySobNpcEventRules.GuildWarExorcimPillarUid,
        "ExorcimPillar",
        LegacySobNpcEventRules.GuildWarExorcimOwnershipChangeDwParam)]
    [InlineData(
        LegacySobNpcEventRules.GuildWarTenacityPillarUid,
        "TenacityPillar",
        LegacySobNpcEventRules.GuildWarTenacityOwnershipChangeDwParam)]
    public async Task HandlerMagicAttackAgainstActiveGuildWarCapturePillarDeathSendsNewSynWarAndCaptureSobNpcUpdate(
        int sobNpcId,
        string sobNpcName,
        uint expectedOwnershipDwParam)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateGuildWarCapturePillarSobNpc(sobNpcId, sobNpcName);
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 10_499, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var guildWarStates = new LegacyGuildWarRuntimeStateStore();
        guildWarStates.Start();
        var casterSent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var peerConnection = CreateSendingConnection(peerSent);
        var guildAttributes = new Dictionary<string, string>
        {
            ["GuildID"] = "77",
            ["GuildName"] = "SourceGuild",
            ["GuildRank"] = "100",
        };
        var casterSession = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 84,
            y: 70,
            spirit: 9_500,
            attributes: guildAttributes);
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 86,
            y: 70,
            attributes: guildAttributes);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(peerConnection, peerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates,
            GuildWarStates: guildWarStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        var ownerGuildId = sobNpcStates.GetOwnerGuildId(sobNpc);
        Assert.Equal(77U, ownerGuildId);
        Assert.Equal("SourceGuild", visibleSobNpc.Name);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Equal(sobNpc.Lookface, visibleSobNpc.Lookface);

        foreach (var spawn in new[] { Assert.Single(ParseSobNpcSpawns(casterSent)), Assert.Single(ParseSobNpcSpawns(peerSent)) })
        {
            Assert.Equal(checked((uint)sobNpc.Id), spawn.Uid);
            Assert.Equal((uint)sobNpc.MaxLife, spawn.Hitpoints);
            Assert.Equal((uint)sobNpc.MaxLife, spawn.MaxHitpoints);
            Assert.Equal(sobNpc.Lookface, spawn.Mesh);
            Assert.Equal(LegacySobNpcEventRules.GuildWarCapturePillarKind, spawn.Type);
            Assert.Equal(17, spawn.Sort);
            Assert.Equal("SourceGuild", spawn.Name);
        }

        foreach (var packet in new[] { Assert.Single(ParseNewSynWarPackets(casterSent)), Assert.Single(ParseNewSynWarPackets(peerSent)) })
        {
            Assert.Equal(LegacyNewSynWarPacket.OwnershipChangedType, packet.Type);
            Assert.Equal(expectedOwnershipDwParam, packet.DwParam);
            Assert.Equal(ownerGuildId, packet.SyndicateId);
            Assert.True(packet.IsOwnershipChanged);
            Assert.False(packet.IsDestroyed);
        }

        Assert.DoesNotContain(ParseDeathAttacks(casterSent), attack => attack.Query.OpponentUid == checked((uint)sobNpc.Id));

        var output = log.ToString();
        Assert.Contains("WorldServer sent LegacySobNpcSpawn GuildWar pillar capture", output);
        Assert.Contains($"dwParam={expectedOwnershipDwParam}", output);
        Assert.Contains("WorldServer handled legacy player-SOB magic", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstNeutralSobNpcSpendsManaThenSkipsDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateNeutralSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: 1_038, x: 84, y: 70);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Mana &&
                entry.Value == 493));
        Assert.Contains(
            "SOB NPC is not a legacy damageable training or event object",
            log.ToString());
    }

    [Theory]
    [InlineData("1", false)]
    [InlineData("0", true)]
    public async Task HandlerMagicAttackAgainstStaticStatueRequiresPkMode(
        string pkMode,
        bool expectedDamage)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateStaticStatueSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcsAndStaticStatues(
            monsterFamily,
            [sobNpc],
            [CreateStaticStatueRecord(sobNpc)],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: sobNpc.MapId,
            x: sobNpc.X - 1,
            y: sobNpc.Y,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = pkMode,
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);

        var spells = ParseSpellAnimations(sent);
        var output = log.ToString();
        if (expectedDamage)
        {
            var spell = Assert.Single(spells);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.Equal(sobNpc.Life - (int)ExpectedPlayerThunderDamage, sobNpcStates.GetVisible(sobNpc).Life);
            Assert.Contains("WorldServer handled legacy player-SOB magic", output);
            Assert.DoesNotContain("rejected static statue", output);
        }
        else
        {
            Assert.Empty(spells);
            Assert.Equal(sobNpc.Life, sobNpcStates.GetVisible(sobNpc).Life);
            Assert.Contains(
                $"source CanAttackNpc.Verified rejected static statue because pkMode={pkMode}",
                output);
            Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", output);
        }
    }

    [Fact]
    public async Task HandlerMagicAttackUpdatesSpellExperienceAndSendsLegacySpellPacket()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, level: 0, experience: 100),
            CreateMagicRow(ThunderSpellId, sort: 1, level: 1, experience: 200));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellEntries:
        [
            new PlayerSpellSnapshotEntry(
                ThunderSpellId,
                Level: 0,
                Experience: 90,
                PreviousLevel: 0,
                SoulLevel: 2,
                UseSpellSoul: 1)
        ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        var spellUpdate = Assert.Single(
            sent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(ThunderSpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);
        Assert.Equal(0, spellUpdate.Experience);
        Assert.Equal(9U, spellUpdate.UseSoul);
        Assert.Equal(LegacySpellPacket.ComputeHashLevelSoul(2), spellUpdate.HashLevelSoul);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(ThunderSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);
        Assert.Equal(2, updatedSpell.SoulLevel);
        Assert.Equal(1, updatedSpell.UseSpellSoul);
        Assert.Contains("WorldServer updated legacy player magic spell", log.ToString());
        Assert.Contains("levelChanged=True", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackBlocksImmediateSecondCastDuringLegacyCooldown()
    {
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, coldTime: 60_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            TimeProvider: clock);
        var frame = ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes());

        await PacketHandlers.HandleAsync(context, frame);
        Assert.True(sessions.TryGet(connection, out var firstCastSession));
        Assert.NotNull(firstCastSession);
        Assert.NotNull(firstCastSession.MagicCooldowns);
        Assert.True(firstCastSession.MagicCooldowns.ContainsKey(ThunderSpellId));
        // Native MsgSpell.ColdTime is a silent field, not a ColdTime wire packet.
        Assert.Equal(clock.GetUtcNow().AddMilliseconds(60_000), firstCastSession.MagicCooldowns[ThunderSpellId]);
        Assert.Empty(ParseMagicColdTimePackets(sent));
        var frameCountAfterFirstCast = sent.Count;
        var spellAnimationCountAfterFirstCast = ParseSpellAnimations(sent).Count;

        await PacketHandlers.HandleAsync(context with { Session = firstCastSession }, frame);

        // ProcescMagic AttackStamp rejects the immediate input BEFORE CanUse costs.
        Assert.Equal(frameCountAfterFirstCast, sent.Count);
        Assert.True(sessions.TryGet(connection, out var immediateSession));
        Assert.NotNull(immediateSession);
        Assert.Equal(493, immediateSession.Character.MinMana);
        Assert.Equal(firstCastSession.MagicCooldowns[ThunderSpellId], immediateSession.MagicCooldowns![ThunderSpellId]);
        // Cadence equality cannot authorize a cost while the independently earned cold deadline is active.
        var beforeBlocked = System.Text.Json.JsonSerializer.Serialize(immediateSession);
        clock.Advance(TimeSpan.FromMilliseconds(1_000));
        await PacketHandlers.HandleAsync(context with { Session = immediateSession }, frame);

        Assert.Equal(frameCountAfterFirstCast, sent.Count);
        Assert.Equal(spellAnimationCountAfterFirstCast, ParseSpellAnimations(sent).Count);
        Assert.Empty(ParseMagicColdTimePackets(sent));
        Assert.True(sessions.TryGet(connection, out var blockedSession));
        Assert.NotNull(blockedSession);
        Assert.Same(immediateSession, blockedSession);
        Assert.Equal(beforeBlocked, System.Text.Json.JsonSerializer.Serialize(blockedSession));
        Assert.Equal(493, blockedSession.Character.MinMana);
        Assert.Equal(firstCastSession.MagicCooldowns[ThunderSpellId], blockedSession.MagicCooldowns![ThunderSpellId]);
        Assert.Contains("cooldownRemainingMs=", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackBypassesLegacyCooldownDuringCannonBarrage()
    {
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, coldTime: 60_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCannonBarrageFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            TimeProvider: clock);
        var frame = ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes());

        await PacketHandlers.HandleAsync(context, frame);
        Assert.True(sessions.TryGet(connection, out var firstCastSession));
        Assert.NotNull(firstCastSession);
        Assert.True(firstCastSession.MagicCooldowns is null ||
            !firstCastSession.MagicCooldowns.ContainsKey(ThunderSpellId));
        var frameCountAfterFirstCast = sent.Count;

        await PacketHandlers.HandleAsync(context with { Session = firstCastSession }, frame);

        // CannonBarrage bypasses CanUse ColdTime, NOT ProcescMagic AttackStamp.
        Assert.Equal(frameCountAfterFirstCast, sent.Count);
        Assert.True(sessions.TryGet(connection, out var immediateSession));
        Assert.NotNull(immediateSession);
        Assert.Equal(493, immediateSession.Character.MinMana);
        clock.Advance(TimeSpan.FromMilliseconds(1_000));
        await PacketHandlers.HandleAsync(context with { Session = immediateSession }, frame);

        Assert.True(sent.Count > frameCountAfterFirstCast);
        Assert.True(sessions.TryGet(connection, out var repeatedSession));
        Assert.NotNull(repeatedSession);
        Assert.Equal(486, repeatedSession.Character.MinMana);
        Assert.True(repeatedSession.MagicCooldowns is null ||
            !repeatedSession.MagicCooldowns.ContainsKey(ThunderSpellId));
        Assert.Empty(ParseMagicColdTimePackets(sent));
        Assert.DoesNotContain("cooldownRemainingMs=", log.ToString());
    }

    [Fact]
    public async Task HandlerAttachStatusSelfBuffSpendsResourcesSendsStatusAndAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                MagicShieldSpellId,
                sort: 6,
                useMana: 10,
                useStamina: 5,
                duration: 60,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [MagicShieldSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: MagicShieldSpellId,
                x: checked((ushort)session.Character.X),
                y: checked((ushort)session.Character.Y),
                opponentUid: PlayerUid)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyMagicShieldFlag));
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyMagicShieldExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: 60);
        Assert.Equal(bool.TrueString, updatedSession.Character.Attributes["LegacyMagicShieldRemoveOnDead"]);

        var parsedUpdates = sent
            .Select(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update)
                ? update
                : (LegacyMsgUpdatePacket?)null)
            .Where(update => update is not null)
            .Select(update => update!);
        var statusUpdate = Assert.Single(
            parsedUpdates,
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyMagicShieldFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(MagicShieldSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);

        Assert.Contains("WorldServer handled legacy classic AttachStatus buff", log.ToString());
        Assert.Contains("statusFlag=8", log.ToString());
    }

    [Fact]
    public async Task HandlerAttachStatusZeroOpponentSpendsResourcesWithoutSelfBuff()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                MagicShieldSpellId,
                sort: 6,
                useMana: 10,
                useStamina: 5,
                duration: 60,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [MagicShieldSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: MagicShieldSpellId,
                x: checked((ushort)session.Character.X),
                y: checked((ushort)session.Character.Y),
                opponentUid: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyMagicShieldFlag));

        var updates = ParseMsgUpdates(sent);
        Assert.Contains(
            updates,
            update => update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)]));
        Assert.Contains(
            updates,
            update => update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)]));
        Assert.DoesNotContain(
            updates,
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(MagicShieldSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy classic AttachStatus buff", output);
        Assert.Contains("targetUid=0", output);
    }

    [Fact]
    public async Task HandlerAttachStatusVisiblePlayerTargetSpendsCasterResourcesSendsStatusAndAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                MagicShieldSpellId,
                sort: 6,
                level: 0,
                useMana: 10,
                experience: 50,
                useStamina: 5,
                duration: 60,
                coldTime: 0),
            CreateMagicRow(
                MagicShieldSpellId,
                sort: 6,
                level: 1,
                useMana: 10,
                experience: 200,
                useStamina: 5,
                duration: 60,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    MagicShieldSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0)
            ]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: MagicShieldSpellId,
                x: checked((ushort)targetSession.Character.X),
                y: checked((ushort)targetSession.Character.Y),
                opponentUid: TargetPlayerUid)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyMagicShieldFlag));
        var updatedCasterSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(MagicShieldSpellId, updatedCasterSpell.Id);
        Assert.Equal(1, updatedCasterSpell.Level);
        Assert.Equal(0, updatedCasterSpell.Experience);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(500, updatedTargetSession.Character.MinMana);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyMagicShieldFlag));

        var casterStatusUpdate = Assert.Single(
            ParseMsgUpdates(casterSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var targetStatusUpdate = Assert.Single(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        foreach (var statusUpdate in new[] { casterStatusUpdate, targetStatusUpdate })
        {
            var statusEntry = Assert.Single(statusUpdate.Updates);
            Assert.NotNull(statusEntry.Values);
            Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyMagicShieldFlag));
        }

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(MagicShieldSpellId, spell.SpellId);
            Assert.Equal(checked((ushort)targetSession.Character.X), spell.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), spell.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
        }

        var spellUpdate = Assert.Single(
            casterSent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(MagicShieldSpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);
        Assert.DoesNotContain(
            targetSent,
            bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy classic AttachStatus buff", output);
        Assert.Contains("statusFlag=8", output);
        Assert.Contains("appliedExperience=60", output);
        Assert.DoesNotContain("only self-target status buffs", output);
    }

    [Fact]
    public async Task HandlerAttachStatusMissingVisibleTargetAdvancesProgressAndSendsEmptyAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                MagicShieldSpellId,
                sort: 6,
                useMana: 10,
                useStamina: 5,
                duration: 60,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [MagicShieldSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: MagicShieldSpellId,
                opponentUid: TargetPlayerUid)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyMagicShieldFlag));
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(MagicShieldSpellId, animation.SpellId);
        Assert.Empty(animation.Targets);
        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy classic AttachStatus buff", output);
        Assert.Contains("targetMatched=False", output);
        Assert.Contains("progressDamage=60", output);
    }

    [Fact]
    public async Task HandlerAttachStatusCycloneWithXpListConsumesXpListOpensCycloneAndSendsAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CycloneSpellId,
                sort: 6,
                useMana: 0,
                useStamina: 0,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [CycloneSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag),
                ["XPCount"] = "42",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: CycloneSpellId,
                x: checked((ushort)session.Character.X),
                y: checked((ushort)session.Character.Y),
                opponentUid: PlayerUid)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyCycloneFlag));
        Assert.Equal("0", updatedSession.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            RuntimeLegacyCycloneExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            expectedDurationSeconds: 20);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(CycloneSpellId));

        var updates = ParseMsgUpdates(sent);
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.Mana or LegacyMsgUpdateDataType.Stamina));
        var xpListRemovalIndex = Assert.Single(
            Enumerable.Range(0, updates.Count),
            index => updates[index].Uid == PlayerUid &&
                updates[index].Updates.Count == 1 &&
                updates[index].Updates[0].Type == LegacyMsgUpdateDataType.XPList);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)],
            updates[xpListRemovalIndex].Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.XPCircle).Updates);

        var statusUpdateIndex = Assert.Single(
            Enumerable.Range(0, updates.Count),
            index => updates[index].Uid == PlayerUid &&
                updates[index].Updates.Count == 1 &&
                updates[index].Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.True(xpListRemovalIndex < statusUpdateIndex);
        var statusEntry = Assert.Single(updates[statusUpdateIndex].Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyCycloneFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(CycloneSpellId, spell.SpellId);
        Assert.Equal(checked((ushort)session.Character.X), spell.X);
        Assert.Equal(checked((ushort)session.Character.Y), spell.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Cyclone XPList transition", output);
        Assert.Contains("consumedXpList=True", output);
        Assert.Contains("openedCyclone=True", output);
        Assert.Contains("expiresAtUtc=", output);
        Assert.DoesNotContain("unsupported attach status spell", output);
    }

    [Fact]
    public async Task HandlerAttachStatusSupermanWithXpListConsumesXpListOpensSupermanAndSendsAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                SupermanSpellId,
                sort: 6,
                useMana: 0,
                useStamina: 0,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [SupermanSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag),
                ["XPCount"] = "77",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: SupermanSpellId,
                x: checked((ushort)session.Character.X),
                y: checked((ushort)session.Character.Y),
                opponentUid: PlayerUid)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacySupermanFlag));
        Assert.Equal("0", updatedSession.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            RuntimeLegacySupermanExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            expectedDurationSeconds: 20);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(SupermanSpellId));

        var updates = ParseMsgUpdates(sent);
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.Mana or LegacyMsgUpdateDataType.Stamina));
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)]));
        var statusUpdate = Assert.Single(updates, update => update.Uid == PlayerUid &&
            update.Updates.Count == 1 &&
            update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacySupermanFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(SupermanSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy Superman XPList transition", output);
        Assert.Contains("consumedXpList=True", output);
        Assert.Contains("openedSuperman=True", output);
        Assert.DoesNotContain("unsupported attach status spell", output);
    }

    [Fact]
    public async Task HandlerAttachStatusSuperCycloneWithXpListFillsStaminaOpensFortySecondFlagAndSendsAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                SuperCycloneSpellId,
                sort: 6,
                useMana: 0,
                useStamina: 0,
                duration: 40,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 75,
            spellIds: [SuperCycloneSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag),
                ["XPCount"] = "88",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: SuperCycloneSpellId,
                x: checked((ushort)session.Character.X),
                y: checked((ushort)session.Character.Y),
                opponentUid: PlayerUid)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.Equal("100", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacySuperCycloneFlag));
        Assert.Equal("0", updatedSession.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            RuntimeLegacySuperCycloneExpiresAtUtcAttribute,
            beforeCast,
            afterCast,
            expectedDurationSeconds: 40);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(SuperCycloneSpellId));

        var updates = ParseMsgUpdates(sent);
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Mana));
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 100)]));
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)]));
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)]));
        var statusUpdate = Assert.Single(updates, update => update.Uid == PlayerUid &&
            update.Updates.Count == 1 &&
            update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacySuperCycloneFlag));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(SuperCycloneSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy SuperCyclone XPList transition", output);
        Assert.Contains("consumedXpList=True", output);
        Assert.Contains("openedSuperCyclone=True", output);
        Assert.DoesNotContain("unsupported attach status spell", output);
    }

    [Fact]
    public async Task HandlerAttachStatusCycloneWithoutXpListRunsSourceNoOpAfterResources()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CycloneSpellId,
                sort: 6,
                useMana: 10,
                useStamina: 5,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [CycloneSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: CycloneSpellId,
                x: checked((ushort)session.Character.X),
                y: checked((ushort)session.Character.Y),
                opponentUid: PlayerUid)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyCycloneFlag));
        Assert.False(updatedSession.Character.Attributes.ContainsKey(RuntimeLegacyCycloneExpiresAtUtcAttribute));

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.DoesNotContain(updates, update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or
                LegacyMsgUpdateDataType.XPCircle or
                LegacyMsgUpdateDataType.StatusFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped legacy Cyclone magic", output);
        Assert.Contains("XPList status flag is not active", output);
        Assert.DoesNotContain("unsupported attach status spell", output);
    }

    [Fact]
    public async Task HandlerAttachStatusUnsupportedSpellSpendsResourcesThenLogs()
    {
        const ushort unsupportedSpellId = 9_999;
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                unsupportedSpellId,
                sort: 6,
                useMana: 10,
                useStamina: 5));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [unsupportedSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: unsupportedSpellId,
                opponentUid: PlayerUid)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.Empty(ParseSpellAnimations(sent));
        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Contains("unsupported attach status spell", log.ToString());
    }

    [Fact]
    public async Task HandlerAttachStatusSelfBuffUpdatesSpellExperienceFromDuration()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                StarOfAccuracySpellId,
                sort: 6,
                level: 0,
                experience: 50,
                duration: 60,
                coldTime: 0),
            CreateMagicRow(
                StarOfAccuracySpellId,
                sort: 6,
                level: 1,
                experience: 200,
                duration: 60,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellEntries:
        [
            new PlayerSpellSnapshotEntry(
                StarOfAccuracySpellId,
                Level: 0,
                Experience: 0,
                PreviousLevel: 0,
                SoulLevel: 0,
                UseSpellSoul: 0)
        ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: StarOfAccuracySpellId,
                opponentUid: PlayerUid)).ToFrameBytes()));

        var spellUpdate = Assert.Single(
            sent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(StarOfAccuracySpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);
        Assert.Equal(0, spellUpdate.Experience);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(StarOfAccuracySpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyStarOfAccuracyFlag));
        Assert.Contains("appliedExperience=60", log.ToString());
    }

    [Fact]
    public async Task HandlerAddManaSelfRestoresManaSendsAnimationAndUpdatesSpellExperienceFromPower()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                AddManaSpellId,
                sort: 20,
                level: 0,
                useMana: 10,
                damage: 120,
                experience: 100,
                coldTime: 0),
            CreateMagicRow(
                AddManaSpellId,
                sort: 20,
                level: 1,
                useMana: 10,
                damage: 220,
                experience: 200,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 440,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    AddManaSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ]);
        session = session with { Character = session.Character with { MaxMana = 500 } };
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: checked((ushort)session.Character.X),
                Y: checked((ushort)session.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: AddManaSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.Equal(500, updatedSession.Character.MaxMana);

        var manaUpdates = ParseMsgUpdates(sent)
            .Where(update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana)
            .ToArray();
        Assert.Equal(
        [
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 430),
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 500),
        ],
        manaUpdates.SelectMany(update => update.Updates).ToArray());

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(AddManaSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(70U, target.Damage);
        Assert.True(target.Hit);

        var spellUpdate = Assert.Single(
            sent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(AddManaSpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);

        Assert.True(sessions.TryGet(connection, out updatedSession));
        Assert.NotNull(updatedSession);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(AddManaSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AddMana magic", output);
        Assert.Contains("restoredMana=70", output);
        Assert.Contains("appliedExperience=120", output);
    }

    [Fact]
    public async Task HandlerAddManaWithZeroOpponentUidSpendsManaWithoutRestoringSelf()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                AddManaSpellId,
                sort: 20,
                level: 0,
                useMana: 10,
                damage: 120,
                experience: 100,
                coldTime: 0),
            CreateMagicRow(
                AddManaSpellId,
                sort: 20,
                level: 1,
                useMana: 10,
                damage: 220,
                experience: 200,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 440,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    AddManaSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ]);
        session = session with { Character = session.Character with { MaxMana = 500 } };
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: checked((ushort)session.Character.X),
                Y: checked((ushort)session.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: AddManaSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(430, updatedSession.Character.MinMana);
        Assert.Equal(500, updatedSession.Character.MaxMana);

        var manaUpdates = ParseMsgUpdates(sent)
            .Where(update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana)
            .ToArray();
        Assert.Equal(
        [
            new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 430),
        ],
        manaUpdates.SelectMany(update => update.Updates).ToArray());

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(AddManaSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        var spellUpdate = Assert.Single(
            sent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(AddManaSpellId, spellUpdate.Id);
        Assert.Equal(1, spellUpdate.Level);

        Assert.True(sessions.TryGet(connection, out updatedSession));
        Assert.NotNull(updatedSession);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(AddManaSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AddMana magic", output);
        Assert.Contains("restoredMana=0", output);
        Assert.Contains("appliedExperience=120", output);
    }

    [Fact]
    public async Task HandlerHealingMissingVisibleTargetSpendsResourcesAndSendsEmptyAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                SpiritHealingSpellId,
                sort: 2,
                useMana: 10,
                useStamina: 5,
                damage: 120,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 500,
            stamina: 100,
            spellIds: [SpiritHealingSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)session.Character.X),
                Y: checked((ushort)session.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: SpiritHealingSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var updates = ParseMsgUpdates(sent);
        Assert.Contains(updates, update =>
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)]));
        Assert.Contains(updates, update =>
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)]));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(SpiritHealingSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy healing magic", output);
        Assert.Contains("targets=0 totalHealing=0", output);
    }

    [Fact]
    public async Task HandlerMagicAttackSkipsWhenCurrentManaIsTooLow()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(minMana: 3);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(3, updatedSession.Character.MinMana);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(ThunderSpellId));
        Assert.Contains("requiredMana=7", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackLowManaRejectsWithoutResourceCatalogOrOutputMutation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, useStamina: 5, coldTime: 60_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(minMana: 3, stamina: 10);
        sessions.Set(connection, session);
        Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
            sessions, connection, session.Character.Uid, out var identity));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var beforeMonster));
        var beforeSession = System.Text.Json.JsonSerializer.Serialize(session);
        var row = Assert.IsType<LegacyNativeMagicRow>(worldData.FindNativeMagicRow(ThunderSpellId, 0));
        var beforeCooldown = row.CoolDown;
        var beforeDamage = row.Damage;
        var beforeFields = row.Source.Fields.ToArray();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        Assert.Empty(sent);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(beforeMonster, visibleMonster);
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Same(session, updatedSession);
        Assert.Equal(beforeSession, System.Text.Json.JsonSerializer.Serialize(updatedSession));
        Assert.True(LegacyNativeClientPublication.TryCurrent(
            sessions, connection, session.Character.Uid, identity, out var current));
        Assert.Same(session, current);
        Assert.Equal(3, updatedSession.Character.MinMana);
        Assert.Equal("10", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(ThunderSpellId));
        Assert.Same(row, worldData.FindNativeMagicRow(ThunderSpellId, 0));
        Assert.Equal(beforeCooldown, row.CoolDown);
        Assert.Equal(beforeDamage, row.Damage);
        Assert.Equal(beforeFields, row.Source.Fields);
        Assert.Contains("requiredMana=7", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackSkipsWhenCurrentStaminaIsTooLow()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, useStamina: 5));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 3);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("3", updatedSession.Character.Attributes["Stamina"]);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(ThunderSpellId));
        Assert.Contains("requiredStamina=5", log.ToString());
    }

    [Fact]
    public async Task HandlerMagicAttackUsesMpMasterRuneManaReductionBeforeCheckingMana()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        const uint runeItemId = 4_036_109;
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [spawn],
            [CreateItemType((int)runeItemId, "MPMaster(+9)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 100, coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                ItemId = runeItemId,
                Position = 101,
            }));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession.Character.MinMana);
        Assert.Single(ParseSpellAnimations(sent));
        Assert.DoesNotContain("requiredMana=100", log.ToString());
        Assert.Contains("WorldServer handled legacy player magic", log.ToString());
    }

    [Fact]
    public async Task HandlerPlayerMagicCombatGearCatalogSpellDisplaysRequestedMonsterDamageWithoutLearnedSpell()
    {
        var monsterAttributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["level"] = "120",
        };
        var monsterFamily = CreateMonsterFamily() with { Attributes = monsterAttributes };
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(DragonhowlCombatGearSpellId, sort: 1, useMana: 7, useStamina: 5, coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var preDamage = monster.MaxLife - 50;
        var preHit = monsterStates.ApplyDamage(monster, preDamage, DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.False(preHit.Killed);
        Assert.Equal(50U, preHit.RemainingHitpoints);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 20,
            spellIds: [],
            classCode: 2_000) with
        {
            CombatGear = new CombatGearSnapshot(
                MasteryPoints: 0,
                TaoistReiki: 0,
                PirateExp: 0,
                Lottery: 0,
                Count: 1,
                CombatGearAirPowersSnapshot.Empty,
                Items:
                [
                    new CombatGearSnapshotItem(
                        DragonhowlCombatGearItemId,
                        Level: 0,
                        Exp: 0,
                        Equip: 1,
                        Transform: 0,
                        MasteryScore: 0,
                        Animas: [[], []],
                        BlackJades: []),
                ],
                CombatGearBlackJadeBagSnapshot.Empty),
        };
        var statusFields = LegacyPlayerStatusCalculator.CalculateFields(
            session.Character,
            session.PlayerItems,
            worldData,
            checked((uint)session.Character.MinHitPoints),
            prestigeLevel: 0);
        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, DragonhowlCombatGearSpellId, 0, out var runtimeSpell));
        var expectedDamage = LegacyMagicRuntime.CalculatePlayerMonsterDamageWithEffects(
            session,
            worldData,
            monsterFamily,
            runtimeSpell,
            statusFields).Damage;
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var query = CreateMagicQuery(
            monster,
            legacyEncodedQuery: true,
            DragonhowlCombatGearSpellId) with
        { Damage = 0 };
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(expectedDamage > preHit.RemainingHitpoints);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.False(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(43, updatedSession.Character.MinMana);
        Assert.Equal("15", updatedSession.Character.Attributes["Stamina"]);
    }

    [Fact]
    public async Task HandlerPlayerMagicWeaponProcSpellRequiresLegacyRandomSpell()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                WideStrikeSectorSpellId,
                sort: 1,
                useMana: 7,
                weaponType: 410_420,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(minMana: 50, spellIds: [WideStrikeSectorSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                WideStrikeSectorSpellId)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseMsgUpdates(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(50, updatedSession.Character.MinMana);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
    }

    [Fact]
    public async Task HandlerPlayerMagicWeaponProcSpellConsumesLegacyRandomSpell()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                WideStrikeSectorSpellId,
                sort: 1,
                useMana: 7,
                weaponType: 410_420,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            spellIds: [WideStrikeSectorSpellId],
            legacyRandomSpell: WideStrikeSectorSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                WideStrikeSectorSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(WideStrikeSectorSpellId, spell.SpellId);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < monster.MaxLife);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(43, updatedSession.Character.MinMana);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
    }

    [Fact]
    public async Task HandlerPhoenixAttackStatusWeaponProcConsumesRandomSpellAndHitsMonster()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                PhoenixSpellId,
                sort: 16,
                useMana: 7,
                damage: 120,
                range: 9,
                weaponType: 420_620,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            minMana: 50,
            spellIds: [PhoenixSpellId],
            legacyRandomSpell: PhoenixSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                PhoenixSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PhoenixSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(target.Hit);
        Assert.True(target.Damage > 0);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < monster.MaxLife);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(43, updatedSession.Character.MinMana);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.Contains("AttackStatus", log.ToString());
        Assert.DoesNotContain("unsupported monster magic sort", log.ToString());
    }

    [Fact]
    public async Task HandlerPhoenixAttackStatusAgainstWildPhoenixPlayerConsumesDefenderChargeAndScalesDamageToZero()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateMagicRow(
                    PhoenixSpellId,
                    sort: 16,
                    useMana: 7,
                    damage: 120,
                    range: 9,
                    weaponType: 420_620,
                    coldTime: 0),
                CreateMagicRow(
                    WildPhoenixSpellId,
                    sort: 6,
                    useMana: 0,
                    damage: 4,
                    coldTime: 0,
                    duration: 32,
                    damageOnHuman: 18,
                    damage2: 7),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            minMana: 50,
            spellIds: [PhoenixSpellId],
            legacyRandomSpell: PhoenixSpellId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 835,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var expectedPlayerDamage = 1U / 4;
        Assert.Equal(0U, expectedPlayerDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: PhoenixSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(checked((ushort)targetSession.Character.X), animation.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), animation.Y);
            Assert.Equal(PhoenixSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(43, updatedCasterSession.Character.MinMana);
        Assert.Equal(0, updatedCasterSession.LegacyRandomSpell);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy AttackStatus magic", output);
        Assert.Contains("handledTarget=True", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("totalDamage=0", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAddsScurvyBombSurchargeBeforeCheckingStamina()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, useStamina: 5, coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 10,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyScurvyBombFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false)).ToFrameBytes()));

        Assert.Empty(sent);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("10", updatedSession.Character.Attributes["Stamina"]);
        Assert.Contains("requiredStamina=11", log.ToString());
    }

    [Fact]
    public void LegacyMagicRuntimeParsesCooldownAndProgressionFieldsFromMagicRows()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                ThunderSpellId,
                sort: 1,
                experience: 1_250,
                needLevel: 40,
                weaponType: 410,
                coolDown: 800,
                coldTime: 1_400,
                damageOnHuman: 2_100,
                damage2: -2,
                damage3: 3_300,
                damageOnMonster: 4_400));

        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, ThunderSpellId, 0, out var spell));

        Assert.Equal(ThunderSpellId, spell.Id);
        Assert.Equal(1_250U, spell.Experience);
        Assert.Equal(40, spell.NeedLevel);
        Assert.Equal(410U, spell.WeaponType);
        Assert.Equal(800, spell.CoolDown);
        Assert.Equal(1_400, spell.ColdTime);
        Assert.Equal(2_100U, spell.DamageOnHuman);
        Assert.Equal(unchecked((uint)-2), spell.Damage2);
        Assert.Equal(3_300U, spell.Damage3);
        Assert.Equal(4_400U, spell.DamageOnMonster);
    }

    [Theory]
    [InlineData(LavaNutSpellId)]
    [InlineData(ThunderNutSpellId)]
    [InlineData(FrozenNutSpellId)]
    public void LegacyMagicRuntimeResolvesNutSummonRowsAsSourceSilentSortWithLegacyColdTime(ushort spellId)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                spellId,
                sort: 107,
                useMana: 0,
                damage: 0,
                maxTargets: 0,
                range: 0,
                coldTime: 10_000));

        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, spellId, 0, out var spell));

        Assert.Equal(spellId, spell.Id);
        Assert.Equal((LegacyMagicSort)107, spell.Sort);
        Assert.Equal(0, spell.UseMana);
        Assert.Equal(100U, spell.Damage);
        Assert.Equal(0, spell.MaxTargets);
        Assert.Equal(0, spell.Range);
        Assert.Equal(10_000, spell.ColdTime);
        Assert.Equal(LegacyMagicModelingStatus.SourceSilentNoOp, LegacyMagicSortCapabilities.Classify(spell).Status);
    }

    [Fact]
    public void LegacyMagicRuntimeCooldownHelperBlocksUntilColdTimeExpires()
    {
        var spell = new LegacyMagicRuntimeSpell(
            Id: ThunderSpellId,
            Sort: LegacyMagicSort.Attack,
            Name: "Thunder",
            Level: 0,
            UseMana: 7,
            Damage: 120,
            Experience: 100,
            MaxTargets: 0,
            Range: 9,
            NeedLevel: 1,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 100,
            ColdTime: 1_400);
        var now = DateTimeOffset.Parse("2026-06-10T00:00:00Z", CultureInfo.InvariantCulture);

        var started = LegacyMagicRuntime.TryStartLegacyCooldown(spell, now, currentReadyAtUtc: null);
        var blocked = LegacyMagicRuntime.TryStartLegacyCooldown(spell, now.AddMilliseconds(399), started.ReadyAtUtc);
        var ready = LegacyMagicRuntime.TryStartLegacyCooldown(spell, now.AddMilliseconds(1_400), started.ReadyAtUtc);

        Assert.True(started.Ready);
        Assert.Equal(now.AddMilliseconds(1_400), started.ReadyAtUtc);
        Assert.False(blocked.Ready);
        Assert.Equal(1_001, blocked.RemainingMilliseconds);
        Assert.True(ready.Ready);
        Assert.Equal(0, ready.RemainingMilliseconds);
        Assert.Equal(now.AddMilliseconds(2_800), ready.ReadyAtUtc);
    }

    [Fact]
    public async Task HandlerFastBladeAttackStampCadenceBlocksImmediateRepeatAndAllowsAfterSourceInterval()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                FastBladeSpellId,
                sort: 96,
                useMana: 0,
                damage: 30_115,
                maxTargets: 11,
                range: 11,
                weaponType: 614_410,
                useStamina: 15,
                coolDown: 600,
                coldTime: 300));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [FastBladeSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = ReadFrame(new MsgAttackPacket(CreateMagicQuery(
            monster,
            legacyEncodedQuery: false,
            spellId: FastBladeSpellId,
            x: 834,
            y: 584)).ToFrameBytes());

        await PacketHandlers.HandleAsync(context, query);

        Assert.Equal(2, ParseSpellAnimations(sent).Count);
        Assert.True(sessions.TryGet(connection, out var firstCastSession));
        Assert.NotNull(firstCastSession);
        Assert.Equal("85", firstCastSession.Character.Attributes["Stamina"]);
        Assert.True(firstCastSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));

        sent.Clear();
        await PacketHandlers.HandleAsync(context with { Session = firstCastSession }, query);

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var blockedSession));
        Assert.NotNull(blockedSession);
        Assert.Equal("85", blockedSession.Character.Attributes["Stamina"]);
        Assert.Contains("attackStampRemainingMs=", log.ToString());
        Assert.DoesNotContain("cooldownRemainingMs=", log.ToString());

        var agedAttributes = new Dictionary<string, string>(blockedSession.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeLegacyAttackStampUtcAttribute] = DateTimeOffset.UtcNow
                .AddMilliseconds(-600)
                .ToString("O", CultureInfo.InvariantCulture),
        };
        var agedCooldowns = blockedSession.MagicCooldowns is null
            ? new Dictionary<ushort, DateTimeOffset>()
            : new Dictionary<ushort, DateTimeOffset>(blockedSession.MagicCooldowns);
        agedCooldowns[FastBladeSpellId] = DateTimeOffset.UtcNow.AddMilliseconds(-1);
        var readySession = blockedSession with
        {
            Character = blockedSession.Character with { Attributes = agedAttributes },
            MagicCooldowns = agedCooldowns,
        };
        sessions.Set(connection, readySession);

        sent.Clear();
        await PacketHandlers.HandleAsync(context with { Session = readySession }, query);

        Assert.Equal(2, ParseSpellAnimations(sent).Count);
        Assert.True(sessions.TryGet(connection, out var thirdCastSession));
        Assert.NotNull(thirdCastSession);
        Assert.Equal("70", thirdCastSession.Character.Attributes["Stamina"]);
    }

    [Fact]
    public async Task HandlerHerculesAttackStampCadenceBlocksImmediateRepeatBeforeResources()
    {
        const ushort herculesSpellId = 1_115;

        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                herculesSpellId,
                sort: 5,
                useMana: 0,
                damage: 30_100,
                maxTargets: 8,
                range: 10,
                useStamina: 20,
                coolDown: 0,
                coldTime: 300));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [herculesSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = ReadFrame(new MsgAttackPacket(CreateMagicQuery(
            monster,
            legacyEncodedQuery: false,
            spellId: herculesSpellId,
            x: 832,
            y: 584)).ToFrameBytes());

        await PacketHandlers.HandleAsync(context, query);

        Assert.Single(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var firstCastSession));
        Assert.NotNull(firstCastSession);
        Assert.Equal("80", firstCastSession.Character.Attributes["Stamina"]);
        Assert.True(firstCastSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));

        sent.Clear();
        await PacketHandlers.HandleAsync(context with { Session = firstCastSession }, query);

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var blockedSession));
        Assert.NotNull(blockedSession);
        Assert.Equal("80", blockedSession.Character.Attributes["Stamina"]);
        Assert.Contains("attackStampRemainingMs=", log.ToString());
    }

    [Fact]
    public void LegacyMagicRuntimeSpellExperienceHelperLevelsAndBuildsSpellPacket()
    {
        var runtimeSpell = new LegacyMagicRuntimeSpell(
            Id: ThunderSpellId,
            Sort: LegacyMagicSort.Attack,
            Name: "Thunder",
            Level: 0,
            UseMana: 7,
            Damage: 120,
            Experience: 100,
            MaxTargets: 0,
            Range: 9,
            NeedLevel: 40,
            WeaponType: 0,
            UseStamina: 0,
            CoolDown: 100,
            ColdTime: 0);
        var playerSpell = new PlayerSpellSnapshotEntry(
            Id: ThunderSpellId,
            Level: 0,
            Experience: 90,
            PreviousLevel: 0,
            SoulLevel: 2,
            UseSpellSoul: 1);

        var result = LegacyMagicRuntime.ApplySpellExperience(
            playerSpell,
            runtimeSpell,
            damage: 11,
            experienceRate: 1,
            availableLevelCount: 2,
            playerLevel: 40);

        Assert.True(result.Updated);
        Assert.True(result.LevelChanged);
        Assert.Equal(11, result.AppliedExperience);
        Assert.Equal(1, result.Spell.Level);
        Assert.Equal(0, result.Spell.Experience);
        Assert.Equal(LegacySpellPacket.FromSnapshot(result.Spell), result.Packet);
        Assert.Equal(
        [
            0xE8, 0x03,
            0x01, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00,
            0x02,
            0x01,
            0x00
        ],
        LegacySpellPacket.ToLegacySnapshotRecordBytes(result.Spell));
    }

    [Fact]
    public async Task HandlerSectorMagicHitsVisibleMonstersInsideLegacyConeOnly()
    {
        var monsterFamily = CreateMonsterFamily();
        var eastSpawn = CreateMonsterSpawn(generatorId: 1, boundX: 832);
        var westSpawn = CreateMonsterSpawn(generatorId: 2, boundX: 828);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [eastSpawn, westSpawn],
            CreateMagicRow(WideStrikeSectorSpellId, sort: 4, range: 5));
        var eastMonsters = CreateRuntimeMonsterInstances(worldData, eastSpawn, monsterFamily);
        var westMonsters = CreateRuntimeMonsterInstances(worldData, westSpawn, monsterFamily);
        var eastMonster = eastMonsters.First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [WideStrikeSectorSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                eastMonster,
                legacyEncodedQuery: false,
                spellId: WideStrikeSectorSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(WideStrikeSectorSpellId, spell.SpellId);
        Assert.Equal(eastMonsters.Count, spell.Targets.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.Contains(eastMonsters, monster => monster.Uid == target.Uid);
            Assert.Equal(ExpectedLegacyPhysicalLineSectorDamage, target.Damage);
        });

        foreach (var monster in eastMonsters)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedEast));
            Assert.Equal(monster.MaxLife - ExpectedLegacyPhysicalLineSectorDamage, damagedEast.Hitpoints);
        }

        foreach (var monster in westMonsters)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var untouchedWest));
            Assert.Equal(monster.MaxLife, untouchedWest.Hitpoints);
        }
    }

    [Theory]
    [InlineData(FastBladeSpellId)]
    [InlineData(ScentSwordSpellId)]
    public async Task HandlerTrojanLineSkillDoublesMonsterAnimationDamageOnly(ushort spellId)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                spellId,
                sort: 96,
                useMana: 0,
                damage: 30_115,
                maxTargets: 11,
                range: 11,
                useStamina: 0,
                coolDown: 0,
                coldTime: 0));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [spellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: spellId,
                x: 834,
                y: 584)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        Assert.Equal(2, spells.Count);
        Assert.Equal(
            spells[0].Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            spells[1].Targets.Select(target => (target.Uid, target.Damage)).ToArray());
        var spell = spells[0];
        var target = Assert.Single(spell.Targets);
        Assert.Equal(spellId, spell.SpellId);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        var appliedDamage = monster.MaxLife - damagedMonster.Hitpoints;
        Assert.True(appliedDamage > 0);
        Assert.Equal(
            LegacyCombatDamageCalculator.AddLegacySaturating(appliedDamage, appliedDamage),
            target.Damage);
    }

    [Fact]
    public async Task HandlerFatalCrossHitsVisibleMonstersOnLegacyLineWithTriplePhysicalDamageAndUpdatesProgress()
    {
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-fatalcross-spell-xp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture),
            "PlayersSpells");
        try
        {
            var monsterFamily = CreateMonsterFamily();
            var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584);
            var nearLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 838, boundY: 585);
            var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838, boundY: 587);
            var beyondRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 844, boundY: 584);
            var worldData = CreateMagicCatalog(
                monsterFamily,
                [onLineSpawn, nearLineSpawn, outsideWidthSpawn, beyondRangeSpawn],
                CreateFatalCrossRow(experience: 100),
                CreateFatalCrossRow(level: 1, experience: 200));
            var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
            var nearLineMonster = CreateRuntimeMonsterInstance(worldData, nearLineSpawn, monsterFamily);
            var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
            var beyondRangeMonster = CreateRuntimeMonsterInstance(worldData, beyondRangeSpawn, monsterFamily);
            var allMonsters = new[] { onLineMonster, nearLineMonster, outsideWidthMonster, beyondRangeMonster };
            var monsterStates = new MonsterRuntimeStateStore();
            var sessions = new WorldSessionStore();
            var sent = new List<byte[]>();
            using var log = new StringWriter();
            var connection = CreateSendingConnection(sent);
            var session = CreateSession(strength: 300, spellIds: [FatalCrossSpellId]);
            sessions.Set(connection, session);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                PlayerSpellsRoot: spellsRoot);

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                    onLineMonster,
                    legacyEncodedQuery: false,
                    spellId: FatalCrossSpellId,
                    x: 842,
                    y: 584)).ToFrameBytes()));

            var spell = Assert.Single(ParseSpellAnimations(sent));
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(842, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(FatalCrossSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            Assert.Equal(0, spell.LevelHu);

            var expectedDamage = ExpectedLegacyPhysicalLineSectorDamage * 3;
            var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
            Assert.Equal(
                new[] { onLineMonster.Uid, nearLineMonster.Uid }.Order().ToArray(),
                damageByUid.Keys.Order().ToArray());
            Assert.All(spell.Targets, target =>
            {
                Assert.Equal(expectedDamage, target.Damage);
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });

            foreach (var monster in allMonsters)
            {
                Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
                if (damageByUid.TryGetValue(monster.Uid, out var damage))
                {
                    Assert.Equal(monster.MaxLife - damage, visibleMonster.Hitpoints);
                }
                else
                {
                    Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
                }
            }

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(490, updatedSession.Character.MinMana);
            Assert.True(updatedSession.Character.Experience > 0);
            var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
            Assert.Equal(1, updatedSpell.Level);
            Assert.Equal(0, updatedSpell.Experience);
        }
        finally
        {
            var tempRoot = Path.GetDirectoryName(spellsRoot);
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerFatalCrossHitsVisiblePlayersOnLegacyLineWithSourcePhysicalScale()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateFatalCrossRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, spellIds: [FatalCrossSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 838, y: 585);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: FatalCrossSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(FatalCrossSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
        }

        var appliedDamage = Assert.Single(casterSpell.Targets).Damage;
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)appliedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Contains("playerTargets=1", log.ToString());
    }

    [Fact]
    public async Task HandlerFatalCrossAgainstWildPhoenixPlayerConsumesDefenderChargeAndKeepsZeroDamageTarget()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateFatalCrossRow(),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, spellIds: [FatalCrossSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 838,
            y: 585,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var expectedPlayerDamage = 1U * 22 / 28;
        Assert.Equal(0U, expectedPlayerDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: FatalCrossSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(FatalCrossSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy FatalCross magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("totalAppliedDamage=0", output);
        Assert.Contains("source=FatalCross.cs", output);
    }

    [Fact]
    public async Task HandlerFatalCrossHitsVisibleSobNpcOnLegacyLineWithTripleAnimationDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(monsterFamily, [sobNpc], CreateFatalCrossRow());
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_039,
            x: 84,
            y: 70,
            strength: 300,
            spellIds: [FatalCrossSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: 97,
                Y: 70,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: FatalCrossSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(FatalCrossSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.True(target.Damage > 0);
        Assert.Equal(0U, target.Damage % 3);
        Assert.True(target.Hit);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(10_000 - (int)(target.Damage / 3), visibleSobNpc.Life);
        Assert.Contains("sobNpcTargets=1", log.ToString());
    }

    [Theory]
    [InlineData(90UL, false, 110, 100)]
    [InlineData(145UL, true, 165, 150)]
    public async Task HandlerFatalCrossSuperCycloneRestoresSourceStaminaBelowThreshold(
        ulong startingStamina,
        bool hasHeavenBlessing,
        ushort expectedStamina,
        ushort expectedThreshold)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateFatalCrossRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var statusFlags = hasHeavenBlessing
            ? BuildStatusFlags(LegacySuperCycloneFlag, LegacyHeavenBlessingFlag)
            : BuildStatusFlags(LegacySuperCycloneFlag);
        var attributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = statusFlags,
        };
        if (hasHeavenBlessing)
        {
            attributes["HeavenBlessing"] = "3600";
        }

        var session = CreateSession(
            strength: 300,
            stamina: startingStamina,
            spellIds: [FatalCrossSpellId],
            attributes: attributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: FatalCrossSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(expectedStamina.ToString(CultureInfo.InvariantCulture), updatedSession.Character.Attributes["Stamina"]);
        Assert.Contains($"threshold={expectedThreshold}", log.ToString());
        Assert.Contains($"restoredStamina={expectedStamina}", log.ToString());
        var staminaUpdate = Assert.Single(
            ParseMsgUpdates(sent),
            update => update.Updates.SequenceEqual(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, expectedStamina)]));
        Assert.Equal(PlayerUid, staminaUpdate.Uid);
        Assert.Single(ParseSpellAnimations(sent));
    }

    [Theory]
    [InlineData(843, true)]
    [InlineData(844, false)]
    public async Task HandlerFatalCrossAcceptsCastPointAtThirteenCellsOnly(
        ushort castX,
        bool expectsHit)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateFatalCrossRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [FatalCrossSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: FatalCrossSpellId,
                x: castX,
                y: 584)).ToFrameBytes()));

        var spells = ParseSpellAnimations(sent);
        if (expectsHit)
        {
            var spell = Assert.Single(spells);
            Assert.Equal(FatalCrossSpellId, spell.SpellId);
            Assert.Equal(castX, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(ExpectedLegacyPhysicalLineSectorDamage * 3, Assert.Single(spell.Targets).Damage);
            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(490, updatedSession.Character.MinMana);
        }
        else
        {
            Assert.Empty(spells);
            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(490, updatedSession.Character.MinMana);
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
            Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        }
    }

    [Fact]
    public async Task HandlerDirectAttackSpendsManaThenSkipsProtectedReviverPrimaryTarget()
    {
        var reviverFamily = CreateReviverMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: reviverFamily.Id,
            sourceMonsterName: reviverFamily.Name);
        var worldData = CreateMagicCatalog(
            reviverFamily,
            [spawn],
            CreateMagicRow(ThunderSpellId, sort: 48, useMana: 7, damage: 120, range: 9, coldTime: 0));
        var reviver = CreateRuntimeMonsterInstance(worldData, spawn, reviverFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [ThunderSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                reviver,
                legacyEncodedQuery: false,
                spellId: ThunderSpellId)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(reviver, DateTimeOffset.UtcNow, out var visibleReviver));
        Assert.Equal(reviver.MaxLife, visibleReviver.Hitpoints);

        var manaUpdate = Assert.Single(
            ParseMsgUpdates(sent),
            update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana);
        Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)], manaUpdate.Updates);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Contains("WorldServer skipped legacy player magic", log.ToString());
        Assert.Contains("no visible monsters matched shape", log.ToString());
    }

    [Fact]
    public async Task HandlerBombMagicFansOutAnimationInThirtyTargetChunksAndSpendsResourcesOnce()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 814, boundWidth: 33, maxPerGenerator: 33);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                LightningBombSpellId,
                sort: 5,
                useMana: 10,
                maxTargets: 18,
                range: 18,
                useStamina: 5));
        var monsters = CreateRuntimeMonsterInstances(worldData, spawn, monsterFamily);
        Assert.Equal(33, monsters.Count);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [LightningBombSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monsters[0],
                legacyEncodedQuery: false,
                spellId: LightningBombSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var spellFrames = ParseSpellAnimations(sent);
        Assert.Equal(2, spellFrames.Count);
        Assert.Equal(30, spellFrames[0].Targets.Count);
        Assert.Equal(3, spellFrames[1].Targets.Count);
        Assert.All(spellFrames, spell =>
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(830, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(LightningBombSpellId, spell.SpellId);
        });
        Assert.Equal(33, spellFrames.SelectMany(spell => spell.Targets).Select(target => target.Uid).Distinct().Count());

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.Contains("targets=33", log.ToString());
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombLevelZeroHitsThreeVisibleMonstersAndSpendsResourcesOnce()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 814, boundWidth: 33, maxPerGenerator: 33);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                range: 30,
                useStamina: 5));
        var monsters = CreateRuntimeMonsterInstances(worldData, spawn, monsterFamily);
        Assert.Equal(33, monsters.Count);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [GaleBombSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monsters[0],
                legacyEncodedQuery: false,
                spellId: GaleBombSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var spellFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => frame.Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.True(LegacySpellAnimationPacket.TryParse(spellFrame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(830, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(GaleBombSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        Assert.Equal(3, spell.Targets.Count);

        var monsterByUid = monsters.ToDictionary(monster => monster.Uid);
        var targetUids = spell.Targets.Select(target => target.Uid).ToHashSet();
        Assert.Equal(3, targetUids.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
            Assert.True(monsterByUid.ContainsKey(target.Uid));
        });

        foreach (var monster in monsters)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
            if (targetUids.Contains(monster.Uid))
            {
                Assert.True(visibleMonster.Hitpoints < monster.MaxLife);
            }
            else
            {
                Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
            }
        }

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy", output);
        Assert.Contains("BombPirte/GaleBomb", output);
        Assert.Contains("spellId=11070", output);
        Assert.Contains("targets=3", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombLevelZeroHitsVisiblePlayerWithHalfPhysicalDamageAndSpendsResourcesOnce()
    {
        const uint expectedDamage = 180;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [GaleBombSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        var casterSpellFrame = Assert.Single(
            casterSent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        var targetSpellFrame = Assert.Single(
            targetSent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        foreach (var frame in new[] { casterSpellFrame, targetSpellFrame })
        {
            Assert.Equal((ushort)2239, frame.Header.Opcode);
        }

        Assert.True(LegacySpellAnimationPacket.TryParse(casterSpellFrame, out var casterSpell));
        Assert.True(LegacySpellAnimationPacket.TryParse(targetSpellFrame, out var targetSpell));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(830, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(GaleBombSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)expectedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyScurvyBombFlag));
        Assert.Equal(832, updatedTargetSession.Character.X);
        Assert.Equal(589, updatedTargetSession.Character.Y);
        Assert.True(updatedTargetSession.Character.Attributes.TryGetValue(
            RuntimeLegacyProtectExpiresAtUtcAttribute,
            out var rawProtectExpiresAt));
        Assert.True(updatedTargetSession.Character.Attributes.TryGetValue(
            RuntimeLegacyAttackStampUtcAttribute,
            out var rawAttackStampUtc));
        Assert.Equal(rawProtectExpiresAt, rawAttackStampUtc);
        Assert.True(DateTimeOffset.TryParse(
            rawProtectExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var protectExpiresAt));
        Assert.True(protectExpiresAt >= beforeCast);
        Assert.True(protectExpiresAt <= beforeCast.AddSeconds(5));

        var casterUpdates = ParseMsgUpdates(casterSent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(casterUpdates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(casterUpdates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        Assert.Contains(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 5_000 - expectedDamage));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy", output);
        Assert.Contains("BombPirte/GaleBomb", output);
        Assert.Contains("spellId=11070", output);
        Assert.Contains("sort=60", output);
        Assert.Contains("targets=1", output);
        Assert.DoesNotContain("visiblePlayerTargets=", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombAgainstWildPhoenixPlayerConsumesDefenderChargeAndKeepsZeroDamageTarget()
    {
        const uint expectedDamage = 1U / 2;
        const long targetStartingHitpoints = 100_000;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [GaleBombSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 832,
            y: 584,
            minHitPoints: targetStartingHitpoints,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var beforeCast = DateTimeOffset.UtcNow;
        Assert.Equal(0U, expectedDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(830, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(GaleBombSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
            Assert.Equal(832, target.NewX);
            Assert.Equal(589, target.NewY);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(targetStartingHitpoints, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(832, updatedTargetSession.Character.X);
        Assert.Equal(589, updatedTargetSession.Character.Y);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));
        Assert.True(updatedTargetSession.Character.Attributes.TryGetValue(
            RuntimeLegacyProtectExpiresAtUtcAttribute,
            out var rawProtectExpiresAt));
        Assert.True(updatedTargetSession.Character.Attributes.TryGetValue(
            RuntimeLegacyAttackStampUtcAttribute,
            out var rawAttackStampUtc));
        Assert.Equal(rawProtectExpiresAt, rawAttackStampUtc);
        Assert.True(DateTimeOffset.TryParse(
            rawProtectExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var protectExpiresAt));
        Assert.True(protectExpiresAt >= beforeCast);
        Assert.True(protectExpiresAt <= beforeCast.AddSeconds(5));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy BombPirte/GaleBomb monster/player/SOB magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("totalDamage=0", output);
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombKeepsPlayerMoveAtOriginalPositionWhenFirstPushbackTileBlocked()
    {
        const uint expectedDamage = 180;
        const ushort targetX = 2;
        const ushort targetY = 2;
        const ushort blockedX = targetX + 1;
        const ushort blockedY = targetY;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var terrain = CreateGuildWarTerrainWithBlockedTile(
            targetX,
            targetY,
            blockedX,
            blockedY);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 1,
            y: targetY,
            strength: 300,
            stamina: 100,
            spellIds: [GaleBombSpellId],
            attributes: new Dictionary<string, string>
            {
                ["Angle"] = "6",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: targetX,
            y: targetY,
            attributes: new Dictionary<string, string>
            {
                ["CurrentTerrainMask"] = "3",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            Terrain: terrain,
            GuildWarStates: new LegacyGuildWarRuntimeStateStore());
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: targetX,
                Y: targetY,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        var spellAnimations = ParseSpellAnimations(casterSent).Concat(ParseSpellAnimations(targetSent)).ToArray();
        Assert.Equal(2, spellAnimations.Length);
        foreach (var spell in spellAnimations)
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(targetX, spell.X);
            Assert.Equal(targetY, spell.Y);
            Assert.Equal(GaleBombSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(targetX, target.NewX);
            Assert.Equal(targetY, target.NewY);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)expectedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(targetX, updatedTargetSession.Character.X);
        Assert.Equal(targetY, updatedTargetSession.Character.Y);
        Assert.True(updatedTargetSession.Character.Attributes.TryGetValue(
            RuntimeLegacyProtectExpiresAtUtcAttribute,
            out var rawProtectExpiresAt));
        Assert.True(updatedTargetSession.Character.Attributes.TryGetValue(
            RuntimeLegacyAttackStampUtcAttribute,
            out var rawAttackStampUtc));
        Assert.Equal(rawProtectExpiresAt, rawAttackStampUtc);
        Assert.True(DateTimeOffset.TryParse(
            rawProtectExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var protectExpiresAt));
        Assert.True(protectExpiresAt >= beforeCast);
        Assert.True(protectExpiresAt <= beforeCast.AddSeconds(5));

        Assert.DoesNotContain(
            ParseMsgActions(targetSent),
            action => action.Type == MsgActionType.SetLocation &&
                action.ObjId == TargetPlayerUid);
        Assert.Contains("BombPirte/GaleBomb", log.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(13)]
    public async Task HandlerBombPirteGaleBombGuildWarBlockedGateEndpointDamagesPlayerWithoutAnimationMoveOrProtection(
        int endpointSurface)
    {
        const uint expectedDamage = 180;
        const ushort targetX = 2;
        const ushort targetY = 2;
        const ushort pushedX = targetX + 5;
        const ushort pushedY = targetY;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var terrain = CreateGuildWarMoveLineTerrain(
            targetX,
            targetY,
            pushedX,
            pushedY,
            (ushort)endpointSurface);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: 1,
            y: targetY,
            strength: 300,
            stamina: 100,
            spellIds: [GaleBombSpellId],
            attributes: new Dictionary<string, string>
            {
                ["Angle"] = "6",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: targetX,
            y: targetY,
            attributes: new Dictionary<string, string>
            {
                ["CurrentTerrainMask"] = "3",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            Terrain: terrain,
            GuildWarStates: new LegacyGuildWarRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: targetX,
                Y: targetY,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        var spellAnimations = ParseSpellAnimations(casterSent).Concat(ParseSpellAnimations(targetSent)).ToArray();
        Assert.Equal(2, spellAnimations.Length);
        foreach (var spell in spellAnimations)
        {
            Assert.Equal(GaleBombSpellId, spell.SpellId);
            Assert.Empty(spell.Targets);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)expectedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(targetX, updatedTargetSession.Character.X);
        Assert.Equal(targetY, updatedTargetSession.Character.Y);
        Assert.False(updatedTargetSession.Character.Attributes.ContainsKey(RuntimeLegacyProtectExpiresAtUtcAttribute));
        Assert.False(updatedTargetSession.Character.Attributes.ContainsKey(RuntimeLegacyAttackStampUtcAttribute));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.Contains(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 5_000 - expectedDamage));
        var systemTalk = Assert.Single(
            ParseLegacyMessages(targetSent),
            message => message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                message.Content.Count > 3 &&
                message.Content[3] == "Illegal jumping over the gates detected.");
        Assert.Equal("SYSTEM", systemTalk.Content[0]);

        var pullback = Assert.Single(
            ParseMsgActions(targetSent),
            action => action.Type == MsgActionType.SetLocation &&
                action.ObjId == TargetPlayerUid);
        Assert.Equal((uint)LegacySobNpcEventRules.GuildWarMapId, pullback.DwParam);
        Assert.Equal(targetX, pullback.PositionX);
        Assert.Equal(targetY, pullback.PositionY);

        var output = log.ToString();
        Assert.Contains("BombPirte/GaleBomb", output);
        Assert.Contains("targets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("blocked legacy BombPirte/GaleBomb GuildWar gate crossing", output);
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombAcalanathaForcedSuccessBreaksPlayerLoopBeforeDamage()
    {
        const uint acalanathaRuneItemId = 4_037_101;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateItemType((int)acalanathaRuneItemId, "Acalanatha")],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var rollAttributes = new Dictionary<string, string>
        {
            ["LegacyBombPirteAcalanathaRoll"] = "54",
        };
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [GaleBombSpellId],
            attributes: rollAttributes);
        sessions.Set(casterConnection, casterSession);

        var targetConnections = new Dictionary<uint, ConquerTcpConnection>();
        var targetRuneItems = new Dictionary<uint, List<PlayerItemSnapshotItem>>();
        foreach (var (uid, name, x) in new (uint Uid, string Name, long X)[]
        {
            (TargetPlayerUid, "Protected", 831),
            (TargetPlayerUid + 1U, "LaterOne", 832),
            (TargetPlayerUid + 2U, "LaterTwo", 833),
        })
        {
            var targetSent = new List<byte[]>();
            var targetConnection = CreateSendingConnection(targetSent);
            var runeItems = new List<PlayerItemSnapshotItem>();
            targetConnections[uid] = targetConnection;
            targetRuneItems[uid] = runeItems;
            sessions.Set(
                targetConnection,
                CreateSession(
                    uid: uid,
                    name: name,
                    x: x,
                    y: 584,
                    spellIds: [GaleBombSpellId],
                    attributes: rollAttributes,
                    playerItems: CreatePlayerItems(runeItems)));
        }

        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(3, targetOrder.Count);
        var protectedUid = targetOrder[0];
        targetRuneItems[protectedUid].Add(new PlayerItemSnapshotItem
        {
            Uid = acalanathaRuneItemId,
            ItemId = acalanathaRuneItemId,
            Position = 101,
        });

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: protectedUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(casterSent).SelectMany(spell => spell.Targets));
        foreach (var targetUid in targetOrder)
        {
            Assert.True(sessions.TryGet(targetConnections[targetUid], out var updatedTargetSession));
            Assert.NotNull(updatedTargetSession);
            Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        }
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombNatureShieldForcedSuccessSkipsOnlyProtectedPlayer()
    {
        const uint expectedDamage = 180;
        const uint natureShieldRuneItemId = 4_038_109;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateItemType((int)natureShieldRuneItemId, "NatureShield(+9)")],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var rollAttributes = new Dictionary<string, string>
        {
            ["LegacyBombPirteNatureShieldRoll"] = "69",
        };
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [GaleBombSpellId],
            attributes: rollAttributes);
        sessions.Set(casterConnection, casterSession);

        var targetConnections = new Dictionary<uint, ConquerTcpConnection>();
        var targetRuneItems = new Dictionary<uint, List<PlayerItemSnapshotItem>>();
        foreach (var (uid, name, x) in new (uint Uid, string Name, long X)[]
        {
            (TargetPlayerUid, "Protected", 831),
            (TargetPlayerUid + 1U, "LaterOne", 832),
            (TargetPlayerUid + 2U, "LaterTwo", 833),
        })
        {
            var targetSent = new List<byte[]>();
            var targetConnection = CreateSendingConnection(targetSent);
            var runeItems = new List<PlayerItemSnapshotItem>();
            targetConnections[uid] = targetConnection;
            targetRuneItems[uid] = runeItems;
            sessions.Set(
                targetConnection,
                CreateSession(
                    uid: uid,
                    name: name,
                    x: x,
                    y: 584,
                    spellIds: [GaleBombSpellId],
                    attributes: rollAttributes,
                    playerItems: CreatePlayerItems(runeItems)));
        }

        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(3, targetOrder.Count);
        var protectedUid = targetOrder[0];
        var nextEligibleUid = targetOrder[1];
        targetRuneItems[protectedUid].Add(new PlayerItemSnapshotItem
        {
            Uid = natureShieldRuneItemId,
            ItemId = natureShieldRuneItemId,
            Position = 101,
        });

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: protectedUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        var casterTargets = ParseSpellAnimations(casterSent).SelectMany(spell => spell.Targets).ToArray();
        Assert.DoesNotContain(casterTargets, target => target.Uid == protectedUid);
        var nextTarget = Assert.Single(casterTargets, target => target.Uid == nextEligibleUid);
        Assert.Equal(expectedDamage, nextTarget.Damage);
        Assert.True(nextTarget.Hit);

        Assert.True(sessions.TryGet(targetConnections[protectedUid], out var protectedSession));
        Assert.NotNull(protectedSession);
        Assert.Equal(5_000, protectedSession.Character.MinHitPoints);

        Assert.True(sessions.TryGet(targetConnections[nextEligibleUid], out var damagedSession));
        Assert.NotNull(damagedSession);
        Assert.Equal(5_000 - (long)expectedDamage, damagedSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombNatureShieldRollAtRateDoesNotAvoidPlayerDamage()
    {
        const uint expectedDamage = 180;
        const uint natureShieldRuneItemId = 4_038_109;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateItemType((int)natureShieldRuneItemId, "NatureShield(+9)")],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var rollAttributes = new Dictionary<string, string>
        {
            ["LegacyBombPirteNatureShieldRoll"] = "70",
        };
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [GaleBombSpellId],
            attributes: rollAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            spellIds: [GaleBombSpellId],
            attributes: rollAttributes,
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = natureShieldRuneItemId,
                ItemId = natureShieldRuneItemId,
                Position = 101,
            }));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var target = Assert.Single(casterSpell.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)expectedDamage, updatedTargetSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombLevelZeroHitsVisibleDamageableSobNpcUpdatesRuntimeAndSpellExperience()
    {
        const uint expectedDamage = 3_600;
        const uint expectedExperienceDamage = expectedDamage / 1_000;
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                level: 0,
                useMana: 10,
                damage: 120,
                experience: 10_000,
                range: 30,
                useStamina: 5),
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                level: 1,
                useMana: 10,
                damage: 120,
                experience: 20_000,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: sobNpc.MapId,
            x: 84,
            y: 70,
            strength: 3_000,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    GaleBombSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0)
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        var spellFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => frame.Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.Equal((ushort)2239, spellFrame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(spellFrame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(sobNpc.X, spell.X);
        Assert.Equal(sobNpc.Y, spell.Y);
        Assert.Equal(GaleBombSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(10_000 - (int)expectedDamage, visibleSobNpc.Life);
        Assert.Equal(10_000, visibleSobNpc.MaxLife);

        var spellUpdate = Assert.Single(
            sent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(GaleBombSpellId, spellUpdate.Id);
        Assert.Equal(0, spellUpdate.Level);
        Assert.Equal(expectedExperienceDamage, (uint)spellUpdate.Experience);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(GaleBombSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);
        Assert.Equal(expectedExperienceDamage, (uint)updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy", output);
        Assert.Contains("BombPirte/GaleBomb", output);
        Assert.Contains("spellId=11070", output);
        Assert.Contains("sort=60", output);
        Assert.Contains("targets=1", output);
        Assert.Contains($"totalDamage={expectedExperienceDamage}", output);
        Assert.Contains("WorldServer updated legacy player magic spell", output);
        Assert.DoesNotContain("visibleSobNpcTargets=", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    [Fact]
    public async Task HandlerBombPirteGaleBombLevelZeroUsesMonsterPlayerSobNpcTargetBudgetOrder()
    {
        const uint expectedPlayerDamage = 180;
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 830, boundY: 584, boundWidth: 2, maxPerGenerator: 2);
        var sobNpc = CreateMoveLineSobNpc(id: 65_070, x: 836, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [
                CreateMagicRow(
                    GaleBombSpellId,
                    sort: 60,
                    useMana: 10,
                    damage: 120,
                    range: 30,
                    useStamina: 5)
            ]);
        var monsters = CreateRuntimeMonsterInstances(worldData, spawn, monsterFamily);
        Assert.Equal(2, monsters.Count);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [GaleBombSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 835, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monsters[0],
                legacyEncodedQuery: false,
                spellId: GaleBombSpellId,
                x: 830,
                y: 584,
                opponentUid: TargetPlayerUid)).ToFrameBytes()));

        var casterSpellFrame = Assert.Single(
            casterSent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        var targetSpellFrame = Assert.Single(
            targetSent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        Assert.True(LegacySpellAnimationPacket.TryParse(casterSpellFrame, out var casterSpell));
        Assert.True(LegacySpellAnimationPacket.TryParse(targetSpellFrame, out var targetSpell));

        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(GaleBombSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            Assert.Equal(3, spell.Targets.Count);
            Assert.Equal(
                monsters.Select(monster => monster.Uid)
                    .Concat([TargetPlayerUid])
                    .Order()
                    .ToArray(),
                spell.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.DoesNotContain(spell.Targets, target => target.Uid == checked((uint)sobNpc.Id));
            Assert.All(spell.Targets, target => Assert.True(target.Hit));
        }

        var playerTarget = Assert.Single(casterSpell.Targets, target => target.Uid == TargetPlayerUid);
        Assert.Equal(expectedPlayerDamage, playerTarget.Damage);
        foreach (var monster in monsters)
        {
            var monsterTarget = Assert.Single(casterSpell.Targets, target => target.Uid == monster.Uid);
            Assert.True(monsterTarget.Damage > 0);
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
            Assert.Equal(monster.MaxLife - monsterTarget.Damage, visibleMonster.Hitpoints);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        var updates = ParseMsgUpdates(casterSent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var output = log.ToString();
        Assert.Contains("BombPirte/GaleBomb", output);
        Assert.Contains("targets=3", output);
        Assert.DoesNotContain("visiblePlayerTargets=", output);
        Assert.DoesNotContain("visibleSobNpcTargets=", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
    }

    [Theory]
    [InlineData(1_038)]
    [InlineData(3_868)]
    public async Task HandlerBombPirteGaleBombSobNpcBranchReturnsBeforeDamageOnLegacyGuardMaps(int blockedMapId)
    {
        var monsterFamily = CreateMonsterFamily();
        var blockedMapIdText = blockedMapId.ToString(CultureInfo.InvariantCulture);
        var sobNpc = CreateEventSobNpc() with
        {
            MapId = blockedMapId,
            SourceLine = blockedMapId,
            RawLine = $"65005,GuildPole,10,1137,{blockedMapIdText},85,70,50000000,50000000,17,0",
            Fields = ["65005", "GuildPole", "10", "1137", blockedMapIdText, "85", "70", "50000000", "50000000", "17", "0"],
        };
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(
                GaleBombSpellId,
                sort: 60,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: blockedMapId,
            x: 84,
            y: 70,
            strength: 3_000,
            stamina: 100,
            spellIds: [GaleBombSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: GaleBombSpellId)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(sent, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var output = log.ToString();
        Assert.DoesNotContain("visibleSobNpcTargets=", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    [Fact]
    public async Task HandlerCannonBarrageWithStatusFlagHitsVisibleMonstersInsideRadiusAndSpendsResourcesOnce()
    {
        var monsterFamily = CreateMonsterFamily();
        var centerSpawn = CreateMonsterSpawn(generatorId: 1, boundX: 830, boundY: 584);
        var edgeSpawn = CreateMonsterSpawn(generatorId: 2, boundX: 836, boundY: 584);
        var outsideSpawn = CreateMonsterSpawn(generatorId: 3, boundX: 837, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [centerSpawn, edgeSpawn, outsideSpawn],
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                useMana: 10,
                range: 30,
                useStamina: 5));
        var centerMonsters = CreateRuntimeMonsterInstances(worldData, centerSpawn, monsterFamily);
        var edgeMonsters = CreateRuntimeMonsterInstances(worldData, edgeSpawn, monsterFamily);
        var outsideMonsters = CreateRuntimeMonsterInstances(worldData, outsideSpawn, monsterFamily);
        var centerMonster = centerMonsters.First();
        var monsters = centerMonsters
            .Concat(edgeMonsters)
            .Concat(outsideMonsters)
            .ToArray();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [CannonBarrageSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCannonBarrageFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                centerMonster,
                legacyEncodedQuery: false,
                spellId: CannonBarrageSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var spellFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => frame.Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.Equal((ushort)2239, spellFrame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(spellFrame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(830, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(CannonBarrageSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);

        var expectedTargetUids = monsters
            .Where(monster => Math.Abs(monster.X - 830) <= 6 && Math.Abs(monster.Y - 584) <= 6)
            .Select(monster => monster.Uid)
            .Order()
            .ToArray();
        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(expectedTargetUids, damageByUid.Keys.Order().ToArray());
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        foreach (var monster in monsters)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
            if (damageByUid.TryGetValue(monster.Uid, out var damage))
            {
                Assert.Equal(monster.MaxLife - damage, visibleMonster.Hitpoints);
            }
            else
            {
                Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
            }
        }

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(CannonBarrageSpellId));

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy", output);
        Assert.Contains("CannonBarrage", output);
        Assert.Contains("spellId=11050", output);
        Assert.Contains("sort=57", output);
        Assert.Contains($"targets={expectedTargetUids.Length}", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
    }

    [Fact]
    public async Task HandlerCannonBarrageWithStatusFlagHitsVisiblePlayerInsideRadiusAndSpendsResourcesOnce()
    {
        const uint expectedDamage = 32;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [CannonBarrageSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCannonBarrageFlag),
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 836, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CannonBarrageSpellId)).ToFrameBytes()));

        var casterSpellFrame = Assert.Single(
            casterSent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        var targetSpellFrame = Assert.Single(
            targetSent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        foreach (var frame in new[] { casterSpellFrame, targetSpellFrame })
        {
            Assert.Equal((ushort)2239, frame.Header.Opcode);
        }

        Assert.True(LegacySpellAnimationPacket.TryParse(casterSpellFrame, out var casterSpell));
        Assert.True(LegacySpellAnimationPacket.TryParse(targetSpellFrame, out var targetSpell));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(830, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(CannonBarrageSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.True(updatedCasterSession.MagicCooldowns is null ||
            !updatedCasterSession.MagicCooldowns.ContainsKey(CannonBarrageSpellId));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - expectedDamage, updatedTargetSession.Character.MinHitPoints);

        var casterUpdates = ParseMsgUpdates(casterSent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(casterUpdates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(casterUpdates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        Assert.Contains(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 5_000 - expectedDamage));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy", output);
        Assert.Contains("CannonBarrage", output);
        Assert.Contains("spellId=11050", output);
        Assert.Contains("sort=57", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.DoesNotContain("CannonBarrage monster damage is modeled, but player /11 damage", output);
        Assert.DoesNotContain("visiblePlayerTargets=", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
    }

    [Fact]
    public async Task HandlerCannonBarrageAgainstWildPhoenixPlayerConsumesDefenderChargeAndScalesDamageToZero()
    {
        const uint expectedDamage = 1U / 11;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [CannonBarrageSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCannonBarrageFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 836,
            y: 584,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CannonBarrageSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(830, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(CannonBarrageSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy CannonBarrage monster/player/SOB magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("playerTargets=1", output);
    }

    [Fact]
    public async Task HandlerCannonBarrageWithStatusFlagHitsVisibleDamageableSobNpcInsideRadiusAndSpendsResourcesOnce()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateEventSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                level: 0,
                useMana: 10,
                damage: 120,
                experience: 10_000,
                range: 30,
                useStamina: 5,
                coldTime: 60_000),
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                level: 1,
                useMana: 10,
                damage: 120,
                experience: 20_000,
                range: 30,
                useStamina: 5,
                coldTime: 60_000));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: sobNpc.MapId,
            x: 84,
            y: 70,
            strength: 3_000,
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyCannonBarrageFlag),
            },
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    CannonBarrageSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0)
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        const ushort attackX = 79;
        const ushort attackY = 70;
        Assert.True(Math.Abs(sobNpc.X - attackX) <= 6);
        Assert.Equal(attackY, sobNpc.Y);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: attackX,
                Y: attackY,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CannonBarrageSpellId)).ToFrameBytes()));

        var spellFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => frame.Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.Equal((ushort)2239, spellFrame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(spellFrame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(attackX, spell.X);
        Assert.Equal(attackY, spell.Y);
        Assert.Equal(CannonBarrageSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.True(target.Damage >= 1_000);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - (int)target.Damage, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        var expectedExperienceDamage = target.Damage / 1_000;
        Assert.True(expectedExperienceDamage > 0);
        var spellUpdate = Assert.Single(
            sent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(CannonBarrageSpellId, spellUpdate.Id);
        Assert.Equal(0, spellUpdate.Level);
        Assert.Equal(expectedExperienceDamage, (uint)spellUpdate.Experience);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(CannonBarrageSpellId));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(CannonBarrageSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);
        Assert.Equal(expectedExperienceDamage, (uint)updatedSpell.Experience);

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CannonBarrage monster/player/SOB magic", output);
        Assert.Contains("spellId=11050", output);
        Assert.Contains("sort=57", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"totalDamage={expectedExperienceDamage}", output);
        Assert.Contains("WorldServer updated legacy player magic spell", output);
        Assert.Contains($"appliedExperience={expectedExperienceDamage}", output);
        Assert.DoesNotContain(
            "CannonBarrage monster and player /11 damage are modeled, but SOB runtime state is not loaded",
            output);
        Assert.DoesNotContain("visibleSobNpcTargets=", output);
        Assert.DoesNotContain("cooldownRemainingMs=", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    [Fact]
    public async Task HandlerCannonBarrageWithXpListAndNoStatusFlagOpensCannonBarrageConsumesXpListAndSkipsDamageAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 830, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                useMana: 10,
                range: 30,
                useStamina: 5,
                coldTime: 60_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [CannonBarrageSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: CannonBarrageSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyCannonBarrageFlag));
        Assert.Equal("0", updatedSession.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            RuntimeLegacyCannonBarrageExpiresAtUtcAttribute,
            beforeCast,
            afterCast);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(CannonBarrageSpellId));

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var xpListRemovalIndex = Assert.Single(
            Enumerable.Range(0, updates.Count),
            index => updates[index].Uid == PlayerUid &&
                updates[index].Updates.Count == 1 &&
                updates[index].Updates[0].Type == LegacyMsgUpdateDataType.XPList);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)],
            updates[xpListRemovalIndex].Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.XPCircle).Updates);

        var statusUpdateIndex = Assert.Single(
            Enumerable.Range(0, updates.Count),
            index => updates[index].Uid == PlayerUid &&
                updates[index].Updates.Count == 1 &&
                updates[index].Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.True(xpListRemovalIndex < statusUpdateIndex);
        var statusEntry = Assert.Single(updates[statusUpdateIndex].Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyCannonBarrageFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CannonBarrage XPList transition", output);
        Assert.Contains("consumedXpList=True", output);
        Assert.Contains("openedCannonBarrage=True", output);
        Assert.Contains("expiresAtUtc=", output);
        Assert.DoesNotContain("WorldServer handled legacy CannonBarrage monster/player/SOB magic", output);
    }

    [Fact]
    public async Task HandlerCannonBarrageWithXpListAndStatusFlagRefreshesCannonBarrageConsumesXpListAndSkipsDamageAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 830, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                useMana: 10,
                range: 30,
                useStamina: 5,
                coldTime: 60_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var oldExpiresAt = DateTimeOffset.UtcNow.AddSeconds(5);
        var session = CreateSession(
            stamina: 100,
            spellIds: [CannonBarrageSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag, LegacyCannonBarrageFlag),
                ["XPCount"] = "37",
                [RuntimeLegacyCannonBarrageExpiresAtUtcAttribute] =
                    oldExpiresAt.ToString("O", CultureInfo.InvariantCulture),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: CannonBarrageSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyCannonBarrageFlag));
        Assert.Equal("0", updatedSession.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            RuntimeLegacyCannonBarrageExpiresAtUtcAttribute,
            beforeCast,
            afterCast);
        Assert.True(DateTimeOffset.Parse(
                updatedSession.Character.Attributes[RuntimeLegacyCannonBarrageExpiresAtUtcAttribute],
                CultureInfo.InvariantCulture) > oldExpiresAt);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(CannonBarrageSpellId));

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.XPList).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.XPCircle).Updates);

        var statusUpdate = Assert.Single(updates, update => update.Uid == PlayerUid &&
            update.Updates.Count == 1 &&
            update.Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        var statusEntry = Assert.Single(statusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyCannonBarrageFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CannonBarrage XPList transition", output);
        Assert.Contains("consumedXpList=True", output);
        Assert.Contains("openedCannonBarrage=True", output);
        Assert.Contains("refreshedCannonBarrage=True", output);
        Assert.Contains("expiresAtUtc=", output);
        Assert.DoesNotContain("WorldServer handled legacy CannonBarrage monster/player/SOB magic", output);
        Assert.DoesNotContain("cooldownRemainingMs=", output);
    }

    [Fact]
    public async Task HandlerPirateXpSkillWithXpListAndNoBlackbeardsRageOpensBlackbeardsRageConsumesXpListAndSkipsDamageAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 830, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                PirateXpSkillSpellId,
                sort: 63,
                useMana: 10,
                range: 30,
                useStamina: 5,
                coldTime: 60_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [PirateXpSkillSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag, LegacySupermanFlag),
                ["XPCount"] = "12",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: PirateXpSkillSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacySupermanFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyBlackbeardsRageFlag));
        Assert.Equal("0", updatedSession.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute,
            beforeCast,
            afterCast);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(PirateXpSkillSpellId));

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var xpListRemovalIndex = Assert.Single(
            Enumerable.Range(0, updates.Count),
            index => updates[index].Uid == PlayerUid &&
                updates[index].Updates.Count == 1 &&
                updates[index].Updates[0].Type == LegacyMsgUpdateDataType.XPList);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)],
            updates[xpListRemovalIndex].Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.XPCircle).Updates);

        var statusUpdateIndex = Assert.Single(
            Enumerable.Range(0, updates.Count),
            index => updates[index].Uid == PlayerUid &&
                updates[index].Updates.Count == 1 &&
                updates[index].Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.True(xpListRemovalIndex < statusUpdateIndex);
        var statusEntry = Assert.Single(updates[statusUpdateIndex].Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacySupermanFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyBlackbeardsRageFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy PirateXpSkill XPList transition", output);
        Assert.Contains("expiresAtUtc=", output);
    }

    [Fact]
    public async Task HandlerPirateXpSkillWithXpListAndBlackbeardsRageRefreshesBlackbeardsRageConsumesXpListAndSkipsDamageAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 830, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                PirateXpSkillSpellId,
                sort: 63,
                useMana: 10,
                range: 30,
                useStamina: 5,
                coldTime: 60_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var oldExpiresAt = DateTimeOffset.UtcNow.AddSeconds(5);
        var session = CreateSession(
            stamina: 100,
            spellIds: [PirateXpSkillSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag, LegacyBlackbeardsRageFlag),
                ["XPCount"] = "29",
                [RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute] =
                    oldExpiresAt.ToString("O", CultureInfo.InvariantCulture),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeCast = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: PirateXpSkillSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));
        var afterCast = DateTimeOffset.UtcNow;

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyBlackbeardsRageFlag));
        Assert.Equal("0", updatedSession.Character.Attributes["XPCount"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute,
            beforeCast,
            afterCast);
        Assert.True(DateTimeOffset.Parse(
                updatedSession.Character.Attributes[RuntimeLegacyBlackbeardsRageExpiresAtUtcAttribute],
                CultureInfo.InvariantCulture) > oldExpiresAt);
        Assert.True(updatedSession.MagicCooldowns is null ||
            !updatedSession.MagicCooldowns.ContainsKey(PirateXpSkillSpellId));

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var xpListRemovalIndex = Assert.Single(
            Enumerable.Range(0, updates.Count),
            index => updates[index].Uid == PlayerUid &&
                updates[index].Updates.Count == 1 &&
                updates[index].Updates[0].Type == LegacyMsgUpdateDataType.XPList);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPList, 0)],
            updates[xpListRemovalIndex].Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.XPCircle, 0)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.XPCircle).Updates);

        var statusUpdateIndex = Assert.Single(
            Enumerable.Range(0, updates.Count),
            index => updates[index].Uid == PlayerUid &&
                updates[index].Updates.Count == 1 &&
                updates[index].Updates[0].Type == LegacyMsgUpdateDataType.StatusFlag);
        Assert.True(xpListRemovalIndex < statusUpdateIndex);
        var statusEntry = Assert.Single(updates[statusUpdateIndex].Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyXpListFlag));
        Assert.True(IsLegacyStatusFlagSet(statusEntry.Values!, LegacyBlackbeardsRageFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy PirateXpSkill XPList transition", output);
        Assert.Contains("refreshedBlackbeardsRage=True", output);
        Assert.Contains("expiresAtUtc=", output);
    }

    [Fact]
    public async Task HandlerPirateXpSkillWithBlackbeardsRageHitsVisibleMonsterPlayerAndSobTargetsAndBypassesCooldown()
    {
        const uint expectedBasePlayerPhysicalDamage = 360;
        const uint expectedPlayerDamage = expectedBasePlayerPhysicalDamage * 2;
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 836, boundY: 584);
        var sobNpc = new SobNpc(
            Id: 65_060,
            Name: "PracticePole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: 836,
            Y: 584,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 6,
            RawLine: "65060,PracticePole,10,1137,1015,836,584,50000,50000,17,0",
            Fields: ["65060", "PracticePole", "10", "1137", "1015", "836", "584", "50000", "50000", "17", "0"]);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [CreateMagicRow(
                PirateXpSkillSpellId,
                sort: 63,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000)]);
        var monsters = CreateRuntimeMonsterInstances(worldData, spawn, monsterFamily);
        var monster = monsters.First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [PirateXpSkillSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyBlackbeardsRageFlag),
            });
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 836, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: PirateXpSkillSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var expectedTargetUids = monsters
            .Select(target => target.Uid)
            .Concat([TargetPlayerUid, checked((uint)sobNpc.Id)])
            .Order()
            .ToArray();
        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(830, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(PirateXpSkillSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            Assert.Equal(expectedTargetUids.Length, spell.Targets.Count);
        }

        Assert.Equal(
            casterSpell.Targets
                .OrderBy(target => target.Uid)
                .Select(target => (target.Uid, target.Damage, target.Hit, target.Effect))
                .ToArray(),
            targetSpell.Targets
                .OrderBy(target => target.Uid)
                .Select(target => (target.Uid, target.Damage, target.Hit, target.Effect))
                .ToArray());

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(expectedTargetUids, damageByUid.Keys.Order().ToArray());
        Assert.All(monsters, monsterTarget =>
        {
            Assert.True(damageByUid.TryGetValue(monsterTarget.Uid, out var monsterDamage));
            Assert.True(monsterDamage > 0);
        });
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.True(damageByUid.TryGetValue(checked((uint)sobNpc.Id), out var sobNpcDamage));
        Assert.True(sobNpcDamage > 0);
        Assert.All(casterSpell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        foreach (var monsterTarget in monsters)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(
                monsterTarget,
                DateTimeOffset.UtcNow,
                out var visibleMonster));
            Assert.Equal(monsterTarget.MaxLife - damageByUid[monsterTarget.Uid], visibleMonster.Hitpoints);
        }

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - (int)sobNpcDamage, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.True(updatedCasterSession.MagicCooldowns is null ||
            !updatedCasterSession.MagicCooldowns.ContainsKey(PirateXpSkillSpellId));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);

        var casterUpdates = ParseMsgUpdates(casterSent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(casterUpdates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(casterUpdates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);
        Assert.DoesNotContain(
            casterUpdates,
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.XPList));

        Assert.Contains(
            ParseMsgUpdates(targetSent),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 5_000UL - expectedPlayerDamage));
    }

    [Fact]
    public async Task HandlerPirateXpSkillWithBlackbeardsRageAgainstWildPhoenixPlayerConsumesDefenderChargeAndDealsDoubleOverrideDamage()
    {
        const uint expectedDamage = 2U;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                PirateXpSkillSpellId,
                sort: 63,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [PirateXpSkillSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyBlackbeardsRageFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 832,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: PirateXpSkillSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(checked((ushort)targetSession.Character.X), animation.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), animation.Y);
            Assert.Equal(PirateXpSkillSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - (long)expectedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var targetUpdates = ParseMsgUpdates(targetSent);
        var archiveEntry = Assert.Single(
            targetUpdates.SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);
        Assert.Contains(
            targetUpdates,
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Value == 100_000UL - expectedDamage));

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("previousCharges=2", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("damageOverride=1", output);
        Assert.Contains("WorldServer handled legacy PirateXpSkill monster/player/SOB magic", output);
        Assert.Contains("spellId=11060", output);
        Assert.Contains("sort=63", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("totalDamage=0", output);
    }

    [Fact]
    public async Task HandlerMoveLineNormalCastRepositionsCasterToLandingCoordinateAndPreservesSpendAndAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 838, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMoveLineRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [MoveLineSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: MoveLineSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(842, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(MoveLineSpellId, spell.SpellId);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(842, updatedSession.Character.X);
        Assert.Equal(584, updatedSession.Character.Y);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(13)]
    public async Task HandlerMoveLineGuildWarBlockedGateEndpointPullsBackAndKeepsOriginalLanding(int endpointSurface)
    {
        const ushort originalX = 2;
        const ushort originalY = 2;
        const ushort endpointX = 14;
        const ushort endpointY = originalY;
        const string illegalJumpMessage = "Illegal jumping over the gates detected.";
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMoveLineRow());
        var terrain = CreateGuildWarMoveLineTerrain(
            originalX,
            originalY,
            endpointX,
            endpointY,
            (ushort)endpointSurface);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: originalX,
            y: originalY,
            strength: 300,
            stamina: 100,
            spellIds: [MoveLineSpellId],
            attributes: new Dictionary<string, string>
            {
                ["CurrentTerrainMask"] = "3",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            Terrain: terrain,
            SobNpcStates: new SobNpcRuntimeStateStore(),
            GuildWarStates: new LegacyGuildWarRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: endpointX,
                Y: endpointY,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: MoveLineSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(originalX, spell.X);
        Assert.Equal(originalY, spell.Y);
        Assert.Equal(MoveLineSpellId, spell.SpellId);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal((long)originalX, updatedSession.Character.X);
        Assert.Equal((long)originalY, updatedSession.Character.Y);

        var systemTalk = Assert.Single(
            ParseLegacyMessages(sent),
            message => message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                message.Content.Count > 3 &&
                message.Content[3] == illegalJumpMessage);
        Assert.Equal("SYSTEM", systemTalk.Content[0]);

        var pullback = Assert.Single(
            ParseMsgActions(sent),
            action => action.Type == MsgActionType.SetLocation &&
                action.ObjId == PlayerUid);
        Assert.Equal((uint)LegacySobNpcEventRules.GuildWarMapId, pullback.DwParam);
        Assert.Equal((uint)originalX, pullback.PositionX);
        Assert.Equal((uint)originalY, pullback.PositionY);
    }

    [Fact]
    public async Task HandlerMoveLineWithEquippedTideTrapAndLearnedTrapSpellLeavesCasterInPlaceAndDropsTrapAtLanding()
    {
        const uint tideTrapRuneItemId = 730_001;
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 838, boundY: 584);
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [spawn],
            [CreateItemType((int)tideTrapRuneItemId, "TideTrap")],
            CreateMoveLineRow(),
            CreateMoveLineRow(level: 1),
            CreateMagicRow(TideTrapSpellId, sort: 58, useMana: 0, damage: 0, coldTime: 0));
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        var monsterStates = new MonsterRuntimeStateStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(MoveLineSpellId, level: 1),
                CreatePlayerSpell(TideTrapSpellId),
            ],
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = 73_001,
                ItemId = tideTrapRuneItemId,
                Position = 101,
            }));
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: MoveLineSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(842, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(MoveLineSpellId, spell.SpellId);
        Assert.NotEmpty(spell.Targets);
        Assert.Contains(spell.Targets, target => target.Uid == monster.Uid && target.Damage > 0 && target.Hit);
        AssertBombEqualsOneIfParserSupportsIt(spell);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(830, updatedSession.Character.X);
        Assert.Equal(584, updatedSession.Character.Y);

        var trap = Assert.Single(ParseLegacyFloorItems(sent));
        Assert.Equal(1080U, trap.ItemId);
        Assert.NotEqual(1080U, trap.Uid);
        Assert.Equal("trap", trap.Name);
        Assert.Equal(PlayerUid, trap.ItemOwnerUid);
        Assert.Equal(830, trap.OwnerX);
        Assert.Equal(584, trap.OwnerY);
        Assert.Equal(842, trap.X);
        Assert.Equal(584, trap.Y);
        Assert.Equal(LegacyFloorItemDropType.Effect, trap.DropType);
        Assert.Equal(14, trap.PlusOrColor);
        Assert.True(floorItems.TryGetVisibleEntry(trap.Uid, out var storedTrap));
        Assert.Equal(trap, storedTrap.Packet);
        Assert.NotNull(storedTrap.Effect);
        Assert.Equal(1, storedTrap.Effect.SpellLevel);
    }

    [Fact]
    public async Task HandlerMoveLineTideTrapRuntimeLoopInvokesTrapDamageAndRemoveEffectWhenDue()
    {
        const uint tideTrapRuneItemId = 730_001;
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 838, boundY: 584);
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [spawn],
            [CreateItemType((int)tideTrapRuneItemId, "TideTrap")],
            CreateMoveLineRow(damage: 10),
            CreateMoveLineRow(level: 1, damage: 200),
            CreateMagicRow(TideTrapSpellId, sort: 58, useMana: 0, damage: 10, coldTime: 0));
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(MoveLineSpellId, level: 1),
                CreatePlayerSpell(TideTrapSpellId),
            ],
            playerItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = 73_001,
                ItemId = tideTrapRuneItemId,
                Position = 101,
            }));
        SetHandlerSession(handler, connection, session);
        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            var attackStart = sent.Count;
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                    monster,
                    legacyEncodedQuery: false,
                    spellId: MoveLineSpellId,
                    x: 842,
                    y: 584)).ToFrameBytes()),
                cancel.Token);

            var trap = Assert.Single(
                ParseLegacyFloorItems(sent.ToArray().Skip(attackStart)),
                floorItem => floorItem.ItemId == 1080 &&
                    floorItem.DropType == LegacyFloorItemDropType.Effect &&
                    floorItem.Name == "trap");

            await WaitUntilAsync(
                () =>
                {
                    var frames = sent.ToArray().Skip(attackStart).ToArray();
                    return ParseSpellAnimations(frames).Any(spell =>
                            spell.SpellId == TideTrapSpellId &&
                            spell.AttackerUid == PlayerUid &&
                            spell.X == 830 &&
                            spell.Y == 584 &&
                            spell.Targets.Any(target =>
                                target.Uid == monster.Uid &&
                                target.Damage > 0 &&
                                target.Hit)) &&
                        ParseLegacyFloorItems(frames).Any(floorItem =>
                            floorItem.Uid == trap.Uid &&
                            floorItem.ItemId == 1080 &&
                            floorItem.DropType == LegacyFloorItemDropType.RemoveEffect);
                },
                TimeSpan.FromSeconds(1));

            var framesAfterAttack = sent.ToArray().Skip(attackStart).ToArray();
            var invoke = Assert.Single(
                ParseSpellAnimations(framesAfterAttack),
                spell => spell.SpellId == TideTrapSpellId);
            Assert.Equal(PlayerUid, invoke.AttackerUid);
            Assert.Equal(830, invoke.X);
            Assert.Equal(584, invoke.Y);
            Assert.Equal(0, invoke.SpellLevel);
            Assert.Contains(
                invoke.Targets,
                target => target.Uid == monster.Uid && target.Damage == 1_350 && target.Hit);

            var remove = Assert.Single(
                ParseLegacyFloorItems(framesAfterAttack),
                floorItem => floorItem.Uid == trap.Uid &&
                    floorItem.DropType == LegacyFloorItemDropType.RemoveEffect);
            Assert.Equal(1080U, remove.ItemId);
            Assert.Equal("trap", remove.Name);
            Assert.False(GetHandlerFloorItems(handler).TryGetVisiblePacket(trap.Uid, out _));
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerLayTrapTwilightDanceRuntimeLoopInvokesTrapDamageAndRemoveEffectWhenDue()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                LegacyLayTrap.TwilightDanceSpellId,
                sort: LegacyLayTrap.LayTrapSortValue,
                useMana: 0,
                damage: 120,
                coldTime: 0));
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(LegacyLayTrap.TwilightDanceSpellId),
            ]);
        SetHandlerSession(handler, connection, session);
        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            var attackStart = sent.Count;
            const ushort targetX = 850;
            const ushort targetY = 584;
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                    monster,
                    legacyEncodedQuery: false,
                    spellId: LegacyLayTrap.TwilightDanceSpellId,
                    x: targetX,
                    y: targetY)).ToFrameBytes()),
                cancel.Token);

            var expectedCoordinates = LegacyLayTrap
                .SelectTwilightDanceCoordinates(830, 584, targetX, targetY)
                .Select(coordinate => (coordinate.X, coordinate.Y))
                .ToArray();
            var floorEffects = ParseLegacyFloorItems(sent.ToArray().Skip(attackStart))
                .Where(floorItem =>
                    floorItem.ItemId == LegacyLayTrap.TwilightDanceFloorItemId &&
                    floorItem.DropType == LegacyFloorItemDropType.Effect &&
                    floorItem.Name == LegacyLayTrap.TwilightDanceFloorName)
                .ToArray();
            Assert.Equal(3, floorEffects.Length);
            Assert.Equal(expectedCoordinates, floorEffects.Select(floorItem => (floorItem.X, floorItem.Y)).ToArray());
            Assert.All(floorEffects, floorItem =>
            {
                Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
                Assert.Equal(830, floorItem.OwnerX);
                Assert.Equal(584, floorItem.OwnerY);
            });

            await WaitUntilAsync(
                () =>
                {
                    var frames = sent.ToArray().Skip(attackStart).ToArray();
                    var runtimeAnimationSeen = ParseSpellAnimations(frames).Any(spell =>
                        spell.SpellId == LegacyLayTrap.TwilightDanceSpellId &&
                        spell.AttackerUid == PlayerUid &&
                        expectedCoordinates.Contains((spell.X, spell.Y)) &&
                        spell.Targets.Any(target =>
                            target.Uid == monster.Uid &&
                            target.Damage > 0 &&
                            target.Hit));
                    var removalSeen = ParseLegacyFloorItems(frames).Any(floorItem =>
                        floorEffects.Any(effect => effect.Uid == floorItem.Uid) &&
                        floorItem.DropType == LegacyFloorItemDropType.RemoveEffect);
                    return runtimeAnimationSeen && removalSeen;
                },
                TimeSpan.FromSeconds(1));

            var framesAfterAttack = sent.ToArray().Skip(attackStart).ToArray();
            var invoke = Assert.Single(
                ParseSpellAnimations(framesAfterAttack),
                spell => spell.SpellId == LegacyLayTrap.TwilightDanceSpellId &&
                    expectedCoordinates.Contains((spell.X, spell.Y)) &&
                    spell.Targets.Any(target =>
                        target.Uid == monster.Uid &&
                        target.Damage > 0 &&
                        target.Hit));
            Assert.Equal(PlayerUid, invoke.AttackerUid);

            var remove = ParseLegacyFloorItems(framesAfterAttack).First(floorItem =>
                floorEffects.Any(effect => effect.Uid == floorItem.Uid) &&
                floorItem.DropType == LegacyFloorItemDropType.RemoveEffect);
            var removedEffect = Assert.Single(floorEffects, effect => effect.Uid == remove.Uid);
            Assert.Equal(LegacyLayTrap.TwilightDanceFloorItemId, remove.ItemId);
            Assert.Equal(LegacyLayTrap.TwilightDanceFloorName, remove.Name);
            Assert.False(GetHandlerFloorItems(handler).TryGetVisiblePacket(remove.Uid, out _));

            var removalAction = ParseMsgActions(framesAfterAttack).First(action =>
                action.Type == (MsgActionType)LegacyLayTrap.TwilightDanceRemovalActionType &&
                action.ObjId == PlayerUid &&
                action.PositionX == removedEffect.OwnerX &&
                action.PositionY == removedEffect.OwnerY &&
                action.TargetPositionX == removedEffect.X &&
                action.TargetPositionY == removedEffect.Y);
            Assert.Equal(PlayerUid, removalAction.ObjId);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearSandMistFloorSpellCreatesSourceFloorEffectBeforeLayTrap()
    {
        const ushort SandMistSpellId = 21_650;
        const uint SandMistFloorItemId = 4_130;

        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                SandMistSpellId,
                sort: 21,
                useMana: 0,
                damage: 120,
                maxTargets: 3,
                range: 14,
                coldTime: 0));
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData, log);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(SandMistSpellId),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        SetHandlerSession(handler, connection, session);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 833,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: SandMistSpellId)).ToFrameBytes()));

        var floor = Assert.Single(ParseLegacyFloorItems(sent));
        Assert.Equal(SandMistFloorItemId, floor.ItemId);
        Assert.Equal(LegacyFloorItemDropType.Effect, floor.DropType);
        Assert.Equal(14, floor.PlusOrColor);
        Assert.Equal(PlayerUid, floor.ItemOwnerUid);
        Assert.Equal(77U, floor.GuildId);
        Assert.Equal(833, floor.X);
        Assert.Equal(584, floor.Y);
        Assert.Equal(830, floor.OwnerX);
        Assert.Equal(584, floor.OwnerY);
        Assert.Equal("STR_TRAP_ID_@@" + floor.Uid.ToString(CultureInfo.InvariantCulture), floor.Name);

        Assert.True(GetHandlerFloorItems(handler).TryGetVisibleEntry(floor.Uid, out var storedFloor));
        Assert.Equal(floor, storedFloor.Packet);
        Assert.NotNull(storedFloor.Effect);
        Assert.Equal(SandMistSpellId, storedFloor.Effect.SpellId);
        Assert.Equal(14, storedFloor.Effect.Distance);
        Assert.Equal(3, storedFloor.Effect.HitRadius);
        Assert.Equal(900, storedFloor.Effect.RepeatMilliseconds);
        Assert.NotNull(storedFloor.Effect.ExpiresAt);

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(SandMistSpellId, animation.SpellId);
        Assert.Equal(833, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Empty(animation.Targets);

        Assert.DoesNotContain("unsupported LayTrap", log.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandlerCombatGearSandMistFloorRuntimeUsesSourceRadiusThreeWhenDue()
    {
        const ushort SandMistSpellId = 21_650;
        const uint SandMistFloorItemId = 4_130;
        const uint FloorUid = 900_216;

        var monsterFamily = CreateMonsterFamily();
        var insideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 21651, boundX: 836, boundY: 584);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 21652, boundX: 837, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [insideSpawn, outsideSpawn],
            CreateMagicRow(
                SandMistSpellId,
                sort: 21,
                useMana: 0,
                damage: 120,
                maxTargets: 3,
                range: 14,
                coldTime: 0));
        var monsters = new[] { insideSpawn, outsideSpawn }
            .SelectMany(spawn => LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn)))
            .ToArray();
        var insideMonster = Assert.Single(monsters, monster => monster.X == 836);
        var outsideMonster = Assert.Single(monsters, monster => monster.X == 837);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var runtimeAnchor = DateTimeOffset.UtcNow.AddSeconds(5);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(runtimeAnchor)),
            log);
        var session = CreateSession(strength: 300, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(SandMistSpellId),
        ]);
        SetHandlerSession(handler, connection, session);

        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            SandMistFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900216");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                session.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    SandMistSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    runtimeAnchor.AddMilliseconds(-1),
                    Distance: 14,
                    HitRadius: 3)),
            DateTimeOffset.UtcNow);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () =>
                {
                    var snapshot = sent.ToArray();
                    return ParseSpellAnimations(snapshot).Any(spell =>
                        spell.SpellId == SandMistSpellId &&
                        spell.AttackerUid == FloorUid &&
                        spell.Targets.Any(target =>
                            target.Uid == insideMonster.Uid &&
                            target.Damage > 0 &&
                            target.Hit));
                },
                TimeSpan.FromSeconds(1));

            var invoke = Assert.Single(
                ParseSpellAnimations(sent),
                spell => spell.SpellId == SandMistSpellId && spell.AttackerUid == FloorUid);
            Assert.Equal(floorPacket.OwnerX, invoke.X);
            Assert.Equal(floorPacket.OwnerY, invoke.Y);
            Assert.Contains(invoke.Targets, target => target.Uid == insideMonster.Uid && target.Damage > 0 && target.Hit);
            Assert.DoesNotContain(invoke.Targets, target => target.Uid == outsideMonster.Uid);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearIceAgeFloorSpellCreatesSourceFloorEffectAndTriggersColdBloodline()
    {
        const ushort IceAgeSpellId = 21_560;
        const ushort ColdBloodlineProcSpellId = 21_550;
        const int ColdBloodlineFlag = 0x19A;
        const uint IceAgeFloorItemId = 4_180;

        var monsterFamily = CreateMonsterFamily();
        var coldBloodline = CreateMagicRow(
            ColdBloodlineProcSpellId,
            sort: 1,
            useMana: 0,
            damage: 0,
            maxTargets: 0,
            range: 0,
            coldTime: 0,
            duration: 5);
        var coldFields = coldBloodline.Fields.ToArray();
        coldFields[35] = "37";
        coldBloodline = coldBloodline with { Fields = coldFields };
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                IceAgeSpellId,
                sort: 21,
                useMana: 0,
                damage: 120,
                maxTargets: 5,
                range: 15,
                coldTime: 0),
            coldBloodline);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var runtimeAnchor = DateTimeOffset.UtcNow.AddSeconds(5);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(runtimeAnchor)),
            log);
        var session = CreateSession(
            x: 830,
            y: 584,
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(IceAgeSpellId),
                CreatePlayerSpell(ColdBloodlineProcSpellId),
            ]);
        SetHandlerSession(handler, connection, session);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 833,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: IceAgeSpellId)).ToFrameBytes()));

        var floor = Assert.Single(ParseLegacyFloorItems(sent));
        Assert.Equal(IceAgeFloorItemId, floor.ItemId);
        Assert.Equal(LegacyFloorItemDropType.Effect, floor.DropType);
        Assert.Equal(29, floor.PlusOrColor);
        Assert.Equal(PlayerUid, floor.ItemOwnerUid);
        Assert.Equal(833, floor.X);
        Assert.Equal(584, floor.Y);
        Assert.Equal(830, floor.OwnerX);
        Assert.Equal(584, floor.OwnerY);

        Assert.True(GetHandlerFloorItems(handler).TryGetVisibleEntry(floor.Uid, out var storedFloor));
        Assert.NotNull(storedFloor.Effect);
        Assert.Equal(IceAgeSpellId, storedFloor.Effect.SpellId);
        Assert.Equal(5, storedFloor.Effect.HitRadius);
        Assert.Equal(900, storedFloor.Effect.RepeatMilliseconds);
        Assert.NotNull(storedFloor.Effect.ExpiresAt);

        var placementAnimation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(IceAgeSpellId, placementAnimation.SpellId);
        Assert.Equal(PlayerUid, placementAnimation.AttackerUid);
        Assert.Empty(placementAnimation.Targets);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () => GetHandlerSessions(handler).TryGet(connection, out var updated) &&
                    updated is not null &&
                    HasLegacyStatusFlag(updated.Character.Attributes, ColdBloodlineFlag) &&
                    ParseSpellAnimations(sent).Any(spell => spell.SpellId == ColdBloodlineProcSpellId),
                TimeSpan.FromSeconds(3));

            var updatedSession = AssertHandlerSession(handler, connection);
            Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, ColdBloodlineFlag));

            var coldBloodlineAnimation = Assert.Single(
                ParseSpellAnimations(sent),
                spell => spell.SpellId == ColdBloodlineProcSpellId);
            Assert.Equal(PlayerUid, coldBloodlineAnimation.AttackerUid);
            var target = Assert.Single(coldBloodlineAnimation.Targets);
            Assert.Equal(PlayerUid, target.Uid);
            Assert.True(target.Hit);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearPheasantBeakFloorSpellCreatesSourceFloorEffectImmediateRadiusAndLinePulse()
    {
        const ushort PheasantBeakSpellId = 21_580;
        const uint PheasantBeakFloorItemId = 4_170;

        var monsterFamilyAttributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["life"] = "500000",
            ["defense"] = "0",
        };
        var monsterFamily = CreateMonsterFamily() with { Attributes = monsterFamilyAttributes };
        var immediateSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 21581, boundX: 835, boundY: 584);
        var lineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 21582, boundX: 827, boundY: 584);
        var offLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 21583, boundX: 827, boundY: 590);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [immediateSpawn, lineSpawn, offLineSpawn],
            CreateMagicRow(
                PheasantBeakSpellId,
                sort: 21,
                useMana: 0,
                damage: 120,
                maxTargets: 5,
                range: 12,
                coldTime: 0));
        var immediateMonster = CreateRuntimeMonsterInstance(worldData, immediateSpawn, monsterFamily);
        var lineMonster = CreateRuntimeMonsterInstance(worldData, lineSpawn, monsterFamily);
        var offLineMonster = CreateRuntimeMonsterInstance(worldData, offLineSpawn, monsterFamily);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var runtimeAnchor = DateTimeOffset.UtcNow.AddMilliseconds(1_200);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(runtimeAnchor)),
            log);
        var session = CreateSession(strength: 300, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(PheasantBeakSpellId),
        ]);
        SetHandlerSession(handler, connection, session);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: immediateMonster.Uid,
                X: 833,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: PheasantBeakSpellId)).ToFrameBytes()));

        var floor = Assert.Single(ParseLegacyFloorItems(sent));
        Assert.Equal(PheasantBeakFloorItemId, floor.ItemId);
        Assert.Equal(LegacyFloorItemDropType.Effect, floor.DropType);
        Assert.Equal(30, floor.PlusOrColor);
        Assert.Equal(PlayerUid, floor.ItemOwnerUid);
        Assert.Equal(833, floor.X);
        Assert.Equal(584, floor.Y);
        Assert.Equal(830, floor.OwnerX);
        Assert.Equal(584, floor.OwnerY);

        Assert.True(GetHandlerFloorItems(handler).TryGetVisibleEntry(floor.Uid, out var storedFloor));
        Assert.NotNull(storedFloor.Effect);
        Assert.Equal(PheasantBeakSpellId, storedFloor.Effect.SpellId);
        Assert.Equal(12, storedFloor.Effect.Distance);
        Assert.Equal(5, storedFloor.Effect.HitRadius);
        Assert.Equal(900, storedFloor.Effect.RepeatMilliseconds);

        var immediateAnimation = Assert.Single(
            ParseSpellAnimations(sent),
            spell => spell.SpellId == PheasantBeakSpellId && spell.AttackerUid == PlayerUid);
        Assert.Contains(
            immediateAnimation.Targets,
            target => target.Uid == immediateMonster.Uid && target.Damage > 0 && target.Hit);
        Assert.DoesNotContain(immediateAnimation.Targets, target => target.Uid == lineMonster.Uid);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () => ParseSpellAnimations(sent).Any(spell =>
                    spell.SpellId == PheasantBeakSpellId &&
                    spell.AttackerUid == floor.Uid &&
                    spell.Targets.Any(target => target.Uid == lineMonster.Uid && target.Damage > 0 && target.Hit)),
                TimeSpan.FromSeconds(1));

            var runtimeAnimation = Assert.Single(
                ParseSpellAnimations(sent),
                spell => spell.SpellId == PheasantBeakSpellId && spell.AttackerUid == floor.Uid);
            Assert.Equal(floor.OwnerX, runtimeAnimation.X);
            Assert.Equal(floor.OwnerY, runtimeAnimation.Y);
            Assert.Contains(
                runtimeAnimation.Targets,
                target => target.Uid == lineMonster.Uid && target.Damage > 0 && target.Hit);
            Assert.DoesNotContain(runtimeAnimation.Targets, target => target.Uid == offLineMonster.Uid);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearStarVolcanoFloorRuntimeInvokesSourcePulseDamageWhenDue()
    {
        const ushort StarVolcanoPassiveSpellId = 22_070;
        const uint StarVolcanoFloorItemId = 4_200;
        const uint FloorUid = 900_220;

        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                StarVolcanoPassiveSpellId,
                sort: 1,
                useMana: 0,
                damage: 120,
                maxTargets: 4,
                range: 15,
                coldTime: 0));
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var session = CreateSession(strength: 300, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(StarVolcanoPassiveSpellId),
        ]);
        SetHandlerSession(handler, connection, session);

        var now = DateTimeOffset.UtcNow;
        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            StarVolcanoFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900220");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                session.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    StarVolcanoPassiveSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    now.AddMilliseconds(-1),
                    HitRadius: 4)),
            now);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () =>
                {
                    var snapshot = sent.ToArray();
                    return ParseSpellAnimations(snapshot).Any(spell =>
                        spell.SpellId == StarVolcanoPassiveSpellId &&
                        spell.X == floorPacket.OwnerX &&
                        spell.Y == floorPacket.OwnerY &&
                        spell.Targets.Any(target =>
                            target.Damage > 0 &&
                            target.Hit));
                },
                TimeSpan.FromSeconds(1));

            var sentSnapshot = sent.ToArray();
            var invoke = Assert.Single(
                ParseSpellAnimations(sentSnapshot),
                spell => spell.SpellId == StarVolcanoPassiveSpellId &&
                    spell.X == floorPacket.OwnerX &&
                    spell.Y == floorPacket.OwnerY &&
                    spell.Targets.Any(target => target.Damage > 0 && target.Hit));
            Assert.Equal(FloorUid, invoke.AttackerUid);
            Assert.Contains(invoke.Targets, target => target.Uid == monster.Uid && target.Damage > 0 && target.Hit);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearStarVolcanoFloorRuntimeRepeatsUntilSourceExpirationThenRemovesEffect()
    {
        const ushort StarVolcanoPassiveSpellId = 22_070;
        const uint StarVolcanoFloorItemId = 4_200;
        const uint FloorUid = 900_226;

        var monsterFamilyAttributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["life"] = "500000",
            ["defense"] = "0",
        };
        var monsterFamily = CreateMonsterFamily() with { Attributes = monsterFamilyAttributes };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 22072, boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                StarVolcanoPassiveSpellId,
                sort: 1,
                useMana: 0,
                damage: 10,
                maxTargets: 4,
                range: 15,
                coldTime: 0));
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var runtimeAnchor = DateTimeOffset.UtcNow.AddSeconds(5);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(runtimeAnchor)),
            log);
        var session = CreateSession(strength: 10, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(StarVolcanoPassiveSpellId),
        ]);
        SetHandlerSession(handler, connection, session);

        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            StarVolcanoFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900226");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                session.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    StarVolcanoPassiveSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    runtimeAnchor.AddMilliseconds(-1),
                    HitRadius: 4,
                    ExpiresAt: runtimeAnchor.AddMilliseconds(600),
                    RepeatMilliseconds: 80)),
            DateTimeOffset.UtcNow);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () =>
                {
                    var snapshot = sent.ToArray();
                    var pulseCount = ParseSpellAnimations(snapshot).Count(spell =>
                        spell.SpellId == StarVolcanoPassiveSpellId &&
                        spell.AttackerUid == FloorUid);
                    var removed = ParseLegacyFloorItems(snapshot).Any(floorItem =>
                        floorItem.Uid == FloorUid &&
                        floorItem.ItemId == StarVolcanoFloorItemId &&
                        floorItem.DropType == LegacyFloorItemDropType.RemoveEffect);
                    return pulseCount >= 2 && removed;
                },
                TimeSpan.FromSeconds(3));

            var sentSnapshot = sent.ToArray();
            var pulses = ParseSpellAnimations(sentSnapshot)
                .Where(spell => spell.SpellId == StarVolcanoPassiveSpellId && spell.AttackerUid == FloorUid)
                .ToArray();
            Assert.True(pulses.Length >= 2);
            Assert.All(pulses.Take(2), pulse =>
            {
                Assert.Equal(floorPacket.OwnerX, pulse.X);
                Assert.Equal(floorPacket.OwnerY, pulse.Y);
                Assert.Contains(pulse.Targets, target => target.Uid == monster.Uid && target.Damage > 0 && target.Hit);
            });

            var remove = Assert.Single(
                ParseLegacyFloorItems(sentSnapshot),
                floorItem => floorItem.Uid == FloorUid &&
                    floorItem.DropType == LegacyFloorItemDropType.RemoveEffect);
            Assert.Equal(StarVolcanoFloorItemId, remove.ItemId);
            Assert.False(GetHandlerFloorItems(handler).TryGetVisiblePacket(FloorUid, out _));
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearStarVolcanoFloorRuntimeHitsVisibleSobNpcInsideSourceRadius()
    {
        const ushort StarVolcanoPassiveSpellId = 22_070;
        const uint StarVolcanoFloorItemId = 4_200;
        const uint FloorUid = 900_224;

        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateMoveLineSobNpc(id: 65_070, x: 835, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows:
            [
                CreateMagicRow(
                    StarVolcanoPassiveSpellId,
                    sort: 1,
                    useMana: 0,
                    damage: 120,
                    maxTargets: 4,
                    range: 15,
                    coldTime: 0),
            ]);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var session = CreateSession(strength: 300, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(StarVolcanoPassiveSpellId),
        ]);
        SetHandlerSession(handler, connection, session);

        var now = DateTimeOffset.UtcNow;
        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            StarVolcanoFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900224");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                session.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    StarVolcanoPassiveSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    now.AddMilliseconds(-1),
                    HitRadius: 4)),
            now);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () => ParseSpellAnimations(sent.ToArray()).Any(spell => spell.SpellId == StarVolcanoPassiveSpellId),
                TimeSpan.FromSeconds(1));

            var sentSnapshot = sent.ToArray();
            var invoke = Assert.Single(
                ParseSpellAnimations(sentSnapshot),
                spell => spell.SpellId == StarVolcanoPassiveSpellId);
            var sobTarget = Assert.Single(
                invoke.Targets,
                target => target.Uid == checked((uint)sobNpc.Id));
            Assert.True(sobTarget.Hit);
            Assert.True(sobTarget.Damage > 0);

            var visibleSobNpc = GetHandlerSobNpcStates(handler).GetVisible(sobNpc);
            Assert.Equal(sobNpc.Life - (int)sobTarget.Damage, visibleSobNpc.Life);
            Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearStarVolcanoFloorRuntimeHitsVisiblePlayerInsideSourceRadius()
    {
        const ushort StarVolcanoPassiveSpellId = 22_070;
        const uint StarVolcanoFloorItemId = 4_200;
        const uint FloorUid = 900_225;

        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                StarVolcanoPassiveSpellId,
                sort: 1,
                useMana: 0,
                damage: 120,
                maxTargets: 4,
                range: 15,
                coldTime: 0));
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(StarVolcanoPassiveSpellId),
        ]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 835, y: 584);
        SetHandlerSession(handler, casterConnection, casterSession);
        SetHandlerSession(handler, targetConnection, targetSession);

        var now = DateTimeOffset.UtcNow;
        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            StarVolcanoFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900225");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                casterSession.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    StarVolcanoPassiveSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    now.AddMilliseconds(-1),
                    HitRadius: 4)),
            now);

        StartHandlerRuntimeUpdateLoop(handler, casterConnection, casterSession, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () =>
                {
                    var snapshot = casterSent.ToArray();
                    return ParseSpellAnimations(snapshot).Any(spell =>
                        spell.SpellId == StarVolcanoPassiveSpellId &&
                        spell.Targets.Any(target => target.Uid == TargetPlayerUid && target.Damage > 0 && target.Hit));
                },
                TimeSpan.FromSeconds(1));

            var casterSnapshot = casterSent.ToArray();
            var targetSnapshot = targetSent.ToArray();
            var casterInvoke = Assert.Single(
                ParseSpellAnimations(casterSnapshot),
                spell => spell.SpellId == StarVolcanoPassiveSpellId &&
                    spell.Targets.Any(target => target.Uid == TargetPlayerUid && target.Damage > 0 && target.Hit));
            var targetInvoke = Assert.Single(
                ParseSpellAnimations(targetSnapshot),
                spell => spell.SpellId == StarVolcanoPassiveSpellId &&
                    spell.Targets.Any(target => target.Uid == TargetPlayerUid && target.Damage > 0 && target.Hit));
            Assert.Equal(FloorUid, casterInvoke.AttackerUid);
            Assert.Equal(FloorUid, targetInvoke.AttackerUid);

            var target = Assert.Single(casterInvoke.Targets, target => target.Uid == TargetPlayerUid);
            Assert.True(GetHandlerSessions(handler).TryGet(targetConnection, out var updatedTargetSession));
            Assert.NotNull(updatedTargetSession);
            Assert.Equal(targetSession.Character.MinHitPoints - target.Damage, updatedTargetSession.Character.MinHitPoints);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearLavaSeaFloorRuntimeInvokesSourcePulseDamageWhenDue()
    {
        const ushort LavaSeaPassiveSpellId = 22_080;
        const uint LavaSeaFloorItemId = 4_190;
        const uint FloorUid = 900_221;

        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 22081, boundX: 835, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                LavaSeaPassiveSpellId,
                sort: 1,
                useMana: 0,
                damage: 120,
                maxTargets: 4,
                range: 15,
                coldTime: 0));
        var monster = LegacyMonsterInstance
            .FromGenerator(spawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var session = CreateSession(strength: 300, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(LavaSeaPassiveSpellId),
        ]);
        SetHandlerSession(handler, connection, session);

        var now = DateTimeOffset.UtcNow;
        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            LavaSeaFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 14,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900221");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                session.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    LavaSeaPassiveSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    now.AddMilliseconds(-1),
                    HitRadius: 4)),
            now);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () =>
                {
                    var snapshot = sent.ToArray();
                    return ParseSpellAnimations(snapshot).Any(spell =>
                        spell.SpellId == LavaSeaPassiveSpellId &&
                        spell.X == floorPacket.OwnerX &&
                        spell.Y == floorPacket.OwnerY &&
                        spell.Targets.Any(target =>
                            target.Damage > 0 &&
                            target.Hit));
                },
                TimeSpan.FromSeconds(1));

            var sentSnapshot = sent.ToArray();
            var invoke = Assert.Single(
                ParseSpellAnimations(sentSnapshot),
                spell => spell.SpellId == LavaSeaPassiveSpellId &&
                    spell.X == floorPacket.OwnerX &&
                    spell.Y == floorPacket.OwnerY &&
                    spell.Targets.Any(target => target.Damage > 0 && target.Hit));
            Assert.Equal(FloorUid, invoke.AttackerUid);
            Assert.Contains(invoke.Targets, target => target.Uid == monster.Uid && target.Damage > 0 && target.Hit);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGearDrukyleFloorRuntimeUsesSourceLinePulseWhenDue()
    {
        const ushort DrukylePassiveSpellId = 22_100;
        const uint DrukyleFloorItemId = 4_210;
        const uint FloorUid = 900_222;

        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 22101, boundX: 827, boundY: 584);
        var offLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 22102, boundX: 827, boundY: 590);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [onLineSpawn, offLineSpawn],
            CreateMagicRow(
                DrukylePassiveSpellId,
                sort: 1,
                useMana: 0,
                damage: 120,
                maxTargets: 5,
                range: 12,
                coldTime: 0));
        var onLineMonster = LegacyMonsterInstance
            .FromGenerator(onLineSpawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(onLineSpawn))
            .First();
        var offLineMonster = LegacyMonsterInstance
            .FromGenerator(offLineSpawn, monsterFamily, worldData.GetLegacyMonsterRuntimeUidBase(offLineSpawn))
            .First();
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var session = CreateSession(strength: 300, stamina: 100, spellEntries:
        [
            CreatePlayerSpell(DrukylePassiveSpellId),
        ]);
        SetHandlerSession(handler, connection, session);

        var now = DateTimeOffset.UtcNow;
        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            DrukyleFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 30,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900222");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                session.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    DrukylePassiveSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    now.AddMilliseconds(-1),
                    Distance: 12,
                    HitRadius: 5)),
            now);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            // Allow scheduler contention; game due time remains controlled by the anchored provider.
            await WaitUntilAsync(
                () =>
                {
                    var snapshot = SnapshotFrames(sent);
                    return ParseSpellAnimations(snapshot).Any(spell =>
                        spell.SpellId == DrukylePassiveSpellId &&
                        spell.Targets.Any(target => target.Uid == onLineMonster.Uid && target.Damage > 0 && target.Hit));
                },
                TimeSpan.FromSeconds(5));

            var sentSnapshot = SnapshotFrames(sent);
            var invoke = Assert.Single(
                ParseSpellAnimations(sentSnapshot),
                spell => spell.SpellId == DrukylePassiveSpellId &&
                    spell.Targets.Any(target => target.Uid == onLineMonster.Uid && target.Damage > 0 && target.Hit));
            Assert.Equal(FloorUid, invoke.AttackerUid);
            Assert.Equal(floorPacket.OwnerX, invoke.X);
            Assert.Equal(floorPacket.OwnerY, invoke.Y);
            Assert.Contains(invoke.Targets, target => target.Uid == onLineMonster.Uid && target.Damage > 0 && target.Hit);
            Assert.DoesNotContain(invoke.Targets, target => target.Uid == offLineMonster.Uid);
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCombatGear22090FloorRuntimeTriggersColdBloodlineWhenOwnerIsInsideSourceRadius()
    {
        const ushort ColdBloodlineSourceFloorSpellId = 22_090;
        const ushort ColdBloodlineProcSpellId = 21_550;
        const int ColdBloodlineFlag = 0x19A;
        const uint ColdBloodlineFloorItemId = 4_220;
        const uint FloorUid = 900_223;

        var monsterFamily = CreateMonsterFamily();
        var coldBloodline = CreateMagicRow(
            ColdBloodlineProcSpellId,
            sort: 1,
            useMana: 0,
            damage: 0,
            maxTargets: 0,
            range: 0,
            coldTime: 0,
            duration: 5);
        var coldFields = coldBloodline.Fields.ToArray();
        coldFields[35] = "37";
        coldBloodline = coldBloodline with { Fields = coldFields };
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                ColdBloodlineSourceFloorSpellId,
                sort: 1,
                useMana: 0,
                damage: 120,
                maxTargets: 5,
                range: 15,
                coldTime: 0),
            coldBloodline);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 54_003,
                    ItemId = 421_003,
                    Durability = 100,
                    Position = 4,
                },
            ],
            RuneItems: []);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var session = CreateSession(
            x: 830,
            y: 584,
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(ColdBloodlineProcSpellId),
            ],
            playerItems: playerItems);
        SetHandlerSession(handler, connection, session);

        var now = DateTimeOffset.UtcNow;
        var floorPacket = new LegacyFloorItemPacket(
            FloorUid,
            ColdBloodlineFloorItemId,
            833,
            584,
            MaxLife: 0,
            LegacyFloorItemDropType.Effect,
            Life: 0,
            PlusOrColor: 29,
            ItemOwnerUid: PlayerUid,
            GuildId: 0,
            FlowerType: 0,
            Timer: 0,
            OwnerX: 830,
            OwnerY: 584,
            Name: "STR_TRAP_ID_@@900223");
        GetHandlerFloorItems(handler).AddVisible(
            FloorItemRuntimeEntry.ForEffect(
                session.Character.MapId,
                floorPacket,
                new FloorItemRuntimeEffect(
                    ColdBloodlineSourceFloorSpellId,
                    floorPacket.OwnerX,
                    floorPacket.OwnerY,
                    now.AddMilliseconds(-1),
                    HitRadius: 5)),
            now);

        StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

        try
        {
            await WaitUntilAsync(
                () => GetHandlerSessions(handler).TryGet(connection, out var updated) &&
                    updated is not null &&
                    HasLegacyStatusFlag(updated.Character.Attributes, ColdBloodlineFlag) &&
                    HasColdBloodlineRuntimeFrames(),
                TimeSpan.FromSeconds(3));

            var updatedSession = AssertHandlerSession(handler, connection);
            Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, ColdBloodlineFlag));

            var sentSnapshot = sent.ToArray();
            var parsedUpdates = sentSnapshot
                .Select(bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update)
                    ? update
                    : (LegacyMsgUpdatePacket?)null)
                .Where(update => update is not null)
                .Select(update => update!);
            Assert.Contains(
                parsedUpdates,
                update => update.Uid == PlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        IsLegacyStatusFlagSet(entry.Values, ColdBloodlineFlag)));
            Assert.Contains(
                parsedUpdates,
                update => update.Uid == PlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                        entry.Flag == ColdBloodlineFlag &&
                        entry.Time == 5 &&
                        entry.Damage == 37));

            var animation = Assert.Single(
                ParseSpellAnimations(sentSnapshot),
                spell => spell.SpellId == ColdBloodlineProcSpellId);
            Assert.Equal(PlayerUid, animation.AttackerUid);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, target.Uid);
            Assert.True(target.Hit);
            Assert.Equal(0U, target.Damage);

            var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot));
            Assert.Equal(
                LegacyShowEquipmentPacket.CreateEquipmentRefresh(playerItems, alternateEquipmentActive: false),
                equipmentRefresh);
            Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);
            Assert.Equal(0U, equipmentRefresh.WParam);
            Assert.Equal(0U, equipmentRefresh.Alternante);
            Assert.Equal(54_003U, equipmentRefresh.EquipmentSlots.RightWeapon);

            var statusIndex = SingleFrameIndex(
                sentSnapshot,
                frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                    update.Uid == PlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        IsLegacyStatusFlagSet(entry.Values, ColdBloodlineFlag)));
            var archiveIndex = SingleFrameIndex(
                sentSnapshot,
                frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                    update.Uid == PlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                        entry.Flag == ColdBloodlineFlag &&
                        entry.Time == 5 &&
                        entry.Damage == 37));
            var animationIndex = SingleFrameIndex(
                sentSnapshot,
                frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                    packet.SpellId == ColdBloodlineProcSpellId);
            var equipmentIndex = SingleFrameIndex(
                sentSnapshot,
                frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
            Assert.True(statusIndex < archiveIndex);
            Assert.True(archiveIndex < animationIndex);
            Assert.True(animationIndex < equipmentIndex);
        }
        finally
        {
            await cancel.CancelAsync();
        }

        bool HasColdBloodlineRuntimeFrames()
        {
            var snapshot = sent.ToArray();
            var parsedUpdates = ParseMsgUpdates(snapshot);
            return parsedUpdates.Any(update =>
                    update.Uid == PlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null &&
                        IsLegacyStatusFlagSet(entry.Values, ColdBloodlineFlag))) &&
                parsedUpdates.Any(update =>
                    update.Uid == PlayerUid &&
                    update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                        entry.Flag == ColdBloodlineFlag &&
                        entry.Time == 5 &&
                        entry.Damage == 37)) &&
                ParseSpellAnimations(snapshot).Any(spell => spell.SpellId == ColdBloodlineProcSpellId) &&
                ParseShowEquipmentRefreshes(snapshot).Count == 1;
        }
    }

    [Fact]
    public async Task HandlerMoveLineHitsOnlyMonstersOnOrNearTwelveCellLineAndSendsAnimationAtLineEnd()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584);
        var nearLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 838, boundY: 585);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838, boundY: 586);
        var beyondLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 844, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [onLineSpawn, nearLineSpawn, outsideWidthSpawn, beyondLineSpawn],
            CreateMoveLineRow());
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var nearLineMonster = CreateRuntimeMonsterInstance(worldData, nearLineSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var beyondLineMonster = CreateRuntimeMonsterInstance(worldData, beyondLineSpawn, monsterFamily);
        var allMonsters = new[] { onLineMonster, nearLineMonster, outsideWidthMonster, beyondLineMonster };
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [MoveLineSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                onLineMonster,
                legacyEncodedQuery: false,
                spellId: MoveLineSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var spellFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        Assert.Equal((ushort)2239, spellFrame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(spellFrame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(842, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(MoveLineSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);

        var expectedTargetUids = new[] { onLineMonster.Uid, nearLineMonster.Uid }.Order().ToArray();
        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(expectedTargetUids, damageByUid.Keys.Order().ToArray());
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        foreach (var monster in allMonsters)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
            if (damageByUid.TryGetValue(monster.Uid, out var damage))
            {
                Assert.Equal(monster.MaxLife - damage, visibleMonster.Hitpoints);
            }
            else
            {
                Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
            }
        }

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
    }

    [Fact]
    public async Task HandlerMoveLineCanHitVisiblePlayerAndSobTargetsWithPhysicalDamageAndNoZeroDamageMarks()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateMoveLineSobNpc(id: 65_064, x: 838, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows: [CreateMoveLineRow()]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [MoveLineSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 836, y: 584);
        // Physical.OnPlayer has a spell-11110 101% branch, not the SOB row's
        // generic 120%. Keep this integration expectation aligned with the
        // shared calculator; the independent coefficient test pins that branch.
        Assert.True(LegacyMagicRuntime.TryResolveSpell(worldData, MoveLineSpellId, 0, out var moveLineSpell));
        var expectedBasePlayerPhysicalDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            moveLineSpell,
            worldData,
            1U);
        // This ungeared fixture cannot penetrate: Physical's !flag2 branch adds
        // its 50% bonus before MoveLine's caller fraction.
        var expectedPlayerDamage = (expectedBasePlayerPhysicalDamage + expectedBasePlayerPhysicalDamage * 50 / 100) * 12 / 16;
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: MoveLineSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(842, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(MoveLineSpellId, spell.SpellId);
            Assert.Equal(
                new[] { TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                spell.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.DoesNotContain(spell.Targets, target => target.Damage == 0);
            Assert.All(spell.Targets, target =>
            {
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
            Assert.Contains(spell.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerDamage);
            Assert.Contains(spell.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage > 0);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);

        var sobTarget = Assert.Single(casterSpell.Targets, target => target.Uid == checked((uint)sobNpc.Id));
        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - (int)sobTarget.Damage, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);
        Assert.Empty(ParseLegacyBlackspotPackets(casterSent));
    }

    [Fact]
    public async Task HandlerMoveLineAgainstWildPhoenixPlayerConsumesDefenderChargeAndPreservesZeroDamageTarget()
    {
        const uint expectedPlayerDamage = 1U * 12 / 16;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMoveLineRow(),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [MoveLineSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 836,
            y: 584,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: MoveLineSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(842, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(MoveLineSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy MoveLine magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("totalDamage=0", output);
    }

    [Fact]
    public async Task HandlerNebulousHuntSectorBackAgainstWildPhoenixPlayerConsumesDefenderChargeAndKeepsZeroDamageTarget()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                NebulousHuntSpellId,
                sort: 69,
                useMana: 10,
                damage: 30_500,
                maxTargets: 15,
                range: 10,
                weaponType: 61_606_606,
                useStamina: 5,
                coolDown: 0,
                coldTime: 0,
                duration: 25_025,
                damageOnHuman: 25_025,
                damage2: 30_100,
                damage3: 25_025),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [NebulousHuntSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 836,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        const uint expectedPlayerDamage = 1U / 7;
        Assert.Equal(0U, expectedPlayerDamage);
        Assert.Equal(0x170, LegacyWildPhoenixFlag);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: NebulousHuntSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(842, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(NebulousHuntSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            Assert.Equal(0, spell.LevelHu);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.False(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("previousCharges=2", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("damageOverride=1", output);
        Assert.Contains("WorldServer handled legacy NebulousHunt SectorBack magic", output);
        Assert.Contains("spellId=10990", output);
        Assert.Contains("sort=69", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("totalAppliedDamage=0", output);
    }

    [Fact]
    public async Task HandlerMoveLineWithLearnedBlackSpotMarksSurvivingMonsterTargetsAndSendsActivePacket()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMoveLineRow(),
            CreateBlackSpotRow(duration: 30));
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(MoveLineSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: MoveLineSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(MoveLineSpellId, spell.SpellId);
        Assert.Equal(842, spell.X);
        Assert.Equal(584, spell.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(target.Damage > 0);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.True(visibleMonster.Hitpoints > 0);
        Assert.Equal(monster.MaxLife - target.Damage, visibleMonster.Hitpoints);
        Assert.True(monsterStates.IsBlackSpotted(monster, DateTimeOffset.UtcNow));

        var blackspotFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => frame.Header.Opcode == WorldPacketOpcodes.LegacyBlackspot);
        Assert.Equal((ushort)2188, blackspotFrame.Header.Opcode);
        Assert.True(LegacyBlackspotPacket.TryParse(blackspotFrame, out var blackspot));
        Assert.True(blackspot.Active);
        Assert.Equal(monster.Uid, blackspot.Uid);
    }

    [Fact]
    public async Task HandlerBlackSpotAgainstVisibleMonsterWithinCasterDistanceMarksBlackSpotAndSendsActiveDeadMark()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                level: 0,
                useMana: 10,
                damage: 120,
                experience: 100_000,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12),
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                level: 1,
                useMana: 10,
                damage: 120,
                experience: 100_000,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [BlackSpotSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: BlackSpotSpellId)).ToFrameBytes()));

        var spellFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => frame.Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.True(LegacySpellAnimationPacket.TryParse(spellFrame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(monster.X, spell.X);
        Assert.Equal(monster.Y, spell.Y);
        Assert.Equal(BlackSpotSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife - target.Damage, visibleMonster.Hitpoints);
        Assert.True(monsterStates.IsBlackSpotted(monster, DateTimeOffset.UtcNow));

        var blackspot = Assert.Single(ParseLegacyBlackspotPackets(sent));
        Assert.True(blackspot.Active);
        Assert.Equal(monster.Uid, blackspot.Uid);

        var spellUpdate = Assert.Single(
            sent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(BlackSpotSpellId, spellUpdate.Id);
        Assert.Equal(0, spellUpdate.Level);
        Assert.Equal(checked((int)target.Damage), spellUpdate.Experience);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(BlackSpotSpellId));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(BlackSpotSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);
        Assert.Equal(checked((int)target.Damage), updatedSpell.Experience);

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy BlackSpot monster magic", output);
        Assert.Contains("BlackSpot", output);
        Assert.Contains("spellId=11120", output);
        Assert.Contains("sort=58", output);
        Assert.Contains($"monsterUid={monster.Uid}", output);
        Assert.Contains("damage=", output);
        Assert.Contains("blackSpotExpiresAtUtc=", output);
        Assert.Contains("WorldServer updated legacy player magic spell", output);
        Assert.Contains($"appliedExperience={target.Damage}", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
    }

    [Fact]
    public async Task HandlerBlackSpotSkipsProtectedReviverMonsterTarget()
    {
        var reviverFamily = CreateReviverMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: reviverFamily.Id,
            sourceMonsterName: reviverFamily.Name);
        var worldData = CreateMagicCatalog(
            reviverFamily,
            [spawn],
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12));
        var reviver = CreateRuntimeMonsterInstance(worldData, spawn, reviverFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [BlackSpotSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                reviver,
                legacyEncodedQuery: false,
                spellId: BlackSpotSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(BlackSpotSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);
        Assert.Empty(ParseLegacyBlackspotPackets(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(reviver, DateTimeOffset.UtcNow, out var visibleReviver));
        Assert.Equal(reviver.MaxLife, visibleReviver.Hitpoints);
        Assert.False(monsterStates.IsBlackSpotted(reviver, DateTimeOffset.UtcNow));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped legacy BlackSpot monster magic", output);
        Assert.Contains("CanAttackMonster.Verified rejected Reviver monster family", output);
    }

    [Fact]
    public async Task HandlerBlackSpotAgainstVisibleMonsterOutsideCasterDistanceSpendsResourcesAndSendsEmptyAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, stamina: 100, spellIds: [BlackSpotSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: BlackSpotSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(monster.X, spell.X);
        Assert.Equal(monster.Y, spell.Y);
        Assert.Equal(BlackSpotSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);

        Assert.Empty(ParseLegacyBlackspotPackets(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.False(monsterStates.IsBlackSpotted(monster, DateTimeOffset.UtcNow));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(BlackSpotSpellId));
        Assert.DoesNotContain(
            sent,
            bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var output = log.ToString();
        Assert.Contains("BlackSpot", output);
        Assert.Contains("spellId=11120", output);
        Assert.DoesNotContain("WorldServer updated legacy player magic spell", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
    }

    [Fact]
    public async Task HandlerBlackSpotAgainstVisiblePlayerWithinCasterDistanceDamagesMarksBlackSpotAndSendsActivePacket()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [BlackSpotSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: BlackSpotSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(BlackSpotSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.True(target.Damage > 0);

        Assert.Contains(
            ParseLegacyBlackspotPackets(casterSent.Concat(targetSent)),
            packet => packet.Active && packet.Uid == TargetPlayerUid);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.True(updatedTargetSession.Character.MinHitPoints < targetSession.Character.MinHitPoints);
        Assert.Equal("1", updatedTargetSession.Character.Attributes["BlackSpot"]);
        Assert.True(DateTimeOffset.TryParse(
            updatedTargetSession.Character.Attributes["BlackSpotExpiresAtUtc"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var expiresAt));
        Assert.True(expiresAt > DateTimeOffset.UtcNow.AddSeconds(5));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy BlackSpot player magic", output);
        Assert.DoesNotContain("direct player damage/active-state branch is pending", output);
    }

    [Fact]
    public async Task HandlerBlackSpotAgainstWildPhoenixPlayerConsumesDefenderChargeDealsOneDamageAndMarksBlackSpot()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [BlackSpotSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 832,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: BlackSpotSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(checked((ushort)targetSession.Character.X), animation.X);
            Assert.Equal(checked((ushort)targetSession.Character.Y), animation.Y);
            Assert.Equal(BlackSpotSpellId, animation.SpellId);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(1U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.Contains(
            ParseLegacyBlackspotPackets(casterSent.Concat(targetSent)),
            packet => packet.Active && packet.Uid == TargetPlayerUid);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(99_999, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal("1", updatedTargetSession.Character.Attributes["BlackSpot"]);
        Assert.True(DateTimeOffset.TryParse(
            updatedTargetSession.Character.Attributes["BlackSpotExpiresAtUtc"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var expiresAt));
        Assert.True(expiresAt > DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy BlackSpot player magic", output);
        Assert.DoesNotContain("direct player damage/active-state branch is pending", output);
    }

    [Fact]
    public async Task HandlerBlackSpotAgainstVisibleTrainingSobNpcWithinCasterDistanceAppliesPhysicalDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12));
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: sobNpc.MapId,
            x: sobNpc.X - 1,
            y: sobNpc.Y,
            strength: 300,
            stamina: 100,
            spellIds: [BlackSpotSpellId]);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: BlackSpotSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(BlackSpotSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.True(target.Damage > 0);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(10_000 - (int)target.Damage, visibleSobNpc.Life);
        Assert.Equal(10_000, visibleSobNpc.MaxLife);
        Assert.Empty(ParseLegacyBlackspotPackets(sent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy BlackSpot SOB magic", output);
        Assert.DoesNotContain("direct SOB damage branch is pending", output);
    }

    [Fact]
    public async Task HandlerBlackSpotVisiblePlayerAndSobTargetsKeepPendingGapLogExplicitWhenModeled()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = new SobNpc(
            Id: 65_061,
            Name: "BlackSpotPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: 832,
            Y: 584,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 7,
            RawLine: "65061,BlackSpotPole,10,1137,1015,832,584,50000,50000,17,0",
            Fields: ["65061", "BlackSpotPole", "10", "1137", "1015", "832", "584", "50000", "50000", "17", "0"]);
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [BlackSpotSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: BlackSpotSpellId)).ToFrameBytes()));

        var sobSent = new List<byte[]>();
        using var sobLog = new StringWriter();
        var sobConnection = CreateSendingConnection(sobSent);
        var sobSessions = new WorldSessionStore();
        var sobSession = CreateSession(strength: 300, stamina: 100, spellIds: [BlackSpotSpellId]);
        sobSessions.Set(sobConnection, sobSession);
        var sobContext = new WorldPacketHandlerContext(
            sobConnection,
            sobLog,
            sobSession,
            WorldData: worldData,
            Sessions: sobSessions,
            SobNpcStates: new SobNpcRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            sobContext,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: BlackSpotSpellId)).ToFrameBytes()));

        var combinedLog = log.ToString() + "\n" + sobLog.ToString();
        var pendingBlackSpotLines =
            combinedLog
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Where(line =>
                    line.Contains("BlackSpot", StringComparison.OrdinalIgnoreCase) &&
                    line.Contains("pending", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        if (pendingBlackSpotLines.Length == 0)
        {
            return;
        }

        Assert.All(
            pendingBlackSpotLines,
            line => Assert.True(
                line.Contains("player", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("SOB", StringComparison.OrdinalIgnoreCase),
                line));
    }

    [Fact]
    public async Task HandlerAddBlackSpotWithLearnedBlackSpotMarksVisibleMonstersFromAttackPointAndSendsActivePackets()
    {
        var monsterFamily = CreateMonsterFamily();
        var centerSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584);
        var edgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 842, boundY: 584);
        var behindSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 830, boundY: 584);
        var outsideSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 843, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [centerSpawn, edgeSpawn, behindSpawn, outsideSpawn],
            CreateAddBlackSpotRow(),
            CreateBlackSpotRow(duration: 30));
        var centerMonster = CreateRuntimeMonsterInstance(worldData, centerSpawn, monsterFamily);
        var selectedMonsters = new[]
        {
            centerMonster,
            CreateRuntimeMonsterInstance(worldData, edgeSpawn, monsterFamily),
            CreateRuntimeMonsterInstance(worldData, behindSpawn, monsterFamily),
        };
        var outsideMonster = CreateRuntimeMonsterInstance(worldData, outsideSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                centerMonster,
                legacyEncodedQuery: false,
                spellId: AddBlackSpotSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(836, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(AddBlackSpotSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);

        var expectedTargetUids = selectedMonsters.Select(monster => monster.Uid).Order().ToArray();
        Assert.Equal(expectedTargetUids, spell.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.All(spell.Targets, target =>
        {
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        var activeBlackspots = ParseLegacyBlackspotPackets(sent)
            .Where(packet => packet.Active)
            .ToArray();
        Assert.Equal(expectedTargetUids, activeBlackspots.Select(packet => packet.Uid).Order().ToArray());
        var blackspotFrames = sent
            .Select(ReadFrame)
            .Where(frame => frame.Header.Opcode == WorldPacketOpcodes.LegacyBlackspot)
            .ToArray();
        Assert.Equal(expectedTargetUids.Length, blackspotFrames.Length);
        Assert.All(blackspotFrames, frame => Assert.Equal((ushort)2188, frame.Header.Opcode));

        foreach (var monster in selectedMonsters)
        {
            Assert.True(monsterStates.IsBlackSpotted(monster, DateTimeOffset.UtcNow));
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
            Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        }

        Assert.False(monsterStates.IsBlackSpotted(outsideMonster, DateTimeOffset.UtcNow));

        var output = log.ToString();
        Assert.Contains("AddBlackSpot", output);
        Assert.Contains("spellId=11100", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("pirate-special-runtime", output);
    }

    [Fact]
    public async Task HandlerAddBlackSpotSkipsProtectedReviverMonsterTargets()
    {
        var reviverFamily = CreateReviverMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: reviverFamily.Id,
            sourceMonsterName: reviverFamily.Name);
        var worldData = CreateMagicCatalog(
            reviverFamily,
            [spawn],
            CreateAddBlackSpotRow(),
            CreateBlackSpotRow(duration: 30));
        var reviver = CreateRuntimeMonsterInstance(worldData, spawn, reviverFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                reviver,
                legacyEncodedQuery: false,
                spellId: AddBlackSpotSpellId,
                x: reviver.X,
                y: reviver.Y)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(AddBlackSpotSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);
        Assert.Empty(ParseLegacyBlackspotPackets(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(reviver, DateTimeOffset.UtcNow, out var visibleReviver));
        Assert.Equal(reviver.MaxLife, visibleReviver.Hitpoints);
        Assert.False(monsterStates.IsBlackSpotted(reviver, DateTimeOffset.UtcNow));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AddBlackSpot monster/player magic", output);
        Assert.Contains("targets=0", output);
        Assert.Contains("playerTargets=0", output);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 4)]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    public async Task HandlerAddBlackSpotTargetBudgetFollowsAddBlackSpotSpellLevel(
        ushort addBlackSpotLevel,
        int expectedTargets)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawns = Enumerable.Range(0, 5)
            .Select(index => CreateSingleInstanceMonsterSpawn(
                generatorId: index + 1,
                boundX: 836 + index,
                boundY: 584))
            .ToArray();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            spawns,
            CreateAddBlackSpotRow(level: checked((byte)addBlackSpotLevel)),
            CreateBlackSpotRow(duration: 30));
        var monsters = spawns
            .Select(spawn => CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily))
            .ToArray();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId, addBlackSpotLevel),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monsters[0],
                legacyEncodedQuery: false,
                spellId: AddBlackSpotSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(AddBlackSpotSpellId, spell.SpellId);
        Assert.Equal(addBlackSpotLevel, spell.SpellLevel);
        Assert.Equal(expectedTargets, spell.Targets.Count);
        Assert.All(spell.Targets, target => Assert.Equal(0U, target.Damage));

        var expectedTargetUids = monsters
            .Take(expectedTargets)
            .Select(monster => monster.Uid)
            .Order()
            .ToArray();
        Assert.Equal(expectedTargetUids, spell.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Equal(
            expectedTargetUids,
            ParseLegacyBlackspotPackets(sent)
                .Where(packet => packet.Active)
                .Select(packet => packet.Uid)
                .Order()
                .ToArray());

        for (var index = 0; index < monsters.Length; index++)
        {
            Assert.Equal(
                index < expectedTargets,
                monsterStates.IsBlackSpotted(monsters[index], DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public async Task HandlerAddBlackSpotDurationUsesLearnedBlackSpotSpellInsteadOfAddBlackSpotRow()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateAddBlackSpotRow(duration: 1),
            CreateBlackSpotRow(duration: 45));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: AddBlackSpotSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        var blackspot = Assert.Single(ParseLegacyBlackspotPackets(sent));
        Assert.True(blackspot.Active);
        Assert.Equal(monster.Uid, blackspot.Uid);
        Assert.True(monsterStates.IsBlackSpotted(monster, DateTimeOffset.UtcNow.AddSeconds(2)));
        Assert.False(monsterStates.IsBlackSpotted(monster, DateTimeOffset.UtcNow.AddSeconds(50)));
    }

    [Fact]
    public async Task HandlerAddBlackSpotWithoutLearnedBlackSpotDoesNotSendActivePacketOrMarkMonsters()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateAddBlackSpotRow(),
            CreateBlackSpotRow(duration: 30));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: AddBlackSpotSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        Assert.DoesNotContain(ParseLegacyBlackspotPackets(sent), packet => packet.Active);
        Assert.False(monsterStates.IsBlackSpotted(monster, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task HandlerAddBlackSpotSelectsVisibleMonstersBeforePlayersAndIgnoresSobNpcsWhenBudgetIsFull()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawns = new[]
        {
            CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584),
            CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 837, boundY: 584),
            CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838, boundY: 584),
        };
        var sobNpc = new SobNpc(
            Id: 65_062,
            Name: "AddBlackSpotPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: 836,
            Y: 584,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 8,
            RawLine: "65062,AddBlackSpotPole,10,1137,1015,836,584,50000,50000,17,0",
            Fields: ["65062", "AddBlackSpotPole", "10", "1137", "1015", "836", "584", "50000", "50000", "17", "0"]);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns,
            [sobNpc],
            [
                CreateAddBlackSpotRow(),
                CreateBlackSpotRow(duration: 30),
            ]);
        var monsters = spawns
            .Select(spawn => CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily))
            .ToArray();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 836, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monsters[0],
                legacyEncodedQuery: false,
                spellId: AddBlackSpotSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        var expectedMonsterUids = monsters.Select(monster => monster.Uid).Order().ToArray();
        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(AddBlackSpotSpellId, casterSpell.SpellId);
        Assert.Equal(3, casterSpell.Targets.Count);
        Assert.Equal(expectedMonsterUids, casterSpell.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.DoesNotContain(casterSpell.Targets, target => target.Uid == TargetPlayerUid);
        Assert.DoesNotContain(casterSpell.Targets, target => target.Uid == checked((uint)sobNpc.Id));

        Assert.Equal(
            expectedMonsterUids,
            ParseLegacyBlackspotPackets(casterSent)
                .Where(packet => packet.Active)
                .Select(packet => packet.Uid)
                .Order()
                .ToArray());
    }

    [Fact]
    public async Task HandlerAddBlackSpotUsesLeftoverBudgetForVisiblePlayersAndStillIgnoresSobNpcs()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawns = new[]
        {
            CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 836, boundY: 584),
            CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 837, boundY: 584),
        };
        var sobNpc = new SobNpc(
            Id: 65_063,
            Name: "IgnoredAddBlackSpotPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: 836,
            Y: 584,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 9,
            RawLine: "65063,IgnoredAddBlackSpotPole,10,1137,1015,836,584,50000,50000,17,0",
            Fields: ["65063", "IgnoredAddBlackSpotPole", "10", "1137", "1015", "836", "584", "50000", "50000", "17", "0"]);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns,
            [sobNpc],
            [
                CreateAddBlackSpotRow(),
                CreateBlackSpotRow(duration: 30),
            ]);
        var monsters = spawns
            .Select(spawn => CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily))
            .ToArray();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 838, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monsters[0],
                legacyEncodedQuery: false,
                spellId: AddBlackSpotSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(AddBlackSpotSpellId, spell.SpellId);
        Assert.Equal(3, spell.Targets.Count);
        Assert.Contains(spell.Targets, target => target.Uid == TargetPlayerUid && target.Damage == 0);
        Assert.DoesNotContain(spell.Targets, target => target.Uid == checked((uint)sobNpc.Id));

        var expectedUids = monsters
            .Select(monster => monster.Uid)
            .Append(TargetPlayerUid)
            .Order()
            .ToArray();
        Assert.Equal(expectedUids, spell.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Equal(
            expectedUids,
            ParseLegacyBlackspotPackets(casterSent)
                .Where(packet => packet.Active)
                .Select(packet => packet.Uid)
                .Order()
                .ToArray());

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(targetSession.Character.MinHitPoints, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal("1", updatedTargetSession.Character.Attributes["BlackSpot"]);
        Assert.True(DateTimeOffset.TryParse(
            updatedTargetSession.Character.Attributes["BlackSpotExpiresAtUtc"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var expiresAt));
        Assert.True(expiresAt > DateTimeOffset.UtcNow.AddSeconds(20));

        foreach (var monster in monsters)
        {
            Assert.True(monsterStates.IsBlackSpotted(monster, DateTimeOffset.UtcNow));
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
            Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        }

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AddBlackSpot monster/player magic", output);
        Assert.Contains("playerTargets=1", output);
        Assert.DoesNotContain("player BlackSpot state branch is pending", output);
    }

    [Fact]
    public async Task HandlerRuntimeLoopExpiresPlayerBlackSpotAndSendsInactivePacketWhenDue()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateAddBlackSpotRow(),
            CreateBlackSpotRow(duration: 30));
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(35))),
            log);
        var casterSession = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        SetHandlerSession(handler, casterConnection, casterSession);
        SetHandlerSession(handler, targetConnection, targetSession);

        try
        {
            await handler.HandleFrameAsync(
                casterConnection,
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid,
                    OpponentUid: TargetPlayerUid,
                    X: checked((ushort)targetSession.Character.X),
                    Y: checked((ushort)targetSession.Character.Y),
                    AttackType: MsgAttackType.Magic,
                    Damage: 0,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0,
                    SpellId: AddBlackSpotSpellId)).ToFrameBytes()),
                cancel.Token);

            var updatedTargetSession = AssertHandlerSession(handler, targetConnection);
            Assert.Equal("1", updatedTargetSession.Character.Attributes["BlackSpot"]);
            Assert.True(DateTimeOffset.TryParse(
                updatedTargetSession.Character.Attributes["BlackSpotExpiresAtUtc"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var expiresAt));
            Assert.True(expiresAt > DateTimeOffset.UtcNow.AddSeconds(20));

            var activePackets = ParseLegacyBlackspotPackets(targetSent);
            Assert.Contains(activePackets, packet => packet.Uid == TargetPlayerUid && packet.Active);

            var runtimeStart = targetSent.Count;
            StartHandlerRuntimeUpdateLoop(handler, targetConnection, updatedTargetSession, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var frames = targetSent.ToArray().Skip(runtimeStart).ToArray();
                    return ParseLegacyBlackspotPackets(frames)
                        .Any(packet => packet.Uid == TargetPlayerUid && !packet.Active);
                },
                TimeSpan.FromSeconds(1));

            var framesAfterRuntimeStart = targetSent.ToArray().Skip(runtimeStart).ToArray();
            Assert.Contains(
                ParseLegacyBlackspotPackets(framesAfterRuntimeStart),
                packet => packet.Uid == TargetPlayerUid && !packet.Active);

            var expiredTargetSession = AssertHandlerSession(handler, targetConnection);
            Assert.False(expiredTargetSession.Character.Attributes.ContainsKey("BlackSpot"));
            Assert.False(expiredTargetSession.Character.Attributes.ContainsKey("BlackSpotExpiresAtUtc"));
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerRuntimeLoopExpiresMonsterBlackSpotAndSendsInactivePacketWhenDue()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 836, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateAddBlackSpotRow(),
            CreateBlackSpotRow(duration: 1));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(DateTimeOffset.UtcNow.AddSeconds(5))),
            log);
        var session = CreateSession(
            stamina: 100,
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        SetHandlerSession(handler, connection, session);

        try
        {
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                    monster,
                    legacyEncodedQuery: false,
                    spellId: AddBlackSpotSpellId,
                    x: 836,
                    y: 584)).ToFrameBytes()),
                cancel.Token);

            Assert.Contains(ParseLegacyBlackspotPackets(sent), packet => packet.Uid == monster.Uid && packet.Active);

            var runtimeStart = sent.Count;
            StartHandlerRuntimeUpdateLoop(handler, connection, AssertHandlerSession(handler, connection), cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var frames = sent.ToArray().Skip(runtimeStart).ToArray();
                    return ParseLegacyBlackspotPackets(frames)
                        .Any(packet => packet.Uid == monster.Uid && !packet.Active);
                },
                TimeSpan.FromSeconds(1));

            var framesAfterRuntimeStart = sent.ToArray().Skip(runtimeStart).ToArray();
            Assert.Contains(
                ParseLegacyBlackspotPackets(framesAfterRuntimeStart),
                packet => packet.Uid == monster.Uid && !packet.Active);
            Assert.Contains("WorldServer sent monster BlackSpot inactive", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    [Fact]
    public async Task HandlerCannonBarrageWithoutStatusFlagSpendsResourcesAndCooldownButDoesNotDamageOrAnimate()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 830, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                useMana: 10,
                range: 30,
                useStamina: 5));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [CannonBarrageSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: CannonBarrageSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(CannonBarrageSpellId));

        var output = log.ToString();
        Assert.Contains("skipped legacy CannonBarrage magic", output);
        Assert.Contains("CannonBarrage status flag is not active", output);
        Assert.DoesNotContain("WorldServer handled legacy CannonBarrage monster magic", output);
    }

    [Fact]
    public async Task HandlerScurvyBombLevelZeroHitsThreeVisibleMonstersAndSpendsResourcesOnce()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 814, boundWidth: 33, maxPerGenerator: 33);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                ScurvyBombSpellId,
                sort: 56,
                useMana: 10,
                range: 30,
                useStamina: 5));
        var monsters = CreateRuntimeMonsterInstances(worldData, spawn, monsterFamily);
        Assert.Equal(33, monsters.Count);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [ScurvyBombSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monsters[0],
                legacyEncodedQuery: false,
                spellId: ScurvyBombSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var spellFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => frame.Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.Equal((ushort)2239, spellFrame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(spellFrame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(830, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(ScurvyBombSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        Assert.Equal(3, spell.Targets.Count);

        var monsterByUid = monsters.ToDictionary(monster => monster.Uid);
        var damageByUid = spell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(3, damageByUid.Count);
        Assert.All(spell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.True(target.Damage > 0);
            Assert.True(monsterByUid.ContainsKey(target.Uid));
        });

        foreach (var monster in monsters)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
            if (damageByUid.TryGetValue(monster.Uid, out var damage))
            {
                Assert.Equal(monster.MaxLife - damage, visibleMonster.Hitpoints);
            }
            else
            {
                Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
            }
        }

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy", output);
        Assert.Contains("ScurvyBomb", output);
        Assert.Contains("spellId=11040", output);
        Assert.Contains("sort=56", output);
        Assert.Contains("targets=3", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
    }

    [Fact]
    public async Task HandlerScurvyBombLevelZeroHitsVisiblePlayerAppliesFatigueStatusAndSpendsResourcesOnce()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                ScurvyBombSpellId,
                sort: 56,
                useMana: 10,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [ScurvyBombSpellId]);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ScurvyBombSpellId)).ToFrameBytes()));

        var casterSpellFrame = Assert.Single(
            casterSent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        var targetSpellFrame = Assert.Single(
            targetSent.Select(ReadFrame),
            frame => LegacySpellAnimationPacket.TryParse(frame, out _));
        foreach (var frame in new[] { casterSpellFrame, targetSpellFrame })
        {
            Assert.Equal((ushort)2239, frame.Header.Opcode);
        }

        Assert.True(LegacySpellAnimationPacket.TryParse(casterSpellFrame, out var casterSpell));
        Assert.True(LegacySpellAnimationPacket.TryParse(targetSpellFrame, out var targetSpell));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(830, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(ScurvyBombSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        var appliedDamage = Assert.Single(casterSpell.Targets).Damage;
        Assert.Equal(appliedDamage, Assert.Single(targetSpell.Targets).Damage);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)appliedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyScurvyBombFlag));

        var casterUpdates = ParseMsgUpdates(casterSent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(casterUpdates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(casterUpdates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var targetUpdates = ParseMsgUpdates(targetSent);
        Assert.Contains(
            targetUpdates,
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyScurvyBombFlag)));
        Assert.Contains(
            targetUpdates,
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Value == 6 &&
                    entry.Type != LegacyMsgUpdateDataType.Hitpoints &&
                    entry.Type != LegacyMsgUpdateDataType.MaxHitpoints &&
                    entry.Type != LegacyMsgUpdateDataType.StatusFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy", output);
        Assert.Contains("ScurvyBomb", output);
        Assert.Contains("spellId=11040", output);
        Assert.Contains("sort=56", output);
        Assert.Contains("targets=1", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    [Fact]
    public async Task HandlerScurvyBombAgainstWildPhoenixPlayerConsumesDefenderChargeDealsTwoDamageAndAppliesFatigueStatus()
    {
        const uint expectedDamage = 2U;
        const ulong expectedFatigue = 6UL;
        const uint expectedFatigueFlag = 0x14U;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(
                ScurvyBombSpellId,
                sort: 56,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, stamina: 100, spellIds: [ScurvyBombSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 832,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyWildPhoenixFlag),
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ScurvyBombSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(830, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(ScurvyBombSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - (long)expectedDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.Equal(
            expectedFatigue.ToString(CultureInfo.InvariantCulture),
            updatedTargetSession.Character.Attributes["UseStamina"]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyScurvyBombFlag));

        var targetUpdates = ParseMsgUpdates(targetSent);
        var archiveEntry = Assert.Single(
            targetUpdates.SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        Assert.Contains(
            targetUpdates,
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyScurvyBombFlag)));
        Assert.Contains(
            targetUpdates.SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.Fatigue &&
                entry.Value == expectedFatigue &&
                entry.Flag == expectedFatigueFlag);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("previousCharges=2", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("damageOverride=1", output);
        Assert.Contains("WorldServer handled legacy ScurvyBomb monster/player/SOB magic", output);
        Assert.Contains("spellId=11040", output);
        Assert.Contains("sort=56", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("totalDamage=0", output);
    }

    [Fact]
    public async Task HandlerScurvyBombNatureShieldForcedSuccessBreaksPlayerLoopBeforeDamage()
    {
        const uint natureShieldRuneItemId = 4_038_109;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateItemType((int)natureShieldRuneItemId, "NatureShield(+9)")],
            CreateMagicRow(
                ScurvyBombSpellId,
                sort: 56,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var rollAttributes = new Dictionary<string, string>
        {
            ["LegacyScurvyBombNatureShieldRoll"] = "69",
        };
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ScurvyBombSpellId],
            attributes: rollAttributes);
        sessions.Set(casterConnection, casterSession);

        var targetConnections = new Dictionary<uint, ConquerTcpConnection>();
        var targetRuneItems = new Dictionary<uint, List<PlayerItemSnapshotItem>>();
        foreach (var (uid, name, x) in new (uint Uid, string Name, long X)[]
        {
            (TargetPlayerUid, "Protected", 831),
            (TargetPlayerUid + 1U, "LaterOne", 832),
            (TargetPlayerUid + 2U, "LaterTwo", 833),
        })
        {
            var targetSent = new List<byte[]>();
            var targetConnection = CreateSendingConnection(targetSent);
            var runeItems = new List<PlayerItemSnapshotItem>();
            targetConnections[uid] = targetConnection;
            targetRuneItems[uid] = runeItems;
            sessions.Set(
                targetConnection,
                CreateSession(
                    uid: uid,
                    name: name,
                    x: x,
                    y: 584,
                    spellIds: [ScurvyBombSpellId],
                    attributes: rollAttributes,
                    playerItems: CreatePlayerItems(runeItems)));
        }

        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(3, targetOrder.Count);
        var protectedUid = targetOrder[0];
        targetRuneItems[protectedUid].Add(new PlayerItemSnapshotItem
        {
            Uid = natureShieldRuneItemId,
            ItemId = natureShieldRuneItemId,
            Position = 101,
        });

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: protectedUid,
                X: 830,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ScurvyBombSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Empty(casterSpell.Targets);

        foreach (var targetUid in targetOrder)
        {
            Assert.True(sessions.TryGet(targetConnections[targetUid], out var updatedTargetSession));
            Assert.NotNull(updatedTargetSession);
            Assert.Equal(5_000, updatedTargetSession.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyScurvyBombFlag));
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);
    }

    [Fact]
    public async Task HandlerScurvyBombAgainstGuardMonsterInPkModeAppliesLegacyFlashingName()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(
            guardFamily,
            [spawn],
            CreateMagicRow(
                ScurvyBombSpellId,
                sort: 56,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var guard = CreateRuntimeMonsterInstance(worldData, spawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ScurvyBombSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                guard,
                legacyEncodedQuery: false,
                spellId: ScurvyBombSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(guard.Uid, target.Uid);
        Assert.True(target.Damage > 0);

        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);
    }

    [Fact]
    public async Task HandlerCannonBarrageAgainstGuardMonsterInPkModeAppliesLegacyFlashingName()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(
            guardFamily,
            [spawn],
            CreateMagicRow(
                CannonBarrageSpellId,
                sort: 57,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var guard = CreateRuntimeMonsterInstance(worldData, spawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [CannonBarrageSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
                ["StatusFlags"] = BuildStatusFlags(LegacyCannonBarrageFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                guard,
                legacyEncodedQuery: false,
                spellId: CannonBarrageSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(guard.Uid, target.Uid);
        Assert.True(target.Damage > 0);

        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);
    }

    [Fact]
    public async Task HandlerPirateXpSkillAgainstGuardMonsterInPkModeAppliesLegacyFlashingName()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(
            guardFamily,
            [spawn],
            CreateMagicRow(
                PirateXpSkillSpellId,
                sort: 63,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5));
        var guard = CreateRuntimeMonsterInstance(worldData, spawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [PirateXpSkillSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                guard,
                legacyEncodedQuery: false,
                spellId: PirateXpSkillSpellId,
                x: 830,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(guard.Uid, target.Uid);
        Assert.True(target.Damage > 0);

        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);
    }

    [Fact]
    public async Task HandlerBlackSpotAgainstGuardMonsterInPkModeAppliesLegacyFlashingName()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(
            guardFamily,
            [spawn],
            CreateMagicRow(
                BlackSpotSpellId,
                sort: 58,
                useMana: 10,
                damage: 120,
                range: 30,
                useStamina: 5,
                coldTime: 60_000,
                duration: 12));
        var guard = CreateRuntimeMonsterInstance(worldData, spawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [BlackSpotSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                guard,
                legacyEncodedQuery: false,
                spellId: BlackSpotSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(guard.Uid, target.Uid);
        Assert.True(target.Damage > 0);
        Assert.Contains(ParseLegacyBlackspotPackets(sent), packet => packet.Active && packet.Uid == guard.Uid);

        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);
    }

    [Fact]
    public async Task HandlerAddBlackSpotAgainstGuardMonsterInPkModeAppliesLegacyFlashingName()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            boundX: 836,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(
            guardFamily,
            [spawn],
            CreateAddBlackSpotRow(),
            CreateBlackSpotRow(duration: 30));
        var guard = CreateRuntimeMonsterInstance(worldData, spawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            },
            spellEntries:
            [
                CreatePlayerSpell(AddBlackSpotSpellId),
                CreatePlayerSpell(BlackSpotSpellId),
            ]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                guard,
                legacyEncodedQuery: false,
                spellId: AddBlackSpotSpellId,
                x: 836,
                y: 584)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(guard.Uid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.Contains(ParseLegacyBlackspotPackets(sent), packet => packet.Active && packet.Uid == guard.Uid);

        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);
    }

    [Fact]
    public async Task HandlerScurvyBombLevelZeroHitsVisibleDamageableSobNpcAndSpendsResourcesOnce()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateEventSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(
                ScurvyBombSpellId,
                sort: 56,
                useMana: 10,
                range: 30,
                useStamina: 5));
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(mapId: sobNpc.MapId, x: 84, y: 70, stamina: 100, spellIds: [ScurvyBombSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ScurvyBombSpellId)).ToFrameBytes()));

        var spellFrame = Assert.Single(
            sent.Select(ReadFrame),
            frame => frame.Header.Opcode == WorldPacketOpcodes.LegacySpellUse);
        Assert.Equal((ushort)2239, spellFrame.Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(spellFrame, out var spell));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(sobNpc.X, spell.X);
        Assert.Equal(sobNpc.Y, spell.Y);
        Assert.Equal(ScurvyBombSpellId, spell.SpellId);
        Assert.Equal(0, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var updates = ParseMsgUpdates(sent);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 490)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Mana).Updates);
        Assert.Equal(
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)],
            Assert.Single(updates, update => update.Uid == PlayerUid &&
                update.Updates.Count == 1 &&
                update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina).Updates);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - (int)target.Damage, visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy", output);
        Assert.Contains("ScurvyBomb", output);
        Assert.Contains("spellId=11040", output);
        Assert.Contains("sort=56", output);
        Assert.Contains("targets=1", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    [Fact]
    public async Task HandlerBossRandomSpellTableSelfHealUpdatesMonsterState()
    {
        var monsterFamily = CreateSnowBansheeBossFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: (int)monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        var damaged = monsterStates.ApplyDamage(monster, monster.MaxLife - 40_000U, now);
        Assert.Equal(40_000U, damaged.RemainingHitpoints);

        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(attributes: new Dictionary<string, string>
        {
            ["LegacyBossSpellIndex"] = "2",
        });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(monster.Uid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(monster.X, spell.X);
        Assert.Equal(monster.Y, spell.Y);
        Assert.Equal(10_372, spell.SpellId);

        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(80_000U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, now.AddSeconds(1), out var healedMonster));
        Assert.Equal(119_999U, healedMonster.Hitpoints);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(5_000, updatedSession.Character.MinHitPoints);
        Assert.Contains("WorldServer sent legacy boss self-heal spell", log.ToString());
        Assert.Contains("spellId=10372", log.ToString());
        Assert.Contains("forcedIndex=True", log.ToString());
    }

    [Fact]
    public async Task HandlerThrillingSpookBossSpellTableResistEarthSpellAppliesPhysicalDamage()
    {
        var monsterFamily = CreateThrillingSpookBossFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "1",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(monster.Uid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(830, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(11_311, spell.SpellId);

        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.ResistEarth, target.Effect);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(20_000U - target.Damage, (uint)updatedSession.Character.MinHitPoints);
        Assert.Contains("WorldServer sent legacy boss physical spell retaliation", log.ToString());
        Assert.Contains("spellId=11311", log.ToString());
        Assert.Contains("forcedIndex=True", log.ToString());
        Assert.DoesNotContain("boss spellId=11311 is not modeled yet", log.ToString());
    }

    [Theory]
    [InlineData(0, 8_003)]
    [InlineData(2, 10_364)]
    public async Task HandlerThrillingSpookBossSpellTablePhysicalSpellsApplyDamageWithoutElementEffect(
        int forcedBossSpellIndex,
        ushort expectedSpellId)
    {
        var monsterFamily = CreateThrillingSpookBossFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = forcedBossSpellIndex.ToString(CultureInfo.InvariantCulture),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(monster.Uid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(830, spell.X);
        Assert.Equal(584, spell.Y);
        Assert.Equal(expectedSpellId, spell.SpellId);

        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(20_000U - target.Damage, (uint)updatedSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss physical spell retaliation", output);
        Assert.Contains($"spellId={expectedSpellId}", output);
        Assert.Contains("forcedIndex=True", output);
        Assert.DoesNotContain($"boss spellId={expectedSpellId} is not modeled yet", output);
    }

    [Theory]
    [InlineData(999_996_641u, 0, 13_190)]
    [InlineData(6_643u, 1, 10_502)]
    [InlineData(7_981u, 0, 10_000)]
    [InlineData(20_055u, 1, 10_001)]
    [InlineData(3_971u, 0, 7_013)]
    [InlineData(40_947u, 3, 7_016)]
    [InlineData(213_883u, 3, 10_531)]
    public async Task HandlerAdditionalBossSpellTableUsesPhysicalSpellsAfterPlayerHit(
        uint bossMonsterId,
        int forcedBossSpellIndex,
        ushort expectedSpellId)
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: (int)bossMonsterId,
            sourceMonsterName: $"Boss-{bossMonsterId}");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var now = DateTimeOffset.Parse("2026-06-07T00:00:00Z");
        if (expectedSpellId == 7_016)
        {
            var damaged = monsterStates.ApplyDamage(monster, monster.MaxLife - 40_000U, now);
            Assert.Equal(40_000U, damaged.RemainingHitpoints);
        }
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = forcedBossSpellIndex.ToString(CultureInfo.InvariantCulture),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(monster.Uid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(monster.X, spell.X);
        Assert.Equal(monster.Y, spell.Y);
        Assert.Equal(expectedSpellId, spell.SpellId);
        var output = log.ToString();

        var target = Assert.Single(spell.Targets);
        if (expectedSpellId == 7_016)
        {
            Assert.Equal(monster.Uid, target.Uid);
            Assert.Equal(80_000U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
            Assert.True(monsterStates.TryGetVisibleInstance(monster, now.AddSeconds(1), out var healedMonster));
            Assert.Equal(119_999U, healedMonster.Hitpoints);
            Assert.Contains("WorldServer sent legacy boss self-heal spell", output);
            Assert.DoesNotContain(
                "WorldServer sent legacy boss physical spell retaliation",
                output);
        }
        else
        {
            Assert.Equal(PlayerUid, target.Uid);
            Assert.True(target.Damage > 0);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
            if (bossMonsterId is 3_971 or 213_883)
            {
                Assert.Contains("WorldServer sent legacy boss area physical spell retaliation", output);
            }
            else
            {
                Assert.Contains("WorldServer sent legacy boss physical spell retaliation", output);
            }
        }

        Assert.Contains($"spellId={expectedSpellId}", output);
        Assert.Contains("forcedIndex=True", output);
        Assert.DoesNotContain($"boss spellId={expectedSpellId} is not modeled yet", output);
    }

    [Fact]
    public async Task HandlerMonster6643BossSpell10500AppliesHundredThirtyPercentPhysicalDamage()
    {
        // Compare the multiplier on the same underlying attack draw. Selecting
        // only the boss spell does not pin PhysicalAttack's independent RNG.
        var attributes = new Dictionary<string, string>
        {
            ["LegacyMonsterPhysicalAttackRoll"] = "3",
            ["LegacyBossStatusRoll"] = "99",
            ["LegacyBossStatusSecondaryRoll"] = "99",
        };
        var boosted = await SendMonster6643BossSpellRetaliationAsync(forcedBossSpellIndex: 0, attributes);
        var normal = await SendMonster6643BossSpellRetaliationAsync(forcedBossSpellIndex: 1, attributes);

        Assert.Equal(10_500, boosted.SpellId);
        Assert.Equal(10_502, normal.SpellId);
        Assert.Equal((uint)(((ulong)normal.Damage * 130UL) / 100UL), boosted.Damage);
        Assert.Equal(50_000U - boosted.Damage, boosted.RemainingHitpoints);
        Assert.Equal(50_000U - normal.Damage, normal.RemainingHitpoints);
        Assert.Contains("WorldServer sent legacy boss physical spell retaliation", boosted.Output);
        Assert.Contains("spellId=10500", boosted.Output);
        Assert.Contains("forcedIndex=True", boosted.Output);
        Assert.DoesNotContain("boss spellId=10500 is not modeled yet", boosted.Output);
    }

    [Fact]
    public async Task HandlerMonster213883BossSpell10362AppliesTargetCenteredAreaFrightened()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 213_883,
            sourceMonsterName: "Boss-213883");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var farSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var peerConnection = CreateSendingConnection(peerSent);
        var farConnection = CreateSendingConnection(farSent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "2",
                ["LegacyBossStatusRoll"] = "0",
            });
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            x: 839,
            y: 584,
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
            });
        var farSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "Far",
            x: 841,
            y: 584,
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
            });
        sessions.Set(connection, session);
        sessions.Set(peerConnection, peerSession);
        sessions.Set(farConnection, farSession);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(10_362, spell.SpellId);
        Assert.Equal(session.Character.X, spell.X);
        Assert.Equal(session.Character.Y, spell.Y);
        Assert.Equal(2, spell.Targets.Count);
        Assert.Contains(spell.Targets, target => target.Uid == PlayerUid && target.Damage > 0);
        Assert.Contains(spell.Targets, target => target.Uid == TargetPlayerUid && target.Damage > 0);
        Assert.DoesNotContain(spell.Targets, target => target.Uid == TargetPlayerUid + 1);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(sessions.TryGet(peerConnection, out var updatedPeerSession));
        Assert.NotNull(updatedPeerSession);
        Assert.True(sessions.TryGet(farConnection, out var updatedFarSession));
        Assert.NotNull(updatedFarSession);
        Assert.True(updatedSession.Character.MinHitPoints < 20_000);
        Assert.True(updatedPeerSession.Character.MinHitPoints < 20_000);
        Assert.Equal(20_000, updatedFarSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFrightenedFlag));
        Assert.True(HasLegacyStatusFlag(updatedPeerSession.Character.Attributes, LegacyFrightenedFlag));
        Assert.False(HasLegacyStatusFlag(updatedFarSession.Character.Attributes, LegacyFrightenedFlag));

        var updates = ParseGameUpdates(sent);
        Assert.Contains(
            updates,
            update => update.Uid == PlayerUid &&
                update.Type == LegacyGameUpdateDataType.Flustered &&
                update.Time == 3U);
        Assert.Contains(
            updates,
            update => update.Uid == TargetPlayerUid &&
                update.Type == LegacyGameUpdateDataType.Flustered &&
                update.Time == 3U);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss area physical spell retaliation", output);
        Assert.Contains("spellId=10362", output);
        Assert.Contains("targets=2", output);
        Assert.Contains("radius=10", output);
        Assert.Contains("statusApplied=2", output);
        Assert.DoesNotContain("boss spellId=10362 is not modeled yet", output);
    }

    [Fact]
    public async Task HandlerMonster3971BossSpell7013AppliesRadiusSixAreaFrightened()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 3_971,
            sourceMonsterName: "Boss-3971");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var farSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var peerConnection = CreateSendingConnection(peerSent);
        var farConnection = CreateSendingConnection(farSent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "0",
                ["LegacyBossStatusRoll"] = "0",
            });
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            x: 836,
            y: 584,
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
            });
        var farSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "Far",
            x: 837,
            y: 584,
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
            });
        sessions.Set(connection, session);
        sessions.Set(peerConnection, peerSession);
        sessions.Set(farConnection, farSession);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count > 0, log.ToString());
        var spell = Assert.Single(spells);
        Assert.Equal(7_013, spell.SpellId);
        Assert.Equal(session.Character.X, spell.X);
        Assert.Equal(session.Character.Y, spell.Y);
        Assert.Equal(2, spell.Targets.Count);
        Assert.Contains(spell.Targets, target => target.Uid == PlayerUid && target.Damage > 0);
        Assert.Contains(spell.Targets, target => target.Uid == TargetPlayerUid && target.Damage > 0);
        Assert.DoesNotContain(spell.Targets, target => target.Uid == TargetPlayerUid + 1);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(sessions.TryGet(peerConnection, out var updatedPeerSession));
        Assert.NotNull(updatedPeerSession);
        Assert.True(sessions.TryGet(farConnection, out var updatedFarSession));
        Assert.NotNull(updatedFarSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFrightenedFlag));
        Assert.True(HasLegacyStatusFlag(updatedPeerSession.Character.Attributes, LegacyFrightenedFlag));
        Assert.False(HasLegacyStatusFlag(updatedFarSession.Character.Attributes, LegacyFrightenedFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss area physical spell retaliation", output);
        Assert.Contains("spellId=7013", output);
        Assert.Contains("targets=2", output);
        Assert.Contains("radius=6", output);
        Assert.Contains("statusApplied=2", output);
    }

    [Fact]
    public async Task HandlerMonster3971BossAreaSpellAppliesCheckForOpponentDeadSideEffects()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 3_971,
            sourceMonsterName: "Boss-3971");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "0",
                ["LegacyBossStatusRoll"] = "99",
                ["ActivePick"] = "1",
                ["Action"] = "250",
                ["Stamina"] = "15",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(7_013, spell.SpellId);
        Assert.Single(spell.Targets);

        Assert.Contains(
            ParseMsgActions(sent),
            action =>
                action.ObjId == PlayerUid &&
                action.DwParam == 220U &&
                action.Type == MsgActionType.ActionPick &&
                action.Facing == 2 &&
                action.Strings.SequenceEqual(["Interrupted"]));
        Assert.Contains(
            ParseMsgUpdates(sent),
            update =>
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Stamina &&
                    entry.Value == 0UL));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.False(updatedSession.Character.Attributes.ContainsKey("ActivePick"));
        Assert.Equal("0", updatedSession.Character.Attributes["Action"]);
        Assert.Equal("0", updatedSession.Character.Attributes["Stamina"]);
        Assert.True(updatedSession.Character.MinHitPoints < 20_000);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy monster hit ActivePick interruption", output);
        Assert.Contains("WorldServer applied legacy monster hit sit interruption", output);
        Assert.Contains("WorldServer sent legacy boss area physical spell retaliation", output);
    }

    [Fact]
    public async Task HandlerMonster3971BossSpell7017AppliesRadiusEightAreaDizzy()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 3_971,
            sourceMonsterName: "Boss-3971");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var farSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var peerConnection = CreateSendingConnection(peerSent);
        var farConnection = CreateSendingConnection(farSent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "4",
                ["LegacyBossStatusRoll"] = "0",
            });
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            x: 838,
            y: 584,
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
            });
        var farSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "Far",
            x: 839,
            y: 584,
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
            });
        sessions.Set(connection, session);
        sessions.Set(peerConnection, peerSession);
        sessions.Set(farConnection, farSession);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(7_017, spell.SpellId);
        Assert.Equal(session.Character.X, spell.X);
        Assert.Equal(session.Character.Y, spell.Y);
        Assert.Equal(2, spell.Targets.Count);
        Assert.Contains(spell.Targets, target => target.Uid == PlayerUid && target.Damage > 0);
        Assert.Contains(spell.Targets, target => target.Uid == TargetPlayerUid && target.Damage > 0);
        Assert.DoesNotContain(spell.Targets, target => target.Uid == TargetPlayerUid + 1);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(sessions.TryGet(peerConnection, out var updatedPeerSession));
        Assert.NotNull(updatedPeerSession);
        Assert.True(sessions.TryGet(farConnection, out var updatedFarSession));
        Assert.NotNull(updatedFarSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyDizzyFlag));
        Assert.True(HasLegacyStatusFlag(updatedPeerSession.Character.Attributes, LegacyDizzyFlag));
        Assert.False(HasLegacyStatusFlag(updatedFarSession.Character.Attributes, LegacyDizzyFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss area physical spell retaliation", output);
        Assert.Contains("spellId=7017", output);
        Assert.Contains("targets=2", output);
        Assert.Contains("radius=8", output);
        Assert.Contains("statusApplied=2", output);
    }

    [Fact]
    public async Task HandlerMonster7981BossSpell10001AppliesSixSecondFrightened()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 7_981,
            sourceMonsterName: "Boss-7981");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "1",
                ["LegacyBossStatusRoll"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeAttack = DateTimeOffset.UtcNow;
        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);
        var afterAttack = DateTimeOffset.UtcNow;

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(10_001, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.True(target.Damage > 0);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFrightenedFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyDizzyFlag));
        AssertLegacyStatusLifetimeMetadata(
            updatedSession.Character.Attributes,
            RuntimeLegacyFrightenedExpiresAtUtcAttribute,
            RuntimeLegacyFrightenedRemoveOnDeadAttribute,
            beforeAttack,
            afterAttack,
            expectedDurationSeconds: 6);

        var gameUpdate = Assert.Single(ParseGameUpdates(sent));
        Assert.Equal(PlayerUid, gameUpdate.Uid);
        Assert.Equal(LegacyGameUpdateDataType.Flustered, gameUpdate.Type);
        Assert.True(gameUpdate.Active);
        Assert.Equal(6U, gameUpdate.Time);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy boss status effect", output);
        Assert.Contains("spellId=10001", output);
        Assert.Contains($"statusFlag={LegacyFrightenedFlag}", output);
        Assert.Contains("seconds=6", output);
        Assert.Contains("statusApplied=True", output);
    }

    [Fact]
    public async Task HandlerMonster7981BossSpell10003AppliesAreaDamageAndDizzy()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 7_981,
            sourceMonsterName: "Boss-7981");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var farSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var peerConnection = CreateSendingConnection(peerSent);
        var farConnection = CreateSendingConnection(farSent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "2",
                ["LegacyBossStatusRoll"] = "0",
            });
        var peerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Peer",
            x: 832,
            y: 584,
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
            });
        var farSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "Far",
            x: 834,
            y: 584,
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
            });
        sessions.Set(connection, session);
        sessions.Set(peerConnection, peerSession);
        sessions.Set(farConnection, farSession);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(10_003, spell.SpellId);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(monster.X, spell.X);
        Assert.Equal(monster.Y, spell.Y);
        Assert.Equal(2, spell.Targets.Count);
        Assert.Contains(spell.Targets, target => target.Uid == PlayerUid && target.Damage > 0);
        Assert.Contains(spell.Targets, target => target.Uid == TargetPlayerUid && target.Damage > 0);
        Assert.DoesNotContain(spell.Targets, target => target.Uid == TargetPlayerUid + 1);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(sessions.TryGet(peerConnection, out var updatedPeerSession));
        Assert.NotNull(updatedPeerSession);
        Assert.True(sessions.TryGet(farConnection, out var updatedFarSession));
        Assert.NotNull(updatedFarSession);
        Assert.True(updatedSession.Character.MinHitPoints < 20_000);
        Assert.True(updatedPeerSession.Character.MinHitPoints < 20_000);
        Assert.Equal(20_000, updatedFarSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyDizzyFlag));
        Assert.True(HasLegacyStatusFlag(updatedPeerSession.Character.Attributes, LegacyDizzyFlag));
        Assert.False(HasLegacyStatusFlag(updatedFarSession.Character.Attributes, LegacyDizzyFlag));

        var updates = ParseGameUpdates(sent);
        Assert.Contains(
            updates,
            update => update.Uid == PlayerUid &&
                update.Type == LegacyGameUpdateDataType.Stun &&
                update.Time == 3U);
        Assert.Contains(
            updates,
            update => update.Uid == TargetPlayerUid &&
                update.Type == LegacyGameUpdateDataType.Stun &&
                update.Time == 3U);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss area physical spell retaliation", output);
        Assert.Contains("spellId=10003", output);
        Assert.Contains("targets=2", output);
        Assert.Contains("statusApplied=2", output);
        Assert.DoesNotContain("boss spellId=10003 is not modeled yet", output);
    }

    [Fact]
    public async Task HandlerMonster6643BossPrimaryStatusRollAppliesSixSecondFrightened()
    {
        var beforeAttack = DateTimeOffset.UtcNow;
        var result = await SendMonster6643BossSpellRetaliationAsync(
            forcedBossSpellIndex: 1,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "0",
                ["LegacyBossStatusSecondaryRoll"] = "99",
            });
        var afterAttack = DateTimeOffset.UtcNow;

        Assert.Equal(10_502, result.SpellId);
        Assert.True(HasLegacyStatusFlag(result.Attributes, LegacyFrightenedFlag));
        Assert.False(HasLegacyStatusFlag(result.Attributes, LegacyDizzyFlag));
        AssertLegacyStatusLifetimeMetadata(
            result.Attributes,
            RuntimeLegacyFrightenedExpiresAtUtcAttribute,
            RuntimeLegacyFrightenedRemoveOnDeadAttribute,
            beforeAttack,
            afterAttack,
            expectedDurationSeconds: 6);

        var gameUpdate = Assert.Single(result.GameUpdates);
        Assert.Equal(PlayerUid, gameUpdate.Uid);
        Assert.Equal(LegacyGameUpdateDataType.Flustered, gameUpdate.Type);
        Assert.True(gameUpdate.Active);
        Assert.Equal(6U, gameUpdate.Time);

        Assert.Contains("WorldServer applied legacy boss status effect", result.Output);
        Assert.Contains($"statusFlag={LegacyFrightenedFlag}", result.Output);
        Assert.Contains("seconds=6", result.Output);
        Assert.Contains("forcedStatusRoll=True", result.Output);
        Assert.Contains("statusApplied=True", result.Output);
    }

    [Fact]
    public async Task HandlerMonster6643BossSecondaryStatusRollAppliesDizzyWhenPrimaryFails()
    {
        var beforeAttack = DateTimeOffset.UtcNow;
        var result = await SendMonster6643BossSpellRetaliationAsync(
            forcedBossSpellIndex: 1,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "99",
                ["LegacyBossStatusSecondaryRoll"] = "0",
            });
        var afterAttack = DateTimeOffset.UtcNow;

        Assert.Equal(10_502, result.SpellId);
        Assert.False(HasLegacyStatusFlag(result.Attributes, LegacyFrightenedFlag));
        Assert.True(HasLegacyStatusFlag(result.Attributes, LegacyDizzyFlag));
        AssertLegacyStatusLifetimeMetadata(
            result.Attributes,
            RuntimeLegacyDustSigilStunnExpiresAtUtcAttribute,
            RuntimeLegacyDustSigilStunnRemoveOnDeadAttribute,
            beforeAttack,
            afterAttack,
            expectedDurationSeconds: 3);

        var gameUpdate = Assert.Single(result.GameUpdates);
        Assert.Equal(PlayerUid, gameUpdate.Uid);
        Assert.Equal(LegacyGameUpdateDataType.Stun, gameUpdate.Type);
        Assert.True(gameUpdate.Active);
        Assert.Equal(3U, gameUpdate.Time);

        Assert.Contains("WorldServer applied legacy boss status effect", result.Output);
        Assert.Contains($"statusFlag={LegacyDizzyFlag}", result.Output);
        Assert.Contains("seconds=3", result.Output);
        Assert.Contains("forcedStatusRoll=True", result.Output);
        Assert.Contains("forcedSecondaryStatusRoll=True", result.Output);
        Assert.Contains("statusApplied=True", result.Output);
    }

    [Fact]
    public async Task HandlerMonster6643BossSecondaryStatusRollSkipsWhenTargetAlreadyFrightened()
    {
        var result = await SendMonster6643BossSpellRetaliationAsync(
            forcedBossSpellIndex: 1,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossStatusRoll"] = "99",
                ["LegacyBossStatusSecondaryRoll"] = "0",
                ["StatusFlags"] = BuildStatusFlags(LegacyFrightenedFlag),
            });

        Assert.Equal(10_502, result.SpellId);
        Assert.True(HasLegacyStatusFlag(result.Attributes, LegacyFrightenedFlag));
        Assert.False(HasLegacyStatusFlag(result.Attributes, LegacyDizzyFlag));
        Assert.Empty(result.GameUpdates);

        Assert.Contains("WorldServer sent legacy boss physical spell retaliation", result.Output);
        Assert.Contains("spellId=10502", result.Output);
        Assert.Contains("statusApplied=False", result.Output);
        Assert.DoesNotContain("WorldServer applied legacy boss status effect", result.Output);
    }

    private static async Task SendPlayerHitThenScheduledBossRetaliationAsync(
        WorldPacketHandlerContext context,
        IReadOnlyList<byte[]> sent,
        LegacyMonsterInstance monster,
        Monster monsterFamily)
    {
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 1,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        AssertSinglePhysicalAttack(sent, monster.Uid);
        Assert.DoesNotContain(
            ParseSpellAnimations(sent),
            animation => animation.AttackerUid == monster.Uid);

        Assert.NotNull(context.Sessions);
        Assert.True(context.Sessions.TryGet(context.Connection, out var currentSession));
        Assert.NotNull(currentSession);
        Assert.NotNull(context.MonsterStates);
        Assert.True(context.MonsterStates.TryGetVisibleInstance(
            monster,
            DateTimeOffset.UtcNow,
            out var visibleMonster));

        await WorldPacketHandlers.TrySendLegacyMonsterRetaliationAsync(
            context with { Session = currentSession },
            currentSession,
            visibleMonster,
            monsterFamily,
            CancellationToken.None);
    }

    private static async Task<BossSpellRetaliationResult> SendMonster6643BossSpellRetaliationAsync(
        int forcedBossSpellIndex,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 6_643,
            sourceMonsterName: "Boss-6643");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var runtimeAttributes = new Dictionary<string, string>
        {
            ["LegacyBossSpellIndex"] = forcedBossSpellIndex.ToString(CultureInfo.InvariantCulture),
        };
        if (attributes is not null)
        {
            foreach (var pair in attributes)
            {
                runtimeAttributes[pair.Key] = pair.Value;
            }
        }

        var session = CreateSession(
            minHitPoints: 50_000,
            attributes: runtimeAttributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(monster.Uid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(monster.X, spell.X);
        Assert.Equal(monster.Y, spell.Y);

        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.True(target.Damage > 0);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new BossSpellRetaliationResult(
            spell.SpellId,
            target.Damage,
            checked((uint)updatedSession.Character.MinHitPoints),
            updatedSession.Character.Attributes,
            ParseGameUpdates(sent),
            log.ToString());
    }

    [Fact]
    public async Task HandlerBansheeTyrantBossStatusRollAppliesDizzyGameUpdate()
    {
        var monsterFamily = CreateSnowBansheeBossFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: (int)monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "0",
                ["LegacyBossStatusRoll"] = "0",
                ["LegacyMonsterPhysicalAttackRoll"] = "0",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        var beforeAttack = DateTimeOffset.UtcNow;
        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);
        var afterAttack = DateTimeOffset.UtcNow;

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(30_013, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(17_600U, target.Damage);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(20_000U - target.Damage, (uint)updatedSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyDizzyFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFrightenedFlag));
        AssertLegacyStatusLifetimeMetadata(
            updatedSession.Character.Attributes,
            RuntimeLegacyDustSigilStunnExpiresAtUtcAttribute,
            RuntimeLegacyDustSigilStunnRemoveOnDeadAttribute,
            beforeAttack,
            afterAttack,
            expectedDurationSeconds: 3);

        var updates = ParseMsgUpdates(sent);
        Assert.Contains(
            updates,
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyDizzyFlag)));

        var gameUpdate = Assert.Single(ParseGameUpdates(sent));
        Assert.Equal(PlayerUid, gameUpdate.Uid);
        Assert.Equal(LegacyGameUpdateDataType.Stun, gameUpdate.Type);
        Assert.True(gameUpdate.Active);
        Assert.Equal(3U, gameUpdate.Time);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy boss status effect", output);
        Assert.Contains("spellId=30013", output);
        Assert.Contains($"statusFlag={LegacyDizzyFlag}", output);
        Assert.Contains("forcedStatusRoll=True", output);
        Assert.Contains("statusApplied=True", output);
    }

    [Fact]
    public async Task HandlerBansheeTyrantBossSuccessfulStatusRollDoesNotRefreshExistingDizzyOrResendStatusPackets()
    {
        var originalDizzyExpiresAtUtc = DateTimeOffset.UtcNow
            .AddMinutes(5)
            .ToString("O", CultureInfo.InvariantCulture);
        var originalDizzyRemoveOnDead = bool.TrueString;
        var monsterFamily = CreateSnowBansheeBossFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: (int)monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "0",
                ["LegacyBossStatusRoll"] = "0",
                ["LegacyMonsterPhysicalAttackRoll"] = "0",
                ["StatusFlags"] = BuildStatusFlags(LegacyDizzyFlag),
                [RuntimeLegacyDustSigilStunnExpiresAtUtcAttribute] = originalDizzyExpiresAtUtc,
                [RuntimeLegacyDustSigilStunnRemoveOnDeadAttribute] = originalDizzyRemoveOnDead,
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(30_013, spell.SpellId);
        Assert.Equal(17_600U, Assert.Single(spell.Targets).Damage);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(2_400, updatedSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyDizzyFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFrightenedFlag));
        Assert.Equal(
            originalDizzyExpiresAtUtc,
            updatedSession.Character.Attributes[RuntimeLegacyDustSigilStunnExpiresAtUtcAttribute]);
        Assert.Equal(
            originalDizzyRemoveOnDead,
            updatedSession.Character.Attributes[RuntimeLegacyDustSigilStunnRemoveOnDeadAttribute]);

        Assert.DoesNotContain(
            ParseMsgUpdates(sent),
            update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        Assert.DoesNotContain(
            ParseGameUpdates(sent),
            update => update.Type == LegacyGameUpdateDataType.Stun);

        var output = log.ToString();
        Assert.Contains("spellId=30013", output);
        Assert.Contains("statusApplied=False", output);
        Assert.DoesNotContain("WorldServer applied legacy boss status effect", output);
    }

    [Fact]
    public async Task HandlerBossStatusGuardSkipsFrightenedWhenTargetAlreadyDizzy()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 3_971,
            sourceMonsterName: "Boss-3971");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minHitPoints: 20_000,
            attributes: new Dictionary<string, string>
            {
                ["LegacyBossSpellIndex"] = "0",
                ["LegacyBossStatusRoll"] = "0",
                ["StatusFlags"] = BuildStatusFlags(LegacyDizzyFlag),
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await SendPlayerHitThenScheduledBossRetaliationAsync(
            context,
            sent,
            monster,
            monsterFamily);

        var spells = ParseSpellAnimations(sent);
        Assert.True(spells.Count > 0, log.ToString());
        var spell = Assert.Single(spells);
        Assert.Equal(7_013, spell.SpellId);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyDizzyFlag));
        Assert.False(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFrightenedFlag));

        Assert.DoesNotContain(
            ParseGameUpdates(sent),
            update => update.Type == LegacyGameUpdateDataType.Flustered);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy boss area physical spell retaliation", output);
        Assert.Contains("spellId=7013", output);
        Assert.Contains("statusApplied=0", output);
        Assert.DoesNotContain("WorldServer applied legacy boss status effect", output);
    }

    [Fact]
    public async Task HandlerBoss20055DeathAwardsLegacyStudyPointsToKillerAndTeammate()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 20_055,
            sourceMonsterName: "TeratoDragon",
            life: "1");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var sent = new List<byte[]>();
        var teammateSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var teammateConnection = CreateSendingConnection(teammateSent);
        var session = CreateSession(
            name: "Leader",
            attributes: new Dictionary<string, string>
            {
                ["SubProfInfo"] = "5#0#0#",
            });
        var teammateSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Mate",
            x: 831,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["SubProfInfo"] = "7#0#0#",
            });
        sessions.Set(connection, session);
        sessions.Set(teammateConnection, teammateSession);
        teamStates.CreateTeam(connection, PlayerUid);
        Assert.True(teamStates.TryAddMember(connection, teammateConnection, TargetPlayerUid));

        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 200,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(sessions.TryGet(teammateConnection, out var updatedTeammateSession));
        Assert.NotNull(updatedTeammateSession);
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updatedSession.Character.Attributes["SubProfInfo"],
            out var updatedState));
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updatedTeammateSession.Character.Attributes["SubProfInfo"],
            out var updatedTeammateState));
        Assert.Equal(25, updatedState.StudyPoints);
        Assert.Equal(27, updatedTeammateState.StudyPoints);

        Assert.Contains(ParseLegacyMessages(sent), message =>
            message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            message.Content[3] == "You received 20 StudyPoints.");
        Assert.Contains(ParseLegacyMessages(teammateSent), message =>
            message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            message.Content[3] == "You received 20 StudyPoints.");
        Assert.Contains(ParseLegacyMessages(sent), message =>
            message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            message.Color == LegacyMsgMessagePacket.RedColor &&
            message.Content[3] == "The TeratoDragon has been destroyed by the team Leader`s ! All team members received 20 Study Points!");
        Assert.Contains(ParseLegacyMessages(teammateSent), message =>
            message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            message.Color == LegacyMsgMessagePacket.RedColor &&
            message.Content[3] == "The TeratoDragon has been destroyed by the team Leader`s ! All team members received 20 Study Points!");

        Assert.Contains(ParseLegacyStrings(sent), packet =>
            packet.Uid == PlayerUid &&
            packet.Id == LegacyStringId.Effect &&
            packet.Strings.SequenceEqual(["zf2-e300"]));
        Assert.Contains(ParseLegacyStrings(teammateSent), packet =>
            packet.Uid == TargetPlayerUid &&
            packet.Id == LegacyStringId.Effect &&
            packet.Strings.SequenceEqual(["zf2-e300"]));
        Assert.Contains(ParseSubClassGuiPackets(sent), packet =>
            packet.Action == LegacySubClassAction.Animation &&
            packet.StudyPoints == 25);
        Assert.Contains(ParseSubClassGuiPackets(teammateSent), packet =>
            packet.Action == LegacySubClassAction.Animation &&
            packet.StudyPoints == 27);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy boss study-point reward", output);
        Assert.Contains("familyId=20055", output);
        Assert.Contains("recipients=2", output);
    }

    [Fact]
    public async Task HandlerBoss4171DeathAwardsLegacyLastHitterConquerPoints()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 4_171,
            sourceMonsterName: "SnowBanshee",
            life: "1");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            name: "Leader",
            attributes: new Dictionary<string, string>
            {
                ["Conquer"] = "0",
                ["LegacyMonsterDeathConquerPointRoll"] = "25",
            });
        sessions.Set(connection, session);

        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 200,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.ConquerPoints);

        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                entry.Value == 500));
        Assert.Contains(ParseLegacyMessages(sent), message =>
            message.Content[3] == "You have received 500 Conquer Points for being the last hitter of SnowBanshee!");

        var output = log.ToString();
        Assert.Contains("WorldServer skipped legacy monster-death random Conquer Point reward", output);
        Assert.Contains("WorldServer applied legacy boss last-hitter Conquer Point reward", output);
        Assert.Contains("familyId=4171", output);
        Assert.Contains("points=500", output);
        Assert.Contains("invocations=1", output);
    }

    [Fact]
    public async Task HandlerNormalMonsterDeathAwardsTunedRandomConquerPoints()
    {
        var monsterFamily = CreateOneHitMonsterFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["VipLevel"] = "3",
                ["LegacyMonsterDeathConquerPointRoll"] = "5",
                ["LegacyMonsterDeathConquerPointRateRoll"] = "0",
            });
        sessions.Set(connection, session);

        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 6_000,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(5, updatedSession.Character.ConquerPoints);
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                entry.Value == 5));
        Assert.Contains(ParseLegacyMessages(sent), message =>
            message.Content[3] == "You`ve received 5 Conquer Points.");
        Assert.DoesNotContain(sent, bytes =>
            ReadFrame(bytes).Header.Opcode == WorldPacketOpcodes.LegacyProtoHeroInfo);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy monster-death random Conquer Point reward", output);
        Assert.Contains("branch=NonBossVipLadder", output);
        Assert.Contains("points=5", output);
        Assert.Contains("range=1..5", output);
        Assert.Contains("ratePercent=50", output);
        Assert.Contains("rateRoll=0", output);
    }

    [Fact]
    public async Task HandlerNormalMonsterDeathSkipsTunedConquerPointsWhenChanceMisses()
    {
        var monsterFamily = CreateOneHitMonsterFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["VipLevel"] = "3",
                ["LegacyMonsterDeathConquerPointRoll"] = "5",
                ["LegacyMonsterDeathConquerPointRateRoll"] = "99",
            });
        sessions.Set(connection, session);

        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 6_000,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.Character.ConquerPoints);
        Assert.DoesNotContain(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ConquerPoints));
        Assert.DoesNotContain(ParseLegacyMessages(sent), message =>
            message.Content.Count > 3 &&
            message.Content[3].Contains("Conquer Points", StringComparison.Ordinal));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped legacy monster-death random Conquer Point reward", output);
        Assert.Contains("branch=NonBossVipLadder", output);
        Assert.Contains("ratePercent=50", output);
        Assert.Contains("rateRoll=99", output);
        Assert.DoesNotContain("WorldServer applied legacy monster-death random Conquer Point reward", output);
    }

    [Fact]
    public async Task HandlerNormalMonsterDeathPersistsTunedConquerPointReward()
    {
        var usersRoot = Path.Combine(Path.GetTempPath(), "wolf-normal-monster-cp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(usersRoot);
        try
        {
            WriteCharacterSnapshot(usersRoot, PlayerUid, conquerPoints: 0);
            var monsterFamily = CreateOneHitMonsterFamily();
            var spawn = CreateMonsterSpawn(
                boundX: 832,
                boundY: 584,
                monsterTypeId: monsterFamily.Id,
                sourceMonsterName: monsterFamily.Name);
            var worldData = CreateMagicCatalog(monsterFamily, spawn);
            var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
            var monsterStates = new MonsterRuntimeStateStore();
            var sessions = new WorldSessionStore((connection, previous, current) =>
            {
                _ = connection;
                if (previous is not null &&
                    !string.IsNullOrWhiteSpace(current.UsersRoot) &&
                    CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(previous.Character, current.Character))
                {
                    Assert.True(
                        CharacterSnapshotScalarPersistence.TryPersist(current.UsersRoot, current.Character, out _, out var reason),
                        reason);
                }
            });
            var sent = new List<byte[]>();
            using var log = new StringWriter();
            var connection = CreateSendingConnection(sent);
            var session = CreateSession(
                    attributes: new Dictionary<string, string>
                    {
                        ["VipLevel"] = "3",
                        ["LegacyMonsterDeathConquerPointRoll"] = "5",
                        ["LegacyMonsterDeathConquerPointRateRoll"] = "0",
                    })
                with
            {
                UsersRoot = usersRoot,
            };
            sessions.Set(connection, session);

            await PacketHandlers.HandleAsync(
                new WorldPacketHandlerContext(
                    connection,
                    log,
                    session,
                    WorldData: worldData,
                    Sessions: sessions,
                    MonsterStates: monsterStates),
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid,
                    OpponentUid: monster.Uid,
                    X: monster.X,
                    Y: monster.Y,
                    AttackType: MsgAttackType.Physical,
                    Damage: 6_000,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0)).ToFrameBytes()));

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(5, updatedSession.Character.ConquerPoints);
            Assert.Contains(ParseMsgUpdates(sent), update =>
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                        entry.Value == 5));

            var persisted = CharacterSnapshotParser.ParseText(
                File.ReadAllText(Path.Combine(usersRoot, PlayerUid.ToString(CultureInfo.InvariantCulture) + ".ini")));
            Assert.Equal(5, persisted.ConquerPoints);
            Assert.Contains("ratePercent=50", log.ToString());
            Assert.Contains("rateRoll=0", log.ToString());
        }
        finally
        {
            if (Directory.Exists(usersRoot))
            {
                Directory.Delete(usersRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerPhysicalMonsterKillExperienceUsesOutgoingDamageCappedByMaxLife()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["VipLevel"] = "3",
                ["LegacyMonsterDeathConquerPointRoll"] = "45",
            });
        sessions.Set(connection, session);

        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 100,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));
        Assert.True(sessions.TryGet(connection, out var afterFirstHit));
        Assert.NotNull(afterFirstHit);
        var highDamageSession = afterFirstHit with
        {
            Character = afterFirstHit.Character with { Strength = 10_000 },
        };
        sessions.Set(connection, highDamageSession);

        await PacketHandlers.HandleAsync(
            context with { Session = highDamageSession },
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 1,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(510, updatedSession.Character.Experience);
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Experience &&
                entry.Value == 510));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy monster experience", output);
        Assert.Contains("gain=500", output);
    }

    [Fact]
    public async Task HandlerChaosGuardMonsterExperienceUsesSourceGuardException()
    {
        var monsterFamily = CreateMonsterFamily() with
        {
            Id = 213_883,
            Name = "ChaosGuard",
        };
        var spawn = CreateMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 100,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(updatedSession.Character.Experience > 0);
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy monster experience", output);
        Assert.Contains("source=AttackHandler.Updates.IncreaseExperience.Up -> GameClient.IncreaseExperience", output);
    }

    [Fact]
    public async Task HandlerMonsterExperienceAppliesLegacyIncreaseExperienceModifiers()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name) with
        {
            MapId = 1_039,
            SourceRelativePath = "1039/1.txt",
        };
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 1_000,
            mapId: 1_039,
            attributes: new Dictionary<string, string>
            {
                ["UserExpRate"] = "2",
                ["DExpTime"] = "1",
                ["RateExp"] = "1.5",
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 1_000,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var hit = AssertSinglePhysicalAttack(sent, monster.Uid);
        Assert.NotEqual(1_000, hit.Query.Damage);
        var expectedBaseGain = CalculateExpectedLegacyMonsterBaseExperience(
            hit.Query.Damage,
            session.Character,
            monster);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(
                session,
                expectedBaseGain));
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedSession.Character.Experience);
        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.Experience &&
                entry.Value == expectedExperience.AppliedExperienceGain));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy monster experience", output);
        Assert.Contains($"baseGain={expectedBaseGain}", output);
        Assert.Contains($"gain={expectedExperience.AppliedExperienceGain}", output);
        Assert.Contains("source=AttackHandler.Updates.IncreaseExperience.Up -> GameClient.IncreaseExperience", output);
    }

    [Fact]
    public async Task HandlerMonsterExperienceRedirectsOblivionAndStillAddsHuntingBlessing()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 1_000,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyOblivionFlag),
                ["HeavenBlessing"] = "3600",
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 1_000,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var hit = AssertSinglePhysicalAttack(sent, monster.Uid);
        Assert.NotEqual(1_000, hit.Query.Damage);
        var expectedBaseGain = CalculateExpectedLegacyMonsterBaseExperience(
            hit.Query.Damage,
            session.Character,
            monster);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(
                session,
                expectedBaseGain));
        Assert.Equal(0, updatedSession.Character.Experience);
        Assert.Equal(
            expectedExperience.OblivionExperienceGain.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["ExpOblivion"]);
        Assert.Equal(
            expectedExperience.OblivionExperienceGain.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["OblivionExperience"]);
        Assert.Equal(
            expectedExperience.HuntingBlessingGain.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["HuntingBlessing"]);
        Assert.Equal(
            expectedExperience.HuntingBlessingGain.ToString(CultureInfo.InvariantCulture),
            updatedSession.Character.Attributes["HuntBlessing"]);
        Assert.DoesNotContain(ParseMsgUpdates(sent), update =>
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy monster experience side effects", output);
        Assert.Contains($"baseGain={expectedBaseGain}", output);
        Assert.Contains("applied=0", output);
        Assert.Contains($"oblivion={expectedExperience.OblivionExperienceGain}", output);
        Assert.Contains($"huntingBlessing={expectedExperience.HuntingBlessingGain}", output);
    }

    [Fact]
    public async Task HandlerBoss4171DeathAwardsLegacyInnerPowerAndTeamStudyPoints()
    {
        var monsterFamily = CreateLegacyBossFamily(
            monsterTypeId: 4_171,
            sourceMonsterName: "SnowBanshee",
            life: "1");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalogWithInnerPower(
            monsterFamily,
            spawn,
            [
                CreateInnerPowerRow((int)PlayerUid, "Leader", potency: 200),
                CreateInnerPowerRow((int)TargetPlayerUid, "Mate", potency: 300),
            ]);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var teamStates = new TeamSessionStateStore();
        var sent = new List<byte[]>();
        var teammateSent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var teammateConnection = CreateSendingConnection(teammateSent);
        var session = CreateSession(
            name: "Leader",
            attributes: new Dictionary<string, string>
            {
                ["SubProfInfo"] = "5#0#0#",
                ["LegacyMonsterDeathConquerPointRoll"] = "25",
            });
        var teammateSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Mate",
            x: 831,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["SubProfInfo"] = "7#0#0#",
            });
        sessions.Set(connection, session);
        sessions.Set(teammateConnection, teammateSession);
        teamStates.CreateTeam(connection, PlayerUid);
        Assert.True(teamStates.TryAddMember(connection, teammateConnection, TargetPlayerUid));

        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            TeamStates: teamStates,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Physical,
                Damage: 200,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(500, updatedSession.Character.ConquerPoints);
        Assert.True(updatedSession.Character.Attributes.TryGetValue("HeroRewards", out var heroRewards));
        Assert.Equal("1/709/0/", heroRewards);
        Assert.True(sessions.TryGet(teammateConnection, out var updatedTeammateSession));
        Assert.NotNull(updatedTeammateSession);
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updatedSession.Character.Attributes["SubProfInfo"],
            out var updatedState));
        Assert.True(LegacySubClassHitpointsCalculator.TryParseLegacyState(
            updatedTeammateSession.Character.Attributes["SubProfInfo"],
            out var updatedTeammateState));
        Assert.Equal(5, updatedState.StudyPoints);
        Assert.Equal(1_007, updatedTeammateState.StudyPoints);
        Assert.Equal("1200", worldData.InnerPowerRanksByCharacterId[(int)PlayerUid][0].Fields[2]);
        Assert.Equal("1300", worldData.InnerPowerRanksByCharacterId[(int)TargetPlayerUid][0].Fields[2]);

        Assert.Contains(ParseMsgUpdates(sent), update =>
            update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.InnerPowerPotency &&
                entry.Value == 1_200));
        Assert.Contains(ParseMsgUpdates(teammateSent), update =>
            update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.InnerPowerPotency &&
                entry.Value == 1_300));
        Assert.DoesNotContain(ParseLegacyMessages(sent), message =>
            message.Content[3] == "You received 1000 StudyPoints.");
        Assert.Contains(ParseLegacyMessages(teammateSent), message =>
            message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
            message.Content[3] == "You received 1000 StudyPoints.");
        Assert.Contains(ParseLegacyMessages(sent), message =>
            message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            message.Color == LegacyMsgMessagePacket.RedColor &&
            message.Content[3] == "The SnowBanshee has been destroyed by the team Leader`s ! All team members received 1000 Study Points!");
        Assert.Contains(ParseLegacyMessages(teammateSent), message =>
            message.ChatType == LegacyMsgMessagePacket.CenterChatType &&
            message.Color == LegacyMsgMessagePacket.RedColor &&
            message.Content[3] == "The SnowBanshee has been destroyed by the team Leader`s ! All team members received 1000 Study Points!");
        Assert.Contains(ParseSubClassGuiPackets(teammateSent), packet =>
            packet.Action == LegacySubClassAction.Animation &&
            packet.StudyPoints == 1_007);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy boss study-point/InnerPower reward", output);
        Assert.Contains("familyId=4171", output);
        Assert.Contains("teammateStudyRecipients=1", output);
        Assert.Contains("killerPotencyApplied=True", output);
        Assert.Contains("teammatePotencyRecipients=1", output);
        Assert.Contains("WorldServer applied legacy monster-death HeroReward goal", output);
        Assert.Contains("goalId=709", output);
        Assert.Contains("added=True", output);
    }

    private static MsgAttackQuery CreateMagicQuery(
        LegacyMonsterInstance monster,
        bool legacyEncodedQuery,
        ushort spellId = ThunderSpellId,
        ushort? x = null,
        ushort? y = null,
        uint? opponentUid = null)
    {
        var query = new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid ?? monster.Uid,
            X: x ?? monster.X,
            Y: y ?? monster.Y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);

        if (!legacyEncodedQuery)
        {
            return query;
        }

        var encoded = MsgAttackPacket.EncodeMagicAttack(query with { Damage = spellId });
        return encoded with { SpellId = unchecked((ushort)encoded.Damage) };
    }

    private static WorldSession CreateSession(
        long uid = PlayerUid,
        string name = "Caster",
        long x = 830,
        long y = 584,
        long minMana = 500,
        long minHitPoints = 5_000,
        long strength = 0,
        long spirit = 300,
        ulong? stamina = null,
        IReadOnlyList<ushort>? spellIds = null,
        IReadOnlyList<PlayerSpellSnapshotEntry>? spellEntries = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        long mapId = 1_015,
        int classCode = 142,
        long reborn = 0,
        PlayerItemSnapshot? playerItems = null,
        PlayerProficiencySnapshot? playerProficiencies = null,
        ushort legacyRandomSpell = 0)
    {
        spellIds ??= [ThunderSpellId];
        var spells = spellEntries ??
            spellIds
                .Select(spellId => new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0))
                .ToArray();
        return new WorldSession(
            AccountId: 1,
            Username: name.ToLowerInvariant(),
            CreateCharacter(uid, name, x, y, minMana, minHitPoints, strength, spirit, stamina, attributes, mapId, classCode, reborn),
            playerItems ?? PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(spells),
            playerProficiencies ?? PlayerProficiencySnapshot.Empty,
            LegacyRandomSpell: legacyRandomSpell);
    }

    private static void WriteCharacterSnapshot(string usersRoot, uint uid, long conquerPoints)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1003
            Face=0
            Name=Caster
            Class=142
            Map=1015
            X=830
            Y=584
            FirstClass=142
            SecoundeClass=0
            Reborn=0
            Level=120
            Haire=0
            Experience=0
            PMap=1015
            PMapX=830
            PMapY=584
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=300
            Atributes=0
            MinHitPoints=5000
            MinMana=500
            ConquerPoints={{conquerPoints}}
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=1
            """);
    }

    private static void PrimeMonsterForServerAuthoritativeFinalHit(
        MonsterRuntimeStateStore monsterStates,
        LegacyMonsterInstance monster)
    {
        var now = DateTimeOffset.UtcNow;
        if (!monsterStates.TryGetVisibleInstance(monster, now, out var visibleMonster) ||
            visibleMonster.Hitpoints <= 1)
        {
            return;
        }

        monsterStates.ApplyDamage(visibleMonster, visibleMonster.Hitpoints - 1, now);
    }

    private static PlayerSpellSnapshotEntry CreatePlayerSpell(ushort spellId, ushort level = 0)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: level,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static CharacterSnapshot CreateCharacter(
        long uid,
        string name,
        long x,
        long y,
        long minMana,
        long minHitPoints,
        long strength,
        long spirit,
        ulong? stamina,
        IReadOnlyDictionary<string, string>? runtimeAttributes,
        long mapId,
        int classCode,
        long reborn)
    {
        var attributes = runtimeAttributes is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(runtimeAttributes);
        if (stamina is { } value)
        {
            attributes["Stamina"] = value.ToString(CultureInfo.InvariantCulture);
        }

        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: classCode,
            FirstClass: classCode,
            SecondClass: 0,
            Reborn: reborn,
            Level: 120,
            Experience: 0,
            MapId: mapId,
            X: x,
            Y: y,
            PreviousMapId: mapId,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: strength,
            Agility: 0,
            Vitality: 0,
            Spirit: spirit,
            AttributePoints: 0,
            MinHitPoints: minHitPoints,
            MinMana: minMana,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://character",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static PlayerItemSnapshot CreatePlayerItems(params PlayerItemSnapshotItem[] runeItems)
    {
        return CreatePlayerItems((IReadOnlyList<PlayerItemSnapshotItem>)runeItems);
    }

    private static PlayerItemSnapshot CreatePlayerItems(IReadOnlyList<PlayerItemSnapshotItem> runeItems)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems: runeItems);
    }

    private static PlayerItemSnapshot CreateEquipmentPlayerItems(uint garmentId)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: garmentId == 0
                ? []
                :
                [
                    new PlayerItemSnapshotItem
                    {
                        ItemId = garmentId,
                        Durability = 100,
                        MaximumDurability = 100,
                        Position = 9,
                    },
                ],
            RuneItems: []);
    }

    private static IReadOnlyList<uint> GetVisibleTargetOrder(
        WorldSessionStore sessions,
        ConquerTcpConnection casterConnection,
        long casterUid)
    {
        return sessions
            .Snapshot()
            .Where(active =>
                !ReferenceEquals(active.Connection, casterConnection) &&
                active.Session.Character.Uid != casterUid)
            .Select(active => checked((uint)active.Session.Character.Uid))
            .ToArray();
    }

    private static async Task<IReadOnlyList<LegacySpellAnimationTarget>> RunPhysicalSpellMonsterTargetsAsync(
        WorldDataCatalog worldData,
        Monster monsterFamily,
        MonsterSpawn spawn,
        WorldSession session)
    {
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster, legacyEncodedQuery: false, WindstormSpellId)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(WindstormSpellId, spell.SpellId);
        var totalDamage = spell.Targets.Aggregate(0UL, (total, target) => total + target.Damage);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - (uint)totalDamage, damagedMonster.Hitpoints);
        return spell.Targets;
    }

    private static ItemType CreateItemType(int id, string name)
    {
        return new ItemType(id, name, [id.ToString(CultureInfo.InvariantCulture), name]);
    }

    private static ItemType CreateStatusItemType(
        int id,
        string name,
        int magicAttack = 0,
        int skillCriticalStrike = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[29] = magicAttack.ToString(CultureInfo.InvariantCulture);
        fields[40] = skillCriticalStrike.ToString(CultureInfo.InvariantCulture);
        return new ItemType(id, name, fields);
    }

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            9_001,
            "TrainingMonster",
            new Dictionary<string, string>
            {
                ["life"] = "5000",
                ["lookface"] = "100",
                ["level"] = "100",
                ["defense"] = "25",
                ["defense2"] = "10000",
                ["attack_range"] = "0",
            },
            []);
    }

    private static Monster CreateOneHitMonsterFamily()
    {
        var attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["life"] = "1",
            ["defense"] = "0",
            ["defense2"] = "10000",
        };

        return CreateMonsterFamily() with { Attributes = attributes };
    }

    private static Monster CreateHighLifeMonsterFamily()
    {
        var attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
        {
            ["life"] = "500000",
        };

        return CreateMonsterFamily() with { Attributes = attributes };
    }

    private static Monster CreateReviverMonsterFamily()
    {
        return CreateMonsterFamily() with
        {
            Id = 10_000,
            Name = "Reviver",
        };
    }

    private static Monster CreateGuardMonsterFamily()
    {
        return CreateMonsterFamily() with
        {
            Id = 900,
            Name = "Guard1",
        };
    }

    private static SobNpc CreateTrainingSobNpc()
    {
        return new SobNpc(
            Id: 25_005,
            Name: "Stake",
            Kind: 21,
            Lookface: 0,
            MapId: 1_039,
            X: 85,
            Y: 70,
            Life: 0,
            MaxLife: 0,
            Unknown9: 1,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: "25005,Stake,21,0,1039,85,70,0,0,1,0",
            Fields: ["25005", "Stake", "21", "0", "1039", "85", "70", "0", "0", "1", "0"]);
    }

    private static SobNpc CreateEventSobNpc()
    {
        return new SobNpc(
            Id: 65_005,
            Name: "GuildPole",
            Kind: 10,
            Lookface: 1_137,
            MapId: 1_038,
            X: 85,
            Y: 70,
            Life: 50_000_000,
            MaxLife: 50_000_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 2,
            RawLine: "65005,GuildPole,10,1137,1038,85,70,50000000,50000000,17,0",
            Fields: ["65005", "GuildPole", "10", "1137", "1038", "85", "70", "50000000", "50000000", "17", "0"]);
    }

    private static SobNpc CreateGuildWarGateSobNpc(int id, string name, int lookface)
    {
        return new SobNpc(
            Id: id,
            Name: name,
            Kind: LegacySobNpcEventRules.GateKind,
            Lookface: lookface,
            MapId: LegacySobNpcEventRules.GuildWarMapId,
            X: 85,
            Y: 70,
            Life: 100,
            MaxLife: 100,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 4,
            RawLine: $"{id},{name},26,{lookface},1038,85,70,100,100,17,0",
            Fields: [id.ToString(CultureInfo.InvariantCulture), name, "26", lookface.ToString(CultureInfo.InvariantCulture), "1038", "85", "70", "100", "100", "17", "0"]);
    }

    private static SobNpc CreateGuildWarCapturePillarSobNpc(int id, string name)
    {
        return new SobNpc(
            Id: id,
            Name: name,
            Kind: LegacySobNpcEventRules.GuildWarCapturePillarKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.GuildWarMapId,
            X: 85,
            Y: 70,
            Life: 100,
            MaxLife: 100,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 5,
            RawLine: $"{id},{name},64,1137,1038,85,70,100,100,17,0",
            Fields: [id.ToString(CultureInfo.InvariantCulture), name, "64", "1137", "1038", "85", "70", "100", "100", "17", "0"]);
    }

    private static SobNpc CreateNeutralSobNpc()
    {
        return new SobNpc(
            Id: 65_006,
            Name: "NeutralSob",
            Kind: 2,
            Lookface: 1_137,
            MapId: 1_038,
            X: 85,
            Y: 70,
            Life: 50_000_000,
            MaxLife: 50_000_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 3,
            RawLine: "65006,NeutralSob,2,1137,1038,85,70,50000000,50000000,17,0",
            Fields: ["65006", "NeutralSob", "2", "1137", "1038", "85", "70", "50000000", "50000000", "17", "0"]);
    }

    private static SobNpc CreateStaticStatueSobNpc()
    {
        return new SobNpc(
            Id: 105_176,
            Name: "StaticStatue",
            Kind: 0,
            Lookface: 0,
            MapId: 1_002,
            X: 301,
            Y: 141,
            Life: 936_350,
            MaxLife: 936_350,
            Unknown9: 0,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 6,
            RawLine: "105176,StaticStatue,0,0,1002,301,141,936350,936350,0,0",
            Fields: ["105176", "StaticStatue", "0", "0", "1002", "301", "141", "936350", "936350", "0", "0"]);
    }

    private static StaticStatueRecord CreateStaticStatueRecord(SobNpc sobNpc)
    {
        var values = new[]
        {
            2,
            34,
            1,
            sobNpc.Id,
            sobNpc.X,
            sobNpc.Y,
            sobNpc.MapId,
            sobNpc.MaxLife,
            sobNpc.Life,
        };

        return new StaticStatueRecord(
            values[0],
            values,
            "StaticStatue.txt",
            2,
            string.Join('/', values) + "/",
            [.. values.Select(value => value.ToString(CultureInfo.InvariantCulture)), string.Empty]);
    }

    private static SobNpc CreateMoveLineSobNpc(int id, int x, int y)
    {
        return new SobNpc(
            Id: id,
            Name: "MoveLinePole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: x,
            Y: y,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 10,
            RawLine: $"{id},MoveLinePole,10,1137,1015,{x},{y},50000,50000,17,0",
            Fields:
            [
                id.ToString(CultureInfo.InvariantCulture),
                "MoveLinePole",
                "10",
                "1137",
                "1015",
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                "50000",
                "50000",
                "17",
                "0",
            ]);
    }

    private static Monster CreateSnowBansheeBossFamily()
    {
        return new Monster(
            4_171,
            "SnowBanshee",
            new Dictionary<string, string>
            {
                ["life"] = "80000000",
                ["lookface"] = "951",
                ["level"] = "138",
                ["defense"] = "43600",
                ["defense2"] = "10000",
                ["attack_min"] = "8800",
                ["attack_max"] = "10228",
                ["attack_range"] = "3",
                ["magic_type"] = "0000",
                ["Boss"] = "1",
            },
            []);
    }

    private static Monster CreateThrillingSpookBossFamily()
    {
        return new Monster(
            4_212,
            "ThrillingSpook",
            new Dictionary<string, string>
            {
                ["life"] = "60000000",
                ["lookface"] = "952",
                ["level"] = "138",
                ["defense"] = "43600",
                ["defense2"] = "10000",
                ["attack_min"] = "5800",
                ["attack_max"] = "8228",
                ["attack_range"] = "3",
                ["magic_type"] = "0000",
                ["Boss"] = "1",
            },
            []);
    }

    private static Monster CreateLegacyBossFamily(
        int monsterTypeId,
        string sourceMonsterName,
        string life = "60000000",
        string lookface = "900")
    {
        return new Monster(
            monsterTypeId,
            sourceMonsterName,
            new Dictionary<string, string>
            {
                ["life"] = life,
                ["lookface"] = lookface,
                ["level"] = "138",
                ["defense"] = "43600",
                ["defense2"] = "10000",
                ["attack_min"] = "5800",
                ["attack_max"] = "8228",
                ["attack_range"] = "3",
                ["magic_type"] = "0000",
                ["Boss"] = "1",
            },
            []);
    }

    private static MonsterSpawn CreateMonsterSpawn(
        int generatorId = 1,
        int boundX = 836,
        int boundY = 584,
        int boundWidth = 1,
        int boundHeight = 1,
        int maxPerGenerator = 1,
        int monsterTypeId = 9_001,
        string sourceMonsterName = "TrainingMonster")
    {
        return new MonsterSpawn(
            GeneratorId: generatorId,
            MapId: 1015,
            MonsterTypeId: monsterTypeId,
            BoundX: boundX,
            BoundY: boundY,
            BoundWidth: boundWidth,
            BoundHeight: boundHeight,
            MaxNpc: 1,
            RestSeconds: 5,
            MaxPerGenerator: maxPerGenerator,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: null,
            SourceMonsterName: sourceMonsterName,
            SourceRelativePath: "1015/1.txt",
            Attributes: new Dictionary<string, string>());
    }

    private static MonsterSpawn CreateSingleInstanceMonsterSpawn(
        int generatorId = 1,
        int boundX = 836,
        int boundY = 584,
        int monsterTypeId = 9_001,
        string sourceMonsterName = "TrainingMonster")
    {
        return CreateMonsterSpawn(
            generatorId: generatorId,
            boundX: boundX,
            boundY: boundY,
            monsterTypeId: monsterTypeId,
            sourceMonsterName: sourceMonsterName) with
        {
            Attributes = new Dictionary<string, string>
            {
                ["legacy_source"] = "LoadMapMonsters",
            },
        };
    }

    private static WorldDataCatalog CreateMagicCatalog(
        Monster monster,
        IReadOnlyList<MonsterSpawn> spawns,
        params MagicType[] magicRows)
    {
        return CreateMagicCatalogCore(monster, spawns, sobNpcs: [], magicRows);
    }

    private static WorldDataCatalog CreateMagicCatalogWithItems(
        Monster monster,
        IReadOnlyList<MonsterSpawn> spawns,
        IReadOnlyList<ItemType> items,
        params MagicType[] magicRows)
    {
        return CreateMagicCatalogCore(monster, spawns, sobNpcs: [], magicRows, items);
    }

    private static WorldDataCatalog CreateMagicCatalogWithSobNpcs(
        Monster monster,
        IReadOnlyList<SobNpc> sobNpcs,
        params MagicType[] magicRows)
    {
        return CreateMagicCatalogCore(monster, spawns: [], sobNpcs, magicRows);
    }

    private static WorldDataCatalog CreateMagicCatalogWithSobNpcsAndStaticStatues(
        Monster monster,
        IReadOnlyList<SobNpc> sobNpcs,
        IReadOnlyList<StaticStatueRecord> staticStatues,
        params MagicType[] magicRows)
    {
        return CreateMagicCatalogCore(
            monster,
            spawns: [],
            sobNpcs,
            magicRows,
            staticStatues: staticStatues);
    }

    private static WorldDataCatalog CreateMagicCatalogWithInnerPower(
        Monster monster,
        MonsterSpawn spawn,
        IReadOnlyList<InnerPowerRankEntry> innerPowerRows)
    {
        return CreateMagicCatalogCore(
            monster,
            [spawn],
            sobNpcs: [],
            magicRows: [CreateMagicRow(ThunderSpellId, sort: 1)],
            innerPowerRows: innerPowerRows);
    }

    private static WorldDataCatalog CreateMagicCatalogCore(
        Monster monster,
        IReadOnlyList<MonsterSpawn> spawns,
        IReadOnlyList<SobNpc> sobNpcs,
        IReadOnlyList<MagicType> magicRows,
        IReadOnlyList<ItemType>? items = null,
        IReadOnlyList<InnerPowerRankEntry>? innerPowerRows = null,
        IReadOnlyList<StaticStatueRecord>? staticStatues = null,
        IReadOnlyList<TransformInfo>? transformInfo = null,
        IReadOnlyList<ProfessionLevelBenefit>? professionLevelBenefits = null,
        IReadOnlyList<GouyuImmortal>? gouyuImmortals = null,
        IReadOnlyList<ItemAddBonus>? itemAddBonuses = null,
        IReadOnlyList<DaoqiDictType>? daoqiDictTypes = null,
        IReadOnlyList<MagicEffect>? magicEffects = null,
        IReadOnlyList<CombatGear>? combatGear = null,
        IReadOnlyList<JiangHuAttribute>? jiangHuAttributes = null,
        IReadOnlyList<CollectionRewardTier>? collectionRewardTiers = null,
        IReadOnlyList<GouyuType>? gouyuTypes = null)
    {
        if (magicRows.Count == 0)
        {
            magicRows = [CreateMagicRow(ThunderSpellId, sort: 1)];
        }

        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://legacy-magic-runtime",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items ?? [],
            itemAddBonuses: itemAddBonuses ?? [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: transformInfo ?? [],
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
            gouyuTypes: gouyuTypes ?? [],
            gouyuImmortals: gouyuImmortals ?? [],
            specialEffectAttributes: [],
            daoqiDictTypes: daoqiDictTypes ?? [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: jiangHuAttributes ?? [],
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
            innerPowerRanks: innerPowerRows ?? [],
            runeRankings: [],
            staticStatues: staticStatues ?? [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: professionLevelBenefits ?? [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: magicRows,
            magicEffects: magicEffects ?? [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: combatGear ?? [],
            traps: [],
            npcs: [],
            sobNpcs: sobNpcs,
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [monster],
            monsterSpawns: spawns,
            lotteryDictionary: [],
            collectionRewardTiers: collectionRewardTiers ?? [],
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

    private static WorldDataCatalog CreateMagicCatalog(Monster monster, MonsterSpawn spawn)
    {
        return CreateMagicCatalog(monster, [spawn], CreateMagicRow(ThunderSpellId, sort: 1));
    }

    private static LegacyMonsterInstance CreateRuntimeMonsterInstance(
        WorldDataCatalog worldData,
        MonsterSpawn spawn,
        Monster monster)
    {
        return CreateRuntimeMonsterInstances(worldData, spawn, monster).First();
    }

    private static IReadOnlyList<LegacyMonsterInstance> CreateRuntimeMonsterInstances(
        WorldDataCatalog worldData,
        MonsterSpawn spawn,
        Monster monster)
    {
        return LegacyMonsterInstance.FromGenerator(
            spawn,
            monster,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn));
    }

    private static InnerPowerRankEntry CreateInnerPowerRow(int characterId, string characterName, int potency)
    {
        var fields = new[]
        {
            characterName,
            characterId.ToString(CultureInfo.InvariantCulture),
            potency.ToString(CultureInfo.InvariantCulture),
            "0",
            string.Empty,
        };

        return new InnerPowerRankEntry(
            characterName,
            characterId,
            RankingFields: fields.Skip(2).Take(fields.Length - 3).ToArray(),
            SourceFile: "fixture://InnerPower.txt",
            LineNumber: 1,
            RawLine: string.Join('/', fields),
            Fields: fields);
    }

    private static MagicType CreateWindstormRow(ushort useMana = 0, byte useStamina = 0)
    {
        return CreateMagicRow(
            WindstormSpellId,
            sort: 55,
            useMana: useMana,
            damage: 0,
            maxTargets: 30,
            range: 0,
            needLevel: 5,
            weaponType: 611_671,
            useStamina: useStamina,
            coldTime: 0);
    }

    private static MagicType CreateKylinSigilRow()
    {
        return CreateMagicRow(
            KylinSigilSpellId,
            sort: 55,
            useMana: 0,
            damage: 100,
            maxTargets: 30,
            range: 0,
            needLevel: 5,
            weaponType: 611_671,
            coldTime: 0);
    }

    private static MagicType CreateBloomOfDeathRow()
    {
        return CreateMagicRow(
            BloomOfDeathSpellId,
            sort: 21,
            useMana: 0,
            damage: 30_180,
            maxTargets: 4,
            range: 0,
            needLevel: 60,
            weaponType: 60_671_670,
            coldTime: 0);
    }

    private static MagicType CreateAddBlackSpotRow(byte level = 0, uint duration = 0)
    {
        return CreateMagicRow(
            AddBlackSpotSpellId,
            sort: 62,
            level: level,
            useMana: 10,
            damage: 0,
            range: 6,
            useStamina: 5,
            coldTime: 60_000,
            duration: duration);
    }

    private static MagicType CreateBlackSpotRow(byte level = 0, uint duration = 12)
    {
        return CreateMagicRow(
            BlackSpotSpellId,
            sort: 58,
            level: level,
            useMana: 10,
            damage: 120,
            range: 30,
            useStamina: 5,
            coldTime: 60_000,
            duration: duration);
    }

    private static MagicType CreateMoveLineRow(uint damage = 120, byte level = 0)
    {
        return CreateMagicRow(
            MoveLineSpellId,
            sort: 61,
            level: level,
            useMana: 10,
            damage: damage,
            range: 30,
            useStamina: 5,
            coldTime: 60_000);
    }

    private static MagicType CreateFatalCrossRow(byte level = 0, uint experience = 0)
    {
        return CreateMagicRow(
            FatalCrossSpellId,
            sort: 71,
            level: level,
            experience: experience,
            useMana: 10,
            damage: 120,
            range: 13,
            coolDown: 0,
            coldTime: 0);
    }

    private static LegacyGameMapTerrainQuery CreateGuildWarMoveLineTerrain(
        int originX,
        int originY,
        int endpointX,
        int endpointY,
        ushort endpointSurface)
    {
        var width = Math.Max(endpointX + 2, 3);
        var height = Math.Max(endpointY + 2, 3);
        var cells = new List<LegacyDMapCell>(width * height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                ushort surface = 1;
                if (x == originX && y == originY)
                {
                    surface = 3;
                }

                if (x == endpointX && y == endpointY)
                {
                    surface = endpointSurface;
                }

                cells.Add(new LegacyDMapCell(x, y, 0, surface, 0));
            }
        }

        return new LegacyGameMapTerrainQuery(
            [
                new LegacyDMapTerrain(
                    LegacySobNpcEventRules.GuildWarMapId,
                    "fixture://guildwar-moveline.dmap",
                    width,
                    height,
                    cells),
            ]);
    }

    private static LegacyGameMapTerrainQuery CreateGuildWarTerrainWithBlockedTile(
        int originalX,
        int originalY,
        int blockedX,
        int blockedY)
    {
        var width = Math.Max(Math.Max(originalX, blockedX) + 2, 3);
        var height = Math.Max(Math.Max(originalY, blockedY) + 2, 3);
        var cells = new List<LegacyDMapCell>(width * height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var terrainFlag = (short)0;
                ushort surface = 1;
                if (x == originalX && y == originalY)
                {
                    surface = 3;
                }

                if (x == blockedX && y == blockedY)
                {
                    terrainFlag = 1;
                }

                cells.Add(new LegacyDMapCell(x, y, terrainFlag, surface, 0));
            }
        }

        return new LegacyGameMapTerrainQuery(
            [
                new LegacyDMapTerrain(
                    LegacySobNpcEventRules.GuildWarMapId,
                    "fixture://guildwar-blocked-pushback.dmap",
                    width,
                    height,
                    cells),
            ]);
    }

    private static MagicType CreateMagicRow(
        ushort spellId,
        byte sort,
        byte level = 0,
        ushort useMana = 7,
        uint damage = 120,
        uint experience = 0,
        byte maxTargets = 0,
        byte range = 9,
        ushort needLevel = 0,
        uint weaponType = 0,
        byte useStamina = 0,
        ushort coolDown = 1_000,
        int coldTime = 300,
        uint duration = 0,
        int damageOnHuman = 0,
        int damage2 = 0,
        int damage3 = 0,
        int damageOnMonster = 0,
        uint gDamage = 0)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = sort.ToString(CultureInfo.InvariantCulture);
        fields[3] = $"Spell{spellId}";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = useMana.ToString(CultureInfo.InvariantCulture);
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[12] = gDamage.ToString(CultureInfo.InvariantCulture);
        fields[13] = duration.ToString(CultureInfo.InvariantCulture);
        fields[18] = experience.ToString(CultureInfo.InvariantCulture);
        fields[20] = needLevel.ToString(CultureInfo.InvariantCulture);
        fields[22] = weaponType.ToString(CultureInfo.InvariantCulture);
        fields[14] = maxTargets.ToString(CultureInfo.InvariantCulture);
        fields[15] = range.ToString(CultureInfo.InvariantCulture);
        fields[29] = useStamina.ToString(CultureInfo.InvariantCulture);
        fields[33] = coolDown.ToString(CultureInfo.InvariantCulture);
        fields[35] = damageOnHuman.ToString(CultureInfo.InvariantCulture);
        fields[36] = damage2.ToString(CultureInfo.InvariantCulture);
        fields[37] = damage3.ToString(CultureInfo.InvariantCulture);
        fields[44] = damageOnMonster.ToString(CultureInfo.InvariantCulture);
        fields[47] = coldTime.ToString(CultureInfo.InvariantCulture);
        return new MagicType(spellId, spellId, $"Spell{spellId}", fields);
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

    private static void SetHandlerSession(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session)
    {
        var sessions = GetHandlerSessions(handler);
        sessions.Set(connection, session);
    }

    private static void StartHandlerRuntimeUpdateLoop(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        var method = typeof(WorldServerFrameHandler).GetMethod(
            "StartLegacyRuntimeUpdateLoop",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(
            handler,
            [connection, session.Character, session.PlayerItems, session.PlayerSpells, cancellationToken]);
    }

    private static WorldSessionStore GetHandlerSessions(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
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

    private static FloorItemRuntimeStateStore GetHandlerFloorItems(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<FloorItemRuntimeStateStore>(field.GetValue(handler));
    }

    private static SobNpcRuntimeStateStore GetHandlerSobNpcStates(WorldServerFrameHandler handler)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_sobNpcStates", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<SobNpcRuntimeStateStore>(field.GetValue(handler));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static byte[][] SnapshotFrames(IEnumerable<byte[]> frames)
    {
        if (frames is List<byte[]> list)
        {
            lock (list)
            {
                return list.ToArray();
            }
        }

        return frames.ToArray();
    }

    private static List<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var spells = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var spell))
            {
                spells.Add(spell);
            }
        }

        return spells;
    }

    private static List<LegacyMagicColdTimePacket> ParseMagicColdTimePackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMagicColdTimePacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyMagicColdTimePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyBlackspotPacket> ParseLegacyBlackspotPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyBlackspotPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyBlackspotPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyFloorItemPacket> ParseLegacyFloorItems(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyFloorItemPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyMsgMessagePacket> ParseLegacyMessages(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgMessagePacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacyStringPacket> ParseLegacyStrings(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyStringPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyStringPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacySubClassGuiPacket> ParseSubClassGuiPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySubClassGuiPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacySubClassGuiPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<MsgActionPacket> ParseMsgActions(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgActionPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (MsgActionPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertBombEqualsOneIfParserSupportsIt(LegacySpellAnimationPacket spell)
    {
        var property = typeof(LegacySpellAnimationPacket).GetProperty("BombFlag") ??
            typeof(LegacySpellAnimationPacket).GetProperty("Bomb") ??
            typeof(LegacySpellAnimationPacket).GetProperty("bomb");
        if (property is null)
        {
            return;
        }

        Assert.Equal(1U, Convert.ToUInt32(property.GetValue(spell), CultureInfo.InvariantCulture));
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("Condition was not met before the timeout expired.");
            }

            await Task.Delay(10);
        }
    }

    private static List<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var updates = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update))
            {
                updates.Add(update);
            }
        }

        return updates;
    }

    private static List<LegacyGameUpdatePacket> ParseGameUpdates(IEnumerable<byte[]> frames)
    {
        var updates = new List<LegacyGameUpdatePacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyGameUpdatePacket.TryParse(ReadFrame(bytes), out var update))
            {
                updates.Add(update);
            }
        }

        return updates;
    }

    private static IReadOnlyList<LegacyProficiencyPacket> ParseProficiencyPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyProficiencyPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyProficiencyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static int SingleFrameIndex(IReadOnlyList<byte[]> frames, Func<PacketFrame, bool> predicate)
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

    private static List<MsgAttackPacket> ParseDeathAttacks(IEnumerable<byte[]> frames)
    {
        var attacks = new List<MsgAttackPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.Death)
            {
                attacks.Add(attack);
            }
        }

        return attacks;
    }

    private static MsgAttackPacket AssertSinglePhysicalAttack(IEnumerable<byte[]> frames, uint opponentUid)
    {
        var attacks = new List<MsgAttackPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
                attack.Query.AttackType == MsgAttackType.Physical &&
                attack.Query.OpponentUid == opponentUid)
            {
                attacks.Add(attack);
            }
        }

        return Assert.Single(attacks);
    }

    private static int CalculateExpectedLegacyMonsterBaseExperience(
        int outgoingDamage,
        CharacterSnapshot attacker,
        LegacyMonsterInstance monster)
    {
        var adjusted = Math.Min(outgoingDamage, checked((int)monster.MaxLife));
        var levelDelta = checked((int)attacker.Level) - monster.Level;
        var nameType = levelDelta >= 3
            ? 1
            : levelDelta >= 0
                ? 2
                : levelDelta >= -5 ? 3 : 4;

        switch (nameType)
        {
            case 1:
                if (levelDelta >= 3 && levelDelta <= 5)
                {
                    adjusted = adjusted * 70 / 100;
                }
                else if (levelDelta > 5 && levelDelta <= 10)
                {
                    adjusted = adjusted * 20 / 100;
                }
                else if (levelDelta > 10 && levelDelta <= 20)
                {
                    adjusted = adjusted * 10 / 100;
                }
                else if (levelDelta > 20)
                {
                    adjusted = adjusted * 5 / 100;
                }

                break;
            case 3:
                adjusted = (int)(adjusted * 1.3);
                break;
            case 4:
                if (levelDelta >= -10 && levelDelta < -5)
                {
                    adjusted = (int)(adjusted * 1.5);
                }
                else if (levelDelta >= -20 && levelDelta < -10)
                {
                    adjusted = (int)(adjusted * 1.8);
                }
                else if (levelDelta < -20)
                {
                    adjusted = (int)(adjusted * 2.3);
                }

                break;
        }

        return Math.Max(10, adjusted);
    }

    private static LegacyExperienceGainInputs CreateExpectedLegacyExperienceInputs(
        WorldSession session,
        double baseAmount)
    {
        return new LegacyExperienceGainInputs(
            baseAmount,
            checked((int)session.Character.Level),
            ReadExpectedIntAttribute(session.Character.Attributes, "CursedTimer"),
            SoulChantEquipped: false,
            SoulChantItemLevel: 0,
            LegacyExperienceGainPolicy.ReadServerExperienceRate(session.Character.Attributes, _ => null),
            RainbowGemPercent: 0,
            ReadExpectedIntAttribute(
                session.Character.Attributes,
                "DExpTime",
                "DoubleExperienceTime",
                "DoubleExpTime"),
            ReadExpectedDoubleAttribute(
                session.Character.Attributes,
                "RateExp",
                "DoubleExperienceRate",
                "DExpRate"),
            checked((int)session.Character.MapId),
            HasLegacyStatusFlag(session.Character.Attributes, LegacyOblivionFlag),
            ReadExpectedUInt64Attribute(session.Character.Attributes, "HeavenBlessing", "HeavensBlessing") > 0);
    }

    private static int ReadExpectedIntAttribute(IReadOnlyDictionary<string, string> attributes, params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetValue(name, out var raw) &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 0;
    }

    private static ulong ReadExpectedUInt64Attribute(IReadOnlyDictionary<string, string> attributes, params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetValue(name, out var raw) &&
                ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 0;
    }

    private static double ReadExpectedDoubleAttribute(IReadOnlyDictionary<string, string> attributes, params string[] names)
    {
        foreach (var name in names)
        {
            if (attributes.TryGetValue(name, out var raw) &&
                double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 1;
    }

    private static List<LegacyNewSynWarPacket> ParseNewSynWarPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyNewSynWarPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacyNewSynWarPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static List<LegacySobNpcSpawnPacket> ParseSobNpcSpawns(IEnumerable<byte[]> frames)
    {
        var spawns = new List<LegacySobNpcSpawnPacket>();
        foreach (var bytes in SnapshotFrames(frames))
        {
            if (LegacySobNpcSpawnPacket.TryParse(ReadFrame(bytes), out var spawn))
            {
                spawns.Add(spawn);
            }
        }

        return spawns;
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
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

    private static void AssertLegacyGuardFlashingNameApplied(
        WorldSessionStore sessions,
        ConquerTcpConnection connection,
        IReadOnlyList<byte[]> frames,
        StringWriter log)
    {
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyFlashingNameFlag));
        Assert.Contains(
            ParseMsgUpdates(frames),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyFlashingNameFlag)));
        Assert.Contains("WorldServer applied legacy guard attack flashing-name", log.ToString());
    }

    private static void AssertLegacyXpSkillExpiresAround(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        DateTimeOffset beforeCast,
        DateTimeOffset afterCast,
        int expectedDurationSeconds = 50)
    {
        Assert.True(attributes.TryGetValue(key, out var rawExpiresAt));
        Assert.True(DateTimeOffset.TryParse(
            rawExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt));
        Assert.True(expiresAt >= beforeCast.AddSeconds(expectedDurationSeconds));
        Assert.True(expiresAt <= afterCast.AddSeconds(expectedDurationSeconds));
    }

    private static void AssertLegacyStatusLifetimeMetadata(
        IReadOnlyDictionary<string, string> attributes,
        string expiresAtKey,
        string removeOnDeadKey,
        DateTimeOffset beforeApply,
        DateTimeOffset afterApply,
        int expectedDurationSeconds)
    {
        Assert.True(attributes.TryGetValue(expiresAtKey, out var rawExpiresAt));
        Assert.True(DateTimeOffset.TryParse(
            rawExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var expiresAt));
        Assert.InRange(
            expiresAt,
            beforeApply.AddSeconds(expectedDurationSeconds),
            afterApply.AddSeconds(expectedDurationSeconds));
        Assert.Equal(bool.TrueString, attributes[removeOnDeadKey]);
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[16];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }
}
