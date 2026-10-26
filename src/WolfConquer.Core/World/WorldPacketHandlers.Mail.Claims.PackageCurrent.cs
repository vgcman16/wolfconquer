using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool MailboxPackagePreparationCurrent(MailboxPackagePrepared prepared)
    {
        var owner = prepared.Baseline.Owner;
        return MailboxClaimAdmissionCurrent(owner) && prepared.Baseline.Mail.IsCurrent() &&
            prepared.Baseline.Items.IsCurrent(owner.Expected.PlayerItems) &&
            prepared.Stacks.IsCurrent(owner.Expected.PlayerItems) &&
            prepared.Producer.IsCurrent(owner.Expected.UsersRoot, owner.Context.PlayerItemsRoot,
                owner.Context.Mailboxes!.ClaimRoot, owner.Context.TradeRecovery) &&
            prepared.Selected.All(mail => mail.IsLive(prepared.Baseline.Mail.Now)) &&
            prepared.Catalogs.All(catalog => catalog.IsCurrent()) &&
            MailboxPackageGearMatches(owner.Expected.CombatGear, prepared.GearBefore) &&
            (prepared.ClassPublication?.IsCurrent() ?? true) &&
            (prepared.NinjaPublication?.IsCurrent() ?? true) &&
            (prepared.PiratePublication?.IsCurrent() ?? true);
    }

    private static bool MailboxPackagePreimagesCurrent(MailboxPackagePrepared prepared) =>
        !LegacyTradeJournal.HasPending(prepared.Baseline.Paths.Users) &&
        ProveMailboxClaimFiles(prepared.Baseline.Paths, prepared.Baseline.Character.Bytes,
            prepared.Baseline.ItemsBefore, prepared.Baseline.Mail.CopyImage()) &&
        CharacterSnapshotFileBaseline.TryReadBytes(prepared.GearPath, MailboxClaimPrefix, out var gear, out _) &&
        gear.AsSpan().SequenceEqual(prepared.GearBefore);

    private static bool MailboxPackageGearMatches(CombatGearSnapshot? snapshot, byte[] bytes)
    {
        if (snapshot is null) return false;
        try { return Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(snapshot)).AsSpan().SequenceEqual(bytes); }
        catch (Exception error) when (IsMailboxClaimPreparationFailure(error)) { return false; }
    }
}
