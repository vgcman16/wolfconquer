namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only the public HandlerProcess148 manual root path enters here; proc/runtime adapters stay separate.
    private static async ValueTask<WorldSession?> ApplyLegacyRootManualItemEffectsAsync(
        WorldPacketHandlerContext context, WorldSession session, ushort originalSpellId,
        CancellationToken cancellationToken,
        object? expectedNativeClientIdentity = null)
    {
        var uid = session.Character.Uid;
        bool Refresh() => TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session, expectedNativeClientIdentity);
        if (!Refresh()) return null;
        if ((LegacyPhysicalStatusOverlay.ForOwner(session).RightWeaponItemEffect ?? 0) == LegacyWeaponEffectMp &&
            (originalSpellId == LegacyWeaponEffectMpSpellId ||
                CheckLegacyWeaponEffectRateRoll(session.Character, LegacyWeaponEffectMpSpellId, 50)))
        {
            if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                    owner => owner with { LegacyRandomSpell = LegacyWeaponEffectMpSpellId },
                    expectedNativeClientIdentity)) return null;
            var currentMana = unchecked((ushort)session.Character.MinMana);
            var maximumMana = LegacyPhysicalStatusOverlay.ForOwner(session).MaxMana ?? 0;
            var restored = maximumMana > currentMana ? Math.Min(300U, maximumMana - currentMana) : 0U;
            // EffectMP builds XY before the setter; all later recipients receive this captured packet.
            var frame = LegacySpellAnimationPacket.ForPlayerMagic(CheckedUInt32(uid, nameof(uid)),
                checked((ushort)session.Character.X), checked((ushort)session.Character.Y), LegacyWeaponEffectMpSpellId, 0, 0,
                [new LegacySpellAnimationTarget(CheckedUInt32(uid, nameof(uid)), restored, true,
                    MsgAttackEffect.None)]).ToFrameBytes();
            var mana = unchecked((ushort)(currentMana + (ushort)restored));
            if (!TryCommitLegacyPlayerMagicPreparation(context, ref session,
                    owner => owner with { Character = owner.Character with { MinMana = mana } },
                    expectedNativeClientIdentity)) return null;
            // Runtime WorldSession is the existing post-FullLoading boundary, even when restored==0.
            await SendScalarUpdateAsync(context with { Session = session }, session.Character,
                LegacyMsgUpdateDataType.Mana, mana, cancellationToken).ConfigureAwait(false);
            if (!Refresh()) return null;
            var sent = await SendLegacyPreparationOwnedFrameAsync(
                context with { Session = session }, session, uid, frame, cancellationToken,
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
