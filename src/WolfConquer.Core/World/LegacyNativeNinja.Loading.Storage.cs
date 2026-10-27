using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed partial class PreparedNinjaSequence
    {
        private sealed class PacketGroup : IDisposable
        {
            private readonly int maximum;
            private readonly byte[] first;
            private LegacyNativeRecycledPacket? rental;
            private LegacyNativePacket? expected;
            private nint address;
            private byte[] current;
            internal LegacyNativePacket Packet => rental?.GetStream() ?? throw UnavailableRanking();

            internal PacketGroup(int maximum, byte[] first)
            { this.maximum = maximum; this.first = first; current = first; }

            internal void Rent()
            {
                if (rental is not null) throw UnavailableRanking();
                try { rental = new(); }
                catch (NullReferenceException error)
                { throw new InvalidOperationException("Ninja loading requires real packet storage.", error); }
                expected = rental.GetStream();
                LegacyNativePreparedPacketStorage.Validate(expected, maximum);
                address = LegacyNativePreparedPacketStorage.Address(expected);
            }

            internal void Initialize() => Write(first);

            internal void Write(byte[] frame)
            {
                var packet = Packet;
                if (!ReferenceEquals(packet, expected) || LegacyNativePreparedPacketStorage.Address(packet) != address)
                    throw UnavailableRanking();
                LegacyNativePreparedPacketStorage.Write(packet, frame);
                current = frame;
                RequireCurrent();
            }

            internal void RequireCurrent()
            {
                if (!ReferenceEquals(Packet, expected) || !LegacyNativePreparedPacketStorage.Matches(Packet, current, address))
                    throw UnavailableRanking();
            }

            public void Dispose()
            {
                var owned = rental; rental = null;
                if (owned is null) return;
                try { owned.Dispose(); }
                catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("Ninja sequence packet cleanup", error); }
            }
        }
    }
}
