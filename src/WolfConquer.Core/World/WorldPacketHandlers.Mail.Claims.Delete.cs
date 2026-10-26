namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsLegacyMailboxDeletion(LegacyMailboxModeType type) =>
        type is LegacyMailboxModeType.Delete or LegacyMailboxModeType.DeleteClaim;

    private static bool TryPrepareLegacyMailboxDeletion(LegacyMailboxClaimBaseline baseline,
        LegacyMailboxModePacket packet, out LegacyMailboxClaimPlan plan, out string reason)
    {
        plan = null!;
        reason = MailboxClaimPrefix + "DeletionInvalid";
        if (!IsLegacyMailboxDeletion(packet.Type)) return false;
        IReadOnlyList<ulong> requested = packet.Type == LegacyMailboxModeType.DeleteClaim ? packet.Items : [packet.Id];
        var now = baseline.Mail.Now;
        var ids = new HashSet<uint>();
        var removed = new List<LegacyMailboxEntry>();
        foreach (var rawId in requested)
        {
            // Preserve the managed unsigned-ID boundary; do not wrap a foreign ulong ID.
            if (rawId > uint.MaxValue || ids.Contains((uint)rawId)) continue;
            var row = baseline.Mail.FindLive((uint)rawId, now);
            if (row is null || !row.CanDelete) continue;
            ids.Add(row.Id);
            removed.Add(row);
        }
        var selected = removed.ToArray();
        var projection = baseline.Mail.ProjectDeletion(selected);
        var frames = new byte[selected.Length][];
        if (frames.Length != 0)
        {
            // Each successful first removal echoes the same complete original request.
            // Share its immutable encoding rather than allocate request-size squared bytes.
            Array.Fill(frames, packet.ToFrameBytes());
        }
        var expected = baseline.Owner.Expected;
        if (!baseline.Items.IsCurrent(expected.PlayerItems) || !baseline.Mail.IsCurrent()) return false;
        plan = new(baseline, expected, baseline.Items, projection, selected, frames, selected.Length != 0);
        reason = string.Empty;
        return true;
    }

    private static Task LogLegacyMailboxDeletionAsync(WorldPacketHandlerContext context,
        LegacyMailboxModePacket packet, LegacyMailboxClaimPlan delivery) =>
        packet.Type == LegacyMailboxModeType.Delete
            ? context.Log.WriteLineAsync($"WorldServer handled LegacyMailboxMode type={packet.Type} id={packet.Id} " +
                $"removed={delivery.Changed} ack={delivery.Frames.Length != 0} committed={delivery.Changed}.")
            : context.Log.WriteLineAsync($"WorldServer handled LegacyMailboxMode type={packet.Type} id={packet.Id} " +
                $"requested={packet.Items.Count} removed={delivery.Selected.Length} ackFrames={delivery.Frames.Length} " +
                $"committed={delivery.Changed}.");
}
