namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static LegacyMailboxClaimPlan? PrepareMailboxPackageCapacityRejection(LegacyMailboxClaimBaseline baseline)
    {
        var owner = baseline.Owner;
        if (!baseline.Items.IsCurrent(owner.Expected.PlayerItems) || !baseline.Mail.IsCurrent() ||
            !MailboxClaimAdmissionCurrent(owner)) return null;
        // A full bag produces the established error only: no success echo,
        // reward projection, new UID intent or partial ClaimAll wallet change.
        return new(baseline, owner.Expected, baseline.Items,
            baseline.Mail.Project(new Dictionary<uint, LegacyMailboxEntry>()), [],
            [LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes()], false);
    }
}
