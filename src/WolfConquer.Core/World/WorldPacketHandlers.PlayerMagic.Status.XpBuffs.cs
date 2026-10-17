using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyAccuracyExpiresAtUtcAttribute = "LegacyAccuracyExpiresAtUtc";
    internal const string RuntimeLegacyAccuracyRemoveOnDeadAttribute = "LegacyAccuracyRemoveOnDead";

    private const ushort LegacyAccuracyXpBuffSpellId = 1_015;
    private const ushort LegacyShieldXpBuffSpellId = 1_020;
    private const ushort LegacyFlyXpBuffSpellId = 8_002;
    private static async ValueTask<bool> HandleLegacyPlayerXpBuffAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            !TryGetLegacyAttachStatusXpBuff(spell.Id, out var definition))
        {
            return false;
        }

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

        if (spell.Id == LegacyShieldXpBuffSpellId)
            return await HandleLegacyFreshShieldXpBuffAsync(context, query, playerSpell, spell,
                resourceSession, cancellationToken).ConfigureAwait(false);

        if (!HasLegacyStatusFlag(resourceSession.Character, LegacyRuntimeUpdateTicker.XpListFlag))
        {
            await context.Log.WriteLineAsync(
                    $"WorldServer skipped legacy {definition.Name} XP buff " +
                    $"{FormatSession(context with { Session = resourceSession })} spellId={spell.Id}; " +
                    "XPList status flag is not active after source resource preparation.")
                .ConfigureAwait(false);
            return true;
        }

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var hadStatus = HasLegacyStatusFlag(resourceSession.Character, definition.StatusFlag);
        var statusSession = resourceSession;
        if (definition.ApplyStatusBeforeXpListRemoval)
        {
            statusSession = ApplyLegacyAttachStatusXpBuff(
                resourceSession,
                definition,
                spell.Duration,
                hadStatus,
                DateTimeOffset.UtcNow);
            context.Sessions?.Set(context.Connection, statusSession);
            if (!hadStatus)
            {
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        context with { Session = statusSession },
                        statusSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var withoutXpListSession = statusSession with
        {
            Character = WithoutLegacyStatusFlag(
                statusSession.Character,
                LegacyRuntimeUpdateTicker.XpListFlag),
        };
        context.Sessions?.Set(context.Connection, withoutXpListSession);
        var withoutXpListContext = context with { Session = withoutXpListSession };

        // Player.RemoveFlag(XPList) sends the XPList scalar before its status-vector update.
        await context.Connection.SendAsync(
                LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(casterUid, value: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendPlayerStatusFlagVectorToVisibleAsync(
                withoutXpListContext,
                withoutXpListSession,
                cancellationToken)
            .ConfigureAwait(false);

        var updatedSession = withoutXpListSession;
        if (!definition.ApplyStatusBeforeXpListRemoval)
        {
            updatedSession = ApplyLegacyAttachStatusXpBuff(
                withoutXpListSession,
                definition,
                spell.Duration,
                hadStatus,
                DateTimeOffset.UtcNow);
            context.Sessions?.Set(context.Connection, updatedSession);

            // AddFlag emits a second status vector. An already-active non-Fly flag is not refreshed.
            if (!hadStatus)
            {
                await SendPlayerStatusFlagVectorToVisibleAsync(
                        context with { Session = updatedSession },
                        updatedSession,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var updatedContext = context with { Session = updatedSession };
        var targetDamage = definition.AnimationUsesDuration ? spell.Duration : 0u;
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(
            casterUid,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            playerSpell.UseSpellSoul,
            [new LegacySpellAnimationTarget(casterUid, targetDamage, Hit: true, MsgAttackEffect.None)]);
        await SendPlayerSpellAnimationToVisibleAsync(
                updatedContext,
                updatedSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {definition.Name} XP buff " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} statusFlag={definition.StatusFlag} " +
                $"duration={spell.Duration} consumedXpList=True hadStatus={hadStatus} " +
                $"refreshedStatus={hadStatus && definition.RefreshWhenActive} " +
                $"animationTargetDamage={targetDamage}; " +
                $"source=AttachStatus.cs case {spell.Id} packetOrder=" +
                $"{DescribeLegacyAttachStatusXpBuffPacketOrder(definition, hadStatus)}.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyAttachStatusXpBuff(
        WorldSession session,
        LegacyAttachStatusXpBuffDefinition definition,
        uint durationSeconds,
        bool hadStatus,
        DateTimeOffset now)
    {
        var updatedCharacter = session.Character;
        if (!hadStatus)
        {
            updatedCharacter = WithLegacyStatusFlag(updatedCharacter, definition.StatusFlag);
        }

        if (!hadStatus || definition.RefreshWhenActive)
        {
            var expiresAt = now
                .ToUniversalTime()
                .AddSeconds(durationSeconds)
                .ToString("O", CultureInfo.InvariantCulture);
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                definition.ExpiresAtAttribute,
                expiresAt);
            updatedCharacter = WithCharacterAttribute(
                updatedCharacter,
                definition.RemoveOnDeadAttribute,
                bool.TrueString);
        }

        return session with { Character = updatedCharacter };
    }

    private static string DescribeLegacyAttachStatusXpBuffPacketOrder(
        LegacyAttachStatusXpBuffDefinition definition,
        bool hadStatus)
    {
        if (definition.ApplyStatusBeforeXpListRemoval)
        {
            return hadStatus
                ? "XPList,StatusWithoutXPList,SpellAnimation2239"
                : "StatusWithXpListAndBuff,XPList,StatusWithoutXPList,SpellAnimation2239";
        }

        return hadStatus
            ? "XPList,StatusWithoutXPList,SpellAnimation2239"
            : "XPList,StatusWithoutXPList,StatusWithBuff,SpellAnimation2239";
    }

    private static bool TryGetLegacyAttachStatusXpBuff(
        ushort spellId,
        out LegacyAttachStatusXpBuffDefinition definition)
    {
        var candidate = spellId switch
        {
            LegacyAccuracyXpBuffSpellId => new LegacyAttachStatusXpBuffDefinition(
                "Accuracy",
                LegacyStarOfAccuracyFlag,
                RuntimeLegacyAccuracyExpiresAtUtcAttribute,
                RuntimeLegacyAccuracyRemoveOnDeadAttribute,
                RefreshWhenActive: false,
                AnimationUsesDuration: false,
                ApplyStatusBeforeXpListRemoval: false),
            LegacyShieldXpBuffSpellId => new LegacyAttachStatusXpBuffDefinition(
                "Shield",
                LegacyMagicShieldFlag,
                RuntimeLegacyMagicShieldExpiresAtUtcAttribute,
                RuntimeLegacyMagicShieldRemoveOnDeadAttribute,
                RefreshWhenActive: false,
                AnimationUsesDuration: true,
                ApplyStatusBeforeXpListRemoval: false),
            LegacyFlyXpBuffSpellId => new LegacyAttachStatusXpBuffDefinition(
                "Fly",
                LegacyFlyFlag,
                RuntimeLegacyFlyExpiresAtUtcAttribute,
                RuntimeLegacyFlyRemoveOnDeadAttribute,
                RefreshWhenActive: true,
                AnimationUsesDuration: true,
                ApplyStatusBeforeXpListRemoval: true),
            _ => default,
        };
        if (candidate is null)
        {
            definition = null!;
            return false;
        }

        definition = candidate;
        return true;
    }

    private sealed record LegacyAttachStatusXpBuffDefinition(
        string Name,
        int StatusFlag,
        string ExpiresAtAttribute,
        string RemoveOnDeadAttribute,
        bool RefreshWhenActive,
        bool AnimationUsesDuration,
        bool ApplyStatusBeforeXpListRemoval);
}
