using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacyCalmWindRefineEffect = (uint)LegacyRefineEffect.CalmWind;
    private const ulong LegacyCalmWindInitialStamina = 42;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerCalmWindSendsCasterRefineEffectAndKeepsDamage()
    {
        var result = await RunPlayerReceiveCalmWindMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.CalmWind"] = "100",
                ["LegacyCalmWindRoll"] = "0",
            });

        Assert.Equal(5_000, result.CasterSession.Character.MinHitPoints);
        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);
        Assert.Equal(
            LegacyCalmWindInitialStamina.ToString(CultureInfo.InvariantCulture),
            result.CasterSession.Character.Attributes["Stamina"]);
        Assert.False(result.TargetSession.Character.Attributes.ContainsKey("StatusFlags"));

        AssertCalmWindRefineEffect(result.CasterSent.Concat(result.TargetSent));
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerCalmWindSkipsEffectWhenRollMissesAndKeepsDamage()
    {
        var result = await RunPlayerReceiveCalmWindMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.CalmWind"] = "100",
                ["LegacyCalmWindRoll"] = "100",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);
        Assert.DoesNotContain(
            ParseLegacyMsgRefineEffects(result.CasterSent.Concat(result.TargetSent)),
            effect => effect.Id == PlayerUid &&
                effect.Param == PlayerUid &&
                effect.Effect == LegacyCalmWindRefineEffect);

        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    private static async Task<PlayerReceiveCalmWindHitResult> RunPlayerReceiveCalmWindMagicAttackAsync(
        IReadOnlyDictionary<string, string> casterAttributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet()],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            stamina: LegacyCalmWindInitialStamina,
            attributes: casterAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584);
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        return new PlayerReceiveCalmWindHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static void AssertCalmWindRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.Contains(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == PlayerUid &&
                effect.Param == PlayerUid &&
                effect.Effect == LegacyCalmWindRefineEffect);
    }

    private sealed record PlayerReceiveCalmWindHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
