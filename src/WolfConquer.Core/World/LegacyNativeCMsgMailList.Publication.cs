using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCMsgMailList
{
    private readonly unsafe struct MailOutputTarget
    {
        private readonly byte* allocation;
        private readonly byte* cursor;
        private readonly int size;

        internal MailOutputTarget(LegacyNativePacket target)
        {
            RequireLiveTarget(target);
            allocation = target.Memory;
            cursor = target.stream;
            size = target.Size;
        }

        internal void RequireCurrent(LegacyNativePacket target)
        {
            RequireLiveTarget(target);
            if (target.Memory != allocation || target.stream != cursor || target.Size != size)
                throw new InvalidOperationException("Mail output target changed during preparation.");
        }

        private static void RequireLiveTarget(LegacyNativePacket target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (target.Memory == null) throw new ObjectDisposedException(nameof(LegacyNativePacket));
            nuint origin = (nuint)target.Memory, position = (nuint)target.stream;
            if (target.Capacity < 4 || position < origin || position - origin > (nuint)target.Capacity ||
                target.Size < 0 || target.Size > target.Capacity)
                throw new InvalidOperationException("Mail output target metadata is outside its actual allocation.");
        }
    }

    private static unsafe void PublishMailPages(LegacyNativeGameClientRuntime client,
        LegacyNativePacket target, PreparedMailPages prepared, MailOutputTarget witness)
    {
        foreach (byte[] frame in prepared.Frames)
            if (frame.Length > target.Capacity || frame.Length > 1032)
                throw new InvalidOperationException("Prepared mail page exceeds its actual output capacity.");
        prepared.RequireCurrent(client);
        witness.RequireCurrent(target);
        if (!string.Equals(prepared.Seal, LegacyNativePacket.SealString, StringComparison.Ordinal))
            throw new InvalidOperationException("Mail framing seal changed before publication.");

        // All producer allocations, serialization and refusal checks finish
        // before this point. Caller-exclusive packet lifetime and cooperating
        // map writers are required; public raw mutations are not made atomic.
        foreach (byte[] frame in prepared.Frames)
        {
            frame.AsSpan().CopyTo(new Span<byte>(target.Memory, frame.Length));
            target.Size = frame.Length;
            target.stream = target.Memory + 4;
            // The real sender synchronously copies to its queue, but can filter
            // or swallow failures. Publication is not delivery acknowledgement.
            client.Send(target);
        }
    }
}
