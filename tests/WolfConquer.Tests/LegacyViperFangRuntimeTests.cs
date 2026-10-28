using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ViperFangSpellId = 11_005;
    private const uint ExpectedViperFangMonsterDamage = 627;
    private const uint ExpectedViperFangPlayerDamage = 142;
    private const uint ExpectedViperFangSobNpcDamage = 285;

    [Fact]
    public async Task HandlerViperFangLineHitsMixedTargetsWithSourceWidthDamageAndSingleAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 833, boundY: 584);
        var edgeWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 834, boundY: 585);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 834, boundY: 586);
        var rangeEdgeSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 835, boundY: 584);
        var sobNpc = CreateMoveLineSobNpc(id: 65_005, x: 833, y: 585);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, edgeWidthSpawn, outsideWidthSpawn, rangeEdgeSpawn],
            [sobNpc],
            [
                CreateViperFangRow(experience: 1),
                CreateViperFangRow(level: 1, experience: 100),
            ]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var edgeWidthMonster = CreateRuntimeMonsterInstance(worldData, edgeWidthSpawn, monsterFamily);
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
            spellIds: [ViperFangSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 585,
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
                spellId: ViperFangSpellId,
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
            Assert.Equal(ViperFangSpellId, spell.SpellId);
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
            new[] { onLineMonster.Uid, edgeWidthMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(ExpectedViperFangMonsterDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(ExpectedViperFangMonsterDamage, damageByUid[edgeWidthMonster.Uid]);
        Assert.Equal(ExpectedViperFangPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.Equal(ExpectedViperFangSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(edgeWidthMonster, DateTimeOffset.UtcNow, out var visibleEdgeWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(rangeEdgeMonster, DateTimeOffset.UtcNow, out var visibleRangeEdge));
        Assert.Equal(onLineMonster.MaxLife - ExpectedViperFangMonsterDamage, visibleOnLine.Hitpoints);
        Assert.Equal(edgeWidthMonster.MaxLife - ExpectedViperFangMonsterDamage, visibleEdgeWidth.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(rangeEdgeMonster.MaxLife, visibleRangeEdge.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000 - ExpectedViperFangPlayerDamage, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)ExpectedViperFangSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.Contains(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 80)]));
        Assert.Empty(ParseMagicColdTimePackets(casterSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("80", updatedCasterSession.Character.Attributes["Stamina"]);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(ViperFangSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ViperFang Line magic", output);
        Assert.Contains("range=5", output);
        Assert.Contains("width=1", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("monsterProgress=1254", output);
        Assert.Contains("sobNpcProgress=285", output);
        Assert.Contains("source=Line.cs case 11005", output);
    }

    [Fact]
    public async Task HandlerViperFangLineAgainstWildPhoenixPlayerConsumesDefenderChargeAndScalesDamageToZero()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateViperFangRow(),
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
            spellIds: [ViperFangSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 834,
            y: 585,
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
        var expectedPlayerDamage = 1U / 2;
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
                SpellId: ViperFangSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(ViperFangSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
            var target = Assert.Single(animation.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(expectedPlayerDamage, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("80", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.Contains(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 80)]));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(100_000, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer handled legacy ViperFang Line magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("totalAppliedDamage=0", output);
        Assert.Contains("source=Line.cs case 11005", output);
    }

    [Fact]
    public async Task HandlerViperFangLineAgainstGuardMonsterInPkModeAppliesLegacyFlashingName()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var guardSpawn = CreateSingleInstanceMonsterSpawn(
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name,
            boundX: 833,
            boundY: 584);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [guardSpawn],
            [],
            [CreateViperFangRow()]);
        var guardMonster = CreateRuntimeMonsterInstance(worldData, guardSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ViperFangSpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
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
                guardMonster,
                legacyEncodedQuery: false,
                spellId: ViperFangSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        AssertLegacyGuardFlashingNameApplied(sessions, connection, sent, log);
        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ViperFang Line magic", output);
        Assert.Contains("source=Line.cs case 11005", output);
    }

    [Theory]
    [InlineData(1_005, "8", 1)]
    [InlineData(6_000, "8", 1)]
    [InlineData(1_015, "7", 0)]
    public async Task HandlerViperFangLineCounterHitMapUpdatesHitShootForSourcePlayerHits(
        long mapId,
        string expectedHitShoot,
        int expectedCounterHitShoots)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 41, boundX: 833, boundY: 584) with
        {
            MapId = checked((int)mapId),
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [CreateViperFangRow()]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ViperFangSpellId],
            mapId: mapId,
            attributes: new Dictionary<string, string>
            {
                ["HitShoot"] = "7",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "CounterTarget",
            x: 834,
            y: 585,
            minHitPoints: 100_000,
            mapId: mapId);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: ViperFangSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(expectedHitShoot, updatedSession.Character.Attributes["HitShoot"]);

        var output = log.ToString();
        Assert.Contains("playerTargets=1", output);
        Assert.Contains($"counterHitShoots={expectedCounterHitShoots}", output);
    }

    private static MagicType CreateViperFangRow(
        byte level = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            ViperFangSpellId,
            sort: 14,
            level: level,
            useMana: 0,
            damage: 30_095,
            experience: experience,
            maxTargets: 4,
            range: 5,
            weaponType: 560,
            useStamina: 20,
            coolDown: 600,
            coldTime: 0);
    }
}
