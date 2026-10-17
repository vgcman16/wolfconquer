using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void CalculateAxeShadowPlayerTargetUsesMatchingDecompileDamageOnHumanPercent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [AxeShadowStatusSetterSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateAxeShadowWildwindRow(duration: 41, damage3Power: 87, damageOnHuman: 30_195));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateAxeShadowWildwindRow(duration: 41, damage3Power: 87, damageOnHuman: 30_250));
        var (_, baselineSpell) = ResolveAxeShadowWildwindSpell(attacker, baselineWorldData);
        var (_, boostedSpell) = ResolveAxeShadowWildwindSpell(attacker, boostedWorldData);
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(195U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(250U, boostedSpell.DamageOnHuman % 1_000);

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

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected matching-decompile AxeShadow Physical.OnPlayer damage percent to use DamageOnHuman. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public async Task HandlerAxeShadowWildwindHitsMixedTargetsScalesAnimationDamageAndAppliesSelfStatus()
    {
        const uint duration = 41;
        const int damage3Power = 87;
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_940, x: 835, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn],
            [sobNpc],
            [CreateAxeShadowWildwindRow(duration, damage3Power)]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
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
            stamina: 100,
            spellIds: [AxeShadowStatusSetterSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: 100_000);
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
        var query = CreateMagicQuery(
            onLineMonster,
            legacyEncodedQuery: false,
            spellId: AxeShadowStatusSetterSpellId,
            x: 838,
            y: 584);
        var (playerSpell, spell) = ResolveAxeShadowWildwindSpell(casterSession, worldData);
        var expectedMonsterRuntimeDamage = InvokePrivateUInt32(
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
            1U) * 7 / 23;
        var expectedSobNpcRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        var before = DateTimeOffset.UtcNow;
        var handled = await InvokeLegacyAxeShadowWildwindAsync(
            context,
            casterSession,
            query,
            playerSpell,
            spell);
        var after = DateTimeOffset.UtcNow;

        Assert.True(handled);
        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(838, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(AxeShadowStatusSetterSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.All(animation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterRuntimeDamage * 50 / 100, damageByUid[onLineMonster.Uid]);
        Assert.Equal(expectedPlayerRuntimeDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcRuntimeDamage * 50 / 100, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());
        Assert.All(casterSpell.Targets, target => Assert.True(target.Hit));

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterRuntimeDamage, visibleOnLine.Hitpoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcRuntimeDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerRuntimeDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal("3000", updatedTargetSession.Character.Attributes["BleedDamage"]);
        Assert.Equal("3000", updatedTargetSession.Character.Attributes["LegacyBleedDamage"]);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, AxeShadowStatusSetterFlag));
        Assert.Equal(
            damage3Power.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes["AxeShadowPower"]);
        Assert.Equal(
            damage3Power.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes["LegacyAxeShadowPower"]);
        AssertAxeShadowExpiresAt(
            updatedCasterSession.Character.Attributes["LegacyAxeShadowExpiresAtUtc"],
            before,
            after,
            duration);

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(casterSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == AxeShadowStatusSetterFlag);
        Assert.Equal(duration, archiveEntry.Time);
        Assert.Equal(0U, archiveEntry.Damage);
        Assert.Equal(0U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy AxeShadow Wildwind magic", output);
        Assert.Contains("spellId=15940", output);
        Assert.Contains("sort=90", output);
        Assert.Contains("width=2", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterRuntimeDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcRuntimeDamage}", output);
        Assert.Contains("source=Wildwind.cs case 15940", output);
    }

    private static async ValueTask<bool> InvokeLegacyAxeShadowWildwindAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerAxeShadowWildwindMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveAxeShadowWildwindSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            AxeShadowStatusSetterSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateAxeShadowWildwindRow(uint duration, int damage3Power, int damageOnHuman = 30_195)
    {
        return CreateMagicRow(
            AxeShadowStatusSetterSpellId,
            sort: 90,
            useMana: 0,
            damage: 30_120,
            maxTargets: 4,
            range: 8,
            useStamina: 0,
            coldTime: 0,
            duration: duration,
            damageOnHuman: damageOnHuman,
            damage3: damage3Power);
    }

    private static void AssertAxeShadowExpiresAt(
        string rawExpiresAt,
        DateTimeOffset before,
        DateTimeOffset after,
        uint duration)
    {
        Assert.True(DateTimeOffset.TryParse(
            rawExpiresAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var expiresAt));
        Assert.InRange(expiresAt, before.AddSeconds(duration), after.AddSeconds(duration));
    }
}
