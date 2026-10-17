using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort KineticSparkSpellId = 11_590;
    private const int KineticSparkFlag = 0x93;
    private const string RuntimeLegacyKineticSparkPassiveRateRollAttribute = "LegacyKineticSparkPassiveRateRoll";

    [Fact]
    public async Task HandlerKineticSparkPassiveMonsterDamageAppliesMonsterHunterBonus()
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(boundX: 832, boundY: 584);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateMagicRow(KineticSparkSpellId, sort: 67, useMana: 0, damage: 100, range: 8, coldTime: 0),
            CreateMagicRow(MonsterHunterSpellId, sort: 1, useMana: 0, damage: 30_300, coldTime: 0));
        var baselineDamage = await RunKineticSparkPassiveMonsterDamageAsync(
            worldData,
            monsterFamily,
            spawn,
            learnedMonsterHunter: false);
        var boostedDamage = await RunKineticSparkPassiveMonsterDamageAsync(
            worldData,
            monsterFamily,
            spawn,
            learnedMonsterHunter: true);

        Assert.True(
            boostedDamage > baselineDamage * 3,
            $"Expected MonsterHunter to add source Range.OnMonster damage percent. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    private static async Task<uint> RunKineticSparkPassiveMonsterDamageAsync(
        WorldDataCatalog worldData,
        Monster monsterFamily,
        MonsterSpawn spawn,
        bool learnedMonsterHunter)
    {
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var spellEntries = learnedMonsterHunter
            ? new[] { CreatePlayerSpell(KineticSparkSpellId), CreatePlayerSpell(MonsterHunterSpellId) }
            : [CreatePlayerSpell(KineticSparkSpellId)];
        var session = CreateSession(
            strength: 1_000,
            spellEntries: spellEntries,
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                ["LegacyPhysicalLuckyStrikeRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "99",
                ["LegacyPhysicalDashRoll"] = "99",
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
                    Damage: 100,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0)).ToFrameBytes()))
            .ConfigureAwait(false);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        var target = Assert.Single(spell.Targets);
        Assert.Equal(monster.Uid, target.Uid);
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.Equal(monster.MaxLife - target.Damage, damagedMonster.Hitpoints);
        Assert.Contains("WorldServer handled legacy KineticSpark passive monster proc", log.ToString());
        return target.Damage;
    }
}
