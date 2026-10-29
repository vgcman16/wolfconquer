using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> HandleLegacyPlayerOpenXpSkillMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        int statusFlag,
        string expiresAtAttribute,
        TimeSpan duration,
        string skillName,
        bool restoreStaminaToMaximum,
        CancellationToken cancellationToken)
    {
        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        if (!HasLegacyStatusFlag(resourceSession.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer interrupted legacy {skillName} XP activation after accepted admission " +
                    $"{FormatSession(context)} spellId={spell.Id}; XPList status flag is no longer active; " +
                    "accepted accounting retained, no refund or retry authorization.")
                .ConfigureAwait(false);
            return true;
        }

        if (restoreStaminaToMaximum)
        {
            if (!TryReadLegacyRuntimeStamina(resourceSession.Character, out var stamina))
            {
                await context.Log.WriteLineAsync(
                        $"WorldServer interrupted legacy {skillName} XP restoration after accepted admission: " +
                        "requiredStaminaInvalid; accepted accounting retained, no refund or retry authorization.")
                    .ConfigureAwait(false);
                return true;
            }
            var maximumStamina = HasLegacyFatalCrossHeavenBlessing(resourceSession.Character)
                ? LegacyFatalCrossHeavenBlessingStaminaThreshold
                : LegacyFatalCrossNormalStaminaThreshold;
            if (stamina < maximumStamina)
            {
                resourceSession = await ApplyLegacyStaminaToPlayerAsync(
                        context,
                        context.Connection,
                        resourceSession,
                        maximumStamina,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var hadStatus = HasLegacyStatusFlag(resourceSession.Character, statusFlag);
        var xpSkillNow = DateTimeOffset.UtcNow;
        var xpListSession = await ApplyLegacyOpenXpSkillAsync(
                context with { Session = resourceSession },
                resourceSession,
                statusFlag,
                expiresAtAttribute,
                xpSkillNow,
                cancellationToken,
                duration)
            .ConfigureAwait(false);

        var casterUid = CheckedUInt32(xpListSession.Character.Uid, nameof(xpListSession.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(
                context with { Session = xpListSession },
                xpListSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {skillName} XPList transition " +
                $"{FormatSession(context)} spellId={spell.Id} sort={(int)spell.Sort} " +
                $"consumedXpList=True opened{skillName}=True refreshed{skillName}={hadStatus} " +
                $"expiresAtUtc={xpListSession.Character.Attributes[expiresAtAttribute]}.")
            .ConfigureAwait(false);
        return true;
    }

}
