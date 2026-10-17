namespace WolfConquer.Core.World;

public static class LegacyEquipmentRebuildAdmission
{
    private const int FineRain1StatusFlag = 212;
    private const int FineRainGuardMilliseconds = 6000;

    public static LegacyEquipmentRebuildResult ApplyCurrent(
        WorldSession session,
        LegacyEquipmentRebuildReason reason,
        ILegacySignedMillisecondClock clock)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(clock);
        _ = reason;
        return ApplyCore(session, null, clock);
    }

    public static LegacyEquipmentRebuildResult ApplyEquipmentMutation(
        WorldSession before,
        WorldSession accepted,
        LegacyPhysicalStatusValues beforeValues,
        LegacyEquipmentRebuildReason reason,
        ILegacySignedMillisecondClock clock)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(clock);
        if (before.Character.Uid != accepted.Character.Uid)
        {
            throw new ArgumentException(
                "Equipment mutation admission requires the same original player UID.",
                nameof(accepted));
        }

        _ = reason;
        return ApplyCore(accepted, beforeValues, clock);
    }

    private static LegacyEquipmentRebuildResult ApplyCore(
        WorldSession session,
        LegacyPhysicalStatusValues? beforeValues,
        ILegacySignedMillisecondClock clock)
    {
        var overlay = LegacyPhysicalStatusOverlay.ForOwner(session);
        var admission = LegacyFineRainEquipmentAdmission.ForOwner(session);
        if (LegacyPlayerDeathState.HasStatusFlag(session.Character.Attributes, FineRain1StatusFlag))
        {
            var now = clock.GetMilliseconds();
            if (now < unchecked(admission.StampMilliseconds + FineRainGuardMilliseconds))
            {
                if (beforeValues is { } retained)
                {
                    overlay = overlay with
                    {
                        Breakthrough = retained.Breakthrough,
                        Counteraction = retained.Counteraction,
                    };
                }

                return new LegacyEquipmentRebuildResult(
                    session with
                    {
                        PhysicalStatusOverlay = overlay,
                        FineRainEquipmentAdmission = admission,
                    },
                    Admitted: false);
            }

            // Source QueryEquipment reads Time32.Now again when committing FineRaneSave.
            admission = admission with { StampMilliseconds = clock.GetMilliseconds() };
        }

        return new LegacyEquipmentRebuildResult(
            session with
            {
                PhysicalStatusOverlay = overlay with { Breakthrough = null, Counteraction = null },
                FineRainEquipmentAdmission = admission,
            },
            Admitted: true);
    }
}
