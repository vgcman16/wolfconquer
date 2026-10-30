using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    internal sealed class PipeItemPublicationException : InvalidOperationException
    {
        internal int SendCallsCompleted { get; }
        internal bool ItemStateCommitted => true;

        internal PipeItemPublicationException(int sendCallsCompleted, Exception inner)
            : base("Pipe item was committed; subsequent transport publication failed. Delivery is unknown.", inner)
            => SendCallsCompleted = sendCallsCompleted;
    }

    private readonly unsafe struct PipeItemTarget
    {
        private readonly byte* memory;
        private readonly byte* cursor;
        private readonly int size;

        internal PipeItemTarget(LegacyNativePacket target)
        {
            RequireLiveTarget(target);
            memory = target.Memory; cursor = target.stream; size = target.Size;
        }

        internal void RequireCurrent(LegacyNativePacket target)
        {
            RequireLiveTarget(target);
            if (target.Memory != memory || target.stream != cursor || target.Size != size)
                throw new InvalidOperationException("Pipe item caller target changed during preparation.");
        }

        internal void Publish(LegacyNativePacket target, byte[] prepared)
        {
            prepared.AsSpan().CopyTo(new Span<byte>(memory, prepared.Length));
            target.Size = prepared.Length;
            target.stream = memory + 4;
        }

        private static void RequireLiveTarget(LegacyNativePacket target)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (target.Memory == null) throw new ObjectDisposedException(nameof(LegacyNativePacket));
            nuint memory = (nuint)target.Memory, cursor = (nuint)target.stream;
            if (target.Capacity < 4 || cursor < memory || cursor - memory > (nuint)target.Capacity ||
                target.Size < 0 || target.Size > target.Capacity)
                throw new InvalidOperationException("Pipe item target metadata exceeds its actual allocation.");
        }
    }

    private static LegacyNativeSecuritySocket? CaptureItemPipe(LegacyNativePipeClientRuntime user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.Alive) return null;
        var socket = user.Socket ?? throw new InvalidOperationException("Active item pipe has no actual socket.");
        if (!socket.Alive) return null;
        if (!ReferenceEquals(socket.Client, user))
            throw new InvalidOperationException("Active item pipe socket belongs to a different client.");
        return socket;
    }

    private static bool ItemPipeIsCurrent(LegacyNativePipeClientRuntime user, LegacyNativeSecuritySocket socket)
    {
        if (!user.Alive) return false;
        if (!ReferenceEquals(user.Socket, socket))
            throw new InvalidOperationException("Actual item pipe socket changed during preparation.");
        if (!socket.Alive) return false;
        if (!ReferenceEquals(socket.Client, user))
            throw new InvalidOperationException("Actual item pipe socket client changed during preparation.");
        return true;
    }

    private static void SendCommittedPipeItem(LegacyNativePipeClientRuntime user,
        LegacyNativeSecuritySocket socket, PipeItemPlan plan)
    {
        int completed = 0;
        try
        {
            if (!ItemPipeIsCurrent(user, socket))
                throw new InvalidOperationException("Item pipe became inactive after commitment.");
            user.Send(plan.Item.Packet); completed++;
            if (plan.Extra is null) return;
            if (!ItemPipeIsCurrent(user, socket))
                throw new InvalidOperationException("Item pipe became inactive between committed outputs.");
            user.Send(plan.Extra.Packet);
        }
        catch (Exception exception) { throw new PipeItemPublicationException(completed, exception); }
        // Native Socket.Send can swallow/drop failures. Completed calls do not
        // establish queue acceptance, network delivery or a reversible commit.
    }
}
