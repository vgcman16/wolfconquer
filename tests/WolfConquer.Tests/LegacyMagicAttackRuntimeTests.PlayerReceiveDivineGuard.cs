using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int LegacyDivineGuardFlag = 0xB1;
    private const uint LegacyDivineGuardRefineEffect = (uint)LegacyRefineEffect.DivineGuard;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerDivineGuardSendsCasterRefineEffectGrantsStatusAndKeepsDamage()
    {
        var result = await RunPlayerReceiveDivineGuardMagicAttackAsync(
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.DivineGuard"] = "100",
                ["LegacyDivineGuardRoll"] = "0",
                ["LegacyDivineGuardStatusRoll"] = "0",
                ["PrestigeLevel"] = "5",
            },
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PrestigeLevel"] = "5",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertDivineGuardRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyDivineGuardFlag));
        AssertDivineGuardAzureShieldUpdate(result.CasterSent);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Theory]
    [InlineData("6", "5", "0")]
    [InlineData("5", "5", "100")]
    public async Task HandlerMagicAttackAgainstVisiblePlayerDivineGuardSkipsWhenPrestigeGateOrMainRollMissesAndKeepsDamage(
        string casterPrestige,
        string targetPrestige,
        string mainRoll)
    {
        var result = await RunPlayerReceiveDivineGuardMagicAttackAsync(
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.DivineGuard"] = "100",
                ["LegacyDivineGuardRoll"] = mainRoll,
                ["LegacyDivineGuardStatusRoll"] = "0",
                ["PrestigeLevel"] = casterPrestige,
            },
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PrestigeLevel"] = targetPrestige,
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertNoDivineGuardRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.False(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyDivineGuardFlag));
        AssertNoDivineGuardAzureShieldUpdate(result.CasterSent);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerDivineGuardExistingFlagStillSendsRefineEffectWithoutDuplicateStatusGrant()
    {
        var result = await RunPlayerReceiveDivineGuardMagicAttackAsync(
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.DivineGuard"] = "100",
                ["LegacyDivineGuardRoll"] = "0",
                ["LegacyDivineGuardStatusRoll"] = "0",
                ["PrestigeLevel"] = "5",
                ["StatusFlags"] = BuildStatusFlags(LegacyDivineGuardFlag),
            },
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PrestigeLevel"] = "5",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertDivineGuardRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyDivineGuardFlag));
        AssertNoDivineGuardAzureShieldUpdate(result.CasterSent);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    private static async Task<PlayerReceiveDivineGuardHitResult> RunPlayerReceiveDivineGuardMagicAttackAsync(
        IReadOnlyDictionary<string, string> casterAttributes,
        IReadOnlyDictionary<string, string> targetAttributes)
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
            y: 584,
            attributes: targetAttributes);
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

        return new PlayerReceiveDivineGuardHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static void AssertDivineGuardRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.Contains(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == PlayerUid &&
                effect.Param == PlayerUid &&
                effect.Effect == LegacyDivineGuardRefineEffect);
    }

    private static void AssertNoDivineGuardRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == PlayerUid &&
                effect.Param == PlayerUid &&
                effect.Effect == LegacyDivineGuardRefineEffect);
    }

    private static void AssertDivineGuardAzureShieldUpdate(IEnumerable<byte[]> frames)
    {
        Assert.Contains(
            ParseMsgUpdates(frames).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AzureShield &&
                entry.Value == 0 &&
                entry.Flag == LegacyDivineGuardFlag &&
                entry.Time == 15 &&
                entry.Damage == 0 &&
                entry.SpellLevel == 0);
    }

    private static void AssertNoDivineGuardAzureShieldUpdate(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            ParseMsgUpdates(frames).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AzureShield &&
                entry.Flag == LegacyDivineGuardFlag);
    }

    private sealed record PlayerReceiveDivineGuardHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
