using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort HookMoonSpellId = 15_930;
    private const int HookMoonStatusFlag = 0x103;
    private const int HookMoonCasterStatusFlag = 0x104;

    [Fact]
    public void CalculateHookMoonPlayerTargetUsesMatchingDecompileDamage3Percent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [HookMoonSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateHookMoonRow(damage3: 30_140));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateHookMoonRow(damage3: 30_180));
        var (_, baselineSpell) = ResolveHookMoonSpell(attacker, baselineWorldData);
        var (_, boostedSpell) = ResolveHookMoonSpell(attacker, boostedWorldData);
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(140U, baselineSpell.Damage3 % 1_000);
        Assert.Equal(180U, boostedSpell.Damage3 % 1_000);

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
            $"Expected matching-decompile HookMoon Physical.OnPlayer damage percent to use Damage3. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public async Task HandlerHookMoonLineHitsMixedTargetsWithSourceScalingSingleAnimationAndStatusArchiveSkill()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var widthEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 834, boundY: 586);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 834, boundY: 587);
        var algorithmEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 842, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_930, x: 836, y: 586);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, widthEdgeSpawn, outsideWidthSpawn, algorithmEdgeSpawn],
            [sobNpc],
            [
                CreateHookMoonRow(experience: 1),
                CreateHookMoonRow(level: 1, experience: 100),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var widthEdgeMonster = CreateRuntimeMonsterInstance(worldData, widthEdgeSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var algorithmEdgeMonster = CreateRuntimeMonsterInstance(worldData, algorithmEdgeSpawn, monsterFamily);
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
            spellIds: [HookMoonSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586,
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
            spellId: HookMoonSpellId,
            x: 848,
            y: 584);
        var (_, spell) = ResolveHookMoonSpell(casterSession, worldData);
        var expectedMonsterRuntimeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineMonster,
            monsterFamily,
            spell) * 2 / 3;
        var expectedMonsterAnimationDamage = expectedMonsterRuntimeDamage * 2;
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 7 / 16;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U) * 50 / 100;

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
            Assert.Equal(848, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(HookMoonSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.All(animation.Targets, target =>
            {
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, widthEdgeMonster.Uid, algorithmEdgeMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterAnimationDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(expectedMonsterAnimationDamage, damageByUid[widthEdgeMonster.Uid]);
        Assert.Equal(expectedMonsterAnimationDamage, damageByUid[algorithmEdgeMonster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(widthEdgeMonster, DateTimeOffset.UtcNow, out var visibleWidthEdge));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(algorithmEdgeMonster, DateTimeOffset.UtcNow, out var visibleAlgorithmEdge));
        Assert.Equal(onLineMonster.MaxLife - expectedMonsterRuntimeDamage, visibleOnLine.Hitpoints);
        Assert.Equal(widthEdgeMonster.MaxLife - expectedMonsterRuntimeDamage, visibleWidthEdge.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(algorithmEdgeMonster.MaxLife - expectedMonsterRuntimeDamage, visibleAlgorithmEdge.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, HookMoonStatusFlag));
        Assert.Equal("true", updatedTargetSession.Character.Attributes["HookMoon"]);
        Assert.Equal("170", updatedTargetSession.Character.Attributes["HookMoonAttackedPower"]);
        AssertHookMoonExpiresAt(
            updatedTargetSession.Character.Attributes["HookMoonExpiresAtUtc"],
            before,
            after,
            spell.Duration);
        AssertHookMoonArchiveSkillUpdate(targetSent, TargetPlayerUid, HookMoonStatusFlag, spell.Duration);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.Empty(ParseMagicColdTimePackets(casterSent));
        Assert.DoesNotContain(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("100", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, HookMoonCasterStatusFlag));
        Assert.Equal("true", updatedCasterSession.Character.Attributes["HookMoonCaster"]);
        Assert.Equal("140", updatedCasterSession.Character.Attributes["HookMoonPower"]);
        AssertHookMoonExpiresAt(
            updatedCasterSession.Character.Attributes["HookMoonCasterExpiresAtUtc"],
            before,
            after,
            spell.Duration);
        AssertHookMoonArchiveSkillUpdate(casterSent, PlayerUid, HookMoonCasterStatusFlag, spell.Duration);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy HookMoon Line magic", output);
        Assert.Contains("range=18", output);
        Assert.Contains("algorithmRange=11", output);
        Assert.Contains("width=2", output);
        Assert.Contains("monsterTargets=3", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterRuntimeDamage * 3UL}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcDamage}", output);
        Assert.Contains("source=Line.cs case 15930", output);
        Assert.DoesNotContain("PARITY-GAP legacy HookMoon target status not modeled", output);
        Assert.DoesNotContain("PARITY-GAP legacy HookMoon caster status not modeled", output);
        Assert.Contains("WorldServer applied legacy HookMoon caster status", output);
        Assert.Contains("duration=14", output);
        Assert.Contains("damage3Source=30140", output);
        Assert.Contains("hookMoonPower=140", output);
    }

    [Fact]
    public async Task HandlerHookMoonLineSobAnimationUsesRequestedDamageWhenAppliedDamageIsClamped()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateMoveLineSobNpc(id: 65_932, x: 836, y: 586) with
        {
            Life = 50,
            MaxLife = 50,
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [sobNpc],
            [CreateHookMoonRow()]);
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [HookMoonSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);
        var query = CreateHookMoonQuery();

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.True(target.Damage > 50U);
        Assert.Equal(0, sobNpcStates.GetVisible(sobNpc).Life);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy HookMoon Line magic", output);
        Assert.Contains("sobNpcProgress=50", output);
    }

    [Fact]
    public async Task HandlerHookMoonLineAgainstWildPhoenixPlayerConsumesDefenderChargeAndKeepsHookMoonStatuses()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateHookMoonRow(),
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
            stamina: 100,
            spellIds: [HookMoonSpellId]);
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
            Sessions: sessions);
        var query = CreateHookMoonQuery();
        var (_, spell) = ResolveHookMoonSpell(casterSession, worldData);
        var expectedPlayerDamage = 1U * 7 / 16;
        Assert.Equal(0U, expectedPlayerDamage);

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
            Assert.Equal(848, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(HookMoonSpellId, animation.SpellId);
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
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, HookMoonStatusFlag));
        Assert.Equal("true", updatedTargetSession.Character.Attributes["HookMoon"]);
        Assert.Equal("170", updatedTargetSession.Character.Attributes["HookMoonAttackedPower"]);
        AssertHookMoonExpiresAt(
            updatedTargetSession.Character.Attributes["HookMoonExpiresAtUtc"],
            before,
            after,
            spell.Duration);
        AssertHookMoonArchiveSkillUpdate(targetSent, TargetPlayerUid, HookMoonStatusFlag, spell.Duration);

        var wildPhoenixArchiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, wildPhoenixArchiveEntry.Time);
        Assert.Equal(18_007U, wildPhoenixArchiveEntry.Damage);
        Assert.Equal(1U, wildPhoenixArchiveEntry.SpellLevel);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, HookMoonCasterStatusFlag));
        Assert.Equal("true", updatedCasterSession.Character.Attributes["HookMoonCaster"]);
        Assert.Equal("140", updatedCasterSession.Character.Attributes["HookMoonPower"]);
        AssertHookMoonExpiresAt(
            updatedCasterSession.Character.Attributes["HookMoonCasterExpiresAtUtc"],
            before,
            after,
            spell.Duration);
        AssertHookMoonArchiveSkillUpdate(casterSent, PlayerUid, HookMoonCasterStatusFlag, spell.Duration);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy HookMoon Line magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("totalAppliedDamage=0", output);
        Assert.Contains("source=Line.cs case 15930", output);
    }

    [Fact]
    public async Task HandlerHookMoonLineSkipsGuardMonstersButStillHitsPlayerAndSobTargets()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            boundX: 833,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var sobNpc = CreateMoveLineSobNpc(id: 65_931, x: 836, y: 586);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            [sobNpc],
            [CreateHookMoonRow()]);
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
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
            spellIds: [HookMoonSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586,
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
            guard,
            legacyEncodedQuery: false,
            spellId: HookMoonSpellId,
            x: 848,
            y: 584);
        var (_, spell) = ResolveHookMoonSpell(casterSession, worldData);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 7 / 16;
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U) * 50 / 100;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Single(ParseSpellAnimations(targetSent));
        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);

        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleGuard));
        Assert.Equal(guard.MaxLife, visibleGuard.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);
    }

    [Fact]
    public async Task HandlerHookMoonLineDuplicateActiveFlagsKeepExistingTimersAndSkipDuplicateArchiveSkill()
    {
        const uint hookMoonPower = 140;
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateHookMoonRow(duration: 30));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [HookMoonSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var query = CreateHookMoonQuery();
        var (_, spell) = ResolveHookMoonSpell(casterSession, worldData);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            spell,
            worldData,
            1U) * 7 / 16;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var firstTargetSession = AssertSession(sessions, targetConnection);
        var firstCasterSession = AssertSession(sessions, casterConnection);
        var firstTargetExpiresAt = firstTargetSession.Character.Attributes["HookMoonExpiresAtUtc"];
        var firstCasterExpiresAt = firstCasterSession.Character.Attributes["HookMoonCasterExpiresAtUtc"];
        Assert.Equal(100_000 - expectedPlayerDamage, firstTargetSession.Character.MinHitPoints);
        AssertHookMoonArchiveSkillUpdate(targetSent, TargetPlayerUid, HookMoonStatusFlag, spell.Duration);
        AssertHookMoonArchiveSkillUpdate(casterSent, PlayerUid, HookMoonCasterStatusFlag, spell.Duration);

        context = context with { Session = firstCasterSession };
        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var duplicateTargetSession = AssertSession(sessions, targetConnection);
        var duplicateCasterSession = AssertSession(sessions, casterConnection);
        var expectedSecondPlayerDamage = expectedPlayerDamage * hookMoonPower / 100;
        Assert.Equal(100_000 - expectedPlayerDamage - expectedSecondPlayerDamage, duplicateTargetSession.Character.MinHitPoints);
        foreach (var spellAnimationFrames in new[] { casterSent, targetSent })
        {
            var damages = ParseSpellAnimations(spellAnimationFrames)
                .Where(animation => animation.SpellId == HookMoonSpellId)
                .SelectMany(animation => animation.Targets)
                .Where(target => target.Uid == TargetPlayerUid)
                .Select(target => target.Damage)
                .ToArray();
            Assert.Equal([expectedPlayerDamage, expectedSecondPlayerDamage], damages);
        }

        Assert.Equal(firstTargetExpiresAt, duplicateTargetSession.Character.Attributes["HookMoonExpiresAtUtc"]);
        Assert.Equal(firstCasterExpiresAt, duplicateCasterSession.Character.Attributes["HookMoonCasterExpiresAtUtc"]);
        Assert.Equal(1, CountHookMoonArchiveSkillUpdates(targetSent, TargetPlayerUid, HookMoonStatusFlag, spell.Duration));
        Assert.Equal(1, CountHookMoonArchiveSkillUpdates(casterSent, PlayerUid, HookMoonCasterStatusFlag, spell.Duration));
        Assert.True(HasLegacyStatusFlag(duplicateTargetSession.Character.Attributes, HookMoonStatusFlag));
        Assert.True(HasLegacyStatusFlag(duplicateCasterSession.Character.Attributes, HookMoonCasterStatusFlag));
    }

    [Fact]
    public async Task RuntimeLoopExpiresHookMoonTargetAndCasterFlagsAndAttributes()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateHookMoonRow(duration: 1));
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
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [HookMoonSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586,
            minHitPoints: 100_000);
        SetHandlerSession(handler, casterConnection, casterSession);
        SetHandlerSession(handler, targetConnection, targetSession);

        try
        {
            var query = CreateHookMoonQuery();
            await handler.HandleFrameAsync(
                casterConnection,
                ReadFrame(new MsgAttackPacket(query).ToFrameBytes()),
                cancel.Token);

            var updatedTargetSession = AssertHandlerSession(handler, targetConnection);
            var updatedCasterSession = AssertHandlerSession(handler, casterConnection);
            Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, HookMoonStatusFlag));
            Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, HookMoonCasterStatusFlag));
            Assert.Equal("true", updatedTargetSession.Character.Attributes["HookMoon"]);
            Assert.Equal("true", updatedCasterSession.Character.Attributes["HookMoonCaster"]);
            AssertHookMoonArchiveSkillUpdate(targetSent, TargetPlayerUid, HookMoonStatusFlag, 1);
            AssertHookMoonArchiveSkillUpdate(casterSent, PlayerUid, HookMoonCasterStatusFlag, 1);

            var targetRuntimeStart = targetSent.Count;
            var casterRuntimeStart = casterSent.Count;
            StartHandlerRuntimeUpdateLoop(handler, targetConnection, updatedTargetSession, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var currentTarget = AssertHandlerSession(handler, targetConnection);
                    var currentCaster = AssertHandlerSession(handler, casterConnection);
                    var targetExpired = !HasLegacyStatusFlag(currentTarget.Character.Attributes, HookMoonStatusFlag) &&
                        !currentTarget.Character.Attributes.ContainsKey("HookMoon") &&
                        !currentTarget.Character.Attributes.ContainsKey("HookMoonAttackedPower") &&
                        !currentTarget.Character.Attributes.ContainsKey("HookMoonExpiresAtUtc");
                    var casterExpired = !HasLegacyStatusFlag(currentCaster.Character.Attributes, HookMoonCasterStatusFlag) &&
                        !currentCaster.Character.Attributes.ContainsKey("HookMoonCaster") &&
                        !currentCaster.Character.Attributes.ContainsKey("HookMoonPower") &&
                        !currentCaster.Character.Attributes.ContainsKey("HookMoonCasterExpiresAtUtc");
                    var targetRemovalSent = ParseMsgUpdates(targetSent.ToArray().Skip(targetRuntimeStart)).Any(update =>
                        update.Uid == TargetPlayerUid &&
                        update.Updates.Any(entry =>
                            entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                            entry.Values is not null &&
                            !IsLegacyStatusFlagSet(entry.Values, HookMoonStatusFlag)));
                    var casterRemovalSent = ParseMsgUpdates(casterSent.ToArray().Skip(casterRuntimeStart)).Any(update =>
                        update.Uid == PlayerUid &&
                        update.Updates.Any(entry =>
                            entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                            entry.Values is not null &&
                            !IsLegacyStatusFlagSet(entry.Values, HookMoonCasterStatusFlag)));
                    return targetExpired &&
                        casterExpired &&
                        targetRemovalSent &&
                        casterRemovalSent;
                },
                TimeSpan.FromSeconds(1));

            var expiredTargetSession = AssertHandlerSession(handler, targetConnection);
            var expiredCasterSession = AssertHandlerSession(handler, casterConnection);
            Assert.False(HasLegacyStatusFlag(expiredTargetSession.Character.Attributes, HookMoonStatusFlag));
            Assert.False(HasLegacyStatusFlag(expiredCasterSession.Character.Attributes, HookMoonCasterStatusFlag));
            Assert.False(expiredTargetSession.Character.Attributes.ContainsKey("HookMoon"));
            Assert.False(expiredTargetSession.Character.Attributes.ContainsKey("HookMoonAttackedPower"));
            Assert.False(expiredTargetSession.Character.Attributes.ContainsKey("HookMoonExpiresAtUtc"));
            Assert.False(expiredCasterSession.Character.Attributes.ContainsKey("HookMoonCaster"));
            Assert.False(expiredCasterSession.Character.Attributes.ContainsKey("HookMoonPower"));
            Assert.False(expiredCasterSession.Character.Attributes.ContainsKey("HookMoonCasterExpiresAtUtc"));
            Assert.Contains(ParseMsgUpdates(targetSent.Skip(targetRuntimeStart)), update =>
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, HookMoonStatusFlag)));
            Assert.Contains(ParseMsgUpdates(casterSent.Skip(casterRuntimeStart)), update =>
                update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, HookMoonCasterStatusFlag)));
            Assert.Contains("WorldServer expired legacy HookMoon flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveHookMoonSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            HookMoonSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateHookMoonRow(
        byte level = 0,
        uint experience = 0,
        uint duration = 14,
        uint damage3 = 30_140)
    {
        var row = CreateMagicRow(
            HookMoonSpellId,
            sort: 14,
            level: level,
            useMana: 0,
            damage: 30_250U,
            experience: experience,
            maxTargets: 11,
            range: 18,
            weaponType: 61_681_680,
            useStamina: 0,
            coolDown: 1,
            coldTime: 0,
            duration: duration + level);
        var fields = row.Fields.ToArray();
        var rowId = HookMoonSpellId + level;
        fields[0] = rowId.ToString(CultureInfo.InvariantCulture);
        fields[1] = HookMoonSpellId.ToString(CultureInfo.InvariantCulture);
        fields[3] = "HookMoon";
        fields[37] = (damage3 + (uint)(level * 5)).ToString(CultureInfo.InvariantCulture);
        return new MagicType(rowId, HookMoonSpellId, "HookMoon", fields);
    }

    private static MsgAttackQuery CreateHookMoonQuery()
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: TargetPlayerUid,
            X: 848,
            Y: 584,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: HookMoonSpellId);
    }

    private static WorldSession AssertSession(
        WorldSessionStore sessions,
        ConquerTcpConnection connection)
    {
        Assert.True(sessions.TryGet(connection, out var session));
        Assert.NotNull(session);
        return session;
    }

    private static void AssertHookMoonArchiveSkillUpdate(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int statusFlag,
        uint duration)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            update => update.Uid == uid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Value == 0 &&
                    entry.Flag == checked((uint)statusFlag) &&
                    entry.Time == duration &&
                    entry.Damage == 0 &&
                    entry.SpellLevel == 0));
    }

    private static int CountHookMoonArchiveSkillUpdates(
        IReadOnlyList<byte[]> frames,
        uint uid,
        int statusFlag,
        uint duration)
    {
        return ParseMsgUpdates(frames)
            .Count(update => update.Uid == uid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Value == 0 &&
                    entry.Flag == checked((uint)statusFlag) &&
                    entry.Time == duration &&
                    entry.Damage == 0 &&
                    entry.SpellLevel == 0));
    }

    private static void AssertHookMoonExpiresAt(
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
