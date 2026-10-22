using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Updates.Hosting;

internal sealed record FeedHttpRequest(string Method, string Target, string? Range, string? IfNoneMatch, bool HasBody)
{
    internal static async Task<FeedHttpRequest> ReadAsync(NetworkStream stream, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        var buffer = new byte[8192];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), deadline.Token).ConfigureAwait(false);
            if (read == 0) throw new IOException("Incomplete request.");
            length += read;
            var end = buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8);
            if (end >= 0) return Parse(buffer, end, length > end + 4);
            var firstLine = buffer.AsSpan(0, length).IndexOf("\r\n"u8);
            if (firstLine > 2048 || (firstLine < 0 && length > 2048)) throw new FeedRequestException(414);
        }
        throw new FeedRequestException(431);
    }

    private static FeedHttpRequest Parse(byte[] bytes, int end, bool extraData)
    {
        if (bytes.AsSpan(0, end).IndexOfAnyInRange((byte)128, byte.MaxValue) >= 0 || bytes.AsSpan(0, end).Contains((byte)0))
            throw new FeedRequestException(400);
        var lines = Encoding.ASCII.GetString(bytes, 0, end).Split("\r\n", StringSplitOptions.None);
        if (lines.Length > 65 || lines[0].Length > 2048) throw new FeedRequestException(431);
        var first = lines[0].Split(' ');
        if (first.Length != 3 || !Regex.IsMatch(first[0], "\\A[!#$%&'*+.^_`|~0-9A-Za-z-]{1,32}\\z") ||
            first[2] is not ("HTTP/1.1" or "HTTP/1.0")) throw new FeedRequestException(400);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0 || !Regex.IsMatch(line[..colon], "\\A[A-Za-z0-9-]+\\z") ||
                line.Any(value => char.IsControl(value) && value != '\t')) throw new FeedRequestException(400);
            if (!headers.TryAdd(line[..colon], line[(colon + 1)..].Trim(' ', '\t'))) throw new FeedRequestException(400);
        }
        if (first[2] == "HTTP/1.1" && (!headers.TryGetValue("Host", out var host) || host.Length == 0))
            throw new FeedRequestException(400);
        var hasBody = extraData || headers.ContainsKey("Transfer-Encoding");
        if (headers.TryGetValue("Content-Length", out var contentLength))
        {
            if (contentLength.Length == 0 || contentLength.Any(value => value is < '0' or > '9'))
                throw new FeedRequestException(400);
            hasBody |= contentLength.Any(value => value != '0');
        }
        return new FeedHttpRequest(first[0], first[1], headers.GetValueOrDefault("Range"),
            headers.GetValueOrDefault("If-None-Match"), hasBody);
    }
}

internal sealed class FeedRequestException(int status) : Exception
{
    internal int Status { get; } = status;
}
