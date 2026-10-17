using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CombatGearColdMoonDirectLineSpellId = 11_950;
    private const ushort CombatGearTharnCutterDirectLineSpellId = 17_390;
    private const ushort CombatGearDragonhowlDirectLineSpellId = 17_460;
    private const ushort CombatGearWindWalkerRangeLineSpellId = 13_060;
    private const ushort CombatGearBloodlustRangeLineSpellId = 17_500;
    private const ushort CombatGearBloodlustTripleLineSpellId = 17_530;
    private const byte CombatGearRectangleSort = 87;
    private const byte CombatGearDirectLineSort = 104;
    private const int CombatGearTharnCutterPrisonSigilFutilityCursedFlag = 0x11D | 0x20;

    [Fact]
    public async Task HandlerCombatGearColdMoonDirectLineUsesDecompiledMixedTargetCase()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31101, boundX: 834, boundY: 584);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31102, boundX: 838, boundY: 587);
        var sobNpc = CreateMoveLineSobNpc(id: 65_170, x: 838, y: 584) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, outsideWidthSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearColdMoonDirectLineSpellId,
                    CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 16),
                CreateMagicRow(
                    CombatGearColdMoonDirectLineSpellId,
                    CombatGearDirectLineSort,
                    level: 1,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 16),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearColdMoonDirectLineSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 836,
            y: 584,
            minHitPoints: 50_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearColdMoonDirectLineSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcProgress = 0UL;
        var expectedProgress = (ulong)expectedMonsterDamage + expectedSobNpcProgress;

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates),
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
                SpellId: CombatGearColdMoonDirectLineSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(836, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(CombatGearColdMoonDirectLineSpellId, animation.SpellId);
            Assert.Equal(
                new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == onLineMonster.Uid &&
                target.Damage == expectedMonsterDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerRuntimeDamage / 2 &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage == expectedSobNpcDamage &&
                target.Hit);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideWidthMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideWidthMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(50_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(checked((int)expectedProgress), Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear direct line magic", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcProgress}", output);
        Assert.Contains("source=CombatGearAttack.cs cases 11950/11951/11952/17400/17401", output);
    }

    [Fact]
    public async Task HandlerCombatGearTharnCutterDirectLineUsesDecompiledPlayerThirdDamageAndBomb()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31103, boundX: 834, boundY: 584);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31104, boundX: 838, boundY: 587);
        var sobNpc = CreateMoveLineSobNpc(id: 65_171, x: 838, y: 584) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, outsideWidthSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearTharnCutterDirectLineSpellId,
                    CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 16),
                CreateMagicRow(
                    CombatGearTharnCutterDirectLineSpellId,
                    CombatGearDirectLineSort,
                    level: 1,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 16),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearTharnCutterDirectLineSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(CombatGearTharnCutterPrisonSigilFutilityCursedFlag),
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 836,
            y: 584,
            minHitPoints: 80_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearTharnCutterDirectLineSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            3U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcProgress = 0UL;
        var expectedProgress = (ulong)expectedMonsterDamage + expectedSobNpcProgress;

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates),
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
                SpellId: CombatGearTharnCutterDirectLineSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(836, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(CombatGearTharnCutterDirectLineSpellId, animation.SpellId);
            Assert.Equal(1, animation.BombFlag);
            Assert.Equal(
                new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == onLineMonster.Uid &&
                target.Damage == expectedMonsterDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerRuntimeDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage == expectedSobNpcDamage &&
                target.Hit);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideWidthMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideWidthMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(checked((int)expectedProgress), Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear direct line magic", output);
        Assert.Contains("playerDamageDivisor=3", output);
        Assert.Contains("playerAnimationDamageDivisor=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcProgress}", output);
        Assert.Contains("source=CombatGearAttack.cs cases 17390/17391/17392", output);
    }

    [Fact]
    public async Task HandlerCombatGearDragonhowlDirectLineUsesDecompiledTwentyRangeFullPlayerDamage()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31105, boundX: 846, boundY: 584);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31106, boundX: 846, boundY: 587);
        var sobNpc = CreateMoveLineSobNpc(id: 65_172, x: 847, y: 584) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, outsideWidthSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearDragonhowlDirectLineSpellId,
                    CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 20),
                CreateMagicRow(
                    CombatGearDragonhowlDirectLineSpellId,
                    CombatGearDirectLineSort,
                    level: 1,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 20),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearDragonhowlDirectLineSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 848,
            y: 584,
            minHitPoints: 80_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearDragonhowlDirectLineSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcProgress = 0UL;
        var expectedProgress = (ulong)expectedMonsterDamage + expectedSobNpcProgress;

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates),
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
                SpellId: CombatGearDragonhowlDirectLineSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(848, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(CombatGearDragonhowlDirectLineSpellId, animation.SpellId);
            Assert.Equal(0, animation.BombFlag);
            Assert.Equal(
                new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == onLineMonster.Uid &&
                target.Damage == expectedMonsterDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerRuntimeDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage == expectedSobNpcDamage &&
                target.Hit);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideWidthMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideWidthMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(checked((int)expectedProgress), Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear direct line magic", output);
        Assert.Contains("range=20", output);
        Assert.Contains("playerDamageDivisor=1", output);
        Assert.Contains("playerAnimationDamageDivisor=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcProgress}", output);
        Assert.Contains("source=CombatGearAttack.cs cases 17460/17470/17480", output);
    }

    [Fact]
    public async Task HandlerCombatGearBloodlustRangeLineSendsManualInteractionsAndSkipsGuard()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 31107,
            boundX: 835,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var sobNpc = CreateMoveLineSobNpc(id: 65_173, x: 838, y: 584) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearBloodlustRangeLineSpellId,
                    CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 8),
                CreateMagicRow(
                    CombatGearBloodlustRangeLineSpellId,
                    CombatGearDirectLineSort,
                    level: 1,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 8),
            ]);
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var onLineMonster = new LegacyMonsterInstance(
            Uid: monsterStates.NextLegacyDynamicMonsterUid(),
            MapId: 1_015,
            X: 834,
            Y: 584,
            Mesh: 100,
            Level: 100,
            Hitpoints: 500_000,
            MaxLife: 500_000,
            MonsterId: checked((uint)monsterFamily.Id),
            Name: monsterFamily.Name,
            Boss: false,
            GeneratorId: 0,
            SpawnIndex: 0,
            RestSeconds: 10,
            DynamicId: 0);
        var outsideRangeMonster = onLineMonster with
        {
            Uid = monsterStates.NextLegacyDynamicMonsterUid(),
            X = 840,
        };
        monsterStates.RegisterDynamicMonster(onLineMonster, monsterFamily);
        monsterStates.RegisterDynamicMonster(outsideRangeMonster, monsterFamily);
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearBloodlustRangeLineSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 836,
            y: 584,
            minHitPoints: 80_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearBloodlustRangeLineSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcProgress = 0UL;
        var expectedProgress = (ulong)expectedMonsterDamage + expectedSobNpcProgress;

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates),
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
                SpellId: CombatGearBloodlustRangeLineSpellId)).ToFrameBytes()));

        var casterMonsterHit = AssertSinglePhysicalAttack(casterSent, onLineMonster.Uid);
        Assert.Equal(PlayerUid, casterMonsterHit.Query.Uid);
        Assert.Equal(834, casterMonsterHit.Query.X);
        Assert.Equal(584, casterMonsterHit.Query.Y);
        Assert.Equal(unchecked((int)expectedMonsterDamage), casterMonsterHit.Query.Damage);
        Assert.Equal(MsgAttackEffect.None, casterMonsterHit.Query.Effect);

        var casterPlayerHit = AssertSinglePhysicalAttack(casterSent, TargetPlayerUid);
        Assert.Equal(PlayerUid, casterPlayerHit.Query.Uid);
        Assert.Equal(836, casterPlayerHit.Query.X);
        Assert.Equal(584, casterPlayerHit.Query.Y);
        Assert.Equal(unchecked((int)expectedPlayerRuntimeDamage), casterPlayerHit.Query.Damage);
        Assert.Equal(MsgAttackEffect.None, casterPlayerHit.Query.Effect);

        var casterSobNpcHit = AssertSinglePhysicalAttack(casterSent, checked((uint)sobNpc.Id));
        Assert.Equal(PlayerUid, casterSobNpcHit.Query.Uid);
        Assert.Equal(838, casterSobNpcHit.Query.X);
        Assert.Equal(584, casterSobNpcHit.Query.Y);
        Assert.Equal(unchecked((int)expectedSobNpcDamage), casterSobNpcHit.Query.Damage);
        Assert.Equal(MsgAttackEffect.None, casterSobNpcHit.Query.Effect);

        Assert.DoesNotContain(casterSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            attack.Query.OpponentUid == guard.Uid);
        Assert.DoesNotContain(casterSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            attack.Query.OpponentUid == outsideRangeMonster.Uid);
        Assert.DoesNotContain(targetSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(836, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(CombatGearBloodlustRangeLineSpellId, animation.SpellId);
            Assert.Equal(0, animation.BombFlag);
            Assert.Equal(
                new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == onLineMonster.Uid &&
                target.Damage == expectedMonsterDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerRuntimeDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage == expectedSobNpcDamage &&
                target.Hit);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == guard.Uid);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideRangeMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideRangeMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideRangeMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var untouchedGuard));
        Assert.Equal(guard.MaxLife, untouchedGuard.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(checked((int)expectedProgress), Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear direct line magic", output);
        Assert.Contains("range=8", output);
        Assert.Contains("playerDamageDivisor=1", output);
        Assert.Contains("playerAnimationDamageDivisor=1", output);
        Assert.Contains("manualInteractionFrames=3", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcProgress}", output);
        Assert.Contains("source=CombatGearAttack.cs cases 17500/17510/17520/17540/17550/17560", output);
    }

    [Fact]
    public async Task HandlerCombatGearWindWalkerRangeLineAppliesCounterHitAndProgressBeforeAnimation()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 31110,
            boundX: 833,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name) with
        {
            MapId = 1_005,
            SourceRelativePath = "1005/1.txt",
        };
        var sobNpc = CreateMoveLineSobNpc(id: 65_175, x: 835, y: 584) with
        {
            MapId = 1_005,
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearWindWalkerRangeLineSpellId,
                    CombatGearRectangleSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 5),
                CreateMagicRow(
                    CombatGearWindWalkerRangeLineSpellId,
                    CombatGearRectangleSort,
                    level: 1,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 5),
            ]);
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var onLineMonster = new LegacyMonsterInstance(
            Uid: monsterStates.NextLegacyDynamicMonsterUid(),
            MapId: 1_005,
            X: 834,
            Y: 584,
            Mesh: 100,
            Level: 100,
            Hitpoints: 500_000,
            MaxLife: 500_000,
            MonsterId: checked((uint)monsterFamily.Id),
            Name: monsterFamily.Name,
            Boss: false,
            GeneratorId: 0,
            SpawnIndex: 0,
            RestSeconds: 10,
            DynamicId: 0);
        var outsideRangeMonster = onLineMonster with
        {
            Uid = monsterStates.NextLegacyDynamicMonsterUid(),
            X = 836,
        };
        monsterStates.RegisterDynamicMonster(onLineMonster, monsterFamily);
        monsterStates.RegisterDynamicMonster(outsideRangeMonster, monsterFamily);
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearWindWalkerRangeLineSpellId],
            mapId: 1_005,
            attributes: new Dictionary<string, string>
            {
                ["HitShoot"] = "7",
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: 80_000,
            mapId: 1_005);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearWindWalkerRangeLineSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcProgress = 0UL;
        var expectedProgress = (ulong)expectedMonsterDamage + expectedSobNpcProgress;

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates),
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
                SpellId: CombatGearWindWalkerRangeLineSpellId)).ToFrameBytes()));

        var casterMonsterHit = AssertSinglePhysicalAttack(casterSent, onLineMonster.Uid);
        Assert.Equal(unchecked((int)expectedMonsterDamage), casterMonsterHit.Query.Damage);
        var casterPlayerHit = AssertSinglePhysicalAttack(casterSent, TargetPlayerUid);
        Assert.Equal(unchecked((int)expectedPlayerRuntimeDamage), casterPlayerHit.Query.Damage);
        var casterSobNpcHit = AssertSinglePhysicalAttack(casterSent, checked((uint)sobNpc.Id));
        Assert.Equal(unchecked((int)expectedSobNpcDamage), casterSobNpcHit.Query.Damage);

        Assert.DoesNotContain(casterSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            attack.Query.OpponentUid == guard.Uid);
        Assert.DoesNotContain(casterSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            attack.Query.OpponentUid == outsideRangeMonster.Uid);

        var spellUpdateIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index =>
                LegacySpellPacket.TryParse(ReadFrame(casterSent[index]), out var packet) &&
                packet.Id == CombatGearWindWalkerRangeLineSpellId);
        var spellAnimationIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out var animation) &&
                animation.SpellId == CombatGearWindWalkerRangeLineSpellId);
        Assert.True(spellUpdateIndex < spellAnimationIndex);

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(832, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(CombatGearWindWalkerRangeLineSpellId, animation.SpellId);
            Assert.Equal(0, animation.BombFlag);
            Assert.Equal(
                new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == onLineMonster.Uid &&
                target.Damage == expectedMonsterDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerRuntimeDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage == expectedSobNpcDamage &&
                target.Hit);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == guard.Uid);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideRangeMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideRangeMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideRangeMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var untouchedGuard));
        Assert.Equal(guard.MaxLife, untouchedGuard.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("8", updatedCasterSession.Character.Attributes["HitShoot"]);
        Assert.Equal(checked((int)expectedProgress), Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear direct line magic", output);
        Assert.Contains("range=5", output);
        Assert.Contains("playerDamageDivisor=1", output);
        Assert.Contains("playerAnimationDamageDivisor=1", output);
        Assert.Contains("manualInteractionFrames=3", output);
        Assert.Contains("progressBeforeAnimation=True", output);
        Assert.Contains("counterHitShoots=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcProgress}", output);
        Assert.Contains("source=CombatGearAttack.cs case 13060", output);
    }

    [Fact]
    public async Task HandlerOtherRectangleSpellDoesNotEnterCombatGearDirectLineRuntime()
    {
        const ushort otherRectangleSpellId = 13_061;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 31111, boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [
                CreateMagicRow(
                    otherRectangleSpellId,
                    CombatGearRectangleSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    range: 5),
            ]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [otherRectangleSpellId]);
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
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: otherRectangleSpellId)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain("handled legacy CombatGear direct line magic", log.ToString());
        Assert.Contains("unsupported monster magic sort", log.ToString());
    }

    [Fact]
    public async Task HandlerCombatGearBloodlustTripleLineUsesDecompiledFixedRangeWithoutProgress()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31108, boundX: 840, boundY: 584);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31109, boundX: 840, boundY: 587);
        var sobNpc = CreateMoveLineSobNpc(id: 65_174, x: 843, y: 584) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, outsideWidthSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearBloodlustTripleLineSpellId,
                    CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 3),
                CreateMagicRow(
                    CombatGearBloodlustTripleLineSpellId,
                    CombatGearDirectLineSort,
                    level: 1,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 3),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearBloodlustTripleLineSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 842,
            y: 584,
            minHitPoints: 80_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearBloodlustTripleLineSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates),
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
                SpellId: CombatGearBloodlustTripleLineSpellId)).ToFrameBytes()));

        Assert.DoesNotContain(casterSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            (attack.Query.OpponentUid == onLineMonster.Uid ||
                attack.Query.OpponentUid == TargetPlayerUid ||
                attack.Query.OpponentUid == checked((uint)sobNpc.Id)));
        Assert.DoesNotContain(targetSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(CombatGearBloodlustTripleLineSpellId, animation.SpellId);
            Assert.Equal(0, animation.BombFlag);
            Assert.Equal(
                new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == onLineMonster.Uid &&
                target.Damage == expectedMonsterDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerRuntimeDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage == expectedSobNpcDamage &&
                target.Hit);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideWidthMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideWidthMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(casterSession.Character.Experience, updatedCasterSession.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear direct line magic", output);
        Assert.Contains("range=15", output);
        Assert.Contains("playerDamageDivisor=1", output);
        Assert.Contains("playerAnimationDamageDivisor=1", output);
        Assert.Contains("manualInteractionFrames=0", output);
        Assert.Contains("progressionApplied=False", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains("source=CombatGearAttack.cs cases 17530/17570", output);
    }
}
