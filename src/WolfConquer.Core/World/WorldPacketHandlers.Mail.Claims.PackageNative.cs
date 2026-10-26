using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record MailboxPackageNative(
        LegacyNativePlayerRuntime.MailboxClassExperienceProjection? Class,
        LegacyNativeNinja.MailboxPointsProjection? Ninja,
        LegacyNativeMyCombatGear.MailboxPirateRewardProjection? Pirate, bool SendClassExperience);

    private static bool TryCaptureMailboxPackageNative(LegacyMailboxClaimOwner owner,
        IReadOnlyDictionary<string, string> savedFields, uint classBefore, uint classIncrement,
        uint ninjaPoints, uint piratePoints, uint pirateExp, CombatGearSnapshot gear, byte[] gearImage,
        out MailboxPackageNative native, out string reason)
    {
        native = null!;
        reason = MailboxClaimPrefix + "PackageNativeOwnerUnavailable";
        var expected = owner.Expected;
        var job = LegacyBossHarmRankPackagePolicy.GetJobFromClassCode(expected.Character.Class);
        if (owner.Client is null)
        {
            if (job is LegacyBossHarmRankPackageJob.Ninja or LegacyBossHarmRankPackageJob.Pirate) return false;
            native = new(null, null, null, true);
            return true;
        }
        var client = owner.Client;
        if (owner.Player is not { } player || player.UID != expected.Character.Uid || player.Class != expected.Character.Class ||
            !player.TryPrepareMailboxClassExperience(client, classBefore, classIncrement, out var classProjection)) return false;
        LegacyNativeNinja.MailboxPointsProjection? ninja = null;
        if (ninjaPoints != 0 && (client.MyNinja is not { } ownedNinja ||
            !savedFields.TryGetValue("MsgNinja", out var savedNinja) ||
            !expected.Character.Attributes.TryGetValue("MsgNinja", out var liveNinja) ||
            !string.Equals(savedNinja, liveNinja, StringComparison.Ordinal) ||
            !ownedNinja.TryPrepareMailboxPoints(client, savedNinja, ninjaPoints, out ninja))) return false;
        LegacyNativeMyCombatGear.MailboxPirateRewardProjection? pirate = null;
        if (piratePoints != 0 || pirateExp != 0)
        {
            if (client.CombatGear is not { } ownedGear) return false;
            pirate = ownedGear.CaptureMailboxPirateReward(client, gear, gearImage, piratePoints, pirateExp);
        }
        if (!classProjection!.IsCurrent() || ninja is not null && !ninja.IsCurrent() || pirate is not null && !pirate.IsCurrent())
            return false;
        native = new(classProjection, ninja, pirate, classProjection.SendsUpdate);
        reason = string.Empty;
        return true;
    }
}
