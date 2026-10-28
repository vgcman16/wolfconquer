using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort CruelShadeSpellId = 3_050;
    private const ushort GapingWoundsSpellId = 11_230;
    private const ushort UnsupportedDecLifeSpellId = 11_231;
    private const byte DecLifeSort = 26;
    private const uint ExpectedCruelShadeMonsterDamage = 4_500;
    private const uint ExpectedCruelShadePlayerDamage = 5_000;
    private const uint ExpectedCruelShadeSobNpcDamage = 5_000;

    [Fact]
    public async Task HandlerCruelShadeDirectMonsterTargetSpendsResourcesBeforeAnimationAppliesSourceDamageAndProgress()
    {
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-cruelshade-spell-xp-" + Guid.NewGuid().ToString("N"),
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
                    CreateCruelShadeRow(experience: 100),
                    CreateCruelShadeRow(level: 1, experience: 200),
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
                spellIds: [CruelShadeSpellId]);
            sessions.Set(connection, session);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                PlayerSpellsRoot: spellsRoot);
            using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
            context = combatSpellOwner.Context;

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(CreateCruelShadeQuery(
                    targetMonster.Uid,
                    targetMonster.X,
                    targetMonster.Y)).ToFrameBytes()));

            var spellIndex = Assert.Single(
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
            Assert.True(staminaUpdateIndex < spellIndex);
            Assert.True(manaUpdateIndex < spellIndex);
            Assert.True(coldTimeIndex < spellIndex);

            var spell = Assert.Single(ParseSpellAnimations(sent));
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(targetMonster.Uid, spell.AttackedUid);
            Assert.Equal(targetMonster.X, spell.X);
            Assert.Equal(targetMonster.Y, spell.Y);
            Assert.Equal(CruelShadeSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(targetMonster.Uid, target.Uid);
            Assert.Equal(ExpectedCruelShadeMonsterDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);

            Assert.True(monsterStates.TryGetVisibleInstance(targetMonster, DateTimeOffset.UtcNow, out var visibleTarget));
            Assert.Equal(targetMonster.MaxLife - ExpectedCruelShadeMonsterDamage, visibleTarget.Hitpoints);
            Assert.True(monsterStates.TryGetVisibleInstance(bystanderMonster, DateTimeOffset.UtcNow, out var visibleBystander));
            Assert.Equal(bystanderMonster.MaxLife, visibleBystander.Hitpoints);

            var spellUpdate = Assert.Single(ParseCruelShadeSpellPackets(sent));
            Assert.Equal(CruelShadeSpellId, spellUpdate.Id);
            Assert.Equal(1, spellUpdate.Level);
            Assert.Equal(0, spellUpdate.Experience);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(40, updatedSession.Character.MinMana);
            Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
            Assert.NotNull(updatedSession.MagicCooldowns);
            Assert.True(updatedSession.MagicCooldowns.ContainsKey(CruelShadeSpellId));
            var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
            Assert.Equal(CruelShadeSpellId, updatedSpell.Id);
            Assert.Equal(1, updatedSpell.Level);
            Assert.Equal(0, updatedSpell.Experience);

            var output = log.ToString();
            Assert.Contains("WorldServer handled legacy CruelShade magic", output);
            Assert.Contains("spellId=3050", output);
            Assert.Contains("sort=26", output);
            Assert.Contains("targetKind=monster", output);
            Assert.Contains("source=DecLife.cs", output);
            Assert.DoesNotContain("unsupported monster magic sort", output);
            Assert.DoesNotContain("gap=unsupported-magic", output);
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
    public async Task HandlerCruelShadeDistanceEqualToRangeSpendsResourcesThenStopsBeforeAnimationOrDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 835, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateCruelShadeRow(range: 5));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [CruelShadeSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateCruelShadeQuery(
                monster.Uid,
                monster.X,
                monster.Y)).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(monster.Uid, spell.AttackedUid);
        Assert.Equal(monster.X, spell.X);
        Assert.Equal(monster.Y, spell.Y);
        Assert.Equal(CruelShadeSpellId, spell.SpellId);
        Assert.Empty(spell.Targets);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(CruelShadeSpellId));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CruelShade magic", output);
        Assert.Contains("distance=5", output);
        Assert.Contains("range=5", output);
        Assert.Contains("targets=0", output);
        Assert.DoesNotContain("gap=unsupported-magic", output);
    }

    [Fact]
    public async Task HandlerCruelShadeDirectPlayerTargetAppliesSourceToxinEraserOverwriteDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateCruelShadeRow(coldTime: 0)]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [CruelShadeSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: ExpectedCruelShadePlayerDamage);
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
            ReadFrame(new MsgAttackPacket(CreateCruelShadeQuery(
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y))).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(TargetPlayerUid, spell.AttackedUid);
            Assert.Equal(targetSession.Character.X, spell.X);
            Assert.Equal(targetSession.Character.Y, spell.Y);
            Assert.Equal(CruelShadeSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedCruelShadePlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(40, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(0, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CruelShade magic", output);
        Assert.Contains("targetKind=player", output);
        Assert.Contains("source=DecLife.cs", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
    }

    [Fact]
    public async Task HandlerGapingWoundsDirectPlayerTargetInRegularArenaSendsQualifierScoreBeforeAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [],
            magicRows: [CreateGapingWoundsRow(coldTime: 0)]);
        var sessions = new WorldSessionStore();
        var arenaStates = new ArenaSessionStateStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var targetAttributes = CreateRegularArenaPlayerReceiveQualifierAttributes(mapId: 700)!;
        targetAttributes["PerfectionStatus.ToxinEraser"] = "30";
        targetAttributes["LegacyGapingWoundsToxinEraserRoll"] = "99";
        var casterSession = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [GapingWoundsSpellId],
            attributes: CreateRegularArenaPlayerReceiveQualifierAttributes(mapId: 700),
            mapId: 700);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 584,
            minHitPoints: 10_000,
            attributes: targetAttributes,
            mapId: 700);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        ActivateRegularArenaMatch(arenaStates, casterConnection, targetConnection);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            ArenaStates: arenaStates,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateGapingWoundsQuery(
                TargetPlayerUid,
                checked((ushort)targetSession.Character.X),
                checked((ushort)targetSession.Character.Y))).ToFrameBytes()));

        foreach (var spell in new[]
                 {
                     Assert.Single(ParseSpellAnimations(casterSent)),
                     Assert.Single(ParseSpellAnimations(targetSent)),
                 })
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

        AssertRegularArenaQualifierScore(
            Assert.Single(ParseArenaMatchScores(casterSent)),
            expectedCasterDamage: ExpectedGapingWoundsToxinEraserPlayerDamage,
            expectedTargetDamage: 0);
        AssertRegularArenaQualifierScore(
            Assert.Single(ParseArenaMatchScores(targetSent)),
            expectedCasterDamage: ExpectedGapingWoundsToxinEraserPlayerDamage,
            expectedTargetDamage: 0);

        var casterScoreIndex = SingleFrameIndex(casterSent, frame =>
            LegacyArenaMatchScorePacket.TryParse(frame, out _));
        var casterAnimationIndex = SingleFrameIndex(casterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == GapingWoundsSpellId);
        var targetScoreIndex = SingleFrameIndex(targetSent, frame =>
            LegacyArenaMatchScorePacket.TryParse(frame, out _));
        var targetAnimationIndex = SingleFrameIndex(targetSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
            packet.SpellId == GapingWoundsSpellId);
        Assert.True(casterScoreIndex < casterAnimationIndex);
        Assert.True(targetScoreIndex < targetAnimationIndex);

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

        var output = log.ToString();
        Assert.Contains("spellId=11230", output);
        Assert.Contains("targetKind=player", output);
        Assert.Contains("WorldServer applied legacy player receive qualifier arena score", output);
        AssertNoGapingWoundsRuntimeGap(output);
    }

    [Fact]
    public async Task HandlerCruelShadeDirectSobNpcTargetAppliesDamagePercentWithoutMonsterOrPlayerTargets()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateMoveLineSobNpc(id: 65_350, x: 834, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            spawns: [],
            sobNpcs: [sobNpc],
            magicRows: [CreateCruelShadeRow(coldTime: 0)]);
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [CruelShadeSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateCruelShadeQuery(
                checked((uint)sobNpc.Id),
                checked((ushort)sobNpc.X),
                checked((ushort)sobNpc.Y))).ToFrameBytes()));

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(checked((uint)sobNpc.Id), spell.AttackedUid);
        Assert.Equal(sobNpc.X, spell.X);
        Assert.Equal(sobNpc.Y, spell.Y);
        Assert.Equal(CruelShadeSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
        Assert.Equal(ExpectedCruelShadeSobNpcDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - checked((int)ExpectedCruelShadeSobNpcDamage), visibleSobNpc.Life);
        Assert.Equal(sobNpc.MaxLife, visibleSobNpc.MaxLife);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy CruelShade magic", output);
        Assert.Contains("targetKind=sob-npc", output);
        Assert.Contains("source=DecLife.cs", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
    }

    [Fact]
    public async Task HandlerCruelShadeDoesNotClaimUnknownDecLifeSpellRuntimeAndLogsGapAfterResources()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateUnsupportedDecLifeRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            minMana: 50,
            stamina: 100,
            spellIds: [UnsupportedDecLifeSpellId]);
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
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: UnsupportedDecLifeSpellId)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(40, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        Assert.NotNull(updatedSession.MagicCooldowns);
        Assert.True(updatedSession.MagicCooldowns.ContainsKey(UnsupportedDecLifeSpellId));

        var output = log.ToString();
        Assert.Contains("gap=unsupported-magic", output);
        Assert.Contains("spellId=11231", output);
        Assert.Contains("sort=26", output);
        Assert.Contains("DecLife", output);
        Assert.DoesNotContain("WorldServer handled legacy CruelShade magic", output);
    }

    private static MsgAttackQuery CreateCruelShadeQuery(uint opponentUid, ushort x, ushort y)
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
            SpellId: CruelShadeSpellId);
    }

    private static MagicType CreateCruelShadeRow(
        byte level = 0,
        uint experience = 0,
        ushort useMana = 10,
        byte range = 6,
        int coldTime = 600)
    {
        return CreateDecLifeRow(
            CruelShadeSpellId,
            "CruelShade",
            level,
            experience,
            useMana,
            range,
            coldTime,
            damage: 30_010);
    }

    private static MagicType CreateUnsupportedDecLifeRow()
    {
        return CreateDecLifeRow(
            UnsupportedDecLifeSpellId,
            "UnsupportedDecLife",
            level: 0,
            experience: 0,
            useMana: 10,
            range: 6,
            coldTime: 600,
            damage: 30_010);
    }

    private static MagicType CreateDecLifeRow(
        ushort spellId,
        string name,
        byte level,
        uint experience,
        ushort useMana,
        byte range,
        int coldTime,
        uint damage)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = DecLifeSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = name;
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = useMana.ToString(CultureInfo.InvariantCulture);
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[14] = "0";
        fields[15] = range.ToString(CultureInfo.InvariantCulture);
        fields[18] = experience.ToString(CultureInfo.InvariantCulture);
        fields[20] = "15";
        fields[29] = "5";
        fields[33] = "1";
        fields[35] = damage.ToString(CultureInfo.InvariantCulture);
        fields[36] = "200000";
        fields[37] = "20000";
        fields[47] = coldTime.ToString(CultureInfo.InvariantCulture);
        return new MagicType(spellId, spellId, name, fields);
    }

    private static List<LegacySpellPacket> ParseCruelShadeSpellPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
