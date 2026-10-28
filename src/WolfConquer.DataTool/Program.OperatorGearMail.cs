using System.Buffers.Binary;
using System.IO.Pipes;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

internal static partial class Program
{
    private static int SendOperatorGearMail(string[] args) => SendOperatorGearMailAsync(args).GetAwaiter().GetResult();

    private static async Task<int> SendOperatorGearMailAsync(string[] args)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || args.Length != 3 || !LocalOperatorMailPipe.IsOperatorPipeName(args[1]))
                throw new InvalidOperationException();
            var path = args[2];
            if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
                throw new InvalidOperationException();
            for (string? part = Path.GetFullPath(path); part is not null; part = Path.GetDirectoryName(part))
                if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException();
            byte[] body;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length is < 1 or > LocalOperatorMailPipe.MaximumRequestBytes) throw new InvalidOperationException();
                body = new byte[(int)file.Length];
                file.ReadExactly(body);
            }
            var request = LocalOperatorMailPipe.ParseRequest(body);
            if (!string.Equals(args[1], LocalOperatorMailPipe.NameFor(request.ExpectedDomain), StringComparison.Ordinal))
                throw new InvalidOperationException();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var pipe = new NamedPipeClientStream(".", args[1], PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
            await pipe.WriteAsync(header, deadline.Token).ConfigureAwait(false);
            await pipe.WriteAsync(body, deadline.Token).ConfigureAwait(false);
            await pipe.FlushAsync(deadline.Token).ConfigureAwait(false);
            await pipe.ReadExactlyAsync(header, deadline.Token).ConfigureAwait(false);
            var responseLength = BinaryPrimitives.ReadInt32LittleEndian(header);
            if (responseLength is < 1 or > LocalOperatorMailPipe.MaximumResponseBytes) throw new InvalidOperationException();
            var response = new byte[responseLength];
            await pipe.ReadExactlyAsync(response, deadline.Token).ConfigureAwait(false);
            var result = LocalOperatorMailPipe.ParseResult(response);
            if (result.OperationId != request.OperationId) throw new InvalidOperationException();
            // Only a complete parsed response for this exact operation permits
            // the server to disconnect. Never acknowledge an unproven body.
            await pipe.WriteAsync(LocalOperatorMailPipe.CreateResponseReceipt(result.OperationId), deadline.Token).ConfigureAwait(false);
            await pipe.FlushAsync(deadline.Token).ConfigureAwait(false);
            Console.WriteLine(LocalOperatorMailPipe.FormatResult(result));
            return result.Status switch
            {
                LegacyOperatorGearMailStatus.CommittedNotificationCompleted or LegacyOperatorGearMailStatus.CommittedNotificationStopped => 0,
                LegacyOperatorGearMailStatus.Rejected => 2,
                LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven => 3,
                _ => 4
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or OperationCanceledException or System.Text.Json.JsonException or NotSupportedException)
        {
            Console.Error.WriteLine("Operator gear-mail transport refused or stopped; outcome may be unknown. No automatic retry.");
            return 1;
        }
    }
}
