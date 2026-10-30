namespace WolfConquer.Core.Networking;

internal partial class LegacyNativePacket
{
    // Controlled incremental codecs write an entire unframed segment before
    // publishing it. The callback must write sequential initialized bytes.
    internal void PrepareAppend(Action<LegacyNativePacket> prepareBody) => PrepareSegment(null, prepareBody);

    internal void PrepareWriteAt(int offset, Action<LegacyNativePacket> prepareBody) => PrepareSegment(offset, prepareBody);

    private unsafe void PrepareSegment(int? offset, Action<LegacyNativePacket> prepareBody)
    {
        ArgumentNullException.ThrowIfNull(prepareBody);
        int initialPosition = ValidatePhysicalWrite(0);
        if (Size < 0 || Size > Capacity)
            throw new InvalidOperationException("Packet target Size is outside its allocation.");
        int destination = offset ?? initialPosition;
        if (destination < 0 || destination > Capacity)
            throw new ArgumentOutOfRangeException(nameof(offset));
        byte* allocation = Memory;
        byte* cursor = stream;
        int size = Size;

        // Explicit destinations use their own remaining extent, never the old
        // cursor's remaining space. Empty appends can still prepare at EOF.
        using var scratch = new LegacyNativePacket(Math.Max(1, Capacity - destination));
        prepareBody(scratch);
        int count = scratch.ValidatePhysicalWrite(0);
        if (scratch.Size != 0)
            throw new InvalidOperationException("Prepared segment must remain unframed.");
        byte[] prepared = new ReadOnlySpan<byte>(scratch.Memory, count).ToArray();

        ValidatePhysicalWrite(0);
        if (Memory != allocation || stream != cursor || Size != size)
            throw new InvalidOperationException("Packet target changed during segment preparation.");
        if (prepared.Length > Capacity - destination)
            throw new InvalidOperationException("Prepared segment exceeds the target allocation.");

        // All callbacks, allocation and ordinary validation precede mutation.
        // This is exclusive-caller failure safety, not a shared lifetime gate.
        prepared.AsSpan().CopyTo(new Span<byte>(allocation + destination, prepared.Length));
        stream = allocation + destination + prepared.Length;
    }
}
