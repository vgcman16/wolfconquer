using System.Net;

namespace WolfConquer.Core.Networking;

public sealed class ConquerTcpConnection
{
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? _sendAsync;
    private readonly Func<CancellationToken, ValueTask>? _disconnectAsync;

    public ConquerTcpConnection(
        EndPoint? localEndPoint,
        EndPoint? remoteEndPoint,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>? sendAsync = null,
        Func<CancellationToken, ValueTask>? disconnectAsync = null)
    {
        LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
        _sendAsync = sendAsync;
        _disconnectAsync = disconnectAsync;
    }

    public EndPoint? LocalEndPoint { get; }

    public EndPoint? RemoteEndPoint { get; }

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
