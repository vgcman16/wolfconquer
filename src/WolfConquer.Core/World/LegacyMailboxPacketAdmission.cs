using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

internal static class LegacyMailboxPacketAdmission
{
    internal static bool TryRequireCurrent(LegacyMailboxPackageProducer producer, WorldSessionStore sessions,
        Networking.ConquerTcpConnection connection, PacketFrame frame, out string reason)
    {
        reason = "mailboxClaimRecoveryRequired:packetOwnerUnavailable";
        if (!ReferenceEquals(sessions.MailboxPackages, producer) ||
            !sessions.TryGet(connection, out var current) || current is null) return false;
        if (!producer.HasPendingAccountWork(current.AccountId, out var pending, out reason)) return false;
        if (!pending) return producer.TryRequireAccountWritable(current.AccountId, out reason);
        // Verification changes only the genuine connection's PIN admission;
        // the dedicated handler path retains exact saved state under its gate.
        if (LegacySecondaryPasswordPacket.TryParse(frame, out var secondary) && secondary.Action == 1)
        { reason = string.Empty; return true; }
        if (!LegacyMailboxModePacket.TryParse(frame, out var packet))
        { reason = "mailboxClaimRecoveryRequired:onlyOriginalPackageReplay"; return false; }
        return producer.TryAdmitRequest(current.AccountId, checked((uint)current.Character.Uid), packet, out reason);
    }
}
