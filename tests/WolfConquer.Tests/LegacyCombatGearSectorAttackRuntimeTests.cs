using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CombatGearNormalAttack3SectorSpellId = 15_690;

    [Fact]
    public async Task HandlerCombatGearNormalAttack3SectorUsesDecompiledFixedConeAndManualInteractions()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var onSectorSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31111, boundX: 833, boundY: 584);
        var outsideDistanceSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31112, boundX: 836, boundY: 584);
        var outsideAngleSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31113, boundX: 830, boundY: 589);
        var sobNpc = CreateMoveLineSobNpc(id: 65_176, x: 835, y: 584) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onSectorSpawn, outsideDistanceSpawn, outsideAngleSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    CombatGearNormalAttack3SectorSpellId,
                    sort: 87,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 4),
                CreateMagicRow(
                    CombatGearNormalAttack3SectorSpellId,
                    sort: 87,
                    level: 1,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 4),
            ]);
        var onSectorMonster = CreateRuntimeMonsterInstance(worldData, onSectorSpawn, monsterFamily);
        var outsideDistanceMonster = CreateRuntimeMonsterInstance(worldData, outsideDistanceSpawn, monsterFamily);
        var outsideAngleMonster = CreateRuntimeMonsterInstance(worldData, outsideAngleSpawn, monsterFamily);
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
            spellIds: [CombatGearNormalAttack3SectorSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: 80_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            CombatGearNormalAttack3SectorSpellId,
            out _,
            out var spell));
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onSectorMonster,
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
                OpponentUid: TargetPlayerUid,
                X: 999,
                Y: 998,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearNormalAttack3SectorSpellId)).ToFrameBytes()));

        var casterMonsterHit = AssertSinglePhysicalAttack(casterSent, onSectorMonster.Uid);
        Assert.Equal(833, casterMonsterHit.Query.X);
        Assert.Equal(584, casterMonsterHit.Query.Y);
        Assert.Equal(unchecked((int)expectedMonsterDamage), casterMonsterHit.Query.Damage);
        var casterPlayerHit = AssertSinglePhysicalAttack(casterSent, TargetPlayerUid);
        Assert.Equal(834, casterPlayerHit.Query.X);
        Assert.Equal(584, casterPlayerHit.Query.Y);
        Assert.Equal(unchecked((int)expectedPlayerRuntimeDamage), casterPlayerHit.Query.Damage);
        var casterSobNpcHit = AssertSinglePhysicalAttack(casterSent, checked((uint)sobNpc.Id));
        Assert.Equal(835, casterSobNpcHit.Query.X);
        Assert.Equal(584, casterSobNpcHit.Query.Y);
        Assert.Equal(unchecked((int)expectedSobNpcDamage), casterSobNpcHit.Query.Damage);

        Assert.DoesNotContain(casterSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            (attack.Query.OpponentUid == outsideDistanceMonster.Uid ||
                attack.Query.OpponentUid == outsideAngleMonster.Uid));
        Assert.DoesNotContain(targetSent, frame =>
            MsgAttackPacket.TryParse(ReadFrame(frame), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);

        var spellUpdateIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index =>
                LegacySpellPacket.TryParse(ReadFrame(casterSent[index]), out var packet) &&
                packet.Id == CombatGearNormalAttack3SectorSpellId);
        var spellAnimationIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out var animation) &&
                animation.SpellId == CombatGearNormalAttack3SectorSpellId);
        Assert.True(spellUpdateIndex < spellAnimationIndex);

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(834, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(CombatGearNormalAttack3SectorSpellId, animation.SpellId);
            Assert.Equal(
                new[] { onSectorMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == onSectorMonster.Uid &&
                target.Damage == expectedMonsterDamage &&
                target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == TargetPlayerUid &&
                target.Damage == expectedPlayerRuntimeDamage &&
                !target.Hit);
            Assert.Contains(animation.Targets, target =>
                target.Uid == checked((uint)sobNpc.Id) &&
                target.Damage == expectedSobNpcDamage &&
                target.Hit);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideDistanceMonster.Uid);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideAngleMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(onSectorMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onSectorMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideDistanceMonster, DateTimeOffset.UtcNow, out var untouchedDistanceMonster));
        Assert.Equal(outsideDistanceMonster.MaxLife, untouchedDistanceMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideAngleMonster, DateTimeOffset.UtcNow, out var untouchedAngleMonster));
        Assert.Equal(outsideAngleMonster.MaxLife, untouchedAngleMonster.Hitpoints);
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
        Assert.Contains("WorldServer handled legacy CombatGear sector magic", output);
        Assert.Contains("spellId=15690", output);
        Assert.Contains("sort=87", output);
        Assert.Contains("sectorSize=100", output);
        Assert.Contains("sectorDistance=5", output);
        Assert.Contains("rowRange=4", output);
        Assert.Contains("manualInteractionFrames=3", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcProgress}", output);
        Assert.Contains("source=CombatGearAttack.cs case 15690", output);
    }
}
