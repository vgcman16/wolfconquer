using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort SpeedGunSpellId = 1_260;

    [Fact]
    public async Task HandlerSpeedGunLineHitsMixedTargetsWithSourceWidthPlayerRangeAndNoGuardExclusion()
    {
        var guardFamily = CreateGuardMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 833,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var edgeWidthSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 2,
            boundX: 834,
            boundY: 585,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 3,
            boundX: 834,
            boundY: 586,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var rangeEdgeSpawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 4,
            boundX: 839,
            boundY: 584,
            monsterTypeId: guardFamily.Id,
            sourceMonsterName: guardFamily.Name);
        var sobNpc = CreateMoveLineSobNpc(id: 65_260, x: 836, y: 585);
        var worldData = CreateMagicCatalogCore(
            guardFamily,
            [onLineSpawn, edgeWidthSpawn, outsideWidthSpawn, rangeEdgeSpawn],
            [sobNpc],
            [
                CreateSpeedGunRow(experience: 1),
                CreateSpeedGunRow(level: 1, experience: 100),
            ]);
        var onLineGuard = CreateRuntimeMonsterInstance(worldData, onLineSpawn, guardFamily);
        var edgeWidthGuard = CreateRuntimeMonsterInstance(worldData, edgeWidthSpawn, guardFamily);
        var outsideWidthGuard = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, guardFamily);
        var rangeEdgeGuard = CreateRuntimeMonsterInstance(worldData, rangeEdgeSpawn, guardFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var closeTargetSent = new List<byte[]>();
        var edgeTargetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var closeTargetConnection = CreateSendingConnection(closeTargetSent);
        var edgeTargetConnection = CreateSendingConnection(edgeTargetSent);
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SpeedGunSpellId],
            legacyRandomSpell: SpeedGunSpellId,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        var closeTargetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "CloseTarget",
            x: 832,
            y: 584,
            minHitPoints: 100_000);
        var edgeTargetSession = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "EdgeTarget",
            x: 833,
            y: 584,
            minHitPoints: 100_000);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(closeTargetConnection, closeTargetSession);
        sessions.Set(edgeTargetConnection, edgeTargetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        var query = CreateMagicQuery(
            onLineGuard,
            legacyEncodedQuery: false,
            spellId: SpeedGunSpellId,
            x: 842,
            y: 584);
        var (_, spell) = ResolveSpeedGunSpell(casterSession, worldData);
        var expectedOnLineGuardDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            onLineGuard,
            guardFamily,
            spell);
        var expectedEdgeWidthGuardDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerMonsterPhysicalSpellDamage",
            casterSession,
            worldData,
            edgeWidthGuard,
            guardFamily,
            spell);
        var expectedPlayerDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            closeTargetSession,
            spell,
            worldData,
            1U);
        var expectedSobNpcDamage = InvokePrivateUInt32(
            "CalculateLegacySobNpcPhysicalSpellDamage",
            casterSession,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var closeTargetSpell = Assert.Single(ParseSpellAnimations(closeTargetSent));
        var edgeTargetSpell = Assert.Single(ParseSpellAnimations(edgeTargetSent));
        foreach (var animation in new[] { casterSpell, closeTargetSpell, edgeTargetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(SpeedGunSpellId, animation.SpellId);
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
            new[] { onLineGuard.Uid, edgeWidthGuard.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(expectedOnLineGuardDamage, damageByUid[onLineGuard.Uid]);
        Assert.Equal(expectedEdgeWidthGuardDamage, damageByUid[edgeWidthGuard.Uid]);
        Assert.Equal(expectedPlayerDamage, damageByUid[TargetPlayerUid]);
        Assert.DoesNotContain(TargetPlayerUid + 1, damageByUid.Keys);
        Assert.Equal(expectedSobNpcDamage, damageByUid[checked((uint)sobNpc.Id)]);
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            closeTargetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());
        Assert.Equal(
            casterSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            edgeTargetSpell.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineGuard, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(edgeWidthGuard, DateTimeOffset.UtcNow, out var visibleEdgeWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthGuard, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(rangeEdgeGuard, DateTimeOffset.UtcNow, out var visibleRangeEdge));
        Assert.Equal(onLineGuard.MaxLife - expectedOnLineGuardDamage, visibleOnLine.Hitpoints);
        Assert.Equal(edgeWidthGuard.MaxLife - expectedEdgeWidthGuardDamage, visibleEdgeWidth.Hitpoints);
        Assert.Equal(outsideWidthGuard.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(rangeEdgeGuard.MaxLife, visibleRangeEdge.Hitpoints);

        Assert.True(sessions.TryGet(closeTargetConnection, out var updatedCloseTargetSession));
        Assert.NotNull(updatedCloseTargetSession);
        Assert.Equal(100_000 - expectedPlayerDamage, updatedCloseTargetSession.Character.MinHitPoints);
        Assert.True(sessions.TryGet(edgeTargetConnection, out var updatedEdgeTargetSession));
        Assert.NotNull(updatedEdgeTargetSession);
        Assert.Equal(100_000, updatedEdgeTargetSession.Character.MinHitPoints);
        Assert.Equal(sobNpc.Life - checked((int)expectedSobNpcDamage), sobNpcStates.GetVisible(sobNpc).Life);

        Assert.Contains(casterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 99)]));
        Assert.Empty(ParseMagicColdTimePackets(casterSent));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("99", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey("HitShoot"));
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(SpeedGunSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy SpeedGun Line magic", output);
        Assert.Contains("range=9", output);
        Assert.Contains("width=1", output);
        Assert.Contains("playerRange=3", output);
        Assert.Contains("targets=4", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains($"monsterProgress={expectedOnLineGuardDamage + expectedEdgeWidthGuardDamage}", output);
        Assert.Contains($"sobNpcProgress={expectedSobNpcDamage}", output);
        Assert.Contains("counterHitShoots=0", output);
        Assert.Contains("source=Line.cs case 1260", output);
    }

    [Fact]
    public async Task HandlerSpeedGunLineAgainstWildPhoenixPlayerConsumesDefenderChargeAndDealsOneDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [],
            [],
            [
                CreateSpeedGunRow(),
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
            spellIds: [SpeedGunSpellId],
            legacyRandomSpell: SpeedGunSpellId,
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "WildPhoenixTarget",
            x: 832,
            y: 584,
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
                SpellId: SpeedGunSpellId)).ToFrameBytes()));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(SpeedGunSpellId, animation.SpellId);
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
        Assert.Contains("WorldServer handled legacy SpeedGun Line magic", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("totalAppliedDamage=1", output);
        Assert.Contains("source=Line.cs case 1260", output);
    }

    [Theory]
    [InlineData(1_005, "8", 1)]
    [InlineData(1_015, "7", 0)]
    public async Task HandlerSpeedGunLineCounterHitMapUpdatesHitShootForSourcePlayerHits(
        long mapId,
        string expectedHitShoot,
        int expectedCounterHitShoots)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 31, boundX: 833, boundY: 584) with
        {
            MapId = checked((int)mapId),
        };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [CreateSpeedGunRow()]);
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
            spellIds: [SpeedGunSpellId],
            legacyRandomSpell: SpeedGunSpellId,
            mapId: mapId,
            attributes: new Dictionary<string, string>
            {
                ["HitShoot"] = "7",
                ["PkMode"] = "0",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "CounterTarget",
            x: 832,
            y: 584,
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
                spellId: SpeedGunSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(expectedHitShoot, updatedSession.Character.Attributes["HitShoot"]);

        var output = log.ToString();
        Assert.Contains($"counterHitShoots={expectedCounterHitShoots}", output);
    }

    [Fact]
    public async Task HandlerSpeedGunLineMissSpendsResourceButSuppressesAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 586);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [outsideWidthSpawn],
            [],
            [CreateSpeedGunRow()]);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SpeedGunSpellId],
            legacyRandomSpell: SpeedGunSpellId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var query = CreateMagicQuery(
            outsideWidthMonster,
            legacyEncodedQuery: false,
            spellId: SpeedGunSpellId,
            x: 842,
            y: 584);
        _ = ResolveSpeedGunSpell(session, worldData);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(query).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(outsideWidthMonster.MaxLife, visibleMonster.Hitpoints);
        Assert.Contains(sent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 99)]));
        Assert.Empty(ParseMagicColdTimePackets(sent));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("99", updatedSession.Character.Attributes["Stamina"]);
        Assert.False(updatedSession.Character.Attributes.ContainsKey("HitShoot"));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(SpeedGunSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy SpeedGun Line magic", output);
        Assert.Contains("targets=0", output);
        Assert.Contains("monsterTargets=0", output);
        Assert.Contains("playerTargets=0", output);
        Assert.Contains("sobNpcTargets=0", output);
        Assert.Contains("monsterProgress=0", output);
        Assert.Contains("sobNpcProgress=0", output);
        Assert.Contains("counterHitShoots=0", output);
        Assert.Contains("source=Line.cs case 1260", output);
    }

    private static (PlayerSpellSnapshotEntry PlayerSpell, LegacyMagicRuntimeSpell Spell) ResolveSpeedGunSpell(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            session.PlayerSpells,
            worldData,
            SpeedGunSpellId,
            out var playerSpell,
            out var spell));
        return (playerSpell, spell);
    }

    private static MagicType CreateSpeedGunRow(
        byte level = 0,
        uint experience = 0)
    {
        return CreateMagicRow(
            SpeedGunSpellId,
            sort: 14,
            level: level,
            useMana: 0,
            damage: level switch
            {
                0 => 30_190U,
                1 => 30_205U,
                2 => 30_215U,
                3 => 30_230U,
                4 => 30_240U,
                5 => 30_255U,
                6 => 30_265U,
                7 => 30_280U,
                8 => 30_290U,
                _ => 30_300U,
            },
            experience: experience,
            maxTargets: level switch
            {
                0 or 1 => 3,
                2 or 3 => 4,
                4 or 5 => 5,
                6 or 7 => 6,
                _ => 7,
            },
            range: 9,
            needLevel: level switch
            {
                0 => 15,
                1 => 30,
                2 => 45,
                3 => 60,
                4 => 70,
                5 => 80,
                6 => 90,
                7 => 100,
                8 => 110,
                _ => 0,
            },
            weaponType: 560,
            useStamina: 1,
            coolDown: 1,
            coldTime: 0);
    }
}
