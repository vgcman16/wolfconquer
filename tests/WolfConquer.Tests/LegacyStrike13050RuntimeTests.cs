using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort Strike13050SpellId = 13_050;

    [Fact]
    public async Task Strike13050HitsMixedVisibleTargetsAnchorsClickedPlayerAndSendsOrderedInteractions()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_350, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [CreateStrike13050Row(useMana: 9, useStamina: 4)]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            stamina: 100,
            spellIds: [Strike13050SpellId]);
        var playerTarget = CreateSession(
            uid: TargetPlayerUid,
            name: "Strike13050Target",
            x: 833,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, playerTarget);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var (playerSpell, spell) = ResolveStrikeSpell(casterSession, worldData, Strike13050SpellId);
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
            playerTarget,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        var handled = await InvokeStrike13050Async(
            context,
            casterSession,
            CreateStrikeQuery(Strike13050SpellId, TargetPlayerUid, x: 840, y: 590),
            playerSpell,
            spell);

        Assert.True(handled);
        var animations = ParseSpellAnimations(casterSent);
        Assert.True(animations.Count == 1, log.ToString());
        var animation = Assert.Single(animations);
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((833, 584), (animation.X, animation.Y));
        Assert.Equal(Strike13050SpellId, animation.SpellId);
        Assert.Equal(
            new[] { monster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) },
            animation.Targets.Select(target => target.Uid).ToArray());
        Assert.Equal(expectedMonsterDamage, animation.Targets[0].Damage);
        Assert.Equal(expectedPlayerDamage, animation.Targets[1].Damage);
        Assert.Equal(expectedSobNpcDamage, animation.Targets[2].Damage);

        var interactions = ParseStrike13050PhysicalInteractions(casterSent);
        Assert.Equal(3, interactions.Count);
        Assert.Equal(
            new[] { monster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) },
            interactions.Select(packet => packet.Query.OpponentUid).ToArray());
        Assert.Equal(
            new[] { (monster.X, monster.Y), (833, 584), (sobNpc.X, sobNpc.Y) },
            interactions.Select(packet => ((int)packet.Query.X, (int)packet.Query.Y)).ToArray());
        Assert.Equal(
            new[] { unchecked((int)expectedMonsterDamage), unchecked((int)expectedPlayerDamage), unchecked((int)expectedSobNpcDamage) },
            interactions.Select(packet => packet.Query.Damage).ToArray());
        Assert.All(interactions, packet =>
        {
            Assert.Equal(PlayerUid, packet.Query.Uid);
            Assert.Equal(MsgAttackType.Physical, packet.Query.AttackType);
            Assert.Equal(0, packet.Query.KilledMonster);
            Assert.Equal(0U, packet.Query.ResponseDamage);
        });
        Assert.Equal(animation.Targets[0].Effect, interactions[0].Query.Effect);
        Assert.Equal(animation.Targets[1].Effect, interactions[1].Query.Effect);
        Assert.Equal(animation.Targets[2].Effect, interactions[2].Query.Effect);

        var animationIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out _));
        var interactionIndices = Enumerable.Range(0, casterSent.Count)
            .Where(index =>
                MsgAttackPacket.TryParse(ReadFrame(casterSent[index]), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical)
            .ToArray();
        Assert.Equal(3, interactionIndices.Length);
        Assert.All(interactionIndices, index => Assert.True(index < animationIndex));

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife - expectedMonsterDamage, visibleMonster.Hitpoints);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedTarget.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(491, updatedCaster.Character.MinMana);
        Assert.Equal("96", updatedCaster.Character.Attributes["Stamina"]);
        Assert.Contains("manualInteractionFrames=3", log.ToString());
        Assert.Contains("autoAttackFanout=fenced", log.ToString());
        Assert.Contains("source=Strike.cs case 13050", log.ToString());
    }

    [Fact]
    public async Task HandlerStrike13050UsesCasterRadiusWhenClickedPlayerAnchorsAnimationOutsideIt()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateStrike13050Row());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var clickedTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var clickedTargetConnection = CreateSendingConnection(clickedTargetSent);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [Strike13050SpellId],
            legacyRandomSpell: Strike13050SpellId);
        var clickedTargetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "ClickedOutsideRadius",
            x: 840,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(clickedTargetConnection, clickedTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateStrikeQuery(
                Strike13050SpellId,
                TargetPlayerUid,
                x: 900,
                y: 900)).ToFrameBytes()));

        var animations = ParseSpellAnimations(casterSent);
        Assert.True(animations.Count == 1, log.ToString());
        var animation = Assert.Single(animations);
        Assert.Equal((840, 584), (animation.X, animation.Y));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.DoesNotContain(animation.Targets, candidate => candidate.Uid == TargetPlayerUid);
        Assert.True(sessions.TryGet(clickedTargetConnection, out var unchangedClickedTarget));
        Assert.NotNull(unchangedClickedTarget);
        Assert.Equal(100_000, unchangedClickedTarget.Character.MinHitPoints);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(0, updatedCaster.LegacyRandomSpell);
        Assert.Contains("targetX=840 targetY=584 radius=5 targets=1 monsterTargets=1 playerTargets=0", log.ToString());
    }

    [Fact]
    public async Task Strike13050RejectsDeadPlayerAndLivePlayerBeyondCasterRadius()
    {
        const uint outsidePlayerUid = TargetPlayerUid + 1;
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateStrike13050Row());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var deadPlayerConnection = CreateSendingConnection([]);
        var outsidePlayerConnection = CreateSendingConnection([]);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 300,
            spellIds: [Strike13050SpellId]);
        var deadPlayerSession = CreateSession(
            uid: TargetPlayerUid,
            name: "DeadInsideRadius",
            x: 833,
            y: 584,
            minHitPoints: 0);
        var outsidePlayerSession = CreateSession(
            uid: outsidePlayerUid,
            name: "LiveOutsideRadius",
            x: 836,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(deadPlayerConnection, deadPlayerSession);
        sessions.Set(outsidePlayerConnection, outsidePlayerSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var (playerSpell, spell) = ResolveStrikeSpell(casterSession, worldData, Strike13050SpellId);

        var handled = await InvokeStrike13050Async(
            context,
            casterSession,
            CreateStrikeQuery(Strike13050SpellId, opponentUid: 999_999, x: 840, y: 590),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(monster.Uid, Assert.Single(animation.Targets).Uid);
        Assert.Equal(monster.Uid, Assert.Single(ParseStrike13050PhysicalInteractions(casterSent)).Query.OpponentUid);
        Assert.True(sessions.TryGet(deadPlayerConnection, out var unchangedDeadPlayer));
        Assert.NotNull(unchangedDeadPlayer);
        Assert.Equal(0, unchangedDeadPlayer.Character.MinHitPoints);
        Assert.True(sessions.TryGet(outsidePlayerConnection, out var unchangedOutsidePlayer));
        Assert.NotNull(unchangedOutsidePlayer);
        Assert.Equal(100_000, unchangedOutsidePlayer.Character.MinHitPoints);
        Assert.Contains("targets=1 monsterTargets=1 playerTargets=0 sobNpcTargets=0", log.ToString());
    }

    [Fact]
    public async Task Strike13050ProgressUsesMonsterAndSobNpcReturnsButExcludesAcceptedPlayerDamage()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_351, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [CreateStrike13050Row()]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var playerTargetConnection = CreateSendingConnection([]);
        var casterSession = CreateSession(
            x: 830,
            y: 584,
            strength: 3_000,
            spellIds: [Strike13050SpellId]);
        var playerTarget = CreateSession(
            uid: TargetPlayerUid,
            name: "ProgressExcludedPlayer",
            x: 833,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(playerTargetConnection, playerTarget);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var (playerSpell, spell) = ResolveStrikeSpell(casterSession, worldData, Strike13050SpellId);
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
            playerTarget,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);
        var expectedMonsterProgress = CalculateExpectedLegacyMonsterBaseExperience(
            checked((int)expectedMonsterDamage),
            casterSession.Character,
            monster);
        const uint expectedSobNpcProgress = 0;
        var expectedProgress = checked((ulong)expectedMonsterProgress + expectedSobNpcProgress);
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress));
        var experienceIfPlayerDamageWereCounted = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(casterSession, expectedProgress + expectedPlayerDamage));

        Assert.True(expectedMonsterProgress > 0);
        Assert.True(expectedSobNpcDamage > 0);
        Assert.True(expectedPlayerDamage > 0);

        var handled = await InvokeStrike13050Async(
            context,
            casterSession,
            CreateStrikeQuery(Strike13050SpellId, TargetPlayerUid, x: 900, y: 900),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedCaster.Character.Experience);
        Assert.NotEqual(
            checked((long)experienceIfPlayerDamageWereCounted.AppliedExperienceGain),
            updatedCaster.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedCaster.PlayerSpells.Spells).Experience);
        Assert.Contains($"monsterProgress={expectedMonsterProgress}", log.ToString());
        Assert.Contains($"sobNpcProgress={expectedSobNpcProgress}", log.ToString());
        Assert.Contains("playerTargets=1", log.ToString());
    }

    [Fact]
    public async Task Strike13050TrainingSobNpcReturnContributesPositiveCharacterProgress()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateTrainingSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateStrike13050Row());
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: 1_039,
            x: 84,
            y: 70,
            strength: 3_000,
            spellIds: [Strike13050SpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);
        var (playerSpell, spell) = ResolveStrikeSpell(session, worldData, Strike13050SpellId);
        var expectedDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            session,
            spell,
            worldData,
            1U);
        var expectedAppliedDamage = Math.Min(expectedDamage, 10_000U);
        var expectedProgress = expectedAppliedDamage >= 10_000
            ? expectedAppliedDamage / 10
            : expectedAppliedDamage / 1_000;
        var expectedExperience = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateExpectedLegacyExperienceInputs(session, expectedProgress));

        Assert.True(expectedProgress > 0);

        var handled = await InvokeStrike13050Async(
            context,
            session,
            CreateStrikeQuery(
                Strike13050SpellId,
                checked((uint)sobNpc.Id),
                checked((ushort)sobNpc.X),
                checked((ushort)sobNpc.Y)),
            playerSpell,
            spell);

        Assert.True(handled);
        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(checked((uint)sobNpc.Id), Assert.Single(animation.Targets).Uid);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(checked((long)expectedExperience.AppliedExperienceGain), updatedSession.Character.Experience);
        Assert.Equal(0, Assert.Single(updatedSession.PlayerSpells.Spells).Experience);
        Assert.Contains("monsterProgress=0", log.ToString());
        Assert.Contains($"sobNpcProgress={expectedProgress}", log.ToString());
        Assert.Contains("playerTargets=0 sobNpcTargets=1", log.ToString());
    }

    [Fact]
    public async Task Strike13050WithoutAcceptedTargetsSpendsResourcesAndSendsNoInteractionOrAnimation()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateStrike13050Row(useMana: 9, useStamina: 4));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(stamina: 100, spellIds: [Strike13050SpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore());
        var (playerSpell, spell) = ResolveStrikeSpell(session, worldData, Strike13050SpellId);

        var handled = await InvokeStrike13050Async(
            context,
            session,
            CreateStrikeQuery(Strike13050SpellId, opponentUid: 999_999, x: 840, y: 590),
            playerSpell,
            spell);

        Assert.True(handled);
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseStrike13050PhysicalInteractions(sent));
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(491, updatedSession.Character.MinMana);
        Assert.Equal("96", updatedSession.Character.Attributes["Stamina"]);
        Assert.Contains("targets=0", log.ToString());
        Assert.Contains("no-target silence", log.ToString());
    }

    private static MagicType CreateStrike13050Row(ushort useMana = 0, byte useStamina = 0)
    {
        return CreateMagicRow(
            Strike13050SpellId,
            sort: 81,
            useMana: useMana,
            damage: 100,
            range: 5,
            weaponType: 61_626_626,
            useStamina: useStamina,
            coldTime: 0);
    }

    private static async ValueTask<bool> InvokeStrike13050Async(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "HandleLegacyPlayerStrike13050MagicAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var result = method.Invoke(
            null,
            [context, session, query, playerSpell, spell, CancellationToken.None]);
        var valueTask = Assert.IsType<ValueTask<bool>>(result);
        return await valueTask.ConfigureAwait(false);
    }

    private static List<MsgAttackPacket> ParseStrike13050PhysicalInteractions(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical)
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
