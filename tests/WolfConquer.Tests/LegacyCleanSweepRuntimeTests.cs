using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CleanSweepSpellId = 15_870;
    private const int CleanSweepStatusFlag = 0xFE;

    [Fact]
    public void CalculateCleanSweepPlayerTargetUsesMatchingDecompileDamageOnHumanPercent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [CleanSweepSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateCleanSweepRow(damageOnHuman: 30_145));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateCleanSweepRow(damageOnHuman: 30_180));
        var (_, baselineSpell) = ResolveCleanSweepSpell(attacker, baselineWorldData);
        var (_, boostedSpell) = ResolveCleanSweepSpell(attacker, boostedWorldData);
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(145U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(180U, boostedSpell.DamageOnHuman % 1_000);

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
            $"Expected matching-decompile CleanSweep Physical.OnPlayer damage percent to use DamageOnHuman. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public async Task HandlerCleanSweepSectorBackHitsMixedTargetsConsumesWildPhoenixAndAppliesCasterStatus()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_870, x: 835, y: 586);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn],
            [sobNpc],
            [
                CreateCleanSweepRow(),
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
            spellIds: [CleanSweepSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 834,
            y: 586,
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
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var query = CreateMagicQuery(
            onLineMonster,
            legacyEncodedQuery: false,
            spellId: CleanSweepSpellId,
            x: 835,
            y: 584);
        var (_, spell) = ResolveCleanSweepSpell(casterSession, worldData);
        var expectedMonsterRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell) / 2;
        var expectedPlayerDamage = 1U * 5 / 12;
        Assert.Equal(0U, expectedPlayerDamage);
        var expectedSobNpcRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcAnimationDamage = expectedSobNpcRuntimeDamage * 50 / 100;

        var before = DateTimeOffset.UtcNow;
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));
        var after = DateTimeOffset.UtcNow;

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(835, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(CleanSweepSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.All(animation.Targets, target => Assert.Equal(MsgAttackEffect.None, target.Effect));
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterRuntimeDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcAnimationDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage, target.Hit)).ToArray());
        Assert.False(Assert.Single(casterSpell.Targets, target => target.Uid == TargetPlayerUid).Hit);
        Assert.All(casterSpell.Targets.Where(target => target.Uid != TargetPlayerUid), target => Assert.True(target.Hit));

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterRuntimeDamage, visibleOnLine.Hitpoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcRuntimeDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var wildPhoenixArchiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, wildPhoenixArchiveEntry.Time);
        Assert.Equal(18_007U, wildPhoenixArchiveEntry.Damage);
        Assert.Equal(1U, wildPhoenixArchiveEntry.SpellLevel);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, CleanSweepStatusFlag));
        Assert.Equal(spell.Damage3.ToString(CultureInfo.InvariantCulture), updatedCasterSession.Character.Attributes["LegacyCleanSweepPower"]);
        AssertCleanSweepExpiresAt(
            updatedCasterSession.Character.Attributes["LegacyCleanSweepExpiresAtUtc"],
            before,
            after,
            spell.Duration);
        AssertCleanSweepArchiveSkillUpdate(casterSent, PlayerUid, spell.Duration, spell.Damage3);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy CleanSweep SectorBack magic", output);
        Assert.Contains("spellId=15870", output);
        Assert.Contains("sort=69", output);
        Assert.Contains("width=2", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterRuntimeDamage}", output);
        Assert.Contains("source=SectorBack.cs case 15870", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresCleanSweepFlagAndClearsPowerAttributes()
    {
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var now = DateTimeOffset.UtcNow;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(10))),
            log: log);
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(CleanSweepStatusFlag),
                ["CleanSweepPower"] = "30145",
                ["CleanSweepExpiresAtUtc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
                ["LegacyCleanSweepPower"] = "30145",
                ["LegacyCleanSweepExpiresAtUtc"] = now.AddSeconds(-1).ToString("O", CultureInfo.InvariantCulture),
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, CleanSweepStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("CleanSweepPower") &&
                        !current.Character.Attributes.ContainsKey("CleanSweepExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyCleanSweepPower") &&
                        !current.Character.Attributes.ContainsKey("LegacyCleanSweepExpiresAtUtc");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, CleanSweepStatusFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("CleanSweepPower"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("CleanSweepExpiresAtUtc"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyCleanSweepPower"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyCleanSweepExpiresAtUtc"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, CleanSweepStatusFlag)));
            Assert.Contains("WorldServer expired legacy CleanSweep flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveCleanSweepSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            CleanSweepSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateCleanSweepRow(int damageOnHuman = 30_145)
    {
        return CreateMagicRow(
            CleanSweepSpellId,
            sort: 69,
            useMana: 0,
            damage: 30_400,
            maxTargets: 4,
            range: 5,
            useStamina: 1,
            coldTime: 0,
            duration: 35,
            damageOnHuman: damageOnHuman,
            damage3: 30_145);
    }

    private static void AssertCleanSweepArchiveSkillUpdate(
        IReadOnlyList<byte[]> frames,
        uint uid,
        uint duration,
        uint damage3)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            update => update.Uid == uid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Value == 0 &&
                    entry.Flag == CleanSweepStatusFlag &&
                    entry.Time == duration &&
                    entry.Damage == damage3 &&
                    entry.SpellLevel == damage3));
    }

    private static void AssertCleanSweepExpiresAt(
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
