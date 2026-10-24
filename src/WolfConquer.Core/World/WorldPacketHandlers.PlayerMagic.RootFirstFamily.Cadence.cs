namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPrepareLegacyRootCadence(
        WorldPacketHandlerContext context, WorldSession session, LegacyMagicRuntimeSpell spell,
        DateTimeOffset now, bool ignoreAttackStamp, out ushort effective, out string reason)
    {
        reason = string.Empty;
        var provider = context.TimeProvider ?? TimeProvider.System;
        var localNow = LegacyArrowBladesFlagRegistration.LocalTime(provider, now);
        var hasStamp = TryReadLegacyPlayerAttackStamp(session.Character, out var stamp);
        var localStamp = hasStamp ? LegacyArrowBladesFlagRegistration.LocalTime(provider, stamp) : DateTime.MinValue;
        var milliseconds = (int)LegacyMagicRuntime.ReadNativeCoolDown(context.WorldData!, spell);
        if (milliseconds == 0) milliseconds = 500;
        var special = spell.Id switch { 1_045 => 500, 1_115 => 800, 11_110 => 650, _ => 0 };
        if (special != 0 && localNow < localStamp.AddMilliseconds(special))
        {
            reason = $"attackStampRemainingMs={LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(
                stamp.ToUniversalTime().AddMilliseconds(special), now)}";
            effective = checked((ushort)milliseconds);
            return false;
        }
        if (!ignoreAttackStamp)
        {
            if (milliseconds is > 1_000 and < 3_000) milliseconds = 2_500;
            var cadence = spell.Id == 11_660 ? 800 : spell.Id == 11_610 ? 1_000 : milliseconds;
            if (spell.Id is not 1_045 and not 1_046 and not 11_005 &&
                localNow < localStamp.AddMilliseconds(cadence))
            {
                reason = $"attackStampRemainingMs={LegacyMagicRuntime.GetLegacyCooldownRemainingMilliseconds(
                    stamp.ToUniversalTime().AddMilliseconds(cadence), now)}";
                effective = checked((ushort)milliseconds);
                return false;
            }
        }
        effective = checked((ushort)milliseconds);
        return true;
    }
}
