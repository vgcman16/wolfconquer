using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort ArcherPreKineticCrescentShadowSpellId = 18_750;
    private const ushort ArcherPreKineticNebulousHuntSpellId = 10_990;

    [Fact]
    public async Task HandlerArcherPreKineticNebulousHuntCanProcBeyondCrescentShadowCloseRange()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 833, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateArcherPreKineticCrescentShadowRow(),
            CreateArcherPreKineticNebulousHuntRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds:
            [
                ArcherPreKineticCrescentShadowSpellId,
                ArcherPreKineticNebulousHuntSpellId,
            ],
            attributes: new Dictionary<string, string>
            {
                ["LegacyArcherPreProcRateRoll.18750"] = "0",
                ["LegacyArcherPreProcRateRoll.10990"] = "0",
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
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Archer,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy NebulousHunt SectorBack magic", output);
        Assert.Contains(
            "spellId=" + ArcherPreKineticNebulousHuntSpellId.ToString(CultureInfo.InvariantCulture),
            output);
        Assert.Contains("source=SectorBack.cs case 10990", output);
        Assert.DoesNotContain(
            "spellId=" + ArcherPreKineticCrescentShadowSpellId.ToString(CultureInfo.InvariantCulture),
            output);
        Assert.DoesNotContain(ParseAttacks(sent), packet => packet.Query.AttackType == MsgAttackType.Archer);
        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(ArcherPreKineticNebulousHuntSpellId, spell.SpellId);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
    }

    [Fact]
    public async Task HandlerArcherPreKineticNebulousHuntForcedSuccessBeyondDirectRangeIsSuppressed()
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(boundX: 849, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateArcherPreKineticNebulousHuntRow());
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            spellIds: [ArcherPreKineticNebulousHuntSpellId],
            attributes: new Dictionary<string, string>
            {
                ["LegacyArcherPreProcRateRoll.10990"] = "0",
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
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: monster.Uid,
                X: monster.X,
                Y: monster.Y,
                AttackType: MsgAttackType.Archer,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy Archer", output);
        Assert.Contains("monster", output);
        Assert.Contains("distance=19", output);
        Assert.Contains("AttackID.Archer monster branch Base.GetDistance <= 18", output);
        Assert.DoesNotContain("WorldServer handled legacy NebulousHunt SectorBack magic", output);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic monster proc", output);
        Assert.DoesNotContain(ParseAttacks(sent), packet => packet.Query.AttackType == MsgAttackType.Archer);
        Assert.Empty(ParseSpellAnimations(sent));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
    }

    private static MagicType CreateArcherPreKineticCrescentShadowRow()
    {
        return CreateMagicRow(
            ArcherPreKineticCrescentShadowSpellId,
            sort: 5,
            useMana: 0,
            damage: 100,
            maxTargets: 1,
            range: 3,
            needLevel: 1,
            weaponType: 500,
            useStamina: 0,
            coldTime: 0);
    }

    private static MagicType CreateArcherPreKineticNebulousHuntRow()
    {
        return CreateMagicRow(
            ArcherPreKineticNebulousHuntSpellId,
            sort: 69,
            useMana: 0,
            damage: 100,
            maxTargets: 1,
            range: 18,
            needLevel: 1,
            weaponType: 500,
            useStamina: 0,
            coldTime: 0);
    }

    private static List<MsgAttackPacket> ParseAttacks(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
