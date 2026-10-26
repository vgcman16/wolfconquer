using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record MailboxPackageStep(LegacyMailboxEntry Original, LegacyMailboxEntry After,
        bool GrantsPackage, uint ClassAfter, uint NinjaAfter, uint PirateAccumulated);

    private sealed record MailboxPackagePrepared(LegacyMailboxClaimBaseline Baseline,
        LegacyMailboxPackageProducer Producer, LegacyMailboxEntry[] Selected, MailboxPackageStep[] Steps,
        LegacyMailboxPackageItemRecipe[] Grants, LegacyMailboxPackageCatalog[] Catalogs,
        LegacyMailboxPackageStackPlan Stacks,
        WorldSession Updated, LegacyMailboxStore.ClaimProjection MailAfter,
        string GearPath, byte[] GearBefore, byte[] GearAfter, byte[] CharacterAfter,
        LegacyNativePlayerRuntime.MailboxClassExperienceProjection? ClassPublication,
        LegacyNativeNinja.MailboxPointsProjection? NinjaPublication,
        LegacyNativeMyCombatGear.MailboxPirateRewardProjection? PiratePublication,
        bool SendClassExperience);

    private sealed record MailboxPackagePublication(MailboxPackagePrepared Prepared)
    {
        internal bool CatalogsCurrent() => Prepared.Catalogs.All(catalog => catalog.IsCurrent());

        // Genuine native captures use concrete dictionary snapshots/default local
        // comparers and plain native fields. No replaceable catalog/list callbacks.
        internal bool IsPublishedCurrent() =>
            (Prepared.NinjaPublication?.IsPublishedCurrent() ?? true) &&
            (Prepared.PiratePublication?.IsPublishedCurrent() ?? true) &&
            (Prepared.ClassPublication?.IsPublishedCurrent() ?? true);
    }

    private static bool NeedsMailboxPackageJoin(LegacyMailboxClaimBaseline baseline,
        LegacyMailboxModePacket packet) =>
        packet.Type == LegacyMailboxModeType.ClaimRecordType &&
            baseline.Mail.FindLive(packet.Id, baseline.Mail.Now) is { Flag: not 0 } ||
        packet.Type == LegacyMailboxModeType.ClaimAll && packet.Items.Any(id =>
            id <= uint.MaxValue && baseline.Mail.FindLive((uint)id, baseline.Mail.Now) is { Flag: not 0 });
}
