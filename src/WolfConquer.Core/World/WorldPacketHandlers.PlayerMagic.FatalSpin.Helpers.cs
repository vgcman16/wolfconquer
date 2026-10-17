using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static uint CalculateLegacyFatalSpinMonsterProgressDamage(
        WorldSession session,
        LegacyPlayerMonsterMagicTarget target,
        uint requestedDamage)
    {
        if (requestedDamage == 0 ||
            LegacyMonsterDamageRules.IsLegacyGuardFamily(target.MonsterFamily))
        {
            return 0;
        }

        var adjustedDamage = AdjustLegacyMonsterExperience(
            checked((int)Math.Min(requestedDamage, target.Monster.MaxLife)),
            CheckedInt32(session.Character.Level, nameof(session.Character.Level)),
            target.Monster.Level);
        return checked((uint)Math.Max(0, adjustedDamage));
    }

    private static async ValueTask<WorldSession> ApplyLegacyFatalSpinIncreaseExperienceAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint progressDamage,
        CancellationToken cancellationToken)
    {
        if (progressDamage == 0)
        {
            return session;
        }

        var experienceResult = LegacyExperienceGainPolicy.CalculateAttackUpdate(
            CreateLegacyExperienceGainInputs(context, session, progressDamage));
        var updatedSession = ApplyLegacyExperienceSideEffects(
            context,
            context.Connection,
            session,
            experienceResult);
        if (experienceResult.AppliedExperienceGain == 0)
        {
            return updatedSession;
        }

        return await ApplyLegacyFinalExperienceGainAsync(
                context,
                context.Connection,
                updatedSession,
                experienceResult.AppliedExperienceGain,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static uint CalculateLegacyFatalSpinSoulDamage(uint damage, byte useSpellSoul)
    {
        // Matching Base.CalculateSoul currently returns physical damage unchanged.
        _ = useSpellSoul;
        return damage;
    }

    private static async ValueTask SendLegacyFatalSpinInteractionEchoAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        CancellationToken cancellationToken)
    {
        var frame = new MsgAttackPacket(query with
        {
            Uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
        }).ToFrameBytes();
        await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } sessions)
        {
            return;
        }

        foreach (var active in sessions.Snapshot())
        {
            if (ReferenceEquals(active.Connection, context.Connection) ||
                active.Session.Character.Uid == session.Character.Uid ||
                !CanSeePlayer(active.Session.Character, session.Character))
            {
                continue;
            }

            await active.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask SendLegacyFatalSpinAnimationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        ushort x,
        ushort y,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        IReadOnlyList<LegacySpellAnimationTarget> targets,
        CancellationToken cancellationToken)
    {
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            x,
            y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            targets);
        await SendPlayerSpellAnimationToVisibleAsync(context, session, animation, cancellationToken)
            .ConfigureAwait(false);
    }
}
