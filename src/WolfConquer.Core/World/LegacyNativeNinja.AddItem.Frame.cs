using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed class PreparedRankingItemFrame : IDisposable
    {
        private LegacyNativeRecycledPacket? rental;
        private LegacyNativePacket? expectedPacket;
        private nint expectedAddress;
        private int expectedPosition;
        private byte[] bytes = [];

        internal static PreparedRankingItemFrame Create(uint uid, Item item)
        {
            var bytes = LegacyNativeMsgNinja.PrepareNinjaItemFrame(
                LegacyNativeMsgNinja.MsgNinjaItem.PrepareSingleItem(uid, item,
                    LegacyNativeMsgNinja.MsgNinjaItem.TypeID.Add));
            if (bytes.Length > 1032) throw UnavailableRanking();
            return new PreparedRankingItemFrame { bytes = bytes };
        }

        internal unsafe void RentStorage()
        {
            try
            {
                try { rental = new LegacyNativeRecycledPacket(); }
                catch (NullReferenceException error)
                { throw new InvalidOperationException("Ninja item publication requires real packet storage.", error); }
                var packet = rental.GetStream();
                // Capacity is checked by the shared writer before its first copy,
                // including all header and captured seal bytes.
                LegacyNativeMsgNinja.WritePreparedNinjaItem(packet, bytes);
                expectedPacket = packet;
                expectedAddress = (nint)packet.Memory;
                expectedPosition = packet.Position;
                RequireCurrent();
            }
            catch { Dispose(); throw; }
        }

        internal unsafe void RequireCurrent()
        {
            var packet = rental?.GetStream();
            if (packet is null || !ReferenceEquals(packet, expectedPacket) || packet.Memory == null ||
                (nint)packet.Memory != expectedAddress || packet.Position != expectedPosition || packet.Size != bytes.Length ||
                packet.Capacity < bytes.Length || !new ReadOnlySpan<byte>(packet.Memory, bytes.Length).SequenceEqual(bytes))
                throw UnavailableRanking();
        }

        internal void Publish(LegacyNativeGameClientRuntime client, RankingChange committed)
        {
            // State is already committed. The legacy Send API cannot confirm delivery.
            lock (client.SpellAdmissionSyncRoot)
            lock (committed.Player.SpellAdmissionSyncRoot)
            lock (committed.StateGate)
            {
                if (!committed.HasCurrentCommittedOwner()) return;
                try
                {
                    RequireCurrent();
                    if (committed.HasCurrentCommittedOwner()) client.Send(rental!.GetStream());
                }
                catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("Committed Ninja item publication", error); }
            }
        }

        public void Dispose()
        {
            var owned = rental;
            rental = null;
            if (owned is null) return;
            try { owned.Dispose(); }
            catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("Ninja item packet cleanup", error); }
        }
    }
}
