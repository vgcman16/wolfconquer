using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacyKillingFlashRefineEffect = (uint)LegacyRefineEffect.KillingFlash;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerKillingFlashSendsCasterRefineEffectOpensXpListAndKeepsDamage()
    {
        var result = await RunPlayerReceiveKillingFlashMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.KillingFlash"] = "100",
                ["LegacyKillingFlashRoll"] = "0",
                ["LegacyKillingFlashXPListRoll"] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertKillingFlashRefineEffect(result.CasterSent.Concat(result.TargetSent));

        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.XPList &&
                    entry.Value == 1));

        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyXpListFlag));
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Theory]
    [InlineData(null, "0")]
    [InlineData("100", "100")]
    public async Task HandlerMagicAttackAgainstVisiblePlayerKillingFlashSkipsWhenRateMissingOrMainRollMissesAndKeepsDamage(
        string? killingFlashRate,
        string mainRoll)
    {
        var casterAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyKillingFlashRoll"] = mainRoll,
            ["LegacyKillingFlashXPListRoll"] = "0",
        };
        if (killingFlashRate is not null)
        {
            casterAttributes["PerfectionStatus.KillingFlash"] = killingFlashRate;
        }

        var result = await RunPlayerReceiveKillingFlashMagicAttackAsync(casterAttributes);

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertNoKillingFlashRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.False(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyXpListFlag));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.XPList);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerKillingFlashExistingXpListStillSendsRefineEffectWithoutDuplicateGrant()
    {
        var result = await RunPlayerReceiveKillingFlashMagicAttackAsync(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.KillingFlash"] = "100",
                ["LegacyKillingFlashRoll"] = "0",
                ["LegacyKillingFlashXPListRoll"] = "0",
                ["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag),
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertKillingFlashRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.XPList);
        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyXpListFlag));
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    private static async Task<PlayerReceiveKillingFlashHitResult> RunPlayerReceiveKillingFlashMagicAttackAsync(
        IReadOnlyDictionary<string, string> casterAttributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(attributes: casterAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584);
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

        return new PlayerReceiveKillingFlashHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static void AssertKillingFlashRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.Contains(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == PlayerUid &&
                effect.Param == PlayerUid &&
                effect.Effect == LegacyKillingFlashRefineEffect);
    }

    private static void AssertNoKillingFlashRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == PlayerUid &&
                effect.Param == PlayerUid &&
                effect.Effect == LegacyKillingFlashRefineEffect);
    }

    private sealed record PlayerReceiveKillingFlashHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
