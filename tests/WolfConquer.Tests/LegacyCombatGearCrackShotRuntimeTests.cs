using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CombatGearCrackShotSpellId = 18_690;
    private const ushort CombatGearTharnCutterElementalArrowSpellId = 18_160;
    private const ushort CombatGearTharnCutterPrisonSpellId = 18_710;
    private const ushort CombatGearTharnCutterTop8SpellId = 18_630;
    private const string CombatGearTharnCutterNoJumpRollAttribute = "LegacyCombatGearTharnCutterNoJumpRoll";
    private const int CrackShotPrisonSigilFutilityCursedFlag = 0x11D | 0x20;
    private const int CrackShotWhirlSigilAuraCursedFlag = 0x11E | 0x20;
    private const int CrackShotTop8WeeklyHawksAmbitionFlag = 0x3F | 0x100;
    private const int CrackShotTop4WeeklyHawksAmbitionFlag = 0x40 | 0x100;
    private const int CrackShotTop2WeeklyHawksAmbitionFlag = 0x41 | 0x100;

    [Fact]
    public async Task HandlerCombatGearCrackShotPlayerTargetUsesDecompiledDirectCase()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                useMana: 0,
                damage: 30_500,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearCrackShotSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584,
            minHitPoints: 5_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(0, spell.X);
            Assert.Equal(0, spell.Y);
            Assert.Equal(CombatGearCrackShotSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(108U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(4_892, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear CrackShot magic", output);
        Assert.Contains("source=CombatGearAttack.cs case 18690", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
    }

    [Fact]
    public async Task HandlerCombatGearCrackShotAppliesFixedDecompiledTailExperience()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                level: 0,
                useMana: 0,
                damage: 30_500,
                experience: 5_000,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100),
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                level: 1,
                useMana: 0,
                damage: 30_500,
                experience: 10_000,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    CombatGearCrackShotSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584,
            minHitPoints: 5_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        using var combatSpellOwner = new LegacyCombatSpellTestOwner(new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore()), ref casterSession);
        await PacketHandlers.HandleAsync(
            combatSpellOwner.Context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(CombatGearCrackShotSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);
        Assert.Equal(1_000, updatedSpell.Experience);

        var spellUpdate = Assert.Single(
            casterSent
                .Select(bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet)
                    ? packet
                    : (LegacySpellPacket?)null)
                .Where(packet => packet is not null)
                .Select(packet => packet!.Value));
        Assert.Equal(CombatGearCrackShotSpellId, spellUpdate.Id);
        Assert.Equal(0, spellUpdate.Level);
        Assert.Equal(1_000, spellUpdate.Experience);

        var expectedExperience = LegacyExperienceGainPolicy.CalculateIncreaseExperience(
            CreateExpectedLegacyExperienceInputs(casterSession, 1_000));
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCasterSession.Character.Experience);
        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Experience &&
                    entry.Value == expectedExperience.AppliedExperienceGain));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy CombatGear CrackShot fixed tail experience", output);
        Assert.Contains("baseGain=1000", output);
        Assert.Contains("appliedExperience=1000", output);
        Assert.Contains("source=CombatGearAttack.cs case 18690 IncreaseExperience.Up(..., 1000)", output);
    }

    [Fact]
    public async Task HandlerCombatGearCrackShotExistingSigilAddsTharnCutterBombFlagToAnimation()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 831, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                useMana: 0,
                damage: 30_500,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearCrackShotSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(CrackShotPrisonSigilFutilityCursedFlag),
            });
        sessions.Set(casterConnection, casterSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        var crackShotAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            spell => spell.SpellId == CombatGearCrackShotSpellId);
        Assert.Equal(1, crackShotAnimation.BombFlag);
        Assert.Equal(1, crackShotAnimation.SpellEffect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear CrackShot magic", output);
        Assert.Contains("source=CombatGearAttack.cs case 18690", output);
    }

    [Fact]
    public async Task HandlerCombatGearCrackShotForcedPrisonSigilNoJumpRateSendsEffectWithoutConsumingSigil()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 831, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                useMana: 0,
                damage: 30_500,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearCrackShotSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(CrackShotPrisonSigilFutilityCursedFlag),
                [CombatGearTharnCutterNoJumpRollAttribute] = "0",
            });
        sessions.Set(casterConnection, casterSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        var spellAnimations = ParseSpellAnimations(casterSent);
        Assert.Single(spellAnimations, spell => spell.SpellId == CombatGearCrackShotSpellId);
        var prisonEffect = Assert.Single(
            spellAnimations,
            spell => spell.SpellId == CombatGearTharnCutterPrisonSpellId);
        Assert.Equal(PlayerUid, prisonEffect.AttackerUid);
        Assert.Equal(PlayerUid, prisonEffect.AttackedUid);
        Assert.Equal(monster.X, prisonEffect.X);
        Assert.Equal(monster.Y, prisonEffect.Y);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotPrisonSigilFutilityCursedFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy CombatGear TharnCutter no-jump effect", output);
        Assert.Contains("effect=1", output);
        Assert.Contains("source=CombatGearAttack.cs case 18690 -> MyCombatGear.TharnCutter(jmp:false)", output);
    }

    [Fact]
    public async Task HandlerCombatGearCrackShotForcedTop8NoJumpRateDamagesPlayerWithLegacyEffect()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                useMana: 0,
                damage: 30_500,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearCrackShotSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(CrackShotTop8WeeklyHawksAmbitionFlag),
                [CombatGearTharnCutterNoJumpRollAttribute] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584,
            minHitPoints: 5_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        var top8Effect = Assert.Single(
            ParseSpellAnimations(casterSent),
            spell => spell.SpellId == CombatGearTharnCutterTop8SpellId);
        Assert.Equal(PlayerUid, top8Effect.AttackerUid);
        Assert.Equal(0U, top8Effect.AttackedUid);
        Assert.Equal(831, top8Effect.X);
        Assert.Equal(584, top8Effect.Y);
        var target = Assert.Single(top8Effect.Targets);
        Assert.Equal(TargetPlayerUid, target.Uid);
        Assert.Equal(1_000U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotTop8WeeklyHawksAmbitionFlag));
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(3_892, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy CombatGear TharnCutter no-jump effect", output);
        Assert.Contains("effect=3", output);
        Assert.Contains("source=CombatGearAttack.cs case 18690 -> MyCombatGear.TharnCutter(jmp:false)", output);
    }

    [Fact]
    public async Task HandlerCombatGearCrackShotAllSigilsCloseMonsterRunsTharnCutterNoJumpEffect()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 831, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                useMana: 0,
                damage: 30_500,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearCrackShotSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    CrackShotPrisonSigilFutilityCursedFlag,
                    CrackShotWhirlSigilAuraCursedFlag,
                    CrackShotTop8WeeklyHawksAmbitionFlag,
                    CrackShotTop4WeeklyHawksAmbitionFlag,
                    CrackShotTop2WeeklyHawksAmbitionFlag),
            });
        sessions.Set(casterConnection, casterSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        var spellAnimations = ParseSpellAnimations(casterSent);
        var crackShotAnimation = Assert.Single(
            spellAnimations,
            spell => spell.SpellId == CombatGearCrackShotSpellId);
        Assert.Contains(crackShotAnimation.BombFlag, new byte[] { 1, 2, 4, 8, 16 });

        var prisonEffect = Assert.Single(
            spellAnimations,
            spell => spell.SpellId == CombatGearTharnCutterPrisonSpellId);
        Assert.Equal(PlayerUid, prisonEffect.AttackerUid);
        Assert.Equal(PlayerUid, prisonEffect.AttackedUid);
        Assert.Equal(monster.X, prisonEffect.X);
        Assert.Equal(monster.Y, prisonEffect.Y);

        var top8Effect = Assert.Single(
            spellAnimations,
            spell => spell.SpellId == CombatGearTharnCutterTop8SpellId);
        Assert.Equal(PlayerUid, top8Effect.AttackerUid);
        Assert.Equal(0U, top8Effect.AttackedUid);
        Assert.Equal(monster.X, top8Effect.X);
        Assert.Equal(monster.Y, top8Effect.Y);
        Assert.Empty(top8Effect.Targets);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotPrisonSigilFutilityCursedFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotWhirlSigilAuraCursedFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotTop8WeeklyHawksAmbitionFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotTop4WeeklyHawksAmbitionFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotTop2WeeklyHawksAmbitionFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer sent legacy CombatGear TharnCutter no-jump effect", output);
        Assert.Contains("effect=6", output);
        Assert.Contains("source=CombatGearAttack.cs case 18690 -> MyCombatGear.TharnCutter(jmp:false)", output);
    }

    [Fact]
    public async Task HandlerCombatGearCrackShotElementalArrowGrantsMissingTop2SigilArchiveSkill()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                useMana: 0,
                damage: 30_500,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100),
            CreateMagicRow(
                CombatGearTharnCutterElementalArrowSpellId,
                sort: 104,
                useMana: 0,
                damageOnMonster: 20));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds:
            [
                CombatGearCrackShotSpellId,
                CombatGearTharnCutterElementalArrowSpellId,
            ],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(
                    CrackShotPrisonSigilFutilityCursedFlag,
                    CrackShotWhirlSigilAuraCursedFlag,
                    CrackShotTop8WeeklyHawksAmbitionFlag,
                    CrackShotTop4WeeklyHawksAmbitionFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584,
            minHitPoints: 5_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotPrisonSigilFutilityCursedFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotWhirlSigilAuraCursedFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotTop8WeeklyHawksAmbitionFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotTop4WeeklyHawksAmbitionFlag));
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CrackShotTop2WeeklyHawksAmbitionFlag));

        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, CrackShotTop2WeeklyHawksAmbitionFlag)));
        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == CrackShotTop2WeeklyHawksAmbitionFlag &&
                    entry.Time == 10 &&
                    entry.Damage == 20 &&
                    entry.SpellLevel == 0));
        Assert.Contains(
            ParseMsgUpdates(casterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, CrackShotTop2WeeklyHawksAmbitionFlag)));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear CrackShot magic", output);
        Assert.Contains("WorldServer granted legacy CombatGear TharnCutter missing sigil", output);
        Assert.Contains("WorldServer sent legacy CombatGear TharnCutter no-jump effect", output);
        Assert.Contains($"activeSpellId={CombatGearCrackShotSpellId}", output);
    }

    [Fact]
    public async Task HandlerCombatGearCrackShotMonsterTargetUsesDecompiledDirectCase()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 831, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(
                CombatGearCrackShotSpellId,
                sort: 104,
                useMana: 0,
                damage: 30_500,
                range: 16,
                damageOnHuman: 30_020,
                damage2: 30_500,
                damage3: 30_030,
                gDamage: 100));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearCrackShotSpellId]);
        sessions.Set(casterConnection, casterSession);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearCrackShotSpellId,
            out _,
            out var spell));
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            monster,
            monsterFamily,
            spell);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, casterSpell.AttackerUid);
        Assert.Equal(0U, casterSpell.AttackedUid);
        Assert.Equal(0, casterSpell.X);
        Assert.Equal(0, casterSpell.Y);
        Assert.Equal(CombatGearCrackShotSpellId, casterSpell.SpellId);
        var target = Assert.Single(casterSpell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - expectedDamage, damagedMonster.Hitpoints);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear CrackShot magic", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("source=CombatGearAttack.cs case 18690", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
    }

    [Fact]
    public async Task HandlerCombatGearCrackShotSobNpcTargetUsesDecompiledDirectCase()
    {
        var sobNpc = CreateTrainingSobNpc() with
        {
            X = 831,
            Y = 584,
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(),
            [],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearCrackShotSpellId,
                    sort: 104,
                    useMana: 0,
                    damage: 30_500,
                    range: 16,
                    damageOnHuman: 30_020,
                    damage2: 30_500,
                    damage3: 30_030,
                    gDamage: 100),
            ]);
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            mapId: sobNpc.MapId,
            x: 831,
            y: 584,
            strength: 300,
            spellIds: [CombatGearCrackShotSpellId]);
        sessions.Set(casterConnection, casterSession);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearCrackShotSpellId,
            out _,
            out var spell));
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var visibleBefore = sobNpcStates.GetVisible(sobNpc);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                SobNpcStates: sobNpcStates),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCrackShotSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, casterSpell.AttackerUid);
        Assert.Equal(0U, casterSpell.AttackedUid);
        Assert.Equal(0, casterSpell.X);
        Assert.Equal(0, casterSpell.Y);
        Assert.Equal(CombatGearCrackShotSpellId, casterSpell.SpellId);
        var target = Assert.Single(casterSpell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.Equal(visibleBefore.Life - checked((int)expectedDamage), sobNpcStates.GetVisible(sobNpc).Life);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear CrackShot magic", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("source=CombatGearAttack.cs case 18690", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
    }
}
