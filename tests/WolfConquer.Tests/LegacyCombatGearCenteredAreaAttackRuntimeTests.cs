using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CombatGearCenteredArea21490SpellId = 21_490;

    [Theory]
    [InlineData(21_920, "CombatGearAttack.cs case 21920", 3, "maxTargetsHalf")]
    [InlineData(21_510, "CombatGearAttack.cs cases 21510/21670", 3, "maxTargetsHalf")]
    [InlineData(21_670, "CombatGearAttack.cs cases 21510/21670", 3, "maxTargetsHalf")]
    [InlineData(21_600, "CombatGearAttack.cs cases 21600/21690", 6, "maxTargets")]
    [InlineData(21_690, "CombatGearAttack.cs cases 21600/21690", 6, "maxTargets")]
    public async Task HandlerCombatGearCenteredAreaQueryRadiusUsesDecompiledMaxTargetsRadiusAndCasterOnlyAnimation(
        int rawSpellId,
        string sourceCase,
        int expectedRadius,
        string radiusSource)
    {
        var spellId = checked((ushort)rawSpellId);
        var monsterFamily = CreateHighLifeMonsterFamily();
        var insideAreaSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31126, boundX: 838, boundY: 584);
        var outsideAreaSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 31127,
            boundX: checked((ushort)(836 + expectedRadius + 1)),
            boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_183, x: 836, y: 586) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [insideAreaSpawn, outsideAreaSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    spellId,
                    (byte)CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    maxTargets: 6,
                    range: 1,
                    coldTime: 0),
            ]);
        var insideAreaMonster = CreateRuntimeMonsterInstance(worldData, insideAreaSpawn, monsterFamily);
        var outsideAreaMonster = CreateRuntimeMonsterInstance(worldData, outsideAreaSpawn, monsterFamily);
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
            spellIds: [spellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 837,
            y: 584,
            minHitPoints: 80_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            spellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            insideAreaMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedPlayerDamage = expectedPlayerRuntimeDamage * 75U / 100U;
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
                OpponentUid: 0,
                X: 836,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: spellId)).ToFrameBytes()));

        Assert.DoesNotContain(casterSent, frame =>
            LegacySpellPacket.TryParse(ReadFrame(frame), out var packet) &&
            packet.Id == spellId);

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(836, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(spellId, animation.SpellId);
        Assert.Equal(
            new[] { insideAreaMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Contains(animation.Targets, target =>
            target.Uid == insideAreaMonster.Uid &&
            target.Damage == expectedMonsterDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid &&
            target.Damage == expectedPlayerDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == checked((uint)sobNpc.Id) &&
            target.Damage == expectedSobNpcDamage &&
            target.Hit);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideAreaMonster.Uid);

        Assert.True(monsterStates.TryGetVisibleInstance(insideAreaMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(insideAreaMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideAreaMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideAreaMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        Assert.Equal(0, updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear centered area magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains($"radius={expectedRadius}", output);
        Assert.Contains($"radiusSource={radiusSource}", output);
        Assert.Contains("rowRange=1", output);
        Assert.Contains("playerDamagePercent=75", output);
        Assert.Contains("targets=3", output);
        Assert.Contains("animationFanout=casterOnly", output);
        Assert.Contains("spellExperienceApplied=False", output);
        Assert.Contains("characterExperienceApplied=False", output);
        Assert.Contains($"source={sourceCase}", output);
        Assert.DoesNotContain("WorldServer updated legacy player magic spell", output);
    }

    [Fact]
    public async Task HandlerCombatGearCenteredArea21490UsesCasterRadiusAndAppliesMonsterSobProgress()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var insideCasterAreaSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31128, boundX: 832, boundY: 584);
        var outsideCasterInsideQueryAreaSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31129, boundX: 838, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_184, x: 830, y: 586) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [insideCasterAreaSpawn, outsideCasterInsideQueryAreaSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearCenteredArea21490SpellId,
                    (byte)CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    maxTargets: 6,
                    range: 1,
                    coldTime: 0),
                CreateMagicRow(
                    CombatGearCenteredArea21490SpellId,
                    (byte)CombatGearDirectLineSort,
                    level: 1,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    maxTargets: 6,
                    range: 1,
                    coldTime: 0),
            ]);
        var insideCasterAreaMonster = CreateRuntimeMonsterInstance(worldData, insideCasterAreaSpawn, monsterFamily);
        var outsideCasterAreaMonster = CreateRuntimeMonsterInstance(worldData, outsideCasterInsideQueryAreaSpawn, monsterFamily);
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
            spellIds: [CombatGearCenteredArea21490SpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 831,
            y: 584,
            minHitPoints: 80_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearCenteredArea21490SpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            insideCasterAreaMonster,
            monsterFamily,
            spell);
        var expectedPlayerRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U);
        var expectedPlayerDamage = expectedPlayerRuntimeDamage * 75U / 100U;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcProgress = 0UL;
        var expectedProgress = (ulong)expectedMonsterDamage + expectedSobNpcProgress;

        using var combatSpellOwner = new LegacyCombatSpellTestOwner(new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: sobNpcStates), ref casterSession);
        await PacketHandlers.HandleAsync(
            combatSpellOwner.Context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 836,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearCenteredArea21490SpellId)).ToFrameBytes()));

        var spellUpdateIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index =>
                LegacySpellPacket.TryParse(ReadFrame(casterSent[index]), out var packet) &&
                packet.Id == CombatGearCenteredArea21490SpellId);
        var spellAnimationIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out var animation) &&
                animation.SpellId == CombatGearCenteredArea21490SpellId);
        Assert.True(spellUpdateIndex < spellAnimationIndex);

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Empty(ParseSpellAnimations(targetSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(836, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(CombatGearCenteredArea21490SpellId, animation.SpellId);
        Assert.Equal(
            new[] { insideCasterAreaMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.Contains(animation.Targets, target =>
            target.Uid == insideCasterAreaMonster.Uid &&
            target.Damage == expectedMonsterDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == TargetPlayerUid &&
            target.Damage == expectedPlayerDamage &&
            target.Hit);
        Assert.Contains(animation.Targets, target =>
            target.Uid == checked((uint)sobNpc.Id) &&
            target.Damage == expectedSobNpcDamage &&
            target.Hit);
        Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideCasterAreaMonster.Uid);

        Assert.True(monsterStates.TryGetVisibleInstance(insideCasterAreaMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(insideCasterAreaMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideCasterAreaMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(outsideCasterAreaMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(checked((int)expectedProgress), Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear centered area magic", output);
        Assert.Contains("spellId=21490", output);
        Assert.Contains("targetCenterX=830", output);
        Assert.Contains("targetCenterY=584", output);
        Assert.Contains("centerSource=caster", output);
        Assert.Contains("animationX=836", output);
        Assert.Contains("animationY=584", output);
        Assert.Contains("radius=3", output);
        Assert.Contains("rowRange=1", output);
        Assert.Contains("progressApplied=True", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcProgress}", output);
        Assert.Contains("spellExperienceApplied=True", output);
        Assert.Contains("characterExperienceApplied=True", output);
        Assert.Contains("source=CombatGearAttack.cs case 21490", output);
    }
}
