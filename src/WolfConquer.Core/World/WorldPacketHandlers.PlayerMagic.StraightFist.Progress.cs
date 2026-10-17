namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<WorldSession> ApplyLegacyStraightFistIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ulong progressDamage,
        CancellationToken cancellationToken)
    {
        if (progressDamage == 0)
        {
            return session;
        }

        var boundedProgress = progressDamage > uint.MaxValue ? uint.MaxValue : (uint)progressDamage;
        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, boundedProgress));
        var updatedSession = ApplyLegacyExperienceSideEffects(
            context,
            context.Connection,
            session,
            experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            if (experienceResult.OblivionExperienceGain != 0 ||
                experienceResult.HuntingBlessingGain != 0)
            {
                await context.Log.WriteLineAsync(
                        "WorldServer applied legacy StraightFist experience side effects " +
                        $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                        $"applied=0 oblivion={experienceResult.OblivionExperienceGain} " +
                        $"huntingBlessing={experienceResult.HuntingBlessingGain} " +
                        $"suppression={experienceResult.SuppressionReason}; " +
                        "source=StraightFist.cs IncreaseExperience.Up.")
                    .ConfigureAwait(false);
            }

            return updatedSession;
        }

        updatedSession = await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer sent legacy StraightFist experience " +
                $"{FormatSession(context with { Session = updatedSession })} progressDamage={boundedProgress} " +
                $"gain={experienceResult.AppliedExperienceGain} oblivion={experienceResult.OblivionExperienceGain} " +
                $"huntingBlessing={experienceResult.HuntingBlessingGain} " +
                $"experience={updatedSession.Character.Experience} level={updatedSession.Character.Level}; " +
                "source=StraightFist.cs IncreaseExperience.Up.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static uint CalculateLegacyStraightFistMonsterProgress(
        WorldSession session,
        LegacyMonsterInstance monster,
        MonsterDamageResult result)
    {
        if (result.AlreadyDead)
        {
            return 0;
        }

        return checked((uint)AdjustLegacyMonsterExperience(
            checked((int)Math.Min(result.RequestedDamage, monster.MaxLife)),
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            monster.Level));
    }

    private static uint CalculateLegacyStraightFistSoulDamage(uint damage, byte useSpellSoul)
    {
        _ = useSpellSoul;
        return damage;
    }
}
