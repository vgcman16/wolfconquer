using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace WolfConquer.Updates.Hosting;

internal static class FeedHttpResponse
{
    internal static async Task ErrorAsync(NetworkStream stream, int status, bool head, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes("{\"error\":\"" + ReadOnlyFeedHost.ErrorMessage + "\"}");
        await HeadersAsync(stream, status, bytes.Length, "application/json", "no-store", null,
            status == 405 ? "Allow: GET, HEAD\r\n" : "", token).ConfigureAwait(false);
        if (!head) await WriteAsync(stream, bytes, token).ConfigureAwait(false);
    }

    internal static async Task HealthAsync(NetworkStream stream, bool head, CancellationToken token)
    {
        byte[] body = "{\"status\":\"ok\"}"u8.ToArray();
        await HeadersAsync(stream, 200, body.Length, "application/json", "no-store", null, "", token).ConfigureAwait(false);
        if (!head) await WriteAsync(stream, body, token).ConfigureAwait(false);
    }

    internal static async Task FileAsync(NetworkStream stream, FeedHttpRequest request, FeedFile file,
        BlobVerificationCache verification, CancellationToken token)
    {
        await using var source = file.Stream;
        var length = source.Length;
        var cache = file.IsFeed ? "private, no-store, max-age=0" : file.Hash is not null ? "public, max-age=31536000, immutable" : "no-store";
        var etag = file.Hash is not null ? "\"" + file.Hash + "\"" : null;
        // HEAD reports metadata, not byte-integrity proof. A multi-GB payload must
        // not be read just to return Content-Length/ETag to a health checker.
        if (file.Hash is not null && request.Method != "HEAD")
        {
            if (!await verification.VerifyAsync(file, token).ConfigureAwait(false))
            {
                await ErrorAsync(stream, 404, false, token).ConfigureAwait(false);
                return;
            }
        }
        var type = file.IsFeed ? "application/json" : file.IsZip ? "application/zip" : "application/octet-stream";
        if (etag is not null && request.IfNoneMatch == etag)
        {
            await HeadersAsync(stream, 304, length, type, cache, etag, "", token).ConfigureAwait(false);
            return;
        }
        var offset = 0L;
        var count = length;
        var extra = file.IsFeed ? "Pragma: no-cache\r\n" : "Accept-Ranges: bytes\r\n";
        if (request.Range is not null)
        {
            if (file.IsFeed || !TryRange(request.Range, length, out offset, out count))
            {
                await HeadersAsync(stream, 416, 0, type, "no-store", etag, $"Content-Range: bytes */{length}\r\n", token).ConfigureAwait(false);
                return;
            }
            extra += $"Content-Range: bytes {offset}-{offset + count - 1}/{length}\r\n";
        }
        await HeadersAsync(stream, request.Range is null ? 200 : 206, count, type, cache, etag, extra, token).ConfigureAwait(false);
        if (request.Method == "HEAD") return;
        source.Position = offset;
        var buffer = new byte[65536];
        while (count > 0)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, count)), token).ConfigureAwait(false);
            if (read == 0) throw new IOException();
            await WriteAsync(stream, buffer.AsMemory(0, read), token).ConfigureAwait(false);
            count -= read;
        }
    }

    private static bool TryRange(string value, long length, out long offset, out long count)
    {
        offset = count = 0;
        if (!value.StartsWith("bytes=", StringComparison.Ordinal) || length == 0 || value.Length > 128) return false;
        var parts = value[6..].Split('-');
        if (parts.Length != 2) return false;
        bool Number(string text, out long number) => long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);
        if (parts[0].Length == 0)
        {
            if (!Number(parts[1], out var suffix) || suffix == 0) return false;
            count = Math.Min(suffix, length); offset = length - count; return true;
        }
        if (!Number(parts[0], out offset) || offset >= length) return false;
        var end = length - 1;
        if (parts[1].Length != 0 && (!Number(parts[1], out end) || end < offset)) return false;
        end = Math.Min(end, length - 1); count = end - offset + 1; return true;
    }

    private static Task HeadersAsync(NetworkStream stream, int status, long length, string type, string cache, string? etag,
        string extra, CancellationToken token)
    {
        var reason = status switch { 200 => "OK", 206 => "Partial Content", 304 => "Not Modified", 400 => "Bad Request",
            404 => "Not Found", 405 => "Method Not Allowed", 414 => "URI Too Long", 416 => "Range Not Satisfiable",
            431 => "Request Header Fields Too Large", _ => "Service Unavailable" };
        var header = $"HTTP/1.1 {status} {reason}\r\nContent-Length: {length.ToString(CultureInfo.InvariantCulture)}\r\nContent-Type: {type}\r\n" +
            $"Cache-Control: {cache}\r\nConnection: close\r\nX-Content-Type-Options: nosniff\r\n" +
            (etag is null ? "" : "ETag: " + etag + "\r\n") + extra + "\r\n";
        return WriteAsync(stream, Encoding.ASCII.GetBytes(header), token);
    }

    private static async Task WriteAsync(NetworkStream stream, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        await stream.WriteAsync(bytes, deadline.Token).ConfigureAwait(false);
    }
}
