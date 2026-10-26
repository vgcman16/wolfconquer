using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPrepareMailboxPackageGrants(WorldDataCatalog catalog, uint mailId, uint flag,
        long classCode, List<LegacyMailboxPackageItemRecipe> grants,
        Dictionary<uint, LegacyMailboxPackageCatalog> metadata,
        out uint classExperience, out uint ninjaPoints, out uint piratePoints, out uint pirateExperience,
        out string reason)
    {
        classExperience = ninjaPoints = piratePoints = pirateExperience = 0;
        reason = MailboxClaimPrefix + "PackageUnsupported";
        if (flag > 9 || !LegacyBossHarmRankPackagePolicy.TryGetPackage((int)flag, out var reward)) return false;
        var job = LegacyBossHarmRankPackagePolicy.GetJobFromClassCode(classCode);
        LegacyBossHarmRankPackagePolicy.TryGetJobReward(reward, job, out var jobReward);
        if (!AddMailboxPackageGrantPhase(catalog, mailId, 0, reward.Items.Take(4), grants, metadata, out reason) ||
            !AddMailboxPackageGrantPhase(catalog, mailId, 1, jobReward.Items ?? [], grants, metadata, out reason) ||
            !AddMailboxPackageGrantPhase(catalog, mailId, 2, reward.Items.Skip(4), grants, metadata, out reason)) return false;
        classExperience = reward.ClassExperience;
        ninjaPoints = jobReward.NinjaPoints;
        piratePoints = jobReward.PirateAirPowerPoints;
        pirateExperience = jobReward.PirateExperience;
        return true;
    }

    private static bool AddMailboxPackageGrantPhase(WorldDataCatalog catalog, uint mailId, byte phase,
        IEnumerable<LegacyBossHarmRankPackageItem> source, List<LegacyMailboxPackageItemRecipe> grants,
        Dictionary<uint, LegacyMailboxPackageCatalog> metadata, out string reason)
    {
        reason = MailboxClaimPrefix + "PackageCatalogInvalid";
        foreach (var grant in source)
        {
            if (grant.ItemId == 0 || grant.Quantity == 0 || grant.Plus > byte.MaxValue || grant.SocketProgress < 0) return false;
            if (!metadata.TryGetValue(grant.ItemId, out var entry))
            {
                if (!LegacyMailboxPackageCatalog.TryCapture(catalog, grant.ItemId, out entry, out reason)) return false;
                metadata.Add(grant.ItemId, entry!);
            }
            if (grants.Count >= 256 || grant.Quantity > ushort.MaxValue)
            { reason = MailboxClaimPrefix + "PackageRecipeBound"; return false; }
            grants.Add(new(mailId, phase, grant.ItemId, checked((ushort)grant.Quantity), entry!.Durability, checked((byte)grant.Plus),
                grant.Bound ? (byte)1 : (byte)0, checked((uint)grant.SocketProgress), entry.Digest));
        }
        reason = string.Empty;
        return true;
    }
}
