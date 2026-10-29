using System.Buffers.Binary;
using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

internal static partial class Program
{
    private static int InspectRuntimePlayer(string[] args) => InspectRuntimePlayerAsync(args).GetAwaiter().GetResult();
    private static async Task<int> InspectRuntimePlayerAsync(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || args.Length != 5 || !LocalRuntimeInspectionProtocol.IsPipeName(args[1]) ||
                !Guid.TryParseExact(args[2], "N", out var domain) || domain == Guid.Empty || args[1] != LocalRuntimeInspectionProtocol.NameFor(domain) ||
                !uint.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var account) || account == 0 ||
                !uint.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out var uid) || uid == 0)
                throw new ArgumentException("explicitRuntimeInspectionIdentityRequired");
            var request = new LegacyRuntimeInspectionRequest(1, Guid.NewGuid(), domain, account, uid);
            var body = LocalRuntimeInspectionProtocol.RequestBytes(request);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var peer = new NamedPipeClientStream(".", args[1], PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await peer.ConnectAsync(deadline.Token).ConfigureAwait(false);
            var frame = new byte[4 + body.Length]; BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length); body.CopyTo(frame, 4);
            await peer.WriteAsync(frame, deadline.Token).ConfigureAwait(false); await peer.FlushAsync(deadline.Token).ConfigureAwait(false);
            var header = new byte[4]; await peer.ReadExactlyAsync(header, deadline.Token).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (length is < 1 or > LocalRuntimeInspectionProtocol.MaximumResponseBytes) throw new JsonException("responseLengthInvalid");
            var bytes = new byte[length]; await peer.ReadExactlyAsync(bytes, deadline.Token).ConfigureAwait(false);
            var result = LocalRuntimeInspectionProtocol.ParseResult(bytes);
            if (result.QueryId != request.QueryId || result.Snapshot is { } s &&
                (s.Domain != domain || s.AccountId != account || s.CharacterUid != uid)) throw new JsonException("responseCorrelationMismatch");
            await peer.WriteAsync(LocalRuntimeInspectionProtocol.ResponseReceipt(result.QueryId), deadline.Token).ConfigureAwait(false);
            await peer.FlushAsync(deadline.Token).ConfigureAwait(false);
            Console.WriteLine(LocalRuntimeInspectionProtocol.FormatResult(result));
            return result.Status == LegacyRuntimeInspectionStatus.Observed ? 0 : 2;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or
            OperationCanceledException or JsonException or NotSupportedException)
        {
            Console.Error.WriteLine("Runtime diagnostic refused or transport stopped; no observation completion established. No automatic retry.");
            return 1;
        }
    }
}
