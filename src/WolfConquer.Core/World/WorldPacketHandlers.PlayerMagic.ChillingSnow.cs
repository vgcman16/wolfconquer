namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask TryApplyLegacyPlayerMagicChillingSnowAsync(
        WorldPacketHandlerContext context,
        WorldSession attackerSession,
        IReadOnlyList<LegacyPlayerMagicDamageResult> results,
        CancellationToken cancellationToken)
    {
        if (!HasLegacyStatusFlag(attackerSession.Character, LegacyChillingSnowFlag) ||
            context.WorldData is not { } worldData ||
            !LegacyMagicRuntime.TryResolveLearnedSpell(
                attackerSession.PlayerSpells,
                worldData,
                LegacyChillingSnowSpellId,
                out var chillingSnowPlayerSpell,
                out _) ||
            !TryReadLegacyMagicUInt32Field(
                worldData,
                LegacyChillingSnowSpellId,
                chillingSnowPlayerSpell.Level,
                LegacyMagicDamageField,
                out var durationSeconds) ||
            durationSeconds == 0 ||
            durationSeconds > int.MaxValue)
        {
            return;
        }

        var boundedDurationSeconds = (int)durationSeconds;
        var nowUtc = DateTimeOffset.UtcNow;
        foreach (var result in results)
        {
            if (result.DamageApplied == 0 || result.Killed)
            {
                continue;
            }

            await TryApplyLegacyPlayerMagicChillingSnowToTargetAsync(
                    context,
                    attackerSession,
                    result,
                    boundedDurationSeconds,
                    nowUtc,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask TryApplyLegacyPlayerMagicChillingSnowToTargetAsync(
        WorldPacketHandlerContext context,
        WorldSession attackerSession,
        LegacyPlayerMagicDamageResult result,
        int durationSeconds,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var targetSession = result.UpdatedSession;
        if (context.Sessions is { } sessions &&
            sessions.TryGet(result.ActiveSession.Connection, out var currentTargetSession) &&
            currentTargetSession is not null)
        {
            targetSession = currentTargetSession;
        }

        if (HasLegacyStatusFlag(targetSession.Character, LegacyXChillingSnowFlag))
        {
            return;
        }

        var updatedCooldowns = targetSession.MagicCooldowns is null
            ? null
            : new Dictionary<ushort, DateTimeOffset>(targetSession.MagicCooldowns);
        var extendedSpells = new List<LegacyMagicColdTimeSpell>();
        var extension = TimeSpan.FromSeconds(durationSeconds);
        if (updatedCooldowns is not null)
        {
            foreach (var (spellId, readyAtUtc) in targetSession.MagicCooldowns!)
            {
                if (LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(readyAtUtc, nowUtc) <= 0)
                {
                    continue;
                }

                var extendedReadyAtUtc = AddLegacyChillingSnowCooldownExtension(readyAtUtc, extension);
                updatedCooldowns[spellId] = extendedReadyAtUtc;
                extendedSpells.Add(new LegacyMagicColdTimeSpell(
                    spellId,
                    LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(extendedReadyAtUtc, nowUtc)));
            }
        }

        var updatedTargetSession = targetSession with
        {
            Character = WithLegacyStatusFlag(targetSession.Character, LegacyXChillingSnowFlag),
            MagicCooldowns = extendedSpells.Count == 0 ? targetSession.MagicCooldowns : updatedCooldowns,
        };
        context.Sessions?.Set(result.ActiveSession.Connection, updatedTargetSession);

        await SendPlayerStatusFlagVectorToVisibleAsync(
                context,
                result.ActiveSession.Connection,
                updatedTargetSession,
                cancellationToken)
            .ConfigureAwait(false);

        if (extendedSpells.Count > 0)
        {
            await result.ActiveSession.Connection.SendAsync(
                    new LegacyMagicColdTimePacket(extendedSpells).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy player-target magic ChillingSnow cold-time side effect " +
                $"{FormatSession(context with { Session = attackerSession })} targetUid={targetSession.Character.Uid} " +
                $"statusFlag={LegacyXChillingSnowFlag} durationSeconds={durationSeconds} " +
                $"extendedCooldowns={extendedSpells.Count}; " +
                "source=CanAttackPlayer.Verified ProcessColdTime.OnDequeue.")
            .ConfigureAwait(false);
    }

    private static DateTimeOffset AddLegacyChillingSnowCooldownExtension(
        DateTimeOffset readyAtUtc,
        TimeSpan extension)
    {
        var normalizedReadyAtUtc = readyAtUtc.ToUniversalTime();
        var maximumExtension = DateTimeOffset.MaxValue - normalizedReadyAtUtc;
        return extension >= maximumExtension
            ? DateTimeOffset.MaxValue
            : normalizedReadyAtUtc.Add(extension);
    }
}
