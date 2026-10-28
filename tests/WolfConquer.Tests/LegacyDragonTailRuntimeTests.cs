using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DragonTailSpellId = 11_000;
    private const int DragonTailWildPhoenixFlag = 0x170;
    private const uint ExpectedDragonTailMonsterDamage = 627;
    private const uint ExpectedDragonTailPlayerDamage = 285;
    private const uint ExpectedDragonTailSobNpcDamage = 285;

    [Fact]
    public async Task HandlerDragonTailLineHitsMixedTargetsWithSourceWidthDamageAndSingleAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var secondOnLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 834, boundY: 584);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 833, boundY: 585);
        var rangeEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 835, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_006, x: 832, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, secondOnLineSpawn, outsideWidthSpawn, rangeEdgeSpawn],
            [sobNpc],
            [
                CreateDragonTailRow(experience: 1),
                CreateDragonTailRow(level: 1, experience: 100),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var secondOnLineMonster = CreateRuntimeMonsterInstance(worldData, secondOnLineSpawn, monsterFamily);
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
            spellIds: [DragonTailSpellId]);
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
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                onLineMonster,
                legacyEncodedQuery: false,
                spellId: DragonTailSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(842, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(DragonTailSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            Assert.Equal(0, spell.LevelHu);
            Assert.All(spell.Targets, target =>
            {
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
            });
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, secondOnLineMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(ExpectedDragonTailMonsterDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(ExpectedDragonTailMonsterDamage, damageByUid[secondOnLineMonster.Uid]);
        Assert.Equal(ExpectedDragonTailPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(ExpectedDragonTailSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(secondOnLineMonster, DateTimeOffset.UtcNow, out var visibleSecondOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(rangeEdgeMonster, DateTimeOffset.UtcNow, out var visibleRangeEdge));
        Assert.Equal(onLineMonster.MaxLife - ExpectedDragonTailMonsterDamage, visibleOnLine.Hitpoints);
        Assert.Equal(secondOnLineMonster.MaxLife - ExpectedDragonTailMonsterDamage, visibleSecondOnLine.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(rangeEdgeMonster.MaxLife, visibleRangeEdge.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - ExpectedDragonTailPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)ExpectedDragonTailSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.Contains(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 80)]));
        Assert.Empty(ParseMagicColdTimePackets(casterSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("80", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey("HitShoot"));
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(DragonTailSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy DragonTail Line magic", output);
        Assert.Contains("range=5", output);
        Assert.Contains("width=0", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("monsterProgress=1254", output);
        Assert.Contains("sobNpcProgress=285", output);
        Assert.Contains("counterHitShoots=0", output);
        Assert.Contains("source=Line.cs case 11000", output);
    }

    [Fact]
    public async Task HandlerDragonTailLineAgainstWildPhoenixPlayerConsumesDefenderChargeAndDealsOneDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateDragonTailRow(),
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
            spellIds: [DragonTailSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 834,
            y: 584,
            minHitPoints: 100_000,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(DragonTailWildPhoenixFlag),
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
                SpellId: DragonTailSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(DragonTailSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(1U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(99_999, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, DragonTailWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == DragonTailWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy DragonTail Line magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("totalAppliedDamage=1", output);
        Assert.Contains("source=Line.cs case 11000", output);
    }

    [Theory]
    [InlineData(1_005, "9", 2)]
    [InlineData(6_000, "9", 2)]
    [InlineData(1_015, "7", 0)]
    public async Task HandlerDragonTailLineCounterHitMapUpdatesHitShootForMonsterHits(
        long mapId,
        string expectedHitShoot,
        int expectedCounterHitShoots)
    {
        var monsterFamily = CreateMonsterFamily();
        var firstSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 11, boundX: 833, boundY: 584) with
        {
            MapId = checked((int)mapId),
        };
        var secondSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 12, boundX: 834, boundY: 584) with
        {
            MapId = checked((int)mapId),
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [firstSpawn, secondSpawn],
            [],
            [CreateDragonTailRow()]);
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
            spellIds: [DragonTailSpellId],
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
                spellId: DragonTailSpellId,
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
    public async Task HandlerDragonTailLineCounterHitMapDoesNotUpdateHitShootForPlayerOnlyHits(long mapId)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [CreateDragonTailRow()]);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [DragonTailSpellId],
            mapId: mapId,
            attributes: new Dictionary<string, string>
            {
                ["HitShoot"] = "7",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
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
                SpellId: DragonTailSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("7", updatedCasterSession.Character.Attributes["HitShoot"]);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - ExpectedDragonTailPlayerDamage, updatedTargetSession.Character.MinHitPoints);

        var output = log.ToString();
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("counterHitShoots=0", output);
    }

    private static MagicType CreateDragonTailRow(
        byte level = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            DragonTailSpellId,
            sort: 14,
            level: level,
            useMana: 0,
            damage: 30_095,
            experience: experience,
            maxTargets: 4,
            range: 5,
            weaponType: 561,
            useStamina: 20,
            coolDown: 600,
            coldTime: 0);
    }
}
