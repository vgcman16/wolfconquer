using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint ExpectedGapingWoundsMonsterDamage = 4_500;
    private const uint ExpectedGapingWoundsPlayerDamage = 5_000;
    private const uint ExpectedGapingWoundsToxinEraserPlayerDamage = 7_000;
    private const uint ExpectedGapingWoundsSobNpcDamage = 45_000;
    private const uint ExpectedGapingWoundsBossAnimationDamage = 1;

    [Fact]
    public async Task HandlerGapingWoundsDirectMonsterTargetSpendsResourcesSendsAnimationAppliesHpPercentDamageAndProgress()
    {
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-gapingwounds-spell-xp-" + Guid.NewGuid().ToString("N"),
            "PlayersSpells");
        try
        {
            var monsterFamily = CreateMonsterFamily();
            var targetSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584);
            var bystanderSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 835, boundY: 584);
            var worldData = CreateMagicCatalogCore(
                monsterFamily,
                [targetSpawn, bystanderSpawn],
                sobNpcs: [],
                magicRows:
                [
                    CreateGapingWoundsRow(experience: 100),
                    CreateGapingWoundsRow(level: 1, experience: 200),
                ]);
            var targetMonster = CreateRuntimeMonsterInstance(worldData, targetSpawn, monsterFamily);
            var bystanderMonster = CreateRuntimeMonsterInstance(worldData, bystanderSpawn, monsterFamily);
            var monsterStates = new MonsterRuntimeStateStore();
            var sessions = new WorldSessionStore();
            var sent = new List<byte[]>();
            using var log = new StringWriter();
            var connection = CreateSendingConnection(sent);
            var session = CreateSession(
                minMana: 50,
                stamina: 100,
                spellIds: [GapingWoundsSpellId]);
            sessions.Set(connection, session);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                PlayerSpellsRoot: spellsRoot);

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                    targetMonster.Uid,
                    targetMonster.X,
                    targetMonster.Y)).ToFrameBytes()));

            var spellFrameIndex = Assert.Single(
                Enumerable.Range(0, sent.Count),
                index => LegacySpellAnimationPacket.TryParse(ReadFrame(sent[index]), out _));
            var manaUpdateIndex = Assert.Single(
                Enumerable.Range(0, sent.Count),
                index => LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update) &&
                    update.Updates.Count == 1 &&
                    update.Updates[0].Type == LegacyMsgUpdateDataType.Mana);
            var staminaUpdateIndex = Assert.Single(
                Enumerable.Range(0, sent.Count),
                index => LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[index]), out var update) &&
                    update.Updates.Count == 1 &&
                    update.Updates[0].Type == LegacyMsgUpdateDataType.Stamina);
            var coldTimeIndex = Assert.Single(
                Enumerable.Range(0, sent.Count),
                index => LegacyMagicColdTimePacket.TryParse(ReadFrame(sent[index]), out _));
            Assert.True(staminaUpdateIndex < spellFrameIndex);
            Assert.True(manaUpdateIndex < spellFrameIndex);
            Assert.True(coldTimeIndex < spellFrameIndex);

            var spell = Assert.Single(ParseSpellAnimations(sent));
            AssertGapingWoundsAnimationHeader(spell, targetMonster.Uid, targetMonster.X, targetMonster.Y);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(targetMonster.Uid, target.Uid);
            Assert.Equal(ExpectedGapingWoundsMonsterDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);

            Assert.True(monsterStates.TryGetVisibleInstance(targetMonster, DateTimeOffset.UtcNow, out var visibleTarget));
            Assert.Equal(targetMonster.MaxLife - ExpectedGapingWoundsMonsterDamage, visibleTarget.Hitpoints);
            Assert.True(monsterStates.TryGetVisibleInstance(bystanderMonster, DateTimeOffset.UtcNow, out var visibleBystander));
            Assert.Equal(bystanderMonster.MaxLife, visibleBystander.Hitpoints);

            var spellUpdate = Assert.Single(ParseCruelShadeSpellPackets(sent));
            Assert.Equal(GapingWoundsSpellId, spellUpdate.Id);
            Assert.Equal(1, spellUpdate.Level);
            Assert.Equal(0, spellUpdate.Experience);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(40, updatedSession.Character.MinMana);
            Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
            Assert.NotNull(updatedSession.MagicCooldowns);
            Assert.True(updatedSession.MagicCooldowns.ContainsKey(GapingWoundsSpellId));
            var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
            Assert.Equal(GapingWoundsSpellId, updatedSpell.Id);
            Assert.Equal(1, updatedSpell.Level);
            Assert.Equal(0, updatedSpell.Experience);

            var output = log.ToString();
            Assert.Contains("spellId=11230", output);
            Assert.Contains("targetKind=monster", output);
            Assert.Contains("source=DecLife.cs", output);
            AssertNoGapingWoundsRuntimeGap(output);
        }
        finally
        {
            var tempRoot = Path.GetDirectoryName(spellsRoot);
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task HandlerGapingWoundsDistanceEqualToRangeSpendsResourcesThenStopsBeforeAnimationOrDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 835, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateGapingWoundsRow(range: 5));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                monster.Uid,
                monster.X,
                monster.Y)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        AssertGapingWoundsAnimationHeader(spell, monster.Uid, monster.X, monster.Y);
        Assert.Empty(spell.Targets);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        Assert.Empty(ParseCruelShadeSpellPackets(sent));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(GapingWoundsSpellId));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy GapingWounds magic", output);
        Assert.Contains("distance=5", output);
        Assert.Contains("range=5", output);
        Assert.Contains("targets=0", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    [Fact]
    public async Task HandlerGapingWoundsDirectPlayerTargetWithoutToxinEraserAppliesSourceOverwriteDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateGapingWoundsRow(coldTime: 0)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: ExpectedGapingWoundsPlayerDamage);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y))).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            AssertGapingWoundsAnimationHeader(
                spell,
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y));
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedGapingWoundsPlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.Empty(ParseCruelShadeSpellPackets(casterSent));
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(40, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(0, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("spellId=11230", output);
        Assert.Contains("targetKind=player", output);
        Assert.Contains("playerTargets=1", output);
        Assert.DoesNotContain("PARITY-GAP GapingWounds player ToxinEraser missing", output);
        Assert.DoesNotContain("gap=gapingwounds-toxin-eraser-damage", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    [Fact]
    public async Task HandlerGapingWoundsDirectPlayerTargetWithToxinEraserRollMissAppliesSourceOverwriteDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateGapingWoundsRow(coldTime: 0)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: 10_000,
            attributes: new Dictionary<string, string>
            {
                ["PerfectionStatus.ToxinEraser"] = "30",
                ["LegacyGapingWoundsToxinEraserRoll"] = "99",
                [PlayerReceiveResponseTortoiseAttribute] = "1",
                [PlayerReceiveResponseRecoveryRoll10Attribute] = "0",
                [PlayerReceiveResponseRecoveryRoll5Attribute] = "0",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y))).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            AssertGapingWoundsAnimationHeader(
                spell,
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y));
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedGapingWoundsToxinEraserPlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.Empty(ParseCruelShadeSpellPackets(casterSent));
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(3_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            PlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedTargetSession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(
            TargetPlayerUid.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes[RuntimeLegacyThundercloudSightAttribute]);

        var responseEffectIndex = SingleFrameIndex(targetSent, frame =>
            LegacyStringPacket.TryParse(frame, out var packet) &&
            IsTargetPlayerReceiveResponseEffect(packet, PlayerReceiveResponseRecoveryEffect));
        var spellAnimationIndex = SingleFrameIndex(targetSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == GapingWoundsSpellId &&
            packet.Targets.Count == 1 &&
            packet.Targets[0].Uid == TargetPlayerUid &&
            packet.Targets[0].Damage == ExpectedGapingWoundsToxinEraserPlayerDamage);
        Assert.True(responseEffectIndex < spellAnimationIndex);

        var output = log.ToString();
        Assert.Contains("spellId=11230", output);
        Assert.Contains("targetKind=player", output);
        Assert.Contains("playerTargets=1", output);
        Assert.DoesNotContain("PARITY-GAP GapingWounds player ToxinEraser missing", output);
        Assert.DoesNotContain("gap=gapingwounds-toxin-eraser-damage", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    [Fact]
    public async Task HandlerGapingWoundsDirectPlayerTargetWithToxinEraserSendsEmptyAnimationWithoutPlayerDamageOrProgress()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows:
            [
                CreateGapingWoundsRow(experience: 100),
                CreateGapingWoundsRow(level: 1, experience: 200),
            ]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            attributes: new Dictionary<string, string>
            {
                ["PerfectionStatus.ToxinEraser"] = "30",
                ["LegacyGapingWoundsToxinEraserRoll"] = "0",
            });
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y))).ToFrameBytes()));

        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        AssertGapingWoundsEmptyAnimation(
            casterAnimation,
            TargetPlayerUid,
            checked((ushort)targetSession.Character.X),
            checked((ushort)targetSession.Character.Y));
        var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
        AssertGapingWoundsEmptyAnimation(
            targetAnimation,
            TargetPlayerUid,
            checked((ushort)targetSession.Character.X),
            checked((ushort)targetSession.Character.Y));
        Assert.Empty(ParseCruelShadeSpellPackets(casterSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(40, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.Equal(0, Assert.Single(updatedCasterSession.PlayerSpells.Spells).Experience);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(targetSession.Character.MinHitPoints, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("spellId=11230", output);
        Assert.Contains("targetKind=player", output);
        Assert.Contains("targets=0", output);
        Assert.DoesNotContain("PARITY-GAP GapingWounds player ToxinEraser missing", output);
        Assert.DoesNotContain("gap=gapingwounds-toxin-eraser-damage", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    [Fact]
    public async Task HandlerGapingWoundsDirectSobNpcTargetUsesOneHundredMinusSpellDamagePercent()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateMoveLineSobNpc(id: 65_351, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows: [CreateGapingWoundsRow(coldTime: 0)]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                checked((uint)sobNpc.Id),
                checked((ushort)sobNpc.X),
                checked((ushort)sobNpc.Y))).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        AssertGapingWoundsAnimationHeader(
            spell,
            checked((uint)sobNpc.Id),
            checked((ushort)sobNpc.X),
            checked((ushort)sobNpc.Y));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(ExpectedGapingWoundsSobNpcDamage, target.Damage);
        Assert.NotEqual(ExpectedCruelShadeSobNpcDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - checked((int)ExpectedGapingWoundsSobNpcDamage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("spellId=11230", output);
        Assert.Contains("targetKind=sob-npc", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    [Fact]
    public async Task HandlerGapingWoundsDirectBossMonsterTargetSendsOneDamageAnimationWithoutMutatingHitpointsOrProgress()
    {
        var bossFamily = CreateLegacyBossFamily(
            monsterTypeId: 9_102,
            sourceMonsterName: "GapingWoundsBoss",
            life: "5000",
            lookface: "100");
        var bossSpawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: bossFamily.Id,
            sourceMonsterName: bossFamily.Name,
            boundX: 834,
            boundY: 584);
        var worldData = CreateMagicCatalogCore(
            bossFamily,
            [bossSpawn],
            sobNpcs: [],
            magicRows:
            [
                CreateGapingWoundsRow(experience: 100),
                CreateGapingWoundsRow(level: 1, experience: 200),
            ]);
        var bossMonster = CreateRuntimeMonsterInstance(worldData, bossSpawn, bossFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                bossMonster.Uid,
                bossMonster.X,
                bossMonster.Y)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        AssertGapingWoundsAnimationHeader(spell, bossMonster.Uid, bossMonster.X, bossMonster.Y);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(bossMonster.Uid, target.Uid);
        Assert.Equal(ExpectedGapingWoundsBossAnimationDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        Assert.True(monsterStates.TryGetVisibleInstance(bossMonster, DateTimeOffset.UtcNow, out var visibleBoss));
        Assert.Equal(bossMonster.MaxLife, visibleBoss.Hitpoints);
        Assert.Empty(ParseCruelShadeSpellPackets(sent));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.Equal(0, Assert.Single(updatedSession.PlayerSpells.Spells).Experience);

        var output = log.ToString();
        Assert.Contains("spellId=11230", output);
        Assert.Contains("targetKind=monster", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    private static MsgAttackQuery CreateGapingWoundsQuery(uint opponentUid, ushort x, ushort y)
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
            SpellId: GapingWoundsSpellId);
    }

    private static MagicType CreateGapingWoundsRow(
        byte level = 0,
        uint experience = 0,
        ushort useMana = 10,
        byte range = 6,
        int coldTime = 600,
        uint damage = 30_010)
    {
        return CreateDecLifeRow(
            GapingWoundsSpellId,
            "GapingWounds",
            level,
            experience,
            useMana,
            range,
            coldTime,
            damage);
    }

    private static void AssertGapingWoundsAnimationHeader(
        LegacySpellAnimationPacket animation,
        uint attackedUid,
        ushort x,
        ushort y)
    {
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(attackedUid, animation.AttackedUid);
        Assert.Equal(x, animation.X);
        Assert.Equal(y, animation.Y);
        Assert.Equal(GapingWoundsSpellId, animation.SpellId);
        Assert.Equal(0, animation.SpellLevel);
        Assert.Equal(0, animation.LevelHu);
    }

    private static void AssertGapingWoundsEmptyAnimation(
        LegacySpellAnimationPacket animation,
        uint attackedUid,
        ushort x,
        ushort y)
    {
        AssertGapingWoundsAnimationHeader(animation, attackedUid, x, y);
        Assert.Empty(animation.Targets);
    }

    private static void AssertNoGapingWoundsRuntimeGap(string output)
    {
        Assert.DoesNotContain("gap=unsupported-magic", output);
        Assert.DoesNotContain("unsupported DecLife spell id", output);
        Assert.DoesNotContain("unsupported magic sort", output);
        Assert.DoesNotContain("DecLife handling is currently modeled only for CruelShade", output);
        Assert.DoesNotContain("PARITY-GAP GapingWounds player ToxinEraser missing", output);
        Assert.DoesNotContain("gap=gapingwounds-toxin-eraser-damage", output);
        Assert.DoesNotContain("PARITY-GAP GapingWounds player HP/death is modeled", output);
        Assert.DoesNotContain("gap=gapingwounds-player-receive-side-effects", output);
    }
}
