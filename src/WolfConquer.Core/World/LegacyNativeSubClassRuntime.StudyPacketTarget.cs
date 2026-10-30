using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    private sealed class StudyPacketTarget
    {
        internal readonly LegacyNativePacket Packet;
        private readonly nint memory;
        private int size, position;
        private readonly byte[] expectedBytes;

        internal unsafe StudyPacketTarget(LegacyNativePacket packet)
        {
            Packet = packet;
            if (packet.Memory == null) throw new ObjectDisposedException(nameof(packet));
            nuint origin = (nuint)packet.Memory, cursor = (nuint)packet.stream;
            if (cursor < origin || cursor - origin > (nuint)packet.Capacity ||
                packet.Size < 0 || packet.Size > packet.Capacity)
                throw new InvalidOperationException("Study packet metadata exceeds its physical allocation.");
            memory = (nint)packet.Memory;
            size = packet.Size;
            position = (int)(cursor - origin);
            expectedBytes = new byte[packet.Capacity];
            Marshal.Copy(memory, expectedBytes, 0, expectedBytes.Length);
        }

        internal unsafe void RequireStorage(int required = 0)
        {
            if (Packet.Memory == null || (nint)Packet.Memory != memory || required > Packet.Capacity)
                throw new InvalidOperationException("Study packet storage is absent, changed or too short.");
        }

        internal unsafe void RequireUnchanged()
        {
            RequireStorage();
            if (Packet.Size != size || (nint)Packet.stream != memory + position ||
                !new ReadOnlySpan<byte>(Packet.Memory, expectedBytes.Length).SequenceEqual(expectedBytes))
                throw new InvalidOperationException("Study caller packet changed while owned by this operation.");
        }

        internal void RestoreRentalCursor()
        {
            // Take/Reuse may seek an invalid caller alias; no payload was copied.
            RequireStorage();
            Packet.Size = size;
            Packet.Seek(position);
        }

        internal void Write(byte[] frame)
        {
            RequireStorage(frame.Length);
            RequireUnchanged();
            Marshal.Copy(frame, 0, memory, frame.Length);
            Packet.Size = frame.Length;
            Packet.Seek(4);
            frame.CopyTo(expectedBytes, 0);
            size = frame.Length;
            position = 4;
        }
    }
}
