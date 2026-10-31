using System.Net;

namespace WolfConquer.Core.Networking;

public sealed class ConquerTcpConnection
{
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _sendAsync;
    private readonly Func<CancellationToken, ValueTask>? _disconnectAsync;
    private Func<Task>? _transportCloseCleanup;
    private int _transportClosed;

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

    internal bool HasSendRoute => _sendAsync is not null;
    internal bool HasDisconnectRoute => _disconnectAsync is not null;

    // Only the actual native-game birth producer supplies this. No lazy transport/session inference.
    public WolfConquer.Core.World.LegacyNativeGameClientRuntime? NativeGameClient { get; }

    // Registered by the world owner, invoked only by the real TCP producer after
    // its frame pipeline exits. A canceled frame/update scope is not transport EOF.
    internal void RegisterTransportCloseCleanup(Func<Task> cleanup) => TryRegisterTransportCloseCleanup(cleanup);

    internal bool TryRegisterTransportCloseCleanup(Func<Task> cleanup)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        if (Volatile.Read(ref _transportClosed) != 0) return false;
        var previous = Interlocked.CompareExchange(ref _transportCloseCleanup, cleanup, null);
        return (previous is null || ReferenceEquals(previous, cleanup)) &&
            Volatile.Read(ref _transportClosed) == 0;
    }

    internal bool IsTransportCloseCleanupCurrent(Func<Task> cleanup) =>
        ReferenceEquals(Volatile.Read(ref _transportCloseCleanup), cleanup) &&
        Volatile.Read(ref _transportClosed) == 0;

    // Actual EOF plus exact installed ownership. This denies reference reuse;
    // it cannot establish a live connection or authorize gameplay.
    internal bool IsTransportCloseCleanupClosed(Func<Task> cleanup) =>
        cleanup is not null && Volatile.Read(ref _transportClosed) != 0 &&
        ReferenceEquals(Volatile.Read(ref _transportCloseCleanup), cleanup);

    internal Task CompleteTransportCloseAsync()
    {
        // Publish EOF before reading the callback. A registrar racing an empty
        // callback read cannot later claim that an already closed transport lives.
        Interlocked.Exchange(ref _transportClosed, 1);
        return Volatile.Read(ref _transportCloseCleanup)?.Invoke() ?? Task.CompletedTask;
    }

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
