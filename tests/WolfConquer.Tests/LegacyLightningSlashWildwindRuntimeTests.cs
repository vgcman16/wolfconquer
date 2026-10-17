using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(LightningSlashSpellId, 20U, 30U, 150, 1U, 1U, 30_155)]
    [InlineData(LightningSlashPassiveSpellId, 15U, 55U, 160, 1U, 2U, 30_265)]
    public async Task HandlerLightningSlashWildwindHitsMixedLineTargetsWithSourceScaling(
        ushort spellId,
        uint playerScaleNumerator,
        uint playerScaleDenominator,
        int playerBonusDenominator,
        uint monsterScaleNumerator,
        uint monsterScaleDenominator,
        int damage2)
    {
        const byte lineWidth = 2;
        const int expectedPlayerRangeIndex = 2;
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var offLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 833, boundY: 591);
        var sobNpc = CreateMoveLineSobNpc(id: 65_480, x: 835, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, offLineSpawn],
            [sobNpc],
            [CreateLightningSlashWildwindRow(spellId, damage2, maxTargets: lineWidth)]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var offLineMonster = CreateRuntimeMonsterInstance(worldData, offLineSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var offLineTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var offLineTargetConnection = CreateSendingConnection(offLineTargetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: 100_000,
            spellIds: Array.Empty<ushort>());
        var offLineTargetSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "OffLineTarget",
            x: 834,
            y: 591,
            minHitPoints: 100_000,
            spellIds: Array.Empty<ushort>());
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        sessions.Set(offLineTargetConnection, offLineTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
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
            spell) * monsterScaleNumerator / monsterScaleDenominator;
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * playerScaleNumerator / playerScaleDenominator;
        expectedPlayerDamage += expectedPlayerDamage * (18 - (expectedPlayerRangeIndex * 7)) / checked((uint)playerBonusDenominator);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        if (spellId == LightningSlashPassiveSpellId)
        {
            expectedSobNpcDamage = expectedSobNpcDamage * 22 / 100;
        }

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: 838,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: spellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        var offLineTargetSpell = Assert.Single(ParseSpellAnimations(offLineTargetSent));
        foreach (var animation in new[] { casterSpell, targetSpell, offLineTargetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(838, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(spellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.All(animation.Targets, target => Assert.True(target.Hit));
            Assert.All(animation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            offLineTargetSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterDamage, visibleOnLine.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(offLineMonster, DateTimeOffset.UtcNow, out var visibleOffLine));
        Assert.Equal(offLineMonster.MaxLife, visibleOffLine.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(sessions.TryGet(offLineTargetConnection, out var updatedOffLineTargetSession));
        Assert.NotNull(updatedOffLineTargetSession);
        Assert.Equal(100_000, updatedOffLineTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy LightningSlash Wildwind magic", output);
        Assert.Contains($"spellId={spellId}", output);
        Assert.Contains("sort=90", output);
        Assert.Contains("range=9", output);
        Assert.Contains("width=2", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("totalPlayerRangeIndex=2", output);
        Assert.Contains($"playerAppliedDamage={expectedPlayerDamage}", output);
        Assert.Contains($"source=Wildwind.cs case {spellId}", output);
    }

    private static MagicType CreateLightningSlashWildwindRow(ushort spellId, int damage2, byte maxTargets)
    {
        return CreateMagicRow(
            spellId,
            sort: 90,
            useMana: 0,
            damage: 30_350,
            maxTargets: maxTargets,
            range: 9,
            useStamina: 0,
            coolDown: 0,
            coldTime: 0,
            damage2: damage2);
    }
}
