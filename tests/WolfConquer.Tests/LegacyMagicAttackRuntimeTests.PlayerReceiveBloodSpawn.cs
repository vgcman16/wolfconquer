using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint LegacyBloodSpawnRefineEffect = (uint)LegacyRefineEffect.BloodSpawn;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerBloodSpawnSendsRefineEffectRestoresTargetResourcesThenKeepsDamage()
    {
        var result = await RunPlayerReceiveBloodSpawnMagicAttackAsync(
            targetMinHitPoints: 3_000,
            targetMaxHitpoints: 5_000,
            targetMinMana: 125,
            targetMaxMana: 500,
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.BloodSpawn"] = "100",
                ["LegacyBloodSpawnRoll"] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertBloodSpawnRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.Equal(500, result.TargetSession.Character.MinMana);
        AssertBloodSpawnHitpointUpdatePair(result.TargetSent, maxHitpoints: 5_000, hitpoints: 5_000);
        AssertBloodSpawnManaUpdate(result.TargetSent, mana: 500);
        AssertBloodSpawnRestoreBeforeNormalThunderDamage(result.TargetSent);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Theory]
    [InlineData(null, "0")]
    [InlineData("100", "100")]
    public async Task HandlerMagicAttackAgainstVisiblePlayerBloodSpawnSkipsWhenRateMissingOrMainRollMissesAndKeepsDamage(
        string? bloodSpawnRate,
        string mainRoll)
    {
        var targetAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LegacyBloodSpawnRoll"] = mainRoll,
        };
        if (bloodSpawnRate is not null)
        {
            targetAttributes["PerfectionStatus.BloodSpawn"] = bloodSpawnRate;
        }

        var result = await RunPlayerReceiveBloodSpawnMagicAttackAsync(
            targetMinHitPoints: 3_000,
            targetMaxHitpoints: 5_000,
            targetMinMana: 125,
            targetMaxMana: 500,
            targetAttributes: targetAttributes);

        Assert.Equal(3_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertNoBloodSpawnRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.Equal(125, result.TargetSession.Character.MinMana);
        AssertNoBloodSpawnResourceUpdates(result.TargetSent);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerBloodSpawnFullResourcesStillSendsRefineEffectWithoutResourceUpdates()
    {
        var result = await RunPlayerReceiveBloodSpawnMagicAttackAsync(
            targetMinHitPoints: 5_000,
            targetMaxHitpoints: 5_000,
            targetMinMana: 500,
            targetMaxMana: 500,
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PerfectionStatus.BloodSpawn"] = "100",
                ["LegacyBloodSpawnRoll"] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);

        AssertBloodSpawnRefineEffect(result.CasterSent.Concat(result.TargetSent));
        Assert.Equal(500, result.TargetSession.Character.MinMana);
        AssertNoBloodSpawnResourceUpdates(result.TargetSent);
        AssertNormalThunderDamage(result.CasterSent);
        AssertNormalThunderDamage(result.TargetSent);
    }

    private static async Task<PlayerReceiveBloodSpawnHitResult> RunPlayerReceiveBloodSpawnMagicAttackAsync(
        long targetMinHitPoints,
        long targetMaxHitpoints,
        long targetMinMana,
        long targetMaxMana,
        IReadOnlyDictionary<string, string> targetAttributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(nativeMana500: true)],
            // BloodSpawn resource assertions use zero spell mana cost to isolate restore packets.
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, damage: 541, coldTime: 0));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession();
        var attributes = new Dictionary<string, string>(targetAttributes, StringComparer.OrdinalIgnoreCase)
        {
            ["MaxHitpoints"] = targetMaxHitpoints.ToString(CultureInfo.InvariantCulture),
        };
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584,
            minHitPoints: targetMinHitPoints,
            minMana: targetMinMana,
            attributes: attributes);
        targetSession = targetSession with
        {
            Character = targetSession.Character with { MaxMana = targetMaxMana },
        };
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

        return new PlayerReceiveBloodSpawnHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static void AssertBloodSpawnRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.Contains(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == TargetPlayerUid &&
                effect.Param == TargetPlayerUid &&
                effect.Effect == LegacyBloodSpawnRefineEffect);
    }

    private static void AssertNoBloodSpawnRefineEffect(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(
            ParseLegacyMsgRefineEffects(frames),
            effect => effect.Id == TargetPlayerUid &&
                effect.Param == TargetPlayerUid &&
                effect.Effect == LegacyBloodSpawnRefineEffect);
    }

    private static void AssertBloodSpawnHitpointUpdatePair(
        IEnumerable<byte[]> frames,
        ulong maxHitpoints,
        ulong hitpoints)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.SequenceEqual(
                    [
                        new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maxHitpoints),
                        new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, hitpoints),
                    ]));
    }

    private static void AssertBloodSpawnManaUpdate(IEnumerable<byte[]> frames, ulong mana)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, mana)]));
    }

    private static void AssertBloodSpawnRestoreBeforeNormalThunderDamage(IReadOnlyList<byte[]> frames)
    {
        var hitpointUpdateIndex = SingleFrameIndex(frames, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == TargetPlayerUid &&
            update.Updates.SequenceEqual(
                [
                    new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 5_000),
                    new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 5_000),
                ]));
        var manaUpdateIndex = SingleFrameIndex(frames, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
            update.Uid == TargetPlayerUid &&
            update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 500)]));
        var thunderAnimationIndex = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
            spell.AttackerUid == PlayerUid &&
            spell.SpellId == ThunderSpellId &&
            spell.Targets.Count == 1 &&
            spell.Targets[0].Uid == TargetPlayerUid &&
            spell.Targets[0].Damage == ExpectedPlayerThunderDamage &&
            spell.Targets[0].Hit);

        Assert.True(hitpointUpdateIndex < thunderAnimationIndex);
        Assert.True(manaUpdateIndex < thunderAnimationIndex);
    }

    private static void AssertNoBloodSpawnResourceUpdates(IEnumerable<byte[]> frames)
    {
        var updates = ParseMsgUpdates(frames);
        Assert.DoesNotContain(
            updates,
            update => update.Uid == TargetPlayerUid &&
                update.Updates.SequenceEqual(
                    [
                        new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 5_000),
                        new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, 5_000),
                    ]));
        Assert.DoesNotContain(
            updates,
            update => update.Uid == TargetPlayerUid &&
                update.Updates.SequenceEqual([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 500)]));
    }

    private sealed record PlayerReceiveBloodSpawnHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
