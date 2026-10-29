using System.Buffers.Binary;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Runtime;

public sealed partial class LocalRuntimeInspectionPipe
{
    private async Task<byte[]> ReadAsync(CancellationToken cancellationToken)
    {
        var header = new byte[4]; await pipe.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > LocalRuntimeInspectionProtocol.MaximumRequestBytes) throw new JsonException("requestLengthInvalid");
        var bytes = new byte[length]; await pipe.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false); return bytes;
    }
    private async Task WriteAsync(LegacyRuntimeInspectionResult result, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(LocalRuntimeInspectionProtocol.FormatResult(result));
        if (bytes.Length is < 1 or > LocalRuntimeInspectionProtocol.MaximumResponseBytes) throw new InvalidOperationException("Runtime diagnostic exceeded its bound.");
        var frame = new byte[4 + bytes.Length]; BinaryPrimitives.WriteInt32LittleEndian(frame, bytes.Length); bytes.CopyTo(frame, 4);
        await pipe.WriteAsync(frame, cancellationToken).ConfigureAwait(false); await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
        LastTransportStatus = "diagnosticAwaitingReceipt";
        var receipt = new byte[LocalRuntimeInspectionProtocol.ReceiptLength];
        await pipe.ReadExactlyAsync(receipt, cancellationToken).ConfigureAwait(false);
        if (!receipt.AsSpan().SequenceEqual(LocalRuntimeInspectionProtocol.ResponseReceipt(result.QueryId))) throw new IOException("Runtime diagnostic receipt mismatch.");
    }
    public ValueTask DisposeAsync()
    {
        lock (disposalGate) return new ValueTask(disposal ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    {
        var failures = new List<Exception>();
        try { stop.Cancel(); } catch (Exception error) { failures.Add(error); }
        try { await worker.ConfigureAwait(false); }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { failures.Add(error); }
        try { pipe.Dispose(); } catch (Exception error) { failures.Add(error); }
        try { stop.Dispose(); } catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Runtime inspector shutdown failed.", failures);
    }
}
