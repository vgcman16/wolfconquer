using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyMailboxClaimPlan(LegacyMailboxClaimBaseline Baseline, WorldSession Updated,
        LegacyItemSaveGraph ItemsAfter, LegacyMailboxStore.ClaimProjection MailAfter,
        LegacyMailboxEntry[] Selected, byte[][] Frames, bool Changed)
    {
        internal MailboxPackagePublication? PackagePublication { get; init; }
        internal KeyValuePair<string, string>[] UpdatedFields { get; } = Updated.Character.Attributes.ToArray();
        internal bool HasAcknowledgement => Frames.Any(static frame =>
            PacketHeader.TryRead(frame, out var header) && header.Opcode == WorldPacketOpcodes.LegacyMailboxMode);
    }

    private static bool TryPrepareLegacyMailboxClaims(LegacyMailboxClaimBaseline baseline,
        LegacyMailboxModePacket packet, out LegacyMailboxClaimPlan plan, out string reason)
    {
        if (IsLegacyMailboxDeletion(packet.Type))
            return TryPrepareLegacyMailboxDeletion(baseline, packet, out plan, out reason);
        if (packet.Type == LegacyMailboxModeType.ClaimRecordType)
        {
            // The host has no live package UID producer or joint gear/Ninja reward
            // commit. A snapshot or available bag slot cannot establish those owners.
            plan = null!;
            var package = baseline.Mail.FindLive(packet.Id, baseline.Mail.Now);
            reason = package is null ? MailboxClaimPrefix + "MailUnavailable" :
                package.Flag == 0 ? MailboxClaimPrefix + "PackageEmpty" : MailboxClaimPrefix + "PackageOwnersUnresolved";
            return false;
        }
        plan = null!;
        reason = MailboxClaimPrefix + "PlanInvalid";
        var all = packet.Type == LegacyMailboxModeType.ClaimAll;
        var claimMoney = all || packet.Type == LegacyMailboxModeType.ClaimMoney;
        var claimItem = all || packet.Type == LegacyMailboxModeType.ClaimItem;
        if (!claimMoney && !claimItem) return false;
        uint[] ids = all ? packet.Items.Where(id => id <= uint.MaxValue).Select(id => (uint)id).ToArray() : [packet.Id];
        var now = baseline.Mail.Now;
        var selected = ids.Select(id => baseline.Mail.FindLive(id, now)).OfType<LegacyMailboxEntry>().ToArray();
        // The existing package path also needs CombatGear and a real new-item UID owner.
        // Refuse the complete action before applying its other rewards.
        if (all && selected.Any(mail => mail.Flag != 0))
        { reason = MailboxClaimPrefix + "PackageOwnersUnresolved"; return false; }
        var distinct = selected.DistinctBy(mail => mail.Id).ToArray();
        foreach (var mail in distinct)
            if (claimItem && mail.ItemId != 0 && !baseline.Items.TryGetWarehouse(LegacyMailboxWarehouseId, mail.ItemId, out _))
            { reason = MailboxClaimPrefix + "AttachmentMissing"; return false; }
        var expected = baseline.Owner.Expected;
        var replacements = new Dictionary<uint, LegacyMailboxEntry>();
        var frames = new List<byte[]>();
        var neededSlots = claimItem ? distinct.Count(mail => mail.ItemId != 0) : 0;
        if (neededSlots != 0 && neededSlots > LegacyInventorySlots - expected.PlayerItems.InventoryItems.Count)
        {
            frames.Add(LegacyMsgMessagePacket.SystemTalk("STR_BAG_FULL@@").ToFrameBytes());
            plan = new(baseline, expected, baseline.Items, baseline.Mail.Project(replacements), distinct, frames.ToArray(), false);
            reason = string.Empty;
            return true;
        }
        var updated = expected;
        foreach (var original in selected)
        {
            var mail = replacements.GetValueOrDefault(original.Id) ?? original;
            if (claimMoney) updated = PrepareMailboxClaimMoney(updated, mail, frames);
            var next = claimMoney ? mail.WithClaimedMoney() : mail;
            if (claimItem && mail.ItemId != 0)
            {
                if (!baseline.Items.TryGetWarehouse(LegacyMailboxWarehouseId, mail.ItemId, out var incoming)) return false;
                var item = incoming.CreateInventoryItem();
                updated = updated with
                { PlayerItems = MoveWarehouseItemToInventory(updated.PlayerItems, incoming.Source, item) };
                frames.Add(LegacyGameItemPacket.FromSnapshotItem(item).ToFrameBytes());
                next = next.WithClearedItemId();
            }
            replacements[mail.Id] = next;
            if (all) frames.Add(PrepareMailboxClaimRefresh(next, now, baseline.Owner.Context.ProtocolProfile));
        }
        if (all || selected.Length != 0) frames.Add(packet.ToFrameBytes());
        if (!LegacyItemSaveGraph.TryCapture(updated.PlayerItems, out var after) ||
            !baseline.Items.IsCurrent(expected.PlayerItems)) return false;
        var changed = distinct.Any(mail => claimItem && mail.ItemId != 0 ||
            claimMoney && (mail.Money != 0 || mail.EMoney != 0));
        plan = new(baseline, updated, after, baseline.Mail.Project(replacements), distinct, frames.ToArray(), changed);
        reason = string.Empty;
        return true;
    }

    private static WorldSession PrepareMailboxClaimMoney(WorldSession session, LegacyMailboxEntry mail, List<byte[]> frames)
    {
        var character = session.Character;
        var uid = checked((uint)character.Uid);
        if (mail.Money != 0)
        {
            var money = checked(character.Money + checked((long)mail.Money));
            character = WithCharacterAttribute(character with { Money = money }, "Money", checked((ulong)money));
            frames.Add(LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{mail.Money}@@").ToFrameBytes());
            frames.Add(new LegacyMsgUpdatePacket(uid, [new(LegacyMsgUpdateDataType.Money, checked((ulong)character.Money))]).ToFrameBytes());
        }
        if (mail.EMoney != 0)
        {
            var cp = checked(character.ConquerPoints + mail.EMoney);
            if (cp > uint.MaxValue) throw new OverflowException("Mailbox ConquerPoints exceeds native wallet range.");
            character = WithCharacterAttribute(character with { ConquerPoints = cp }, "ConquerPoints", checked((ulong)cp));
            frames.Add(LegacyMsgMessagePacket.SystemTalk($"You received {mail.EMoney} ConquerPoints.").ToFrameBytes());
            frames.Add(new LegacyMsgUpdatePacket(uid, [new(LegacyMsgUpdateDataType.ConquerPoints, checked((ulong)cp))]).ToFrameBytes());
        }
        return ReferenceEquals(character, session.Character) ? session : session with { Character = character };
    }

    private static byte[] PrepareMailboxClaimRefresh(LegacyMailboxEntry mail, DateTime now,
        ConquerProtocolProfile protocolProfile) =>
        new LegacyMailListPacket(LegacyMailListType.ClaimAllUpdate, 1,
            [LegacyMailListPacket.FromMailboxEntry(mail, now)]).ToFrameBytes(protocolProfile);
}
