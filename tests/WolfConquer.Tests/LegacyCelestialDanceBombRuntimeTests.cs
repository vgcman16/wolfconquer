using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void CalculateCelestialDancePlayerTargetUsesMatchingDecompileDamageOnHumanPercent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [CelestialDanceStatusSetterSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateCelestialDanceBombRow(duration: 37, damageOnHuman: 30_110));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateCelestialDanceBombRow(duration: 37, damageOnHuman: 30_145));
        var (_, baselineSpell) = ResolveCelestialDanceBombSpell(attacker, baselineWorldData);
        var (_, boostedSpell) = ResolveCelestialDanceBombSpell(attacker, boostedWorldData);
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(110U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(145U, boostedSpell.DamageOnHuman % 1_000);

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
            $"Expected matching-decompile CelestialDance Physical.OnPlayer damage percent to use DamageOnHuman. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public async Task HandlerCelestialDanceBombHitsCasterCenteredTargetsAndAppliesSelfStatus()
    {
        const uint duration = 37;
        const int damageOnHuman = 4_321;
        var monsterFamily = CreateMonsterFamily();
        var inRangeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 67_030, x: 835, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [inRangeSpawn],
            [sobNpc],
            [CreateCelestialDanceBombRow(duration, damageOnHuman)]);
        var inRangeMonster = CreateRuntimeMonsterInstance(worldData, inRangeSpawn, monsterFamily);
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
            spellIds: [CelestialDanceStatusSetterSpellId]);
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
            inRangeMonster,
            legacyEncodedQuery: false,
            spellId: CelestialDanceStatusSetterSpellId,
            x: 870,
            y: 620);
        var (playerSpell, spell) = ResolveCelestialDanceBombSpell(casterSession, worldData);
        var expectedMonsterDamage =
            InvokePrivateUInt32(
                "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
                casterSession,
                worldData,
                inRangeMonster,
                monsterFamily,
                spell) / 3;
        var expectedPlayerDamage =
            InvokePrivateUInt32(
                "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
                casterSession,
                targetSession,
                spell,
                worldData,
                1U) * 12 / 27;
        var expectedSobNpcDamage =
            InvokePrivateUInt32(
                "CalculateLegacySobNpcPhysicalSpellDamage",
                casterSession,
                spell,
                worldData,
                1U) / 3;

        var before = DateTimeOffset.UtcNow;
        var handled = await InvokeLegacyCelestialDanceBombAsync(
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
            Assert.Equal(870, animation.X);
            Assert.Equal(620, animation.Y);
            Assert.Equal(CelestialDanceStatusSetterSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.All(animation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { inRangeMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage, damageByUid[inRangeMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());
        Assert.All(casterSpell.Targets, target => Assert.True(target.Hit));

        Assert.True(monsterStates.TryGetVisibleInstance(inRangeMonster, DateTimeOffset.UtcNow, out var visibleInRange));
        Assert.Equal(inRangeMonster.MaxLife - expectedMonsterDamage, visibleInRange.Hitpoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CelestialDanceStatusSetterFlag));
        Assert.Equal(
            (damageOnHuman % 1_000).ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes["CelestialDancePower"]);
        Assert.Equal(
            (damageOnHuman % 1_000).ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes["LegacyCelestialDancePower"]);
        AssertCelestialDanceExpiresAt(
            updatedCasterSession.Character.Attributes["LegacyCelestialDanceExpiresAtUtc"],
            before,
            after,
            duration);

        var danceEntry = Assert.Single(
            ParseMsgUpdates(casterSent).SelectMany(update => update.Updates),
            entry => entry.Type == CelestialDanceStatusSetterUpdateType &&
                entry.Flag == CelestialDanceStatusSetterFlag);
        Assert.Equal(duration, danceEntry.Time);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CelestialDance Bomb magic", output);
        Assert.Contains("spellId=7030", output);
        Assert.Contains("sort=5", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcDamage}", output);
        Assert.Contains("source=Bomb.cs case 7030", output);
    }

    private static async ValueTask<bool> InvokeLegacyCelestialDanceBombAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerCelestialDanceBombMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveCelestialDanceBombSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            CelestialDanceStatusSetterSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateCelestialDanceBombRow(uint duration, int damageOnHuman)
    {
        return CreateMagicRow(
            CelestialDanceStatusSetterSpellId,
            sort: 5,
            useMana: 0,
            damage: 30_120,
            damageOnHuman: damageOnHuman,
            maxTargets: 4,
            range: 8,
            useStamina: 0,
            coldTime: 0,
            duration: duration);
    }

    private static void AssertCelestialDanceExpiresAt(
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
