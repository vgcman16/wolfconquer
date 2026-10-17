using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerAddManaMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell runtimeSpell,
        CancellationToken cancellationToken)
    {
        if (context.Session is not { } session ||
            context.WorldData is not { } worldData ||
            !LegacyMagicSupportRuntime.TryResolveSupportSpell(
                worldData,
                runtimeSpell.Id,
                playerSpell.Level,
                out var supportSpell))
        {
            return false;
        }

        var support = LegacyMagicSupportRuntime.Classify(supportSpell);
        if (!support.Supported || support.Kind != LegacyMagicSupportKind.ManaRestore)
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                runtimeSpell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var targetIsCaster = query.OpponentUid == casterUid;
        var restoredMana = 0U;
        var updatedSession = resourceSession;
        if (targetIsCaster)
        {
            var currentMana = CheckedUInt32(resourceSession.Character.MinMana, nameof(resourceSession.Character.MinMana));
            var maximumMana = CheckedUInt32(resourceSession.Character.MaxMana, nameof(resourceSession.Character.MaxMana));
            var missingMana = maximumMana > currentMana ? maximumMana - currentMana : 0;
            restoredMana = Math.Min(supportSpell.Power, missingMana);
            if (restoredMana > 0)
            {
                var newMana = checked(currentMana + restoredMana);
                updatedSession = resourceSession with
                {
                    Character = resourceSession.Character with
                    {
                        MinMana = newMana,
                    },
                };
                context.Sessions?.Set(context.Connection, updatedSession);
                await SendScalarUpdateAsync(
                        context with { Session = updatedSession },
                        updatedSession.Character,
                        LegacyMsgUpdateDataType.Mana,
                        newMana,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var animationTargets = new List<LegacySpellAnimationTarget>();
        if (targetIsCaster)
        {
            animationTargets.Add(new LegacySpellAnimationTarget(
                casterUid,
                restoredMana,
                Hit: true,
                MsgAttackEffect.None));
        }

        var updatedContext = context with { Session = updatedSession };
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            runtimeSpell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            animationTargets);
        await SendPlayerSpellAnimationToVisibleAsync(updatedContext, updatedSession, animation, cancellationToken)
            .ConfigureAwait(false);

        updatedSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                runtimeSpell,
                supportSpell.Power,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy AddMana magic " +
                $"{FormatSession(updatedContext)} spellId={runtimeSpell.Id} restoredMana={restoredMana}.")
            .ConfigureAwait(false);
        return true;
    }
}
