using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

internal static partial class Program
{
    private static int CreateOperatorTestKit(string[] args)
    {
        var published = false;
        try
        {
            if (args.Length != 6 || !Guid.TryParseExact(args[2], "N", out var domain) || domain == Guid.Empty ||
                args[2] != domain.ToString("N") ||
                !uint.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var accountId) || accountId == 0 ||
                !uint.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out var characterUid) || characterUid == 0)
                throw new ArgumentException("Explicit kit arguments required.");
            var output = RequireNewOperatorRequestPath(args[1]);
            var request = LegacyOperatorTestKits.Create(Guid.NewGuid(), domain, accountId, characterUid, args[5]);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(request, new JsonSerializerOptions
            { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
            // Consume the actual shared strict parser before any filesystem write.
            _ = LocalOperatorMailPipe.ParseRequest(bytes);
            PublishNewOperatorRequest(output, bytes);
            published = true;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                operationId = request.OperationId, expectedDomain = domain, kit = args[5],
                itemRows = request.Items.Count, requestCreated = true, sent = false,
                note = "Retain this file and operation ID. Do not regenerate an uncertain delivery."
            }));
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
            InvalidOperationException or JsonException or NotSupportedException or AggregateException or System.Security.SecurityException)
        {
            Console.Error.WriteLine(published
                ? "Operator test-kit request exists, but reporting failed. Retain that file and operation ID. No mail was sent."
                : "Operator test-kit request was not created; existing files were preserved. No mail was sent.");
            return 1;
        }
    }

    private static string RequireNewOperatorRequestPath(string supplied)
    {
        if (!Path.IsPathFullyQualified(supplied) || supplied.StartsWith(@"\\", StringComparison.Ordinal))
            throw new ArgumentException("An absolute local output is required.");
        var path = Path.GetFullPath(supplied);
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase) ||
            File.Exists(path) || Directory.Exists(path)) throw new IOException("A new JSON output is required.");
        var parent = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException();
        for (string? part = parent; part is not null; part = Path.GetDirectoryName(part))
            if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("An ordinary existing parent is required.");
        return path;
    }

    private static void PublishNewOperatorRequest(string output, byte[] bytes)
    {
        var parent = Path.GetDirectoryName(output)!;
        var temporary = Path.Combine(parent, ".wolf-operator-kit-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        Exception? primary = null;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, output, overwrite: false);
            created = false;
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            if (created)
            {
                try { File.Delete(temporary); }
                catch (Exception cleanup) when (primary is not null)
                { throw new AggregateException("Request creation and temporary cleanup both failed.", primary, cleanup); }
            }
        }
    }
}
