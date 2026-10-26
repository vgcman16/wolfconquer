namespace WolfConquer.Core.Packets;

// Context-owned invocation surrounds the actual resolved delegate, including
// callers that use TryGetHandler instead of Registry.HandleAsync.
public interface IPacketHandlerInvocation<TContext>
{
    ValueTask InvokeHandlerAsync(PacketHandler<TContext> handler, PacketFrame frame,
        CancellationToken cancellationToken);
}
