using System.Net;
using System.Net.Sockets;

namespace WolfConquer.Updates.Hosting;

/// <summary>
/// Read-only loopback origin; remote downloads require a separately authorized HTTPS tunnel.
/// Health/HEAD metadata do not prove signed-release validity or downloaded-byte integrity.
/// </summary>
public sealed class ReadOnlyFeedHost : IAsyncDisposable
{
    public const string ErrorMessage = "Feed request could not be served.";
    private readonly TcpListener _listener;
    private readonly FeedFileStore _files;
    private readonly BlobVerificationCache _blobVerification;
    private readonly SemaphoreSlim _slots;
    private readonly CancellationTokenSource _stop;
    private readonly HashSet<Task> _connections = [];
    private int _disposed;
    public Uri BaseAddress { get; }
    public Task Completion { get; }
    internal long BlobHashPassCount => _blobVerification.HashPassCount;

    private ReadOnlyFeedHost(FeedHostOptions options, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(options.Port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Port, 65535);
        if (options.MaxConcurrentRequests is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(options));
        token.ThrowIfCancellationRequested();
        _files = new FeedFileStore(options);
        _blobVerification = new BlobVerificationCache();
        _slots = new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
        _stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        _listener = new TcpListener(IPAddress.Loopback, options.Port);
        try { _listener.Start(options.MaxConcurrentRequests); }
        catch { _slots.Dispose(); _stop.Dispose(); _blobVerification.Dispose(); throw new IOException("Loopback feed listener could not start."); }
        BaseAddress = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
        Completion = ListenAsync();
    }

    public static Task<ReadOnlyFeedHost> StartAsync(FeedHostOptions options, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ReadOnlyFeedHost(options, cancellationToken));

    private async Task ListenAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await _slots.WaitAsync(_stop.Token).ConfigureAwait(false);
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false); }
                catch { _slots.Release(); throw; }
                var request = ServeAndReleaseAsync(client);
                lock (_connections) _connections.Add(request);
                _ = request.ContinueWith(completed => { lock (_connections) _connections.Remove(completed); },
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (SocketException) when (_stop.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (_stop.IsCancellationRequested) { }
        finally
        {
            _stop.Cancel();
            _listener.Stop();
            Task[] remaining;
            lock (_connections) remaining = _connections.ToArray();
            await Task.WhenAll(remaining).ConfigureAwait(false);
        }
    }

    private async Task ServeAndReleaseAsync(TcpClient client)
    {
        using (client)
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
        {
            // A transfer is finite even if a peer keeps making very slow progress.
            deadline.CancelAfter(TimeSpan.FromMinutes(15));
            NetworkStream? stream = null;
            try
            {
                client.NoDelay = true;
                stream = client.GetStream();
                var request = await FeedHttpRequest.ReadAsync(stream, deadline.Token).ConfigureAwait(false);
                var head = request.Method == "HEAD";
                if (request.Method is not ("GET" or "HEAD"))
                    await FeedHttpResponse.ErrorAsync(stream, 405, false, deadline.Token).ConfigureAwait(false);
                else if (request.HasBody)
                    await FeedHttpResponse.ErrorAsync(stream, 400, head, deadline.Token).ConfigureAwait(false);
                else if (request.Target == "/health")
                    await FeedHttpResponse.HealthAsync(stream, head, deadline.Token).ConfigureAwait(false);
                else if (_files.Open(request.Target) is { } file)
                    await FeedHttpResponse.FileAsync(stream, request, file, _blobVerification, deadline.Token).ConfigureAwait(false);
                else await FeedHttpResponse.ErrorAsync(stream, 404, head, deadline.Token).ConfigureAwait(false);
            }
            catch (FeedRequestException error)
            {
                if (stream is not null)
                {
                    try { await FeedHttpResponse.ErrorAsync(stream, error.Status, false, deadline.Token).ConfigureAwait(false); }
                    catch (Exception exception) when (exception is IOException or OperationCanceledException or SocketException) { }
                }
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException or SocketException or ObjectDisposedException) { }
            finally { _slots.Release(); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        _listener.Stop();
        try { await Completion.ConfigureAwait(false); }
        finally { _slots.Dispose(); _stop.Dispose(); _blobVerification.Dispose(); }
    }
}
