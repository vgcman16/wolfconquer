using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> ExecuteLegacyNativeRiseOfTaoismQueryCastAsync(
        WorldPacketHandlerContext context, MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell, LegacyMagicRuntimeSpell spell,
        WorldSession session, CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        if (context.Sessions is not { } world) return true;
        object identity;
        WorldSession current;
        if (HasPreparedLegacyRootFirstFamilyInvocation(context, session, spell))
        {
            // This exact root invocation already ran native CanUse after its manual prefix.
            if (context.RootFirstFamilyInvocation is not
                    { NativeCanUseEntered: true, NativeClientIdentity: { } originalIdentity } ||
                !LegacyNativeClientPublication.TryCurrent(
                    world, context.Connection, uid, originalIdentity, out current))
                return true;
            identity = originalIdentity;
        }
        else
        {
            if (!LegacyNativeClientPublication.TryCaptureIdentity(
                    world, context.Connection, uid, out identity)) return true;
            var prepared = await TryPrepareLegacyRootFirstFamilyCanUseAsync(
                context, session, spell, cancellationToken, identity).ConfigureAwait(false);
            if (prepared is null || !LegacyNativeClientPublication.TryCurrent(
                    world, context.Connection, uid, identity, out current))
                return true;
        }

        var casterUid = CheckedUInt32(uid, nameof(uid));
        await context.Connection.SendAsync(new MsgAttackPacket(query with { Uid = casterUid }).ToFrameBytes(),
            cancellationToken).ConfigureAwait(false);
        if (!LegacyNativeClientPublication.TryCurrent(world, context.Connection, uid, identity, out current))
            return true;
        var now = DateTimeOffset.UtcNow;
        if (!LegacyNativeClientPublication.TryUpdate(world, context.Connection, uid, identity,
                owner => ApplyLegacyRiseOfTaoismStatus(owner, spell, now), out current))
            return true;

        var vector = LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(
            casterUid, CreateLegacyRuntimeStatusFlagVector(current.Character)).ToFrameBytes();
        var published = await LegacyNativeClientPublication.SendOwnerAndViewAsync(
            world, context.Connection, uid, identity, vector,
            (subject, viewer) => CanSeePlayer(viewer, subject), cancellationToken).ConfigureAwait(false);
        if (published is null) return true;
        current = published;
        var countdown = new LegacyMsgUpdatePacket(casterUid,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.FineRain, Value: 0,
                Flag: checked((uint)LegacyRiseOfTaoismFlag), Time: spell.Duration,
                Damage: LegacyRiseOfTaoismFineRainDamage, SpellLevel: 0)]).ToFrameBytes();
        published = await LegacyNativeClientPublication.SendOwnerAndViewAsync(
            world, context.Connection, uid, identity, countdown,
            (subject, viewer) => CanSeePlayer(viewer, subject), cancellationToken).ConfigureAwait(false);
        if (published is null) return true;
        current = published;

        var animation = new LegacySpellAnimationPacket(casterUid, casterUid, query.X, query.Y,
            spell.Id, (byte)Math.Min(playerSpell.Level, byte.MaxValue), NextSpell: 0,
            LevelHu: playerSpell.UseSpellSoul, SpellEffect: 0,
            [new LegacySpellAnimationTarget(casterUid, 0, Hit: true, MsgAttackEffect.None)]);
        published = await LegacyNativeClientPublication.SendOwnerAndViewAsync(
            world, context.Connection, uid, identity, animation.ToFrameBytes(),
            (subject, viewer) => CanSeePlayer(viewer, subject), cancellationToken).ConfigureAwait(false);
        if (published is null) return true;
        current = published;

        // Native AttachStatus15440: the REAL query follows the complete self-animation.
        // AddRuneAtributes owns HP+50000 then CURRENT cached MA+13000; never duplicate it here.
        var scope = new LegacyEquipmentQueryPublicationScope(uid) { OwnerNativeClientIdentity = identity };
        var rebuilt = await ExecuteLegacyEquipmentQueryAsync(context with { Session = current }, current,
            new LegacyEquipmentQueryRequest(LegacyEquipmentRebuildReason.RiseOfTaoismChange, scope),
            cancellationToken).ConfigureAwait(false);
        if (!rebuilt.Completed || !LegacyNativeClientPublication.TryCurrent(
                world, context.Connection, uid, identity, out current))
            return true;
        await context.Log.WriteLineAsync(
            "WorldServer handled legacy RiseofTaoism AttachStatus magic " +
            $"{FormatSession(context with { Session = current })} spellId={spell.Id} spellLevel={playerSpell.Level} " +
            $"statusFlag={LegacyRiseOfTaoismFlag} duration={spell.Duration} " +
            $"riseofTaoismExtraMDamage={spell.Damage} fineRainDamage={LegacyRiseOfTaoismFineRainDamage}; " +
            "source=AttachStatus.cs case 15440.").ConfigureAwait(false);
        return true;
    }
}
