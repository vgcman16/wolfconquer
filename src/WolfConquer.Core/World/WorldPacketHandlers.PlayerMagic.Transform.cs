using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyTransformSpellProgressDamage = 300;
    private const string LegacyTransformBlockedMessage = "You cant use this skill right now !";

    private static async ValueTask<bool> HandleLegacyPlayerTransformMagicAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        CancellationToken cancellationToken)
    {
        if (!LegacyMagicSortCapabilities.IsSupportedTransformRuntimeSpell(spell.Sort))
        {
            return false;
        }

        if (HasLegacyStatusFlag(session.Character, LegacyFlyFlag) ||
            HasLegacyStatusFlag(session.Character, LegacyRideFlag))
        {
            await context.Connection.SendAsync(
                    LegacyMsgMessagePacket.SystemTopLeft(LegacyTransformBlockedMessage).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer blocked legacy Transform before resource verification " +
                    $"{FormatSession(context)} spellId={spell.Id}; flyOrRide=true.")
                .ConfigureAwait(false);
            return true;
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

        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var castSession = resourceSession;
        var castContext = context with { Session = castSession };
        var scope = new LegacyEquipmentQueryPublicationScope(casterUid);
        bool RefreshCaster()
        {
            var current = RefreshLegacyEquipmentQueryPublication(castContext, castSession, scope);
            if (!current.Completed) return false;
            castSession = current.Session!;
            castContext = context with { Session = castSession };
            return true;
        }
        var consumedXpList = HasLegacyStatusFlag(
            castSession.Character,
            LegacyRuntimeUpdateTicker.XpListFlag);
        if (consumedXpList)
        {
            castSession = castSession with
            {
                Character = WithoutLegacyStatusFlag(
                    castSession.Character,
                    LegacyRuntimeUpdateTicker.XpListFlag),
            };
            context.Sessions?.Set(context.Connection, castSession);
            castContext = context with { Session = castSession };

            await context.Connection.SendAsync(
                    LegacyMsgUpdatePacket.CreateLegacyRuntimeXPListUpdate(casterUid, value: 0).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!RefreshCaster()) return true;
            await SendPlayerStatusFlagVectorToVisibleAsync(
                    castContext,
                    castSession,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!RefreshCaster()) return true;
        TransformInfo? transform = null;
        if (context.WorldData is { } worldData &&
            worldData.TransformInfoByTransformIdAndLevel.TryGetValue(
                (spell.Id, spell.Level),
                out var candidate) &&
            candidate.Lookface > 0 &&
            candidate.MaxLife > 0)
        {
            transform = candidate;
            var now = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
            var previousMaximum = GetLegacyEquipmentRuntimeMaxHitpoints(castSession, context.WorldData);
            var transformedCharacter = LegacyTransformState.Apply(
                castSession.Character,
                checked((uint)candidate.Lookface),
                checked((uint)candidate.MaxLife),
                now.AddSeconds(spell.Duration)) with { MinHitPoints = castSession.Character.MinHitPoints };
            // CreateTransform sets metadata first; peer spawn observes PRIOR HP/Max.
            castSession = castSession with
            {
                Character = transformedCharacter,
                EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(casterUid, previousMaximum),
            };
            context.Sessions?.Set(context.Connection, castSession);
            castContext = context with { Session = castSession };
            var appearance = await SendLegacyCurrentPlayerSpawnToPeersAsync(
                    castContext, castSession, scope, cancellationToken)
                .ConfigureAwait(false);
            if (!appearance.Current.Completed) return true;
            castSession = appearance.Current.Session!;
            castContext = context with { Session = castSession };
            var updated = await UpdateLegacyClientTransformStatusAsync(
                    castContext, castSession, scope, checked((uint)candidate.MaxLife), cancellationToken)
                .ConfigureAwait(false);
            if (!updated.Completed) return true;
            castSession = updated.Session!;
            castContext = context with { Session = castSession };
        }

        var animation = new LegacySpellAnimationPacket(
            casterUid,
            AttackedUid: 0,
            query.X,
            query.Y,
            spell.Id,
            (byte)Math.Min(playerSpell.Level, byte.MaxValue),
            NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul,
            SpellEffect: 0,
            [
                new LegacySpellAnimationTarget(
                    casterUid,
                    spell.Duration,
                    Hit: true,
                    MsgAttackEffect.None),
            ]);
        await SendPlayerSpellAnimationToVisibleAsync(
                castContext,
                castSession,
                animation,
                cancellationToken)
            .ConfigureAwait(false);
        if (!RefreshCaster()) return true;

        castSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                castContext,
                castSession,
                playerSpell,
                spell,
                LegacyTransformSpellProgressDamage,
                cancellationToken)
            .ConfigureAwait(false);
        if (!RefreshCaster()) return true;

        await context.Log.WriteLineAsync(
                "WorldServer handled legacy Transform magic " +
                $"{FormatSession(castContext with { Session = castSession })} spellId={spell.Id} " +
                $"spellLevel={spell.Level} consumedXpList={consumedXpList} " +
                $"transformLookface={transform?.Lookface ?? 0} transformMaxLife={transform?.MaxLife ?? 0} " +
                $"durationSeconds={spell.Duration} progressDamage={LegacyTransformSpellProgressDamage}; " +
                "source=matching DecompiledSource Transform.cs and ClientTransform.cs.")
            .ConfigureAwait(false);
        return true;
    }
}
