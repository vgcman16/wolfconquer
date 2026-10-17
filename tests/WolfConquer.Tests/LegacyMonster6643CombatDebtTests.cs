using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int Monster6643DebtHawksAmbitionFlag = 0x100;

    [Theory]
    [InlineData(0, 0U, 10_500, 15_080U)]
    [InlineData(1, 0U, 10_502, 11_600U)]
    [InlineData(0, 3U, 10_500, 15_087U)]
    [InlineData(1, 3U, 10_502, 11_606U)]
    [InlineData(2, 3U, 10_503, 11_606U)]
    [InlineData(3, 3U, 10_504, 11_606U)]
    [InlineData(4, 3U, 10_506, 11_606U)]
    [InlineData(0, 2_427U, 10_500, 21_390U)]
    [InlineData(1, 2_427U, 10_502, 16_454U)]
    public async Task HandlerMonster6643DebtPhysicalSpellUsesIndependentLiteralAttackRollDamage(
        int spellIndex,
        uint attackRoll,
        int expectedSpellId,
        uint expectedDamage)
    {
        // ActionHandler.PhysicalAttack draws [5800,8228), then adds the configured 100%.
        // The family-6643 caller applies integer 130/100 only to spell-table index zero.
        var result = await RunMonster6643DebtAttackAsync(spellIndex, attackRoll);

        AssertMonster6643DebtAttack(result, expectedSpellId, expectedDamage);
        Assert.Equal(
            [2239, 2176, 2176],
            result.Sent.Select(frame => (int)ReadFrame(frame).Header.Opcode).ToArray());
    }

    [Theory]
    [InlineData(0, 10_500, 5_460U)]
    [InlineData(1, 10_502, 4_200U)]
    public async Task HandlerMonster6643DebtPhysicalSpellBoostFollowsSourceBossMinimum(
        int spellIndex,
        int expectedSpellId,
        uint expectedDamage)
    {
        // Defense reduces 11606 to one, then PhysicalAttack replaces it with 4200.
        // Spell 10500 scales that completed PhysicalAttack result, not its raw attack.
        var result = await RunMonster6643DebtAttackAsync(
            spellIndex,
            attackRoll: 3,
            adjustedDefense: 20_000,
            bossMinimumDamage: 4_200);

        AssertMonster6643DebtAttack(result, expectedSpellId, expectedDamage);
        Assert.Equal(
            [2239, 2176, 2176],
            result.Sent.Select(frame => (int)ReadFrame(frame).Header.Opcode).ToArray());
    }

    [Theory]
    [InlineData(0, 10_500, 2_730U)]
    [InlineData(1, 10_502, 2_100U)]
    public async Task HandlerMonster6643DebtPhysicalSpellConsumesHawksAfterFloorBeforeBoost(
        int spellIndex,
        int expectedSpellId,
        uint expectedDamage)
    {
        // PhysicalAttack applies Hawks after the boss floor: 4200 -> 2100.
        // Only the completed result is then boosted: 2100 * 130 / 100 = 2730.
        var result = await RunMonster6643DebtAttackAsync(
            spellIndex,
            attackRoll: 3,
            adjustedDefense: 20_000,
            bossMinimumDamage: 4_200,
            hawksAmbition: true);

        AssertMonster6643DebtAttack(result, expectedSpellId, expectedDamage);
        Assert.False(HasLegacyStatusFlag(
            result.Session.Character.Attributes,
            Monster6643DebtHawksAmbitionFlag));
        Assert.Contains("consumed legacy HawksAmbition monster-hit reduction", result.Output);
        Assert.Equal(
            [2176, 2239, 2176, 2176],
            result.Sent.Select(frame => (int)ReadFrame(frame).Header.Opcode).ToArray());
        var status = Assert.Single(ParseMsgUpdates(result.Sent), packet =>
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag));
        var statusEntry = Assert.Single(status.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.False(IsLegacyStatusFlagSet(statusEntry.Values, Monster6643DebtHawksAmbitionFlag));
    }

    private static async Task<Monster6643DebtAttackResult> RunMonster6643DebtAttackAsync(
        int spellIndex,
        uint attackRoll,
        uint adjustedDefense = 0,
        uint? bossMinimumDamage = null,
        bool hawksAmbition = false)
    {
        var monsterFamily = CreateLegacyBossFamily(6_643, "Boss-6643-Debt");
        var spawn = CreateMonsterSpawn(
            boundX: 830,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(monsterFamily, spawn);
        var monster = LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)).First();
        var attributes = new Dictionary<string, string>
        {
            ["LegacyBossSpellIndex"] = spellIndex.ToString(CultureInfo.InvariantCulture),
            ["LegacyMonsterPhysicalAttackRoll"] = attackRoll.ToString(CultureInfo.InvariantCulture),
            ["LegacyBossStatusRoll"] = "99",
            ["LegacyBossStatusSecondaryRoll"] = "99",
            ["AjustDefense"] = adjustedDefense.ToString(CultureInfo.InvariantCulture),
        };
        if (bossMinimumDamage is { } minimumDamage)
        {
            attributes["LegacyMonsterBossMinimumDamage"] = minimumDamage.ToString(CultureInfo.InvariantCulture);
        }

        if (hawksAmbition)
        {
            attributes["StatusFlags"] = BuildStatusFlags(Monster6643DebtHawksAmbitionFlag);
            attributes["HawksAmbitionPower"] = "50";
        }

        var session = CreateSession(minHitPoints: 50_000, attributes: attributes);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        sessions.Set(connection, session);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await WorldPacketHandlers.TrySendLegacyMonsterRetaliationAsync(
            context,
            session,
            monster,
            monsterFamily,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new Monster6643DebtAttackResult(monster.Uid, updatedSession, sent, log.ToString());
    }

    private static void AssertMonster6643DebtAttack(
        Monster6643DebtAttackResult result,
        int expectedSpellId,
        uint expectedDamage)
    {
        var animation = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(expectedSpellId, animation.SpellId);
        Assert.Equal(result.MonsterUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((830, 584), ((int)animation.X, (int)animation.Y));
        var target = Assert.Single(animation.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(50_000L - expectedDamage, result.Session.Character.MinHitPoints);

        var hpUpdates = ParseMsgUpdates(result.Sent)
            .Where(packet => packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.MaxHitpoints))
            .ToArray();
        Assert.Equal(2, hpUpdates.Length);
        Assert.All(hpUpdates, packet => Assert.Equal(PlayerUid, packet.Uid));
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, Assert.Single(hpUpdates[0].Updates).Type);
        Assert.Equal(
            [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
            hpUpdates[1].Updates.Select(entry => entry.Type).ToArray());
        Assert.Equal(50_000UL - expectedDamage, hpUpdates[1].Updates[1].Value);
        Assert.Contains("WorldServer sent legacy boss physical spell retaliation", result.Output);
        Assert.Contains($"spellId={expectedSpellId}", result.Output);
        Assert.Contains("forcedIndex=True", result.Output);
        Assert.Contains("statusApplied=False", result.Output);
    }

    private sealed record Monster6643DebtAttackResult(
        uint MonsterUid,
        WorldSession Session,
        List<byte[]> Sent,
        string Output);
}
