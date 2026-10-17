using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task DragonCycloneWithoutXpListWithActiveFlagMovesDamagesPathTargetsAndDoesNotOpenXpSkill()
    {
        const int mapId = 1_000;
        var monsterFamily = CreateMonsterFamily();
        var onPathSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 1, boundX: 838, boundY: 584) with { MapId = mapId };
        var offPathSpawn = CreateSingleInstanceMonsterSpawn(generatorId: 2, boundX: 838, boundY: 587) with { MapId = mapId };
        var sobNpc = CreateMoveLineSobNpc(id: 65_290, x: 837, y: 585) with { MapId = mapId };
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [onPathSpawn, offPathSpawn],
            [sobNpc],
            [CreateDragonCycloneRow()]);
        var onPathMonster = CreateRuntimeMonsterInstance(worldData, onPathSpawn, monsterFamily);
        var offPathMonster = CreateRuntimeMonsterInstance(worldData, offPathSpawn, monsterFamily);
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
            mapId: mapId,
            minMana: 50,
            strength: 300,
            spellIds: [TestDragonCycloneSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(TestDragonCycloneFlag, LegacySupermanFlag),
                ["XPCount"] = "31",
            });
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 837,
            y: 584,
            mapId: mapId,
            minHitPoints: 5_000);
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
                onPathMonster,
                legacyEncodedQuery: false,
                spellId: TestDragonCycloneSpellId,
                x: 842,
                y: 584)).ToFrameBytes()));

        var movementMarker = Assert.Single(ParseDragonCycloneAttackPackets(casterSent), attack =>
            attack.Query.AttackType == MsgAttackType.InMoveSpell);
        Assert.Equal(PlayerUid, movementMarker.Query.Uid);
        Assert.Equal(838, movementMarker.Query.X);
        Assert.Equal(584, movementMarker.Query.Y);

        var casterSpell = Assert.Single(ParseSpellAnimations(casterSent));
        var targetSpell = Assert.Single(ParseSpellAnimations(targetSent));
        foreach (var spell in new[] { casterSpell, targetSpell })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(838, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(TestDragonCycloneSpellId, spell.SpellId);
            Assert.Equal(0, spell.SpellLevel);
            Assert.Equal(0, spell.LevelHu);
        }

        var damageByUid = casterSpell.Targets.ToDictionary(target => target.Uid, target => target.Damage);
        Assert.Equal(
            new[] { onPathMonster.Uid, TargetPlayerUid, checked((uint)sobNpc.Id) }.Order().ToArray(),
            damageByUid.Keys.Order().ToArray());
        Assert.Equal(2_450U, damageByUid[onPathMonster.Uid]);
        Assert.True(damageByUid[TargetPlayerUid] > 0);
        Assert.True(damageByUid[checked((uint)sobNpc.Id)] > 0);
        Assert.All(casterSpell.Targets, target =>
        {
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(onPathMonster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(onPathMonster.MaxLife - 2_450U, damagedMonster.Hitpoints);
        Assert.True(monsterStates.TryGetVisibleInstance(offPathMonster, DateTimeOffset.UtcNow, out var untouchedMonster));
        Assert.Equal(offPathMonster.MaxLife, untouchedMonster.Hitpoints);
        Assert.Equal(sobNpc.Life - (int)damageByUid[checked((uint)sobNpc.Id)], sobNpcStates.GetVisible(sobNpc).Life);

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.Equal(5_000 - (long)damageByUid[TargetPlayerUid], updatedTargetSession.Character.MinHitPoints);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(40, updatedCasterSession.Character.MinMana);
        Assert.Equal(838, updatedCasterSession.Character.X);
        Assert.Equal(584, updatedCasterSession.Character.Y);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyXpListFlag));
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, TestDragonCycloneFlag));
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacySupermanFlag));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey(RuntimeLegacyDragonCycloneExpiresAtUtcAttribute));
        Assert.Equal("31", updatedCasterSession.Character.Attributes["XPCount"]);
        Assert.Contains(ParseMsgUpdates(casterSent), update => update.Uid == PlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 40)]));
        Assert.DoesNotContain(ParseMsgUpdates(casterSent), update => update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.XPList or LegacyMsgUpdateDataType.XPCircle));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy DragonCyclone", output);
        Assert.Contains("monsterTargets=1", output);
        Assert.Contains("playerTargets=1", output);
        Assert.Contains("sobNpcTargets=1", output);
        Assert.DoesNotContain("unsupported-magic", output);
        Assert.DoesNotContain("unsupported monster magic sort", output);
        Assert.DoesNotContain("unsupported player magic sort", output);
        Assert.DoesNotContain("unsupported SOB magic sort", output);
        Assert.DoesNotContain("XPList transition", output);
    }

    private static MagicType CreateDragonCycloneRow()
    {
        return CreateMagicRow(
            TestDragonCycloneSpellId,
            sort: 75,
            useMana: 10,
            damage: 120,
            range: 8,
            coolDown: 0,
            coldTime: 0,
            duration: 27);
    }

    private static List<MsgAttackPacket> ParseDragonCycloneAttackPackets(IEnumerable<byte[]> frames)
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
}
