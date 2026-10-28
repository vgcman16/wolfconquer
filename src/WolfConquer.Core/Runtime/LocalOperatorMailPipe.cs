using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

public sealed class LocalOperatorMailPipe : IAsyncDisposable
{
    public const int MaximumRequestBytes = 16 * 1024;
    public const int MaximumResponseBytes = 64 * 1024;
    private const string Prefix = "wolfconquer-operator-mail-";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 16,
        Converters = { new JsonStringEnumConverter<LegacyOperatorGearMailStatus>(allowIntegerValues: false) }
    };
    private readonly WorldServerFrameHandler handler;
    private readonly LegacyOwnedAdmissionRuntime owner;
    private readonly Guid domain;
    private readonly NamedPipeServerStream pipe;
    private readonly CancellationTokenSource stop;
    private readonly Task worker;
    private readonly object lifetimeGate = new();
    private Task? disposal;
    public string PipeName { get; }
    public Task Completion => worker;
    public string LastTransportStatus { get; private set; } = "waiting";

    private LocalOperatorMailPipe(WorldServerFrameHandler handler, LegacyOwnedAdmissionRuntime owner,
        Guid domain, CancellationToken cancellationToken)
    {
        this.handler = handler;
        this.owner = owner;
        this.domain = domain;
        PipeName = NameFor(domain);
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, owner.ShutdownToken);
        try
        {
            pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly | PipeOptions.FirstPipeInstance,
                MaximumRequestBytes + 4, MaximumResponseBytes + 4);
        }
        catch { stop.Dispose(); throw; }
        worker = RunAsync();
    }

    public static LocalOperatorMailPipe? StartIfEnabled(WorldServerFrameHandler handler,
        LegacyOwnedAdmissionRuntime owner, Guid configuredDomain, CancellationToken cancellationToken = default)
    {
        if (Environment.GetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL") != "1") return null;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Local operator mail requires Windows.");
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.IsStopping) throw new InvalidOperationException("The actual owned runtime is stopping.");
        if (!owner.TryGetOperatorGearMailConfiguration(handler, configuredDomain, out _, out _))
            throw new InvalidOperationException("Operator mail requires the current bound owner and domain.");
        return new LocalOperatorMailPipe(handler, owner, configuredDomain, cancellationToken);
    }

    public static string NameFor(Guid domain) => domain == Guid.Empty
        ? throw new ArgumentException("A genuine configured domain is required.", nameof(domain))
        : Prefix + domain.ToString("N");

    public static bool IsOperatorPipeName(string name) => name is not null && name.Length == Prefix.Length + 32 &&
        name.StartsWith(Prefix, StringComparison.Ordinal) &&
        Guid.TryParseExact(name.AsSpan(Prefix.Length), "N", out var domain) && domain != Guid.Empty &&
        string.Equals(name, NameFor(domain), StringComparison.Ordinal);

    public static LegacyOperatorGearMailRequest ParseRequest(ReadOnlyMemory<byte> body)
    {
        using var document = ParseDocument(body, MaximumRequestBytes);
        var request = document.RootElement.Deserialize<LegacyOperatorGearMailRequest>(JsonOptions)
            ?? throw new JsonException("requestObjectRequired");
        if (request.Items is null || request.Items.Count is < 1 or > 32 ||
            document.RootElement.GetProperty("items").ValueKind != JsonValueKind.Array ||
            document.RootElement.GetProperty("items").EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object))
            throw new JsonException("boundedItemObjectsRequired");
        return request;
    }

    public static LegacyOperatorGearMailResult ParseResult(ReadOnlyMemory<byte> body)
    {
        using var document = ParseDocument(body, MaximumResponseBytes);
        var result = document.RootElement.Deserialize<LegacyOperatorGearMailResult>(JsonOptions)
            ?? throw new JsonException("resultObjectRequired");
        if (result.MailIds is null || result.ItemUids is null || result.MailIds.Length > 32 || result.ItemUids.Length > 32)
            throw new JsonException("boundedResultRequired");
        return result;
    }

    public static string FormatResult(LegacyOperatorGearMailResult result) => JsonSerializer.Serialize(result, JsonOptions);

    private static JsonDocument ParseDocument(ReadOnlyMemory<byte> body, int maximum)
    {
        if (body.Length is 0 || body.Length > maximum) throw new JsonException("messageLengthInvalid");
        var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("objectRequired");
            RejectDuplicateProperties(document.RootElement);
            return document;
        }
        catch { document.Dispose(); throw; }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("duplicateProperty");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }

    private async Task RunAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            await pipe.WaitForConnectionAsync(stop.Token).ConfigureAwait(false);
            Exception? primary = null;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                try { await HandleAsync(deadline.Token).ConfigureAwait(false); }
                catch (PipePeerException) { LastTransportStatus = "peerIoStoppedOutcomeMayBeUnknown"; }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested)
                { LastTransportStatus = "deadlineOrShutdownOutcomeMayBeUnknown"; }
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                try { if (pipe.IsConnected) pipe.Disconnect(); }
                catch (Exception cleanup) when (primary is not null)
                { throw new AggregateException("Operator endpoint and disconnect both failed.", primary, cleanup); }
            }
        }
    }

    private async Task HandleAsync(CancellationToken cancellationToken)
    {
        LegacyOperatorGearMailRequest request;
        try { request = ParseRequest(await ReadAsync(cancellationToken).ConfigureAwait(false)); }
        catch (JsonException)
        {
            await WriteAsync(new(Guid.Empty, LegacyOperatorGearMailStatus.Rejected, "invalidTransportRequest", [], []),
                cancellationToken).ConfigureAwait(false);
            LastTransportStatus = "invalidRequestRejected";
            return;
        }
        if (request.ExpectedDomain != domain)
        {
            await WriteAsync(new(request.OperationId, LegacyOperatorGearMailStatus.Rejected, "pipeDomainMismatch", [], []),
                cancellationToken).ConfigureAwait(false);
            LastTransportStatus = "domainRejected";
            return;
        }
        // No delegate/saver/owner substitution: only the actual Frame entrypoint.
        var result = await handler.DeliverOperatorTestGearMailAsync(owner, request, cancellationToken).ConfigureAwait(false);
        await WriteAsync(result, cancellationToken).ConfigureAwait(false);
        LastTransportStatus = "typedResponseWritten";
    }

    private async Task<byte[]> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var header = new byte[4];
            await pipe.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length is < 1 or > MaximumRequestBytes) throw new JsonException("requestLengthInvalid");
            var body = new byte[length];
            await pipe.ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
            return body;
        }
        catch (IOException error) { throw new PipePeerException(error); }
    }

    private async Task WriteAsync(LegacyOperatorGearMailResult result, CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(FormatResult(result));
        if (body.Length is < 1 or > MaximumResponseBytes) throw new InvalidOperationException("Operator result exceeded its wire bound.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
        try
        {
            await pipe.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await pipe.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException error) { throw new PipePeerException(error); }
    }

    public ValueTask DisposeAsync()
    {
        lock (lifetimeGate) return new ValueTask(disposal ??= DisposeCoreAsync());
    }

    private async Task DisposeCoreAsync()
    {
        var errors = new List<Exception>();
        try { stop.Cancel(); }
        catch (Exception error) { errors.Add(error); }
        try { await worker.ConfigureAwait(false); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { errors.Add(error); }
        try { pipe.Dispose(); }
        catch (Exception error) { errors.Add(error); }
        try { stop.Dispose(); }
        catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Operator endpoint shutdown failed.", errors);
    }

    private sealed class PipePeerException(IOException cause) : IOException("Local pipe peer IO stopped.", cause) { }
}
