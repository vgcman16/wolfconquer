using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacyLightOfStaminaRefineEffect = (uint)LegacyRefineEffect.LightOfStamina;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerLightOfStaminaSendsRefineEffectRestoresStaminaAndKeepsDamage()
    {
        var result = await RunPlayerReceiveLightOfStaminaMagicAttackAsync(
            casterStamina: 35,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.LightOfStamina"] = "100",
                ["LegacyLightOfStaminaRoll"] = "0",
                ["PrestigeLevel"] = "5",
            },
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PrestigeLevel"] = "5",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertLightOfStaminaRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.Equal("100", result.CasterSession.Character.Attributes["Stamina"]);
        Assert.Contains(
            ParseMsgUpdates(result.CasterSent),
            update => update.Uid == PlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Stamina &&
                    entry.Value == 100));

        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerLightOfStaminaSkipsWhenTargetPrestigeBelowAttacker()
    {
        var result = await RunPlayerReceiveLightOfStaminaMagicAttackAsync(
            casterStamina: 35,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.LightOfStamina"] = "100",
                ["LegacyLightOfStaminaRoll"] = "0",
                ["PrestigeLevel"] = "6",
            },
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PrestigeLevel"] = "5",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);
        Assert.DoesNotContain(
            ParseLegacyMsgRefineEffects(result.CasterSent.Concat(result.TargetSent)),
            effect => effect.Id == TargetPlayerUid &&
                effect.Param == TargetPlayerUid &&
                effect.Effect == LegacyLightOfStaminaRefineEffect);
        Assert.Equal("35", result.CasterSession.Character.Attributes["Stamina"]);
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.Stamina);

        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    private static async Task<PlayerReceiveLightOfStaminaHitResult> RunPlayerReceiveLightOfStaminaMagicAttackAsync(
        ulong casterStamina,
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
        var casterSession = CreateSession(
            stamina: casterStamina,
            attributes: casterAttributes);
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

        return new PlayerReceiveLightOfStaminaHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static void AssertLightOfStaminaRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.Contains(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == TargetPlayerUid &&
                effect.Param == TargetPlayerUid &&
                effect.Effect == LegacyLightOfStaminaRefineEffect);
    }

    private sealed record PlayerReceiveLightOfStaminaHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
