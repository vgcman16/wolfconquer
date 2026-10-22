using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacyDrainingTouchRefineEffect = (uint)LegacyRefineEffect.DrainingTouch;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerDrainingTouchSendsRefineEffectRestoresResourcesAndKeepsDamage()
    {
        var result = await RunPlayerReceiveDrainingTouchMagicAttackAsync(
            casterMinHitPoints: 3_000,
            casterMaxHitpoints: 5_000,
            casterMinMana: 125,
            casterMaxMana: 500,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.DrainingTouch"] = "100",
                ["LegacyDrainingTouchRoll"] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertDrainingTouchRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.Equal(5_000, result.CasterSession.Character.MinHitPoints);
        Assert.Equal(500, result.CasterSession.Character.MinMana);
        AssertDrainingTouchHitpointUpdatePair(result.CasterSent, maxHitpoints: 5_000, hitpoints: 5_000);
        AssertDrainingTouchManaUpdate(result.CasterSent, mana: 500);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Theory]
    [InlineData(null, "0")]
    [InlineData("100", "100")]
    public async Task HandlerMagicAttackAgainstVisiblePlayerDrainingTouchSkipsWhenRateMissingOrMainRollMissesAndKeepsDamage(
        string? drainingTouchRate,
        string mainRoll)
    {
        var casterAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyDrainingTouchRoll"] = mainRoll,
        };
        if (drainingTouchRate is not null)
        {
            casterAttributes["PerfectionStatus.DrainingTouch"] = drainingTouchRate;
        }

        var result = await RunPlayerReceiveDrainingTouchMagicAttackAsync(
            casterMinHitPoints: 3_000,
            casterMaxHitpoints: 5_000,
            casterMinMana: 125,
            casterMaxMana: 500,
            casterAttributes: casterAttributes);

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertNoDrainingTouchRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.Equal(3_000, result.CasterSession.Character.MinHitPoints);
        Assert.Equal(125, result.CasterSession.Character.MinMana);
        AssertNoDrainingTouchResourceUpdates(result.CasterSent);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerDrainingTouchFullResourcesStillSendsRefineEffectWithoutResourceUpdates()
    {
        var result = await RunPlayerReceiveDrainingTouchMagicAttackAsync(
            casterMinHitPoints: 5_000,
            casterMaxHitpoints: 5_000,
            casterMinMana: 500,
            casterMaxMana: 500,
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.DrainingTouch"] = "100",
                ["LegacyDrainingTouchRoll"] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertDrainingTouchRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.Equal(5_000, result.CasterSession.Character.MinHitPoints);
        Assert.Equal(500, result.CasterSession.Character.MinMana);
        AssertNoDrainingTouchResourceUpdates(result.CasterSent);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    private static async Task<PlayerReceiveDrainingTouchHitResult> RunPlayerReceiveDrainingTouchMagicAttackAsync(
        long casterMinHitPoints,
        long casterMaxHitpoints,
        long casterMinMana,
        long casterMaxMana,
        IReadOnlyDictionary<string, string> casterAttributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(nativeMana500: true)],
            // DrainingTouch resource assertions use zero spell mana cost to isolate the restore packets.
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var attributes = new Dictionary<string, string>(casterAttributes, StringComparer.OrdinalIgnoreCase)
        {
            ["MaxHitpoints"] = casterMaxHitpoints.ToString(CultureInfo.InvariantCulture),
        };
        var casterSession = CreateSession(
            minHitPoints: casterMinHitPoints,
            minMana: casterMinMana,
            attributes: attributes);
        casterSession = casterSession with
        {
            Character = casterSession.Character with { MaxMana = casterMaxMana },
        };
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584);
        casterSession = CreateRetainedNativeMagic421Input(casterSession, nativeMana500: true);
        targetSession = CreateRetainedNativeMagic421Input(targetSession, nativeMana500: true);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        context = await PrepareRetainedNativeMagic421QueriesAsync(
            context, targetConnection, casterSent, targetSent, nativeMana500: true);

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

        return new PlayerReceiveDrainingTouchHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static void AssertDrainingTouchRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.Contains(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == PlayerUid &&
                effect.Param == PlayerUid &&
                effect.Effect == LegacyDrainingTouchRefineEffect);
    }

    private static void AssertNoDrainingTouchRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == PlayerUid &&
                effect.Param == PlayerUid &&
                effect.Effect == LegacyDrainingTouchRefineEffect);
    }

    private static void AssertDrainingTouchHitpointUpdatePair(
        IEnumerable<byte[]> frames,
        ulong maxHitpoints,
        ulong hitpoints)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            update => update.Uid == PlayerUid &&
                update.Updates.SequenceEqual(
                    [
                        new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maxHitpoints),
                        new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, hitpoints),
                    ]));
    }

    private static void AssertDrainingTouchManaUpdate(IEnumerable<byte[]> frames, ulong mana)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            update => update.Uid == PlayerUid &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, mana)]));
    }

    private static void AssertNoDrainingTouchResourceUpdates(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            ParseMsgUpdates(frames).SelectMany(update => update.Updates),
            entry => entry.Type is
                LegacyMsgUpdateDataType.MaxHitpoints or
                LegacyMsgUpdateDataType.Hitpoints or
                LegacyMsgUpdateDataType.Mana);
    }

    private sealed record PlayerReceiveDrainingTouchHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
