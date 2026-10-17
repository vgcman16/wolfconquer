using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CombatGearWidthLine21530SpellId = 21_530;

    [Theory]
    [InlineData(21_530, true)]
    [InlineData(21_640, true)]
    [InlineData(22_110, true)]
    [InlineData(21_660, false)]
    [InlineData(21_700, false)]
    [InlineData(21_570, false)]
    [InlineData(21_680, false)]
    public async Task HandlerCombatGearWidthLineUsesDecompiledWidthSourceAndPlayer75Percent(
        int rawSpellId,
        bool usesDurationWidth)
    {
        var spellId = checked((ushort)rawSpellId);
        var monsterFamily = CreateHighLifeMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31123, boundX: 835, boundY: 584);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31124, boundX: 836, boundY: 587);
        var outsideRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 31125, boundX: 843, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_182, x: 839, y: 586) with
        {
            Life = 50_000,
            MaxLife = 50_000,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, outsideWidthSpawn, outsideRangeSpawn],
            [sobNpc],
            [
                CreateMagicRow(
                    spellId,
                    (byte)CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    maxTargets: usesDurationWidth ? (byte)0 : (byte)2,
                    range: 10,
                    coldTime: 0,
                    duration: usesDurationWidth ? 2U : 0U),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var outsideRangeMonster = CreateRuntimeMonsterInstance(worldData, outsideRangeSpawn, monsterFamily);
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
            x: 838,
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
                X: 840,
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

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(840, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(spellId, animation.SpellId);
            Assert.Equal(
                new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
                animation.Targets.Select(target => target.Uid).Order().ToArray());
            Assert.Contains(animation.Targets, target =>
                target.Uid == onLineMonster.Uid &&
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
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideWidthMonster.Uid);
            Assert.DoesNotContain(animation.Targets, target => target.Uid == outsideRangeMonster.Uid);
        }

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var untouchedWidthMonster));
        Assert.Equal(outsideWidthMonster.MaxLife, untouchedWidthMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(outsideRangeMonster, DateTimeOffset.UtcNow, out var untouchedRangeMonster));
        Assert.Equal(outsideRangeMonster.MaxLife, untouchedRangeMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(80_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        Assert.Equal(0, updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear width line magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("range=10", output);
        Assert.Contains("width=2", output);
        Assert.Contains($"widthSource={(usesDurationWidth ? "duration" : "maxTargets")}", output);
        Assert.Contains("playerDamagePercent=75", output);
        Assert.Contains("targets=3", output);
        Assert.Contains("spellExperienceApplied=False", output);
        Assert.Contains("characterExperienceApplied=False", output);
        Assert.Contains(
            usesDurationWidth
                ? "source=CombatGearAttack.cs cases 21530/22110/21640"
                : "source=CombatGearAttack.cs cases 21660/21700/21570/21680",
            output);
        Assert.DoesNotContain("WorldServer updated legacy player magic spell", output);
    }

    [Fact]
    public async Task HandlerCombatGearWidthLineSendsEmptyAnimationWithoutProgress()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateMagicRow(
                    CombatGearWidthLine21530SpellId,
                    (byte)CombatGearDirectLineSort,
                    level: 0,
                    useMana: 0,
                    damage: 30_500,
                    experience: 1_000_000,
                    range: 10,
                    coldTime: 0,
                    duration: 2),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var casterSession = CreateSession(
            strength: 300,
            spellIds: [CombatGearWidthLine21530SpellId]);
        sessions.Set(casterConnection, casterSession);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore(),
                SobNpcStates: new SobNpcRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 840,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: CombatGearWidthLine21530SpellId)).ToFrameBytes()));

        Assert.DoesNotContain(casterSent, frame =>
            LegacySpellPacket.TryParse(ReadFrame(frame), out var packet) &&
            packet.Id == CombatGearWidthLine21530SpellId);

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(840, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(CombatGearWidthLine21530SpellId, animation.SpellId);
        Assert.Empty(animation.Targets);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);
        Assert.Equal(0, updatedCasterSession.Character.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CombatGear width line magic", output);
        Assert.Contains("targets=0", output);
        Assert.Contains("spellExperienceApplied=False", output);
        Assert.Contains("characterExperienceApplied=False", output);
    }
}
