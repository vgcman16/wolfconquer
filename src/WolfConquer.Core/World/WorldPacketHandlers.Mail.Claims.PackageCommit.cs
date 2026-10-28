using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryCommitMailboxPackage(LegacyMailboxClaimBaseline baseline, LegacyMailboxModePacket packet,
        CancellationToken cancellationToken, out LegacyMailboxClaimPlan? delivery, out bool durable, out string reason)
    {
        delivery = null;
        durable = false;
        var admitted = false;
        var committed = false;
        LegacyMailboxClaimPlan? result = null;
        LegacyMailboxClaimPlan? rejection = null;
        var failure = MailboxClaimPrefix + "PackageOwnersUnresolved";
        var owner = baseline.Owner;
        if (owner.Context.MailboxPackages is not { } producer) { reason = failure; return false; }
        var gearPath = Path.Combine(producer.CombatGearRoot,
            owner.Expected.Character.Uid.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ini");
        var paths = baseline.Paths.All.Append(gearPath).ToArray();
        try
        {
            if (!producer.TryAdmitRequest(owner.Expected.AccountId, checked((uint)owner.Expected.Character.Uid), packet, out failure))
            { reason = failure; return false; }
            var success = owner.Context.Sessions!.TryCommitPreparedSession(owner.Context.Connection, owner.Expected, producer, () =>
                SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var selected = SelectMailboxPackageRows(baseline, packet);
                    if (!producer.TryInspectRecipeHistory(checked((uint)owner.Expected.AccountId),
                        checked((uint)owner.Expected.Character.Uid), selected, out admitted, out failure)) return null;
                    if (!TryPrepareMailboxPackage(baseline, packet, out var prepared, out failure))
                    {
                        if (!admitted && failure == MailboxClaimPrefix + "PackageCapacity")
                            rejection = PrepareMailboxPackageCapacityRejection(baseline);
                        return null;
                    }
                    if (!MailboxPackagePreparationCurrent(prepared) || !MailboxPackagePreimagesCurrent(prepared) ||
                        !ReferenceEquals(prepared.Updated.PlayerSpells, owner.Expected.PlayerSpells) ||
                        !ReferenceEquals(prepared.Updated.PlayerProficiencies, owner.Expected.PlayerProficiencies) ||
                        !string.Equals(prepared.Updated.UsersRoot, owner.Expected.UsersRoot, StringComparison.Ordinal) ||
                        !ReferenceEquals(prepared.Updated.NativeGameClient, owner.Expected.NativeGameClient) ||
                        prepared.Updated.AccountId != owner.Expected.AccountId ||
                        prepared.Updated.Character.Uid != owner.Expected.Character.Uid) return null;
                    var recipe = LegacyMailboxPackageRecipe.CreateForStacks(producer.Domain, checked((uint)owner.Expected.AccountId),
                        checked((uint)owner.Expected.Character.Uid), packet, prepared.Selected, prepared.Grants,
                        prepared.Stacks,
                        [baseline.Character.Bytes, baseline.ItemsBefore, baseline.Mail.CopyImage(), prepared.GearBefore],
                        prepared.CharacterAfter, prepared.GearAfter, prepared.MailAfter.Image);
                    LegacyItemUidReservationReceipt? receipt = null;
                    if (recipe.Count != 0)
                    {
                        var reserved = producer.TryReserveRecipe(recipe, out receipt, out var reservationAdmitted, out failure);
                        admitted |= reservationAdmitted;
                        if (!reserved) return null;
                    }
                    // Allocating work now owns a durable intent. No-allocation work
                    // is admitted by its real prepared record below, without a UID.
                    var plan = MaterializeMailboxPackage(prepared, packet, receipt);
                    if (!MailboxPackagePreparationCurrent(prepared) || !MailboxClaimPlanCurrent(plan) ||
                        !MailboxPackagePreimagesCurrent(prepared))
                    { failure = MailboxClaimRecoveryRequired + ":packagePreparedStateChanged"; return null; }
                    var itemsAfter = PlayerItemSnapshotLoader.ToLegacyBytes(
                        plan.ItemsAfter.WithOwnedItemsForPersistence(plan.Updated).PlayerItems);
                    if (!TryPreparePackageStackWork(producer, recipe, receipt,
                        [
                            new(LegacyTradeJournalFileKind.Character, owner.Expected.AccountId, baseline.Character.Bytes, prepared.CharacterAfter),
                            new(LegacyTradeJournalFileKind.Items, owner.Expected.Character.Uid, baseline.ItemsBefore, itemsAfter),
                            new(LegacyTradeJournalFileKind.Mailbox, owner.Expected.Character.Uid, baseline.Mail.CopyImage(), prepared.MailAfter.Image),
                            new(LegacyTradeJournalFileKind.CombatGear, owner.Expected.Character.Uid, prepared.GearBefore, prepared.GearAfter),
                        ], ref admitted, out var work, out failure)) return null;
                    if (!work!.TryCommit(out var proof, out committed, out failure)) return null;
                    if (!PublishMailboxPackageNative(prepared, proof!))
                    { failure = MailboxClaimRecoveryRequired + ":packageNativePublicationChanged"; return null; }
                    owner.Context.Mailboxes!.PublishClaims(baseline.Mail, prepared.MailAfter);
                    result = plan;
                    failure = string.Empty;
                    return plan.Updated;
                }));
            if (!success && admitted && !failure.StartsWith(MailboxClaimRecoveryRequired, StringComparison.Ordinal))
                failure = MailboxClaimRecoveryRequired + ":packageIntentRetained:" + failure;
            delivery = success ? result : !admitted && !committed ? rejection : null;
            durable = committed;
            reason = failure;
            return success;
        }
        catch (Exception error) when (IsMailboxClaimPreparationFailure(error))
        {
            durable = committed;
            reason = (admitted || committed ? MailboxClaimRecoveryRequired + ":packagePreparedFault:" :
                MailboxClaimPrefix + "PackagePreparation:") + error.GetType().Name;
            return false;
        }
    }

    private static LegacyMailboxEntry[] SelectMailboxPackageRows(LegacyMailboxClaimBaseline baseline,
        LegacyMailboxModePacket packet)
    {
        uint[] ids = packet.Type == LegacyMailboxModeType.ClaimAll
            ? packet.Items.Where(id => id <= uint.MaxValue).Select(id => (uint)id).ToArray() : [packet.Id];
        return ids.Select(id => baseline.Mail.FindLive(id, baseline.Mail.Now)).OfType<LegacyMailboxEntry>().ToArray();
    }

    private static bool PublishMailboxPackageNative(MailboxPackagePrepared prepared, LegacyMailboxPackageCommitProof proof) =>
        (prepared.ClassPublication?.TryPublishCommitted() ?? true) &&
        (prepared.NinjaPublication?.TryPublishCommitted() ?? true) &&
        (prepared.PiratePublication?.TryPublishCommitted(proof) ?? true) &&
        (prepared.ClassPublication?.IsPublishedCurrent() ?? true) &&
        (prepared.NinjaPublication?.IsPublishedCurrent() ?? true) &&
        (prepared.PiratePublication?.IsPublishedCurrent() ?? true);
}
