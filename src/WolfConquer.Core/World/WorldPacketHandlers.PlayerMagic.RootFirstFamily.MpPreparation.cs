namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyRootManualMpPreparation(
        uint Mana, byte[] ManaFrame, byte[] AnimationFrame);

    private static LegacyRootManualMpPreparation? PrepareLegacyRootManualMp(
        WorldPacketHandlerContext context, WorldSession session, ushort originalSpellId, ushort requiredMana)
    {
        var physical = LegacyPhysicalStatusOverlay.ForOwner(session);
        if ((physical.RightWeaponItemEffect ?? 0) != LegacyWeaponEffectMp) return null;

        // This genuine draw/projection is not a resource or authorization write.
        // A draw spent before a later preparation refusal is not rolled back.
        var current = CheckedUInt32(session.Character.MinMana, nameof(session.Character.MinMana));
        var maximum = physical.MaxMana ?? 0;
        var restored = maximum > current ? Math.Min(300U, maximum - current) : 0U;
        var mana = checked(current + restored);
        // Even a successful proc cannot rescue this known shortage. Reject it
        // through the normal resource check without spending a needless draw.
        if (mana < requiredMana ||
            (originalSpellId != LegacyWeaponEffectMpSpellId &&
             !CheckLegacyWeaponEffectRateRoll(session.Character, LegacyWeaponEffectMpSpellId, 50))) return null;
        var uid = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        var animation = LegacySpellAnimationPacket.ForPlayerMagic(uid,
            checked((ushort)session.Character.X), checked((ushort)session.Character.Y),
            LegacyWeaponEffectMpSpellId, 0, 0,
            [new LegacySpellAnimationTarget(uid, restored, true, MsgAttackEffect.None)]).ToFrameBytes();
        var manaFrame = new LegacyMsgUpdatePacket(uid,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, mana)]).ToFrameBytes();
        return new(mana, manaFrame, animation);
    }

    private static void ValidateLegacyRootKnownManualBounds(
        WorldPacketHandlerContext context, WorldSession session, DateTimeOffset now)
    {
        _ = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid));
        _ = checked((ushort)session.Character.X);
        _ = checked((ushort)session.Character.Y);
        var physical = LegacyPhysicalStatusOverlay.ForOwner(session);
        var provider = context.TimeProvider ?? TimeProvider.System;
        if ((physical.RingItemEffect ?? 0) != 0 && !HasLegacyStatusFlag(session.Character, LegacyStigmaFlag))
        {
            _ = now.AddSeconds(20);
            _ = LegacyArrowBladesFlagRegistration.LocalTime(provider, now).AddSeconds(20);
        }
        if ((physical.NecklaceItemEffect ?? 0) != 0 && !HasLegacyStatusFlag(session.Character, LegacyMagicShieldFlag))
        {
            _ = now.AddSeconds(15);
            _ = LegacyArrowBladesFlagRegistration.LocalTime(provider, now).AddSeconds(15);
        }
    }
}
