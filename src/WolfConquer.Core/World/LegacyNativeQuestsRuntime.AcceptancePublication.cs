using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
    private sealed class QuestAcceptancePublication : IDisposable
    {
        internal readonly LegacyNativePacket Packet;
        private readonly byte[] frame;
        private readonly IntPtr address;
        private readonly int capacity;

        internal unsafe QuestAcceptancePublication(byte[] frame)
        {
            this.frame = frame;
            if (frame.Length is < 4 or > 1032) throw new InvalidDataException("Quest acceptance frame is not sendable.");
            Packet = new LegacyNativePacket(frame.Length);
            address = (IntPtr)Packet.Memory; capacity = Packet.Capacity;
            try
            {
                if (address == IntPtr.Zero || capacity < frame.Length)
                    throw new InvalidDataException("Quest acceptance packet allocation is invalid.");
                Marshal.Copy(frame, 0, address, frame.Length);
                Packet.Size = frame.Length;
                Packet.Seek(4);
                if (!IsPrepared()) throw new InvalidDataException("Quest acceptance packet preparation changed.");
            }
            catch
            {
                Packet.Dispose();
                throw;
            }
        }

        internal unsafe bool IsPrepared() => address != IntPtr.Zero &&
            (IntPtr)Packet.Memory == address && Packet.Capacity == capacity &&
            capacity >= frame.Length && Packet.Size == frame.Length && Packet.Position == 4 &&
            new ReadOnlySpan<byte>(Packet.Memory, frame.Length).SequenceEqual(frame);

        public void Dispose() => Packet.Dispose();
    }
}
