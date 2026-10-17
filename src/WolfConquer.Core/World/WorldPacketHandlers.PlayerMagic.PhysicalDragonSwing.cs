namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string RuntimeLegacyPhysicalDragonSwingRollAttribute =
        "LegacyPhysicalDragonSwingRoll";
    private const string RuntimeLegacyPhysicalDragonSwingDodgeAttribute = "Dodge";
    private const uint LegacyPhysicalDragonSwingSwingingTailBonus = 30;
    private const uint LegacyPhysicalDragonSwingDodgeBonus = 30;
    private const int LegacyPhysicalDragonSwingCompareAndSetAttempts = 4;
    private const string LegacyPhysicalDragonSwingEffect = "poisonmiss";

    private readonly record struct LegacyPlayerPlayerPhysicalDragonSwingResult(
        WorldActiveSession Target,
        uint Damage);

    private static async ValueTask<LegacyPlayerPlayerPhysicalDragonSwingResult>
        ApplyLegacyPlayerPlayerPhysicalDragonSwingAsync(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        LegacyMagicRuntimeSpell? spell,
        uint damage,
        CancellationToken cancellationToken)
    {
        var sessions = context.Sessions;
        var currentSession = target.Session;
        if (sessions?.TryGet(target.Connection, out var storedSession) == true && storedSession is not null)
        {
            currentSession = storedSession;
        }

        target = target with { Session = currentSession };
        if (!HasLegacyStatusFlag(currentSession.Character, LegacyDragonSwingFlag))
        {
            return new LegacyPlayerPlayerPhysicalDragonSwingResult(target, damage);
        }

        var swingingTailBonus = context.WorldData is { } worldData &&
            spell is not null &&
            IsLegacyPhysicalDragonSwingSwingingTailSpell(spell.Id) &&
            TryGetLegacyEligibleEquippedRuneLevel(currentSession, worldData, "SwingingTail", out _)
                ? LegacyPhysicalDragonSwingSwingingTailBonus
                : 0U;
        var baseChance = ReadAttributeUInt32(
            currentSession.Character.Attributes,
            RuntimeLegacyDragonSwingChanceAttribute);
        var chance = checked((int)Math.Min(int.MaxValue, (ulong)baseChance + swingingTailBonus));
        if (!CheckLegacyPercentRoll(
                currentSession.Character.Attributes,
                RuntimeLegacyPhysicalDragonSwingRollAttribute,
                chance))
        {
            return new LegacyPlayerPlayerPhysicalDragonSwingResult(target, damage);
        }

        for (var attempt = 0; attempt < LegacyPhysicalDragonSwingCompareAndSetAttempts; attempt++)
        {
            target = target with { Session = currentSession };
            if (!HasLegacyStatusFlag(currentSession.Character, LegacyDragonSwingFlag))
            {
                return new LegacyPlayerPlayerPhysicalDragonSwingResult(target, damage);
            }

            var currentDodge = CalculateLegacyRuntimeStatusFields(
                currentSession,
                context.WorldData)[LegacyPlayerStatusPacket.DodgeIndex];
            var updatedDodge = AddLegacySaturating(
                currentDodge,
                LegacyPhysicalDragonSwingDodgeBonus);
            var updatedSession = currentSession with
            {
                Character = WithCharacterAttribute(
                    currentSession.Character,
                    RuntimeLegacyPhysicalDragonSwingDodgeAttribute,
                    updatedDodge),
            };
            if (sessions is null ||
                sessions.TryCompareAndSet(target.Connection, currentSession, updatedSession))
            {
                var updatedTarget = target with { Session = updatedSession };
                await SendLegacyPhysicalDragonSwingEffectToVisibleAsync(
                        context,
                        updatedTarget,
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy Physical.OnPlayer DragonSwing avoidance " +
                        $"targetUid={updatedSession.Character.Uid} spellId={spell?.Id ?? 0} " +
                        $"baseChance={baseChance} swingingTailBonus={swingingTailBonus} " +
                        $"dodgeBefore={currentDodge} dodgeAfter={updatedDodge} damage=0; " +
                        "source=Physical.cs DragonSwing tail.")
                    .ConfigureAwait(false);
                return new LegacyPlayerPlayerPhysicalDragonSwingResult(updatedTarget, 0);
            }

            if (!sessions.TryGet(target.Connection, out var latestSession) || latestSession is null)
            {
                break;
            }

            currentSession = latestSession;
        }

        target = target with { Session = currentSession };
        await context.Log.WriteLineAsync(
                "WorldServer skipped legacy Physical.OnPlayer DragonSwing avoidance " +
                $"targetUid={currentSession.Character.Uid}; " +
                "WorldSessionStore.TryCompareAndSet repeatedly rejected the Dodge transition.")
            .ConfigureAwait(false);
        return new LegacyPlayerPlayerPhysicalDragonSwingResult(target, damage);
    }

    private static bool IsLegacyPhysicalDragonSwingSwingingTailSpell(ushort spellId)
    {
        return spellId is 11_030 or 11_060 or 11_590 or 11_600 or 11_650 or 11_660;
    }

    private static async ValueTask SendLegacyPhysicalDragonSwingEffectToVisibleAsync(
        WorldPacketHandlerContext context,
        WorldActiveSession target,
        CancellationToken cancellationToken)
    {
        var targetUid = CheckedUInt32(
            target.Session.Character.Uid,
            nameof(target.Session.Character.Uid));
        var effect = LegacyStringPacket.PlayerEffect(targetUid, LegacyPhysicalDragonSwingEffect);
        await SendFrameToPlayerAndVisiblePeersAsync(
                context,
                target.Connection,
                target.Session,
                effect.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
