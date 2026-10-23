using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
    private sealed class ObjectivePublication
    {
        internal readonly LegacyNativePacket Caller;
        private readonly IntPtr address;
        private readonly int capacity, oldSize, oldPosition;
        private readonly byte[] before, frame;

        internal unsafe ObjectivePublication(LegacyNativePacket caller, byte[] frame)
        {
            Caller = caller ?? throw new InvalidDataException("Quest objective caller packet is unavailable.");
            this.frame = frame;
            address = (IntPtr)caller.Memory; capacity = caller.Capacity;
            oldSize = caller.Size; oldPosition = caller.Position;
            if (address == IntPtr.Zero || capacity < 4 || frame.Length > capacity ||
                oldSize < 0 || oldSize > capacity || oldPosition < 0 || oldPosition > capacity)
                throw new InvalidDataException("Quest objective caller allocation/metadata is invalid.");
            before = new byte[capacity];
            Marshal.Copy(address, before, 0, capacity);
        }

        private unsafe bool HasAllocation() => address != IntPtr.Zero &&
            (IntPtr)Caller.Memory == address && Caller.Capacity == capacity && frame.Length <= capacity;

        internal unsafe bool HasOriginalCaller() => HasAllocation() && Caller.Size == oldSize &&
            Caller.Position == oldPosition && new ReadOnlySpan<byte>(Caller.Memory, capacity).SequenceEqual(before);

        internal unsafe bool HasPreparedCaller() => HasAllocation() && Caller.Size == frame.Length &&
            Caller.Position == 4 && new ReadOnlySpan<byte>(Caller.Memory, frame.Length).SequenceEqual(frame);

        internal void CopyCaller()
        {
            Marshal.Copy(frame, 0, address, frame.Length);
            Caller.Size = frame.Length;
            Caller.Seek(4);
        }
    }
}
