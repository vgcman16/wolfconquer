using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Updates.Hosting;
using Xunit;

namespace WolfConquer.Updates.Tests;

public sealed partial class ReadOnlyFeedHostTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("wolf feed-fixture ").FullName;
        internal string Publish => Path.Combine(Root, "curated publish");
        internal byte[] FeedBytes { get; } = "{\"version\":\"synthetic\"}"u8.ToArray();
        internal byte[] BlobBytes { get; } = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
        internal string Hash => Convert.ToHexString(SHA256.HashData(BlobBytes)).ToLowerInvariant();
        internal ReadOnlyFeedHost Host { get; private set; } = null!;
        internal HttpClient Client { get; private set; } = null!;
        internal List<string> Links { get; } = [];

        internal static async Task<Fixture> StartAsync()
        {
            var fixture = new Fixture();
            try
            {
                foreach (var folder in new[] { "feeds", "blobs", "bootstrap" }) Directory.CreateDirectory(Path.Combine(fixture.Publish, folder));
                File.WriteAllBytes(Path.Combine(fixture.Publish, "feeds", "client-stable.json"), fixture.FeedBytes);
                File.WriteAllBytes(Path.Combine(fixture.Publish, "blobs", fixture.Hash), fixture.BlobBytes);
                File.WriteAllBytes(Path.Combine(fixture.Publish, "bootstrap", "setup.zip"), [0x50, 0x4B, 1, 2]);
                File.WriteAllBytes(Path.Combine(fixture.Publish, "bootstrap", "not-allowed.exe"), [1, 2, 3]);
                File.WriteAllText(Path.Combine(fixture.Publish, "private.txt"), "private fixture");
                fixture.Host = await ReadOnlyFeedHost.StartAsync(new(fixture.Publish, 0, ["setup.zip"]));
                fixture.Client = new HttpClient(new HttpClientHandler { UseProxy = false })
                { BaseAddress = fixture.Host.BaseAddress, Timeout = TimeSpan.FromSeconds(10) };
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        internal async Task<RawResponse> RawAsync(string method, string target, string headers = "")
        {
            using var client = new TcpClient();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(IPAddress.Loopback, Host.BaseAddress.Port, deadline.Token);
            var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"{method} {target} HTTP/1.1\r\nHost: localhost\r\n{headers}\r\n"), deadline.Token);
            using var output = new MemoryStream();
            var buffer = new byte[1024];
            int count;
            while ((count = await stream.ReadAsync(buffer, deadline.Token)) > 0)
            {
                output.Write(buffer, 0, count);
                Assert.True(output.Length <= 16384, "Synthetic response exceeded its bound.");
            }
            var bytes = output.ToArray();
            var delimiter = bytes.AsSpan().IndexOf("\r\n\r\n"u8);
            Assert.True(delimiter > 0);
            var text = Encoding.ASCII.GetString(bytes, 0, delimiter + 2);
            return new RawResponse(int.Parse(text.Split(' ')[1]), text, bytes[(delimiter + 4)..]);
        }

        internal async Task LinkAsync(string link, string target)
        {
            if (!OperatingSystem.IsWindows()) Directory.CreateSymbolicLink(link, target);
            else
            {
                var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
                Assert.True(process.ExitCode == 0, $"Owned junction failed: {await stdout}; {await stderr}");
            }
            Links.Add(link);
            Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
        }

        public async ValueTask DisposeAsync()
        {
            Client?.Dispose();
            if (Host is not null) await Host.DisposeAsync();
            foreach (var link in Links)
            {
                Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
                Directory.Delete(link);
            }
            var resolved = Path.GetFullPath(Root);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("wolf feed-fixture ", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, recursive: true);
        }
    }

    private sealed record RawResponse(int Status, string Headers, byte[] Body);
}
