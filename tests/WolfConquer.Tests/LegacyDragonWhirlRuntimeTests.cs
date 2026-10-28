using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DragonWhirlSpellId = 10_315;
    private const int DragonWhirlWildPhoenixFlag = 0x170;
    private const int DragonWhirlSpawnUidField = 2;
    private const int DragonWhirlSpawnXField = 27;
    private const int DragonWhirlSpawnYField = 28;

    [Fact]
    public async Task HandlerDragonWhirlHitsVisibleMonsterPlayerAndSobTargetsSendsMarkerAndMovesToLanding()
    {
        const int mapId = 1_000;
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584) with { MapId = mapId };
        var edgeWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 838, boundY: 588) with { MapId = mapId };
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838, boundY: 589) with { MapId = mapId };
        var beyondCastLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 847, boundY: 584) with { MapId = mapId };
        var sobNpc = CreateMoveLineSobNpc(id: 65_315, x: 836, y: 586) with { MapId = mapId };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, edgeWidthSpawn, outsideWidthSpawn, beyondCastLineSpawn],
            [sobNpc],
            [CreateDragonWhirlRow()]);
        var onLineMonster = CreateRuntimeMonsterInstance(worldData, onLineSpawn, monsterFamily);
        var edgeWidthMonster = CreateRuntimeMonsterInstance(worldData, edgeWidthSpawn, monsterFamily);
        var outsideWidthMonster = CreateRuntimeMonsterInstance(worldData, outsideWidthSpawn, monsterFamily);
        var beyondCastLineMonster = CreateRuntimeMonsterInstance(worldData, beyondCastLineSpawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, spellIds: [DragonWhirlSpellId], mapId: mapId);
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 838, y: 585, mapId: mapId);
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
            ReadFrame(new MsgAttackPacket(CreateDragonWhirlQuery(
                onLineMonster.Uid,
                x: 842,
                y: 584)).ToFrameBytes()));

        var movementMarkerIndexes = Enumerable.Range(0, casterSent.Count)
            .Where(index => MsgAttackPacket.TryParse(ReadFrame(casterSent[index]), out var attack) &&
                attack.Query.AttackType == MsgAttackType.InMoveSpell)
            .ToList();
        Assert.Equal(2, movementMarkerIndexes.Count);
        var markerIndex = movementMarkerIndexes[0];
        var landingMarkerIndex = movementMarkerIndexes[1];
        var spellIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out _));
        Assert.True(markerIndex < spellIndex);
        Assert.True(landingMarkerIndex > spellIndex);
        var movementMarkers = ParseDragonWhirlAttackPackets(casterSent)
            .Where(attack => attack.Query.AttackType == MsgAttackType.InMoveSpell)
            .ToList();
        var marker = movementMarkers[0];
        Assert.Equal(PlayerUid, marker.Query.Uid);
        Assert.Equal(onLineMonster.Uid, marker.Query.OpponentUid);
        Assert.Equal(842, marker.Query.X);
        Assert.Equal(584, marker.Query.Y);
        Assert.Equal(0, marker.Query.Damage);
        Assert.Equal(DragonWhirlSpellId, marker.Query.KilledMonster);
        Assert.Equal(0, marker.Query.SpellId);
        var landingMarker = movementMarkers[1];
        Assert.Equal(PlayerUid, landingMarker.Query.Uid);
        Assert.Equal(onLineMonster.Uid, landingMarker.Query.OpponentUid);
        Assert.Equal(842, landingMarker.Query.X);
        Assert.Equal(584, landingMarker.Query.Y);
        Assert.Equal(0, landingMarker.Query.Damage);
        Assert.Equal(DragonWhirlSpellId, landingMarker.Query.KilledMonster);
        Assert.Equal(0, landingMarker.Query.SpellId);
        var casterLandingPeerSpawnIndex = Assert.Single(
            Enumerable.Range(landingMarkerIndex + 1, casterSent.Count - landingMarkerIndex - 1),
            index => LegacyPlayerSpawnPacket.TryParse(ReadFrame(casterSent[index]), out var spawn) &&
                spawn.GetNumericField(DragonWhirlSpawnUidField) == TargetPlayerUid);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(casterSent[casterLandingPeerSpawnIndex]), out var casterLandingPeerSpawn));
        Assert.Equal(838UL, casterLandingPeerSpawn.GetNumericField(DragonWhirlSpawnXField));
        Assert.Equal(585UL, casterLandingPeerSpawn.GetNumericField(DragonWhirlSpawnYField));

        var peerMovementMarkerIndexes = Enumerable.Range(0, targetSent.Count)
            .Where(index => MsgAttackPacket.TryParse(ReadFrame(targetSent[index]), out var attack) &&
                attack.Query.AttackType == MsgAttackType.InMoveSpell)
            .ToList();
        Assert.Equal(2, peerMovementMarkerIndexes.Count);
        var peerMovementMarkers = peerMovementMarkerIndexes
            .Select(index => AssertDragonWhirlAttackPacket(targetSent[index]))
            .ToList();
        Assert.Equal(842, peerMovementMarkers[1].Query.X);
        Assert.Equal(584, peerMovementMarkers[1].Query.Y);
        var peerLandingMarkerIndex = peerMovementMarkerIndexes[1];
        var peerLandingSpawnIndex = Assert.Single(
            Enumerable.Range(peerLandingMarkerIndex + 1, targetSent.Count - peerLandingMarkerIndex - 1),
            index => LegacyPlayerSpawnPacket.TryParse(ReadFrame(targetSent[index]), out var spawn) &&
                spawn.GetNumericField(DragonWhirlSpawnUidField) == PlayerUid);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(targetSent[peerLandingSpawnIndex]), out var peerLandingSpawn));
        Assert.Equal(842UL, peerLandingSpawn.GetNumericField(DragonWhirlSpawnXField));
        Assert.Equal(584UL, peerLandingSpawn.GetNumericField(DragonWhirlSpawnYField));

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(842, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(DragonWhirlSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            Assert.Equal(0, spell.LevelHu);
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, edgeWidthMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(ExpectedLegacyPhysicalLineSectorDamage, damageByUid[onLineMonster.Uid]);
        Assert.Equal(ExpectedLegacyPhysicalLineSectorDamage, damageByUid[edgeWidthMonster.Uid]);
        Assert.True(damageByUid[TargetPlayerUid] > 0);
        Assert.True(damageByUid[checked((uint)sobNpc.Id)] > 0);

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(edgeWidthMonster, DateTimeOffset.UtcNow, out var visibleEdgeWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(beyondCastLineMonster, DateTimeOffset.UtcNow, out var visibleBeyondCastLine));
        Assert.Equal(onLineMonster.MaxLife - ExpectedLegacyPhysicalLineSectorDamage, visibleOnLine.Hitpoints);
        Assert.Equal(edgeWidthMonster.MaxLife - ExpectedLegacyPhysicalLineSectorDamage, visibleEdgeWidth.Hitpoints);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(beyondCastLineMonster.MaxLife, visibleBeyondCastLine.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)damageByUid[TargetPlayerUid], updatedTargetSession.Character.MinHitPoints);

        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life - (int)damageByUid[checked((uint)sobNpc.Id)], visibleSobNpc.Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal(842, updatedCasterSession.Character.X);
        Assert.Equal(584, updatedCasterSession.Character.Y);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyFlashingNameFlag));

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy player-target magic flashing-name", output);
        Assert.Contains("WorldServer handled legacy DragonWhirl magic", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("WorldServer applied legacy DragonWhirl movement", output);
        Assert.Contains("WorldServer refreshed LegacyPlayerSpawn DragonWhirl view-role", output);
        Assert.Contains("toCasterCount=1", output);
        Assert.Contains("toPeerCount=1", output);
        Assert.DoesNotContain("PARITY-GAP DragonWhirl movement broadcast skipped", output);
    }

    [Fact]
    public async Task HandlerDragonWhirlAgainstWildPhoenixPlayerConsumesDefenderChargeAndMovesToLanding()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateDragonWhirlRow(),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(strength: 300, spellIds: [DragonWhirlSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 838,
            y: 585,
            spellIds: [WildPhoenixSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(DragonWhirlWildPhoenixFlag),
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
            MonsterStates: new MonsterRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateDragonWhirlQuery(
                TargetPlayerUid,
                x: 842,
                y: 584)).ToFrameBytes()));

        var movementMarkers = ParseDragonWhirlAttackPackets(casterSent)
            .Where(attack => attack.Query.AttackType == MsgAttackType.InMoveSpell)
            .ToList();
        Assert.Equal(2, movementMarkers.Count);
        Assert.All(movementMarkers, marker =>
        {
            Assert.Equal(PlayerUid, marker.Query.Uid);
            Assert.Equal(TargetPlayerUid, marker.Query.OpponentUid);
            Assert.Equal(DragonWhirlSpellId, marker.Query.KilledMonster);
            Assert.Equal(0, marker.Query.Damage);
        });
        Assert.Equal(842, movementMarkers[0].Query.X);
        Assert.Equal(584, movementMarkers[0].Query.Y);
        Assert.Equal(842, movementMarkers[1].Query.X);
        Assert.Equal(584, movementMarkers[1].Query.Y);

        var targetMovementMarkers = ParseDragonWhirlAttackPackets(targetSent)
            .Where(attack => attack.Query.AttackType == MsgAttackType.InMoveSpell)
            .ToList();
        Assert.Equal(2, targetMovementMarkers.Count);

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(842, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(DragonWhirlSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(1U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(4_999, updatedTargetSession.Character.MinHitPoints);
        Assert.Equal(
            "1",
            updatedTargetSession.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, DragonWhirlWildPhoenixFlag));

        var archiveEntry = Assert.Single(
            ParseMsgUpdates(targetSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == DragonWhirlWildPhoenixFlag);
        Assert.Equal(32U, archiveEntry.Time);
        Assert.Equal(18_007U, archiveEntry.Damage);
        Assert.Equal(1U, archiveEntry.SpellLevel);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal(842, updatedCasterSession.Character.X);
        Assert.Equal(584, updatedCasterSession.Character.Y);

        var output = log.ToString();
        Assert.Contains("WorldServer applied legacy WildPhoenix defender charge", output);
        Assert.Contains("remainingCharges=1", output);
        Assert.Contains("WorldServer applied legacy DragonWhirl movement", output);
        Assert.Contains("WorldServer handled legacy DragonWhirl magic", output);
        Assert.Contains("playerTargets=1", output);
    }

    [Fact]
    public async Task HandlerDragonWhirlStrictCastRangeReturnSpendsResourcesWithoutMarkerAnimationOrDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateDragonWhirlRow(range: 12));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [DragonWhirlSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateDragonWhirlQuery(
                monster.Uid,
                x: 842,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(
            ParseDragonWhirlAttackPackets(sent),
            attack => attack.Query.AttackType == MsgAttackType.InMoveSpell);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal(830, updatedSession.Character.X);
        Assert.Equal(584, updatedSession.Character.Y);
        Assert.Contains("distance >= Spell.Range", log.ToString());
    }

    [Fact]
    public async Task HandlerDragonWhirlGuildWarBlockedCastTileReturnsAfterResourcesAndPullsBack()
    {
        const ushort originalX = 2;
        const ushort originalY = 2;
        const ushort endpointX = 14;
        const ushort endpointY = originalY;
        const string illegalJumpMessage = "Illegal jumping over the gates detected.";
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(monsterFamily, [], CreateDragonWhirlRow(range: 20));
        var terrain = CreateGuildWarMoveLineTerrain(
            originalX,
            originalY,
            endpointX,
            endpointY,
            endpointSurface: 0);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: LegacySobNpcEventRules.GuildWarMapId,
            x: originalX,
            y: originalY,
            strength: 300,
            spellIds: [DragonWhirlSpellId],
            attributes: new Dictionary<string, string>
            {
                ["CurrentTerrainMask"] = "3",
            });
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            Terrain: terrain,
            MonsterStates: new MonsterRuntimeStateStore(),
            GuildWarStates: new LegacyGuildWarRuntimeStateStore());

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateDragonWhirlQuery(
                opponentUid: 0,
                x: endpointX,
                y: endpointY)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(
            ParseDragonWhirlAttackPackets(sent),
            attack => attack.Query.AttackType == MsgAttackType.InMoveSpell);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal(originalX, updatedSession.Character.X);
        Assert.Equal(originalY, updatedSession.Character.Y);

        var systemTalk = Assert.Single(
            ParseLegacyMessages(sent),
            message => message.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                message.Content.Count > 3 &&
                message.Content[3] == illegalJumpMessage);
        Assert.Equal("SYSTEM", systemTalk.Content[0]);

        var pullback = Assert.Single(
            ParseMsgActions(sent),
            action => action.Type == MsgActionType.SetLocation &&
                action.ObjId == PlayerUid);
        Assert.Equal((uint)LegacySobNpcEventRules.GuildWarMapId, pullback.DwParam);
        Assert.Equal(originalX, pullback.PositionX);
        Assert.Equal(originalY, pullback.PositionY);

        var output = log.ToString();
        Assert.Contains("blocked legacy DragonWhirl GuildWar gate crossing", output);
        Assert.DoesNotContain("PARITY-GAP DragonWhirl movement broadcast skipped", output);
    }

    [Fact]
    public async Task HandlerDragonWhirlImmediateRepeatSpendsResourcesBeforeCooldownRejection()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateDragonWhirlRow(coldTime: 1_400));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [DragonWhirlSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        var frame = ReadFrame(new MsgAttackPacket(CreateDragonWhirlQuery(
            monster.Uid,
            x: 842,
            y: 584)).ToFrameBytes());

        await PacketHandlers.HandleAsync(context, frame);

        Assert.Single(ParseSpellAnimations(sent));
        Assert.True(sessions.TryGet(connection, out var firstCastSession));
        Assert.NotNull(firstCastSession);
        Assert.Equal(490, firstCastSession.Character.MinMana);
        Assert.NotNull(firstCastSession.MagicCooldowns);
        Assert.True(firstCastSession.MagicCooldowns.ContainsKey(DragonWhirlSpellId));
        var firstColdTime = Assert.Single(ParseMagicColdTimePackets(sent));
        Assert.Equal([new LegacyMagicColdTimeSpell(DragonWhirlSpellId, 1_400)], firstColdTime.Spells);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var afterFirstCast));
        Assert.Equal(monster.MaxLife - ExpectedLegacyPhysicalLineSectorDamage, afterFirstCast.Hitpoints);

        var firstCastValues = System.Text.Json.JsonSerializer.Serialize(firstCastSession);
        sent.Clear();
        await PacketHandlers.HandleAsync(context with { Session = firstCastSession }, frame);

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Empty(ParseMagicColdTimePackets(sent));
        Assert.DoesNotContain(
            ParseDragonWhirlAttackPackets(sent),
            attack => attack.Query.AttackType == MsgAttackType.InMoveSpell);
        Assert.True(sessions.TryGet(connection, out var blockedSession));
        Assert.NotNull(blockedSession);
        Assert.Equal(490, blockedSession.Character.MinMana);
        Assert.Same(firstCastSession, blockedSession);
        Assert.Equal(firstCastValues, System.Text.Json.JsonSerializer.Serialize(blockedSession));
        Assert.Same(firstCastSession.Character.Attributes, blockedSession.Character.Attributes);
        Assert.Same(firstCastSession.PlayerItems, blockedSession.PlayerItems);
        Assert.Same(firstCastSession.PlayerSpells, blockedSession.PlayerSpells);
        Assert.Same(firstCastSession.MagicCooldowns, blockedSession.MagicCooldowns);
        Assert.Empty(sent);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var afterBlockedCast));
        Assert.Equal(afterFirstCast.Hitpoints, afterBlockedCast.Hitpoints);
        var output = log.ToString();
        Assert.Contains("cooldownRemainingMs=", output);
        Assert.DoesNotContain("PARITY-GAP DragonWhirl movement broadcast skipped", output);
    }

    [Fact]
    public async Task HandlerDragonWhirlSendsSpellProgressBeforeSpellAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [
                CreateDragonWhirlRow(experience: 1),
                CreateDragonWhirlRow(level: 1, experience: 100),
            ]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(strength: 300, spellIds: [DragonWhirlSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateDragonWhirlQuery(
                monster.Uid,
                x: 842,
                y: 584)).ToFrameBytes()));

        var spellPacketIndex = SingleFrameIndex(sent, frame =>
            LegacySpellPacket.TryParse(frame, out var packet) &&
            packet.Id == DragonWhirlSpellId);
        var animationIndex = SingleFrameIndex(sent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
            animation.SpellId == DragonWhirlSpellId);
        Assert.True(spellPacketIndex < animationIndex);

        Assert.True(LegacySpellPacket.TryParse(ReadFrame(sent[spellPacketIndex]), out var spellPacket));
        Assert.Equal(DragonWhirlSpellId, spellPacket.Id);
        Assert.Equal(1, spellPacket.Level);
        Assert.Equal(0, spellPacket.Experience);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(DragonWhirlSpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);
        Assert.Contains("WorldServer handled legacy DragonWhirl magic", log.ToString());
    }

    private static MsgAttackQuery CreateDragonWhirlQuery(uint opponentUid, ushort x, ushort y)
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
            SpellId: DragonWhirlSpellId);
    }

    private static MagicType CreateDragonWhirlRow(
        byte level = 0,
        uint experience = 0,
        ushort useMana = 10,
        byte range = 20,
        int coldTime = 0)
    {
        return CreateMagicRow(
            DragonWhirlSpellId,
            sort: 40,
            level: level,
            experience: experience,
            useMana: useMana,
            damage: 120,
            maxTargets: 3,
            range: range,
            coolDown: 0,
            coldTime: coldTime);
    }

    private static List<MsgAttackPacket> ParseDragonWhirlAttackPackets(IEnumerable<byte[]> frames)
    {
        var attacks = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack))
            {
                attacks.Add(attack);
            }
        }

        return attacks;
    }

    private static MsgAttackPacket AssertDragonWhirlAttackPacket(byte[] frame)
    {
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(frame), out var packet));
        return packet;
    }
}
