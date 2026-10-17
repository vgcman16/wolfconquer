namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // Only the public HandlerProcess148 manual root path enters here; proc/runtime adapters stay separate.
    private static async ValueTask<WorldSession?> ApplyLegacyRootManualItemEffectsAsync(
        WorldPacketHandlerContext context, WorldSession session, ushort originalSpellId,
        CancellationToken cancellationToken)
    {
        var uid = session.Character.Uid;
        bool Refresh() => TryRefreshLegacyPlayerMagicResourceSession(context, uid, ref session);
        if (!Refresh()) return null;
        if ((LegacyPhysicalStatusOverlay.ForOwner(session).RightWeaponItemEffect ?? 0) == LegacyWeaponEffectMp &&
            (originalSpellId == LegacyWeaponEffectMpSpellId ||
                CheckLegacyWeaponEffectRateRoll(session.Character, LegacyWeaponEffectMpSpellId, 50)))
        {
            session = session with { LegacyRandomSpell = LegacyWeaponEffectMpSpellId };
            context.Sessions?.Set(context.Connection, session);
            var currentMana = unchecked((ushort)session.Character.MinMana);
            var maximumMana = LegacyPhysicalStatusOverlay.ForOwner(session).MaxMana ?? 0;
            var restored = maximumMana > currentMana ? Math.Min(300U, maximumMana - currentMana) : 0U;
            // EffectMP builds XY before the setter; all later recipients receive this captured packet.
            var frame = LegacySpellAnimationPacket.ForPlayerMagic(CheckedUInt32(uid, nameof(uid)),
                checked((ushort)session.Character.X), checked((ushort)session.Character.Y), LegacyWeaponEffectMpSpellId, 0, 0,
                [new LegacySpellAnimationTarget(CheckedUInt32(uid, nameof(uid)), restored, true,
                    MsgAttackEffect.None)]).ToFrameBytes();
            var mana = unchecked((ushort)(currentMana + (ushort)restored));
            session = session with { Character = session.Character with { MinMana = mana } };
            context.Sessions?.Set(context.Connection, session);
            // Runtime WorldSession is the existing post-FullLoading boundary, even when restored==0.
            await SendScalarUpdateAsync(context with { Session = session }, session.Character,
                LegacyMsgUpdateDataType.Mana, mana, cancellationToken).ConfigureAwait(false);
            if (!Refresh()) return null;
            var sent = await SendLegacyArrowBladesVisibleFrameAsync(
                context with { Session = session }, session, uid, frame, cancellationToken).ConfigureAwait(false);
            if (sent is null) return null;
            session = sent;
        }

        // The draw precedes presence admission, and ANY nonzero cached Ring/Neck effect qualifies.
        if ((LegacyPhysicalStatusOverlay.ForOwner(session).RingItemEffect ?? 0) != 0 &&
            CheckLegacyWeaponEffectRateRoll(session.Character, 1_095, 20) &&
            !HasLegacyStatusFlag(session.Character, LegacyStigmaFlag))
        {
            var sent = await ApplyLegacyRootManualEquipmentBuffAsync(context, session, uid,
                LegacyStigmaFlag, 1_095, 20, RuntimeLegacyStigmaRemoveOnDeadAttribute, cancellationToken).ConfigureAwait(false);
            if (sent is null) return null;
            session = sent;
        }
        if ((LegacyPhysicalStatusOverlay.ForOwner(session).NecklaceItemEffect ?? 0) != 0 &&
            CheckLegacyWeaponEffectRateRoll(session.Character, 1_020, 20) &&
            !HasLegacyStatusFlag(session.Character, LegacyMagicShieldFlag))
        {
            var sent = await ApplyLegacyRootManualEquipmentBuffAsync(context, session, uid,
                LegacyMagicShieldFlag, 1_020, 15, RuntimeLegacyMagicShieldRemoveOnDeadAttribute, cancellationToken).ConfigureAwait(false);
            if (sent is null) return null;
            session = sent;
        }
        return Refresh() ? session : null;
    }

    private static async ValueTask<WorldSession?> ApplyLegacyRootManualEquipmentBuffAsync(
        WorldPacketHandlerContext context, WorldSession session, long uid, int flag, ushort animationId,
        int seconds, string removeOnDeadAttribute, CancellationToken cancellationToken)
    {
        // Native constructs the animation BEFORE AddSpellFlag/vector; do not reread XY after its await.
        var frame = LegacySpellAnimationPacket.ForPlayerMagic(CheckedUInt32(uid, nameof(uid)),
            checked((ushort)session.Character.X), checked((ushort)session.Character.Y), animationId, 4, 0,
            [new LegacySpellAnimationTarget(CheckedUInt32(uid, nameof(uid)), 0, true,
                MsgAttackEffect.None)]).ToFrameBytes();
        var character = WithLegacySourceAddSpellFlag(session.Character, flag, removeOnDead: true);
        character = LegacyRootItemEffectFlagClock.Install(context.Sessions, context.Connection,
            character, flag, seconds, context.TimeProvider ?? TimeProvider.System);
        character = WithCharacterAttribute(character, removeOnDeadAttribute, bool.TrueString);
        session = session with { Character = character };
        context.Sessions?.Set(context.Connection, session);
        var inserted = await SendLegacyArrowBladesVectorAsync(
            context with { Session = session }, session, uid, cancellationToken).ConfigureAwait(false);
        if (inserted is null) return null;
        return await SendLegacyArrowBladesVisibleFrameAsync(
            context with { Session = inserted }, inserted, uid, frame, cancellationToken).ConfigureAwait(false);
    }
}
