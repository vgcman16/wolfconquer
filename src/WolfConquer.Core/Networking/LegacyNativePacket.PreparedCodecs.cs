using System.Buffers.Binary;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    // These delegates compose controlled codecs on caller-exclusively owned
    // packets. They are not permission callbacks or a concurrent lifetime gate.
    internal unsafe T ReadPrepared<T>(Func<LegacyNativePacket, T> parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ValidatePhysicalRead(0);
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        try
        {
            T result = parser(this);
            ValidatePhysicalRead(0);
            if (Memory != allocation || Size != size)
                throw new InvalidOperationException("Packet input changed during codec preparation.");
            return result;
        }
        catch
        {
            // Never resurrect a freed/replaced allocation. Ordinary controlled
            // readers only consume the cursor; restore captured metadata too.
            if (!IsDisposed && Memory == allocation)
            {
                stream = cursor;
                Size = size;
            }
            throw;
        }
    }

    internal unsafe void PrepareFrame(ushort opcode, Action<LegacyNativePacket> prepareBody)
    {
        ArgumentNullException.ThrowIfNull(prepareBody);
        ValidatePreparedTarget();
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;
        string? capturedSeal = seal;
        byte[] sealBytes = CapturePhysicalSeal(required: false);
        RequirePreparedSeal(capturedSeal);

        using var preparedPacket = new LegacyNativePacket(Capacity);
        preparedPacket.InitWriter();
        prepareBody(preparedPacket);
        RequirePreparedSeal(capturedSeal);
        preparedPacket.Finalize(opcode);
        if (preparedPacket.ValidatePhysicalRead(0) != 4)
            throw new InvalidOperationException("Prepared packet has an invalid completed cursor.");
        byte[] prepared = new ReadOnlySpan<byte>(preparedPacket.Memory, preparedPacket.Size).ToArray();
        ValidatePreparedFrame(prepared, opcode, sealBytes);

        ValidatePreparedTarget();
        if (Memory != allocation || stream != cursor || Size != size)
            throw new InvalidOperationException("Packet target changed during codec preparation.");
        RequirePreparedSeal(capturedSeal);
        if (prepared.Length > Capacity)
            throw new InvalidOperationException("Prepared frame exceeds the target allocation.");

        // All callbacks, validation, allocations and encoding precede the first
        // target write. This is ordinary failure safety, not crash atomicity.
        prepared.AsSpan().CopyTo(new Span<byte>(allocation, prepared.Length));
        Size = prepared.Length;
        stream = allocation + 4;
    }

    private void ValidatePreparedTarget()
    {
        ValidatePhysicalWrite(0);
        if (Capacity < 4)
            throw new InvalidOperationException("Packet target has no room for a complete header.");
        if (Size < 0 || Size > Capacity)
            throw new InvalidOperationException("Packet target Size is outside its allocation.");
    }

    private static void RequirePreparedSeal(string? captured)
    {
        if (!string.Equals(seal, captured, StringComparison.Ordinal))
            throw new InvalidOperationException("Packet seal changed during codec preparation.");
    }

    private static void ValidatePreparedFrame(byte[] prepared, ushort opcode, byte[] sealBytes)
    {
        if (prepared.Length < 4)
            throw new InvalidOperationException("Prepared frame has no complete header.");
        int declared = BinaryPrimitives.ReadUInt16LittleEndian(prepared);
        if (declared < 4 || declared + sealBytes.Length != prepared.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(prepared.AsSpan(2)) != opcode ||
            !prepared.AsSpan(declared).SequenceEqual(sealBytes))
            throw new InvalidOperationException("Prepared frame does not match its captured framing state.");
    }
}
