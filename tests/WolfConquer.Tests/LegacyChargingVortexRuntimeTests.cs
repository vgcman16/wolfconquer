using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerChargingVortexHitsWidthFourMixedTargetsSendsTwoMarkersAndMovesToLanding()
    {
        const int mapId = 1_000;
        var monsterFamily = CreateMonsterFamily();
        var onLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 834, boundY: 584) with { MapId = mapId };
        var edgeWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 838, boundY: 588) with { MapId = mapId };
        var outsideWidthSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 3, boundX: 838, boundY: 589) with { MapId = mapId };
        var beyondCastLineSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 4, boundX: 847, boundY: 584) with { MapId = mapId };
        var sobNpc = CreateMoveLineSobNpc(id: 65_190, x: 836, y: 586) with { MapId = mapId };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onLineSpawn, edgeWidthSpawn, outsideWidthSpawn, beyondCastLineSpawn],
            [sobNpc],
            [CreateChargingVortexRow()]);
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
        var casterSession = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ChargingVortexSpellId],
            mapId: mapId);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 838,
            y: 585,
            mapId: mapId);
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
            ReadFrame(new MsgAttackPacket(CreateChargingVortexQuery(
                onLineMonster.Uid,
                x: 842,
                y: 584)).ToFrameBytes()));

        var markerIndexes = Enumerable.Range(0, casterSent.Count)
            .Where(index => MsgAttackPacket.TryParse(ReadFrame(casterSent[index]), out var attack) &&
                attack.Query.AttackType == MsgAttackType.InMoveSpell)
            .ToList();
        Assert.Equal(2, markerIndexes.Count);
        var animationIndex = Assert.Single(
            Enumerable.Range(0, casterSent.Count),
            index => LegacySpellAnimationPacket.TryParse(ReadFrame(casterSent[index]), out _));
        Assert.True(markerIndexes[0] < animationIndex);
        Assert.True(markerIndexes[1] > animationIndex);

        var markers = markerIndexes
            .Select(index => AssertChargingVortexAttackPacket(casterSent[index]))
            .ToList();
        Assert.All(markers, marker =>
        {
            Assert.Equal(PlayerUid, marker.Query.Uid);
            Assert.Equal(onLineMonster.Uid, marker.Query.OpponentUid);
            Assert.Equal(ChargingVortexSpellId, marker.Query.KilledMonster);
            Assert.Equal(0, marker.Query.Damage);
            Assert.Equal(0, marker.Query.SpellId);
        });
        Assert.Equal((842, 584), (markers[0].Query.X, markers[0].Query.Y));
        Assert.Equal((842, 584), (markers[1].Query.X, markers[1].Query.Y));

        var peerMarkers = ParseChargingVortexAttackPackets(targetSent)
            .Where(attack => attack.Query.AttackType == MsgAttackType.InMoveSpell)
            .ToList();
        Assert.Equal(2, peerMarkers.Count);
        Assert.All(peerMarkers, marker =>
            Assert.Equal(ChargingVortexSpellId, marker.Query.KilledMonster));

        var casterAnimation = Assert.Single(ParseSpellAnimations(casterSent));
        var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var animation in new[] { casterAnimation, targetAnimation })
        {
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(842, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(ChargingVortexSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(0, animation.LevelHu);
        }

        var damageByUid = casterAnimation.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onLineMonster.Uid, edgeWidthMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.All(damageByUid.Values, damage => Assert.True(damage > 0));
        Assert.Equal(
            casterAnimation.Targets.Select(target => (target.Uid, target.Damage)).ToArray(),
            targetAnimation.Targets.Select(target => (target.Uid, target.Damage)).ToArray());

        Assert.True(monsterStates.TryGetVisibleInstance(onLineMonster, DateTimeOffset.UtcNow, out var visibleOnLine));
        Assert.True(monsterStates.TryGetVisibleInstance(edgeWidthMonster, DateTimeOffset.UtcNow, out var visibleEdgeWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(outsideWidthMonster, DateTimeOffset.UtcNow, out var visibleOutsideWidth));
        Assert.True(monsterStates.TryGetVisibleInstance(beyondCastLineMonster, DateTimeOffset.UtcNow, out var visibleBeyondCastLine));
        Assert.True(visibleOnLine.Hitpoints < onLineMonster.MaxLife);
        Assert.True(visibleEdgeWidth.Hitpoints < edgeWidthMonster.MaxLife);
        Assert.Equal(outsideWidthMonster.MaxLife, visibleOutsideWidth.Hitpoints);
        Assert.Equal(beyondCastLineMonster.MaxLife, visibleBeyondCastLine.Hitpoints);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.True(updatedTargetSession.Character.MinHitPoints < targetSession.Character.MinHitPoints);
        Assert.True(sobNpcStates.GetVisible(sobNpc).Life < sobNpc.Life);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal("80", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.Equal(500, updatedCasterSession.Character.MinMana);
        Assert.Equal(842, updatedCasterSession.Character.X);
        Assert.Equal(584, updatedCasterSession.Character.Y);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy ChargingVortex magic", output);
        Assert.Contains("spellId=11190 sort=64", output);
        Assert.Contains("lineWidth=4", output);
        Assert.Contains("monsterTargets=2", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.Contains("WorldServer applied legacy ChargingVortex movement", output);
        Assert.Contains("WorldServer refreshed LegacyPlayerSpawn ChargingVortex view-role", output);
        Assert.Contains("source=ChargingVortex.cs", output);
    }

    [Fact]
    public async Task HandlerChargingVortexStrictCastRangeReturnSpendsResourcesWithoutMarkerAnimationOrDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 834, boundY: 584);
        var worldData = CreateMagicCatalog(monsterFamily, [spawn], CreateChargingVortexRow(range: 12));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ChargingVortexSpellId]);
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
            ReadFrame(new MsgAttackPacket(CreateChargingVortexQuery(
                monster.Uid,
                x: 842,
                y: 584)).ToFrameBytes()));

        Assert.Empty(ParseSpellAnimations(sent));
        Assert.DoesNotContain(
            ParseChargingVortexAttackPackets(sent),
            attack => attack.Query.AttackType == MsgAttackType.InMoveSpell);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal("80", updatedSession.Character.Attributes["Stamina"]);
        Assert.Equal(500, updatedSession.Character.MinMana);
        Assert.Equal(830, updatedSession.Character.X);
        Assert.Equal(584, updatedSession.Character.Y);
        Assert.Contains(sent, frame =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
            update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 80)]));

        var output = log.ToString();
        Assert.Contains("WorldServer skipped legacy ChargingVortex magic", output);
        Assert.Contains("source ChargingVortex.cs returns after CanUseSpell.Verified", output);
        Assert.Contains("distance >= Spell.Range", output);
    }

    private static MsgAttackQuery CreateChargingVortexQuery(uint opponentUid, ushort x, ushort y)
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
            SpellId: ChargingVortexSpellId);
    }

    private static MagicType CreateChargingVortexRow(byte range = 20)
    {
        var row = CreateMagicRow(
            ChargingVortexSpellId,
            sort: 64,
            useMana: 0,
            damage: 30_070,
            experience: 191_500,
            maxTargets: 3,
            range: range,
            needLevel: 30,
            weaponType: 50_000,
            useStamina: 20,
            coolDown: 0,
            coldTime: 300,
            damageOnMonster: 15,
            gDamage: 100);
        var fields = row.Fields.ToArray();
        fields[3] = "ChargingVortex";
        return row with
        {
            Name = "ChargingVortex",
            Fields = fields,
        };
    }

    private static List<MsgAttackPacket> ParseChargingVortexAttackPackets(IEnumerable<byte[]> frames)
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

    private static MsgAttackPacket AssertChargingVortexAttackPacket(byte[] frame)
    {
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(frame), out var packet));
        return packet;
    }
}
