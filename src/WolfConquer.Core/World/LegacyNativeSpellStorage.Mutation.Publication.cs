using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSpellStorage
{
    private sealed partial class PreparedSpellMutation
    {
        private byte[][] frames = [];
        private nint address;

        internal void PrepareFrames(LegacyNativePacket stream)
        {
            if (notices.Length == 0) return;
            var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
            frames = notices.Select(notice => notice.Added is { } spell
                ? LegacyNativeSpellPacket.PrepareSpellFrame(spell, seal)
                : LegacyNativeActionPacket.PrepareSpellRemovalFrame(uid, notice.Removed, seal)).ToArray();
            LegacyNativePreparedPacketStorage.Validate(stream, frames.Max(frame => frame.Length));
            address = LegacyNativePreparedPacketStorage.Address(stream);
        }

        internal void RequireStorage(LegacyNativePacket stream)
        {
            if (frames.Length == 0) return;
            LegacyNativePreparedPacketStorage.Validate(stream, frames.Max(frame => frame.Length));
            if (LegacyNativePreparedPacketStorage.Address(stream) != address) throw UnavailableNinjaSpells();
        }

        private bool HasCommittedOwner()
        {
            if (!owner.IsOwnedBy(client) || !ReferenceEquals(client.Player, player) ||
                !ReferenceEquals(player.Owner, client) || player.UID != uid ||
                !ReferenceEquals(client.Socket, socket) || client.Fake != fake ||
                !ReferenceEquals(owner.ClientSpells, candidate)) return false;
            try { RequireNinjaSpellRows(candidate, afterRows); return true; }
            catch (InvalidOperationException) { return false; }
        }

        internal void Publish(LegacyNativePacket stream)
        {
            foreach (var frame in frames)
            {
                if (!HasCommittedOwner()) return;
                // Any failure here is after a committed map, never rollback.
                RequireStorage(stream);
                LegacyNativePreparedPacketStorage.Write(stream, frame);
                if (!LegacyNativePreparedPacketStorage.Matches(stream, frame, address) || !HasCommittedOwner()) return;
                client.Send(stream);
            }
        }
    }
}
