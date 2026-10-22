using System.Net;

namespace WolfConquer.Core.Networking;

public sealed class ConquerTcpConnection
{
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _sendAsync;
    private readonly Func<CancellationToken, ValueTask>? _disconnectAsync;
    private Func<Task>? _transportCloseCleanup;

    public ConquerTcpConnection(
        EndPoint? localEndPoint,
        EndPoint? remoteEndPoint,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? sendAsync = null,
        Func<CancellationToken, ValueTask>? disconnectAsync = null,
        WolfConquer.Core.World.LegacyNativeGameClientRuntime? nativeGameClient = null)
    {
        LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
        _sendAsync = sendAsync;
        _disconnectAsync = disconnectAsync;
        NativeGameClient = nativeGameClient;
    }

    public EndPoint? LocalEndPoint { get; }

    public EndPoint? RemoteEndPoint { get; }

    // Only the actual native-game birth producer supplies this. No lazy transport/session inference.
    public WolfConquer.Core.World.LegacyNativeGameClientRuntime? NativeGameClient { get; }

    // Registered by the world owner, invoked only by the real TCP producer after
    // its frame pipeline exits. A canceled frame/update scope is not transport EOF.
    internal void RegisterTransportCloseCleanup(Func<Task> cleanup) =>
        Interlocked.CompareExchange(ref _transportCloseCleanup, cleanup, null);

    internal Task CompleteTransportCloseAsync() =>
        Volatile.Read(ref _transportCloseCleanup)?.Invoke() ?? Task.CompletedTask;

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        if (_sendAsync is null)
        {
            throw new InvalidOperationException("This TCP connection was not created with a send delegate.");
        }

        return _sendAsync(frame, cancellationToken);
    }

    public ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return _disconnectAsync?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;
    }
}
