using System.IO.Pipelines;

namespace WolfConquer.Core.Networking;

// Disposal only. The actual TCP producer supplies these exact resources; neither
// this object nor a retry authorizes authentication, SQL access or gameplay.
internal sealed class ConquerTcpClientRetirement
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private ConquerTcpConnection? connection;
    private PipeReader? reader;

    internal ConquerTcpClientRetirement(ConquerTcpConnection connection, PipeReader reader)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(reader);
        this.connection = connection; this.reader = reader;
    }

    internal bool HasPendingCleanup => Volatile.Read(ref connection) is not null || Volatile.Read(ref reader) is not null;

    internal async ValueTask<Exception?> TryCompleteAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            Exception? failure = null;
            if (connection is { } actualConnection)
            {
                try
                {
                    await actualConnection.CompleteTransportCloseAsync().ConfigureAwait(false);
                    connection = null;
                }
                catch (Exception error) { failure = error; }
            }
            if (reader is { } actualReader)
            {
                try
                {
                    await actualReader.CompleteAsync().ConfigureAwait(false);
                    reader = null;
                }
                catch (Exception error) { failure = Combine(failure, error); }
            }
            return failure;
        }
        finally { gate.Release(); }
    }

    internal static Exception? Combine(Exception? primary, Exception? cleanup) =>
        primary is null ? cleanup : cleanup is null ? primary : new AggregateException(primary, cleanup);
}
