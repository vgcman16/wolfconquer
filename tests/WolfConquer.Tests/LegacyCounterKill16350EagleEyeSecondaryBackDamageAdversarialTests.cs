using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint EagleEyeSecondaryPlayerUid = 1_293_602;
    private const uint EagleEyeOffLinePlayerUid = 1_293_603;
    private const uint EagleEyeOutOfRangePlayerUid = 1_293_604;

    [Fact]
    public async Task HandlerEagleEyeDirectAttackAddsOnlyUnscaledInLineSecondaryPlayers()
    {
        var result = await RunCounterKill16350EagleEyeSecondaryAttackAsync();

        var incoming = Assert.Single(
            ParseSpellAnimations(result.AttackerSent),
            animation => animation.SpellId == EagleEyeCastSpellId);
        Assert.Equal(
            [TargetPlayerUid, EagleEyeSecondaryPlayerUid],
            incoming.Targets.Select(target => target.Uid).ToArray());

        var primary = incoming.Targets[0];
        var secondary = incoming.Targets[1];
        Assert.True(secondary.Damage > 0);
        Assert.Equal((uint)((ulong)secondary.Damage * 14 / 22), primary.Damage);
        Assert.Equal(100_000L - primary.Damage, result.PrimaryRemainingHitpoints);
        Assert.Equal(100_000L - secondary.Damage, result.SecondaryRemainingHitpoints);
        Assert.Equal(100_000, result.OffLineRemainingHitpoints);
        Assert.Equal(100_000, result.OutOfRangeRemainingHitpoints);
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerEagleEyeSecondaryStatusEarlyReturnsStayUnscaledAndDoNotProc(
        int secondaryStatusFlag)
    {
        var result = await RunCounterKill16350EagleEyeSecondaryAttackAsync(
            secondaryStatusFlag,
            secondaryLearnsCounter: true);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.SecondarySent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        var incoming = Assert.Single(
            ParseSpellAnimations(result.AttackerSent),
            animation => animation.SpellId == EagleEyeCastSpellId);
        var secondary = Assert.Single(
            incoming.Targets,
            target => target.Uid == EagleEyeSecondaryPlayerUid);
        Assert.Equal(1U, secondary.Damage);
        Assert.Equal(99_999, result.SecondaryRemainingHitpoints);
    }

    [Fact]
    public async Task HandlerEagleEyeSecondaryWildPhoenixReturnStaysUnscaledAndDoesNotProc()
    {
        var result = await RunCounterKill16350EagleEyeSecondaryAttackAsync(
            LegacyWildPhoenixFlag,
            secondaryWildPhoenixCharges: 1,
            secondaryLearnsCounter: true);

        Assert.DoesNotContain(
            ParseSpellAnimations(result.AttackerSent.Concat(result.SecondarySent)),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        var incoming = Assert.Single(
            ParseSpellAnimations(result.AttackerSent),
            animation => animation.SpellId == EagleEyeCastSpellId);
        var secondary = Assert.Single(
            incoming.Targets,
            target => target.Uid == EagleEyeSecondaryPlayerUid);
        Assert.Equal(1U, secondary.Damage);
        Assert.Equal(99_999, result.SecondaryRemainingHitpoints);
        Assert.Equal("0", result.RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerEagleEyeSecondaryDefensiveStanceRemainsBackDamageEligible()
    {
        var result = await RunCounterKill16350EagleEyeSecondaryAttackAsync(
            DefensiveStanceStatusFlag,
            secondaryLearnsCounter: true);

        Assert.Contains(
            ParseSpellAnimations(result.SecondarySent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
    }

    [Fact]
    public async Task HandlerEagleEyeSecondaryBackDamageCastsBeforeSharedAnimationAndCarriesState()
    {
        var result = await RunCounterKill16350EagleEyeSecondaryAttackAsync(
            secondaryLearnsCounter: true);

        var procIndex = SingleFrameIndex(
            result.SecondarySent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.SecondarySent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == EagleEyeCastSpellId);
        Assert.True(
            procIndex < incomingIndex,
            "Matching DirectAttack processes secondary Physical.OnPlayer BackDmg before its shared animation.");

        var incoming = Assert.Single(
            ParseSpellAnimations(result.SecondarySent),
            animation => animation.SpellId == EagleEyeCastSpellId);
        Assert.Equal(
            [TargetPlayerUid, EagleEyeSecondaryPlayerUid],
            incoming.Targets.Select(target => target.Uid).ToArray());
        Assert.True(result.AttackerRemainingHitpoints < 100_000);
        Assert.Contains(HeavensWonderRuntimeSpellId, result.SecondaryMagicCooldowns.Keys);
    }

    private static async Task<CounterKill16350EagleEyeSecondaryResult>
        RunCounterKill16350EagleEyeSecondaryAttackAsync(
            int? secondaryStatusFlag = null,
            uint secondaryWildPhoenixCharges = 0,
            bool secondaryLearnsCounter = false)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 833,
            boundY: 583,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalog(
            monsterFamily,
            [spawn],
            CreateEagleEyeCastRow(),
            CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));

        var secondaryAttributes = new Dictionary<string, string>();
        if (secondaryLearnsCounter)
        {
            secondaryAttributes[CounterKill16350BackDamageRollAttribute] =
                0.ToString(CultureInfo.InvariantCulture);
        }

        if (secondaryStatusFlag is not null)
        {
            secondaryAttributes["StatusFlags"] = BuildStatusFlags(secondaryStatusFlag.Value);
        }

        var secondarySpellIds = new List<ushort>();
        if (secondaryLearnsCounter)
        {
            secondarySpellIds.Add(HeavensWonderRuntimeSpellId);
        }

        if (secondaryWildPhoenixCharges > 0)
        {
            secondarySpellIds.Add(WildPhoenixSpellId);
            secondaryAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            secondaryAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] =
                secondaryWildPhoenixCharges.ToString(CultureInfo.InvariantCulture);
        }

        var attackerSession = CreateSession(
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: [EagleEyeCastSpellId]);
        var primarySession = CreateSession(
            uid: TargetPlayerUid,
            name: "EagleEyePrimary",
            x: 832,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: []);
        var secondarySession = CreateSession(
            uid: EagleEyeSecondaryPlayerUid,
            name: "EagleEyeSecondary",
            x: 834,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: secondarySpellIds,
            attributes: secondaryAttributes);
        var offLineSession = CreateSession(
            uid: EagleEyeOffLinePlayerUid,
            name: "EagleEyeOffLine",
            x: 834,
            y: 585,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: []);
        var outOfRangeSession = CreateSession(
            uid: EagleEyeOutOfRangePlayerUid,
            name: "EagleEyeOutOfRange",
            x: 840,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: []);

        var sessions = new WorldSessionStore();
        var attackerSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        var secondarySent = new List<byte[]>();
        using var log = new StringWriter();
        var attackerConnection = CreateSendingConnection(attackerSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var secondaryConnection = CreateSendingConnection(secondarySent);
        var offLineConnection = CreateSendingConnection([]);
        var outOfRangeConnection = CreateSendingConnection([]);
        sessions.Set(attackerConnection, attackerSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(secondaryConnection, secondarySession);
        sessions.Set(offLineConnection, offLineSession);
        sessions.Set(outOfRangeConnection, outOfRangeSession);
        var context = new WorldPacketHandlerContext(
            attackerConnection,
            log,
            attackerSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());

        Assert.True(await InvokeLegacyEagleEyeCastAsync(
            context,
            attackerSession,
            CreateEagleEyeQuery(TargetPlayerUid),
            attackerSession.PlayerSpells.Spells[0],
            CreateEagleEyeRuntimeSpell()));

        Assert.True(sessions.TryGet(attackerConnection, out var finalAttacker));
        Assert.NotNull(finalAttacker);
        Assert.True(sessions.TryGet(primaryConnection, out var finalPrimary));
        Assert.NotNull(finalPrimary);
        Assert.True(sessions.TryGet(secondaryConnection, out var finalSecondary));
        Assert.NotNull(finalSecondary);
        Assert.True(sessions.TryGet(offLineConnection, out var finalOffLine));
        Assert.NotNull(finalOffLine);
        Assert.True(sessions.TryGet(outOfRangeConnection, out var finalOutOfRange));
        Assert.NotNull(finalOutOfRange);
        finalSecondary.Character.Attributes.TryGetValue(
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute,
            out var remainingWildPhoenixCharges);

        return new CounterKill16350EagleEyeSecondaryResult(
            attackerSent,
            secondarySent,
            finalAttacker.Character.MinHitPoints,
            finalPrimary.Character.MinHitPoints,
            finalSecondary.Character.MinHitPoints,
            finalOffLine.Character.MinHitPoints,
            finalOutOfRange.Character.MinHitPoints,
            finalSecondary.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>(),
            remainingWildPhoenixCharges);
    }

    private sealed record CounterKill16350EagleEyeSecondaryResult(
        IReadOnlyList<byte[]> AttackerSent,
        IReadOnlyList<byte[]> SecondarySent,
        long AttackerRemainingHitpoints,
        long PrimaryRemainingHitpoints,
        long SecondaryRemainingHitpoints,
        long OffLineRemainingHitpoints,
        long OutOfRangeRemainingHitpoints,
        IReadOnlyDictionary<ushort, DateTimeOffset> SecondaryMagicCooldowns,
        string? RemainingWildPhoenixCharges);
}
