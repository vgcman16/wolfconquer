using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort StraightFistSpellId = 12_760;
    private const ushort StraightFistSourceSilentSpellId = 12_230;

    [Fact]
    public async Task HandlerStraightFistRequiresExactSortAndKnownSpellId()
    {
        const ushort unknownStraightFistSpellId = 12_761;
        Assert.Equal(76, (int)LegacyMagicSort.StraightFist);

        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateStraightFistRow(spellId: StraightFistSpellId, sort: 75, useMana: 7, useStamina: 5),
            CreateStraightFistRow(spellId: unknownStraightFistSpellId, sort: 76, useMana: 7, useStamina: 5));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [StraightFistSpellId, unknownStraightFistSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);

        var (wrongSortPlayerSpell, wrongSortSpell) = ResolveStraightFistSpell(
            session,
            worldData,
            StraightFistSpellId);
        var wrongSortHandled = await InvokeLegacyStraightFistAsync(
            context,
            session,
            CreateStraightFistQuery(StraightFistSpellId),
            wrongSortPlayerSpell,
            wrongSortSpell);

        var (unknownIdPlayerSpell, unknownIdSpell) = ResolveStraightFistSpell(
            session,
            worldData,
            unknownStraightFistSpellId);
        var unknownIdHandled = await InvokeLegacyStraightFistAsync(
            context,
            session,
            CreateStraightFistQuery(unknownStraightFistSpellId),
            unknownIdPlayerSpell,
            unknownIdSpell);

        Assert.False(wrongSortHandled);
        Assert.False(unknownIdHandled);
        Assert.Empty(sent);
        Assert.True(sessions.TryGet(connection, out var unchangedSession));
        Assert.NotNull(unchangedSession);
        Assert.Equal(500, unchangedSession.Character.MinMana);
        Assert.Equal("100", unchangedSession.Character.Attributes["Stamina"]);
    }

    [Fact]
    public async Task HandlerStraightFist12230PreparesResourcesThenReturnsSourceSilent()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateStraightFistRow(
                spellId: StraightFistSourceSilentSpellId,
                useMana: 7,
                useStamina: 5));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [StraightFistSourceSilentSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveStraightFistSpell(
            session,
            worldData,
            StraightFistSourceSilentSpellId);

        var handled = await InvokeLegacyStraightFistAsync(
            context,
            session,
            CreateStraightFistQuery(StraightFistSourceSilentSpellId),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.Contains("verified 12230 then returned without animation or damage", log.ToString());
    }

    [Fact]
    public async Task HandlerStraightFistWithoutTargetsConsumesResourcesStartsCooldownAndSendsEmptyAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateStraightFistRow(useMana: 7, useStamina: 5, coldTime: 60_000));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            stamina: 100,
            spellIds: [StraightFistSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var (playerSpell, spell) = ResolveStraightFistSpell(session, worldData, StraightFistSpellId);
        var query = CreateStraightFistQuery(StraightFistSpellId);

        var firstHandled = await InvokeLegacyStraightFistAsync(
            context,
            session,
            query,
            playerSpell,
            spell);

        Assert.True(firstHandled);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(840, animation.X);
        Assert.Equal(584, animation.Y);
        Assert.Equal(StraightFistSpellId, animation.SpellId);
        Assert.Empty(animation.Targets);
        Assert.True(sessions.TryGet(connection, out var afterFirst));
        Assert.NotNull(afterFirst);
        Assert.Equal(493, afterFirst.Character.MinMana);
        Assert.Equal("95", afterFirst.Character.Attributes["Stamina"]);
        Assert.NotNull(afterFirst.MagicCooldowns);
        Assert.True(afterFirst.MagicCooldowns.ContainsKey(StraightFistSpellId));

        var secondHandled = await InvokeLegacyStraightFistAsync(
            context with { Session = afterFirst },
            afterFirst,
            query,
            playerSpell,
            spell);

        Assert.True(secondHandled);
        Assert.Single(ParseSpellAnimations(sent));
        Assert.Contains("cooldownRemainingMs=", log.ToString());
    }

    [Fact]
    public async Task HandlerStraightFistHitsMonsterPlayerAndSobWithPhysicalDamageAndProgression()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var monsterSpawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 62_760, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [monsterSpawn],
            [sobNpc],
            [
                CreateStraightFistRow(experience: 1),
                CreateStraightFistRow(level: 1, experience: 100),
            ]);
        var monster = CreateRuntimeMonsterInstance(worldData, monsterSpawn, monsterFamily);
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
            spellIds: [StraightFistSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 833,
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
        var query = CreateStraightFistQuery(
            StraightFistSpellId,
            opponentUid: monster.Uid,
            x: 840,
            y: 584);
        var (playerSpell, spell) = ResolveStraightFistSpell(casterSession, worldData, StraightFistSpellId);
        var expectedMonsterDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            monster,
            monsterFamily,
            spell);
        var expectedPlayerDamage = InvokePrivateUInt32(
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
        var expectedMonsterProgress = InvokeStraightFistPrivateInt32(
            "AdjustLegacyMonsterExperience",
            checked((int)Math.Min(expectedMonsterDamage, monster.MaxLife)),
            checked((int)casterSession.Character.Level),
            monster.Level);

        var handled = await InvokeLegacyStraightFistAsync(
            context,
            casterSession,
            query,
            playerSpell,
            spell);

        Assert.True(handled);
        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterAnimation, targetAnimation })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(840, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(StraightFistSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            Assert.All(animation.Targets, target =>
            {
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        }

        var damageByUid = casterAnimation.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { monster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedMonsterDamage, damageByUid[monster.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterAnimation.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetAnimation.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife - expectedMonsterDamage, visibleMonster.Hitpoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(updatedCasterSession.Character.Experience > 0);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(StraightFistSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy StraightFist magic", output);
        Assert.Contains("spellId=12760", output);
        Assert.Contains("sort=76", output);
        Assert.Contains("range=5", output);
        Assert.Contains("width=0", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedMonsterProgress}", output);
        Assert.Contains("sobNpcProgress=0", output);
        Assert.Contains("source=StraightFist.cs case 12760", output);
    }

    [Fact]
    public async Task HandlerStraightFistTrainingSobContributesSourceNpcProgression()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [sobNpc],
            [
                CreateStraightFistRow(experience: 1),
                CreateStraightFistRow(level: 1, experience: 100),
            ]);
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            x: 82,
            y: 70,
            mapId: 1_039,
            strength: 5_000,
            spellIds: [StraightFistSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);
        var query = CreateStraightFistQuery(
            StraightFistSpellId,
            opponentUid: checked((uint)sobNpc.Id),
            x: 90,
            y: 70);
        var (playerSpell, spell) = ResolveStraightFistSpell(session, worldData, StraightFistSpellId);
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            session,
            spell,
            worldData,
            1U);
        var expectedAppliedDamage = Math.Min(expectedDamage, 10_000U);
        var expectedProgress = expectedDamage >= 10_000
            ? expectedAppliedDamage / 10
            : expectedAppliedDamage / 1_000;
        Assert.True(expectedProgress > 0);

        var handled = await InvokeLegacyStraightFistAsync(
            context,
            session,
            query,
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(10_000 - checked((int)expectedAppliedDamage), sobNpcStates.GetVisible(sobNpc).Life);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Contains($"sobNpcProgress={expectedProgress}", log.ToString());
    }

    [Fact]
    public async Task HandlerStraightFistAlwaysExcludesGuardMonsters()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            boundX: 833,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var worldData = CreateMagicCatalog(
            guardFamily,
            [guardSpawn],
            CreateStraightFistRow());
        var guard = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            spellIds: [StraightFistSpellId],
            attributes: new Dictionary<string, string> { ["PKMode"] = "0" });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = CreateStraightFistQuery(
            StraightFistSpellId,
            opponentUid: guard.Uid,
            x: 840,
            y: 584);
        var (playerSpell, spell) = ResolveStraightFistSpell(session, worldData, StraightFistSpellId);

        var handled = await InvokeLegacyStraightFistAsync(
            context,
            session,
            query,
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Empty(animation.Targets);
        Assert.True(monsterStates.TryGetVisibleInstance(guard, DateTimeOffset.UtcNow, out var visibleGuard));
        Assert.Equal(guard.MaxLife, visibleGuard.Hitpoints);
        Assert.Contains("monsterTargets=0", log.ToString());
    }

    [Fact]
    public async Task HandlerStraightFistUsesSoulWidthAndStrictFiveCellRange()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var soulWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 833, boundY: 585);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 833, boundY: 586);
        var rangeBoundarySpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 835, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [onLineSpawn, soulWidthSpawn, outsideWidthSpawn, rangeBoundarySpawn],
            CreateStraightFistRow());
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var soulWidthMonster = CreateRuntimeMonsterInstance(worldData, soulWidthSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var rangeBoundaryMonster = CreateRuntimeMonsterInstance(worldData, rangeBoundarySpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var playerSpell = new PlayerSpellSnapshotEntry(
            StraightFistSpellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 1,
            UseSpellSoul: 1);
        var session = CreateSession(
            strength: 300,
            spellEntries: [playerSpell]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = CreateStraightFistQuery(
            StraightFistSpellId,
            opponentUid: onLineMonster.Uid,
            x: 840,
            y: 584);
        var (_, spell) = ResolveStraightFistSpell(session, worldData, StraightFistSpellId);
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            session,
            worldData,
            onLineMonster,
            monsterFamily,
            spell);

        var handled = await InvokeLegacyStraightFistAsync(
            context,
            session,
            query,
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(1, animation.LevelHu);
        Assert.Equal(
            new[] { onLineMonster.Uid, soulWidthMonster.Uid }.Order().ToArray(),
            animation.Targets.Select(target => target.Uid).Order().ToArray());
        Assert.All(animation.Targets, target => Assert.Equal(expectedDamage, target.Damage));

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(soulWidthMonster, DateTimeOffset.UtcNow, out var visibleSoulWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(rangeBoundaryMonster, DateTimeOffset.UtcNow, out var visibleRangeBoundary));
        Assert.Equal(onLineMonster.MaxLife - expectedDamage, visibleOnLine.Hitpoints);
        Assert.Equal(soulWidthMonster.MaxLife - expectedDamage, visibleSoulWidth.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(rangeBoundaryMonster.MaxLife, visibleRangeBoundary.Hitpoints);

        var output = log.ToString();
        Assert.Contains("range=5", output);
        Assert.Contains("width=1", output);
        Assert.Contains("monsterTargets=2", output);
    }

    private static async ValueTask<bool> InvokeLegacyStraightFistAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerStraightFistMagicAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        return await Assert.IsType<ValueTask<bool>>(result);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveStraightFistSpell(
        WorldSession session,
        WorldDataCatalog worldData,
        ushort spellId)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            spellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MsgAttackQuery CreateStraightFistQuery(
        ushort spellId,
        uint opponentUid = 0,
        ushort x = 840,
        ushort y = 584)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: opponentUid,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static MagicType CreateStraightFistRow(
        ushort spellId = StraightFistSpellId,
        byte sort = 76,
        byte level = 0,
        uint experience = 0,
        ushort useMana = 0,
        byte useStamina = 0,
        int coldTime = 0)
    {
        return CreateMagicRow(
            spellId,
            sort,
            level: level,
            useMana: useMana,
            damage: 0,
            experience: experience,
            maxTargets: 8,
            range: 8,
            useStamina: useStamina,
            coolDown: 0,
            coldTime: coldTime);
    }

    private static int InvokeStraightFistPrivateInt32(string methodName, params object?[] arguments)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<int>(method.Invoke(null, arguments));
    }
}
