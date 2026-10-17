using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DragonSlashSpellId = 12_350;
    private const int DragonSlashWildPhoenixFlag = 0x170;
    private const uint ExpectedDragonSlashMonsterDamage = 730;
    private const uint ExpectedDragonSlashPlayerDamage = 183;
    private const uint ExpectedDragonSlashSobNpcDamage = 330;

    [Fact]
    public async Task HandlerDragonSlashLineHitsMixedTargetsWithSourceRangeWidthDamageAndSingleAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var algorithmEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 840, boundY: 584);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 836, boundY: 585);
        var rangeEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 841, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_350, x: 836, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, algorithmEdgeSpawn, outsideWidthSpawn, rangeEdgeSpawn],
            [sobNpc],
            [
                CreateDragonSlashRow(experience: 1),
                CreateDragonSlashRow(level: 1, experience: 100),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var algorithmEdgeMonster = CreateRuntimeMonsterInstance(worldData, algorithmEdgeSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var rangeEdgeMonster = CreateRuntimeMonsterInstance(worldData, rangeEdgeSpawn, monsterFamily);
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
            spellIds: [DragonSlashSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 840,
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

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                onLineMonster,
                legacyEncodedQuery: false,
                spellId: DragonSlashSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(DragonSlashSpellId, animation.SpellId);
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
            new[] { onLineMonster.Uid, algorithmEdgeMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(ExpectedDragonSlashMonsterDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(ExpectedDragonSlashMonsterDamage, damageByUid[algorithmEdgeMonster.Uid]);
        Assert.Equal(ExpectedDragonSlashPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(ExpectedDragonSlashSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(algorithmEdgeMonster, DateTimeOffset.UtcNow, out var visibleAlgorithmEdge));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(rangeEdgeMonster, DateTimeOffset.UtcNow, out var visibleRangeEdge));
        Assert.Equal(onLineMonster.MaxLife - ExpectedDragonSlashMonsterDamage, visibleOnLine.Hitpoints);
        Assert.Equal(algorithmEdgeMonster.MaxLife - ExpectedDragonSlashMonsterDamage, visibleAlgorithmEdge.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(rangeEdgeMonster.MaxLife, visibleRangeEdge.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - ExpectedDragonSlashPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)ExpectedDragonSlashSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.Contains(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 85)]));
        Assert.Empty(ParseMagicColdTimePackets(casterSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("85", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey("HitShoot"));
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(DragonSlashSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy DragonSlash Line magic", output);
        Assert.Contains("range=11", output);
        Assert.Contains("algorithmRange=10", output);
        Assert.Contains("width=0", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("monsterProgress=146", output);
        Assert.Contains("sobNpcProgress=330", output);
        Assert.Contains("counterHitShoots=0", output);
        Assert.Contains("source=Line.cs case 12350", output);
    }

    [Fact]
    public async Task HandlerDragonSlashLineCapsMonsterProgressAtLegacyMonsterExecuteReturn()
    {
        const uint lowMaxLife = 100;
        var monsterFamily = CreateMonsterFamily() with
        {
            Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
            {
                ["life"] = lowMaxLife.ToString(),
            },
        };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 9, boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [CreateDragonSlashRow(experience: 10_000)]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [DragonSlashSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: DragonSlashSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.Equal(ExpectedDragonSlashMonsterDamage, target.Damage);
        Assert.False(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out _));

        var output = log.ToString();
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("monsterProgress=10", output);
        Assert.Contains("source=Line.cs case 12350", output);
    }

    [Fact]
    public async Task HandlerDragonSlashLineSpendsStaminaBeforeManaRejection()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [CreateDragonSlashRow(useMana: 20)]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            minMana: 10,
            spellIds: [DragonSlashSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: DragonSlashSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseMagicColdTimePackets(sent));
        Assert.Contains(sent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 85)]));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("85", updatedSession.Character.Attributes["Stamina"]);
        Assert.Equal(10, updatedSession.Character.MinMana);
        Assert.True(updatedSession.MagicCooldowns is null || !updatedSession.MagicCooldowns.ContainsKey(DragonSlashSpellId));

        var output = log.ToString();
        Assert.Contains("spellId=12350", output);
        Assert.Contains("mana=10", output);
        Assert.Contains("requiredMana=20", output);
        Assert.DoesNotContain("WorldServer handled legacy DragonSlash Line magic", output);
    }

    [Fact]
    public async Task HandlerDragonSlashLineAgainstWildPhoenixPlayerConsumesDefenderChargeAndScalesDamageToZero()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateDragonSlashRow(),
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
            spellIds: [DragonSlashSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 840,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(DragonSlashWildPhoenixFlag),
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
        var expectedPlayerDamage = 1U * 10 / 18;
        Assert.Equal(0U, expectedPlayerDamage);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: DragonSlashSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(DragonSlashSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
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
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, DragonSlashWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == DragonSlashWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy DragonSlash Line magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("totalAppliedDamage=0", output);
        Assert.Contains("source=Line.cs case 12350", output);
    }

    [Theory]
    [InlineData(1_005, "9", 2)]
    [InlineData(1_015, "7", 0)]
    public async Task HandlerDragonSlashLineCounterHitMapUpdatesHitShootForMonsterHits(
        long mapId,
        string expectedHitShoot,
        int expectedCounterHitShoots)
    {
        var monsterFamily = CreateMonsterFamily();
        var firstSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 21, boundX: 833, boundY: 584) with
        {
            MapId = checked((int)mapId),
        };
        var secondSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 22, boundX: 840, boundY: 584) with
        {
            MapId = checked((int)mapId),
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [firstSpawn, secondSpawn],
            [],
            [CreateDragonSlashRow()]);
        var firstMonster = CreateRuntimeMonsterInstance(worldData, firstSpawn, monsterFamily);
        _ = CreateRuntimeMonsterInstance(worldData, secondSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [DragonSlashSpellId],
            mapId: mapId,
            attributes: new Dictionary<string, string>
            {
                ["HitShoot"] = "7",
            });
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
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                firstMonster,
                legacyEncodedQuery: false,
                spellId: DragonSlashSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(expectedHitShoot, updatedSession.Character.Attributes["HitShoot"]);

        var output = log.ToString();
        Assert.Contains($"counterHitShoots={expectedCounterHitShoots}", output);
    }

    [Theory]
    [InlineData(1_005)]
    [InlineData(6_000)]
    public async Task HandlerDragonSlashLineCounterHitMapDoesNotUpdateHitShootForPlayerOnlyHits(long mapId)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [CreateDragonSlashRow()]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [DragonSlashSpellId],
            mapId: mapId,
            attributes: new Dictionary<string, string>
            {
                ["HitShoot"] = "7",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 840,
            y: 584,
            mapId: mapId,
            minHitPoints: 100_000);
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
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 842,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: DragonSlashSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("7", updatedCasterSession.Character.Attributes["HitShoot"]);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - ExpectedDragonSlashPlayerDamage, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("counterHitShoots=0", output);
    }

    private static MagicType CreateDragonSlashRow(
        byte level = 0,
        uint experience = 0,
        ushort useMana = 0)
    {
        return CreateMagicRow(
            DragonSlashSpellId,
            sort: 14,
            level: level,
            useMana: useMana,
            damage: level switch
            {
                0 => 30_110U,
                1 => 30_115U,
                2 => 30_120U,
                3 => 30_125U,
                _ => 30_135U,
            },
            experience: experience,
            maxTargets: 4,
            range: 11,
            needLevel: level switch
            {
                0 => 40,
                1 => 50,
                2 => 60,
                3 => 70,
                _ => 0,
            },
            weaponType: 61_617_617,
            useStamina: 15,
            coolDown: 600,
            coldTime: 0);
    }
}
