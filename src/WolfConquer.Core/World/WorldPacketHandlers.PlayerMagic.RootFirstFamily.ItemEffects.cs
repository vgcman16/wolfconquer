namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only the public HandlerProcess148 manual root path enters here; proc/runtime adapters stay separate.
    private static async ValueTask<WorldSession?> ApplyLegacyRootManualItemEffectsAsync(
        WorldPacketHandlerContext context, WorldSession session, LegacyRootManualMpPreparation? mp,
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        var uid = session.Character.Uid;
        bool Refresh() => TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session, expectedNativeClientIdentity);
        if (!Refresh()) return null;
        if (mp is not null)
        {
            // Both the restored projection and final cost were committed by the
            // exact admission CAS. This source-order prefix is presentation only;
            // never overwrite a reentrant same-client edit with restored mana.
            await context.Connection.SendAsync(mp.ManaFrame, cancellationToken).ConfigureAwait(false);
            if (!Refresh()) return null;
            var sent = await SendLegacyPreparationOwnedFrameAsync(
                context with { Session = session }, session, uid, mp.AnimationFrame, cancellationToken,
                expectedNativeClientIdentity).ConfigureAwait(false);
            if (sent is null) return null;
            session = sent;
        }

        // The draw precedes presence admission, and ANY nonzero cached Ring/Neck effect qualifies.
        if ((LegacyPhysicalStatusOverlay.ForOwner(session).RingItemEffect ?? 0) != 0 &&
            CheckLegacyWeaponEffectRateRoll(session.Character, 1_095, 20) &&
            !HasLegacyStatusFlag(session.Character, LegacyStigmaFlag))
        {
            var sent = await ApplyLegacyRootManualEquipmentBuffAsync(context, session, uid,
                LegacyStigmaFlag, 1_095, 20, RuntimeLegacyStigmaRemoveOnDeadAttribute, cancellationToken, expectedNativeClientIdentity).ConfigureAwait(false);
            if (sent is null) return null;
            session = sent;
        }
        if ((LegacyPhysicalStatusOverlay.ForOwner(session).NecklaceItemEffect ?? 0) != 0 &&
            CheckLegacyWeaponEffectRateRoll(session.Character, 1_020, 20) &&
            !HasLegacyStatusFlag(session.Character, LegacyMagicShieldFlag))
        {
            var sent = await ApplyLegacyRootManualEquipmentBuffAsync(context, session, uid,
                LegacyMagicShieldFlag, 1_020, 15, RuntimeLegacyMagicShieldRemoveOnDeadAttribute, cancellationToken, expectedNativeClientIdentity).ConfigureAwait(false);
            if (sent is null) return null;
            session = sent;
        }
        return Refresh() ? session : null;
    }

    private static async ValueTask<WorldSession?> ApplyLegacyRootManualEquipmentBuffAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid, int flag, ushort animationId,
        int seconds, string removeOnDeadAttribute, CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        if (expectedNativeClientIdentity is not null &&
            !TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session, expectedNativeClientIdentity)) return null;
        // Native constructs the animation BEFORE AddSpellFlag/vector; do not reread XY after its await.
        var frame = LegacySpellAnimationPacket.ForPlayerMagic(CheckedUInt32(uid, nameof(uid)),
            checked((ushort)session.Character.X), checked((ushort)session.Character.Y), animationId, 4, 0,
            [new LegacySpellAnimationTarget(CheckedUInt32(uid, nameof(uid)), 0, true,
                MsgAttackEffect.None)]).ToFrameBytes();
        if (!TryCommitLegacyPlayerMagicPreparation(context, ref session, owner =>
        {
            var character = WithLegacySourceAddSpellFlag(owner.Character, flag, removeOnDead: true);
            character = LegacyRootItemEffectFlagClock.Install(context.Sessions, context.Connection,
                character, flag, seconds, context.TimeProvider ?? TimeProvider.System);
            return owner with { Character = WithCharacterAttribute(character, removeOnDeadAttribute, bool.TrueString) };
        }, expectedNativeClientIdentity)) return null;
        var inserted = expectedNativeClientIdentity is null
            ? await SendLegacyArrowBladesVectorAsync(
                context with { Session = session }, session, uid, cancellationToken).ConfigureAwait(false)
            : await SendLegacyPreparationOwnedFrameAsync(context with { Session = session }, session, uid,
                LegacyMsgUpdatePacket.CreateLegacyRuntimeStatusFlagVectorUpdate(CheckedUInt32(uid, nameof(uid)),
                    CreateLegacyRuntimeStatusFlagVector(session.Character)).ToFrameBytes(),
                cancellationToken, expectedNativeClientIdentity).ConfigureAwait(false);
        if (inserted is null) return null;
        return await SendLegacyPreparationOwnedFrameAsync(
            context with { Session = inserted }, inserted, uid, frame, cancellationToken,
            expectedNativeClientIdentity).ConfigureAwait(false);
    }

    private static async ValueTask<WorldSession?> SendLegacyPreparationOwnedFrameAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid, byte[] frame,
        CancellationToken cancellationToken, object? expectedNativeClientIdentity)
    {
        if (expectedNativeClientIdentity is null)
            return await SendLegacyArrowBladesVisibleFrameAsync(
                context, session, uid, frame, cancellationToken).ConfigureAwait(false);
        if (context.Sessions is not { } world) return null;
        return await LegacyNativeClientPublication.SendOwnerAndViewAsync(world, context.Connection, uid,
            expectedNativeClientIdentity, frame, (subject, viewer) => CanSeePlayer(viewer, subject),
            cancellationToken).ConfigureAwait(false);
    }
}
