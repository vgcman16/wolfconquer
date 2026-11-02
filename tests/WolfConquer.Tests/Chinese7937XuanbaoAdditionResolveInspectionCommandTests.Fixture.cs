using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAdditionResolveInspectionCommandTests
{
    private const string Command = "client-xuanbao7937-addition-resolve-inspect";
    private const string PrivateMarker = "SYNTHETIC-PRIVATE-CLI-SENTINEL";
    private const string Addition = "2700000001@@17@@5@@0@@2600000001@@\r\n2700000002@@11@@6@@0@@2600000002@@\r\n";
    private static readonly string[] BoundaryFlags = ["RuntimeBinding", "GameplayActivated", "AdditionMeaningsEstablished",
        "ResolveBranchOwnerEstablished", "CurrentActionEstablished", "RewardOwnershipEstablished", "InputsWritten",
        "LuaExecuted", "NativeClientExecuted"];

    private static async Task WithInputs(Func<Inputs, Task> body)
    {
        var fixture = new Inputs(); var errors = new List<Exception>();
        try { fixture.Initialize(); await body(fixture); } catch (Exception error) { errors.Add(error); }
        try { fixture.Cleanup(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private static string Pin(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private sealed record Result(int ExitCode, string Output, string Error);
    private sealed record FileImage(byte[] Bytes, DateTime LastWriteUtc);
    private sealed record TreeImage(Dictionary<string, FileImage> Files, string[] Directories);

    private sealed class Inputs
    {
        private const string Prefix = "wolf-addition-resolve-command-";
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N"));
        internal string InfoPath => Path.Combine(Root, "private-plain-info.lua");
        internal string AdditionPath => Path.Combine(Root, "private-addition.dat");
        internal byte[] Info = [];
        internal byte[] Packed = [];

        internal void Initialize()
        {
            Directory.CreateDirectory(Root);
            SetInfo(Chinese7937XuanbaoResolveFixture.Gbk("-- " + PrivateMarker + "\n" +
                Chinese7937XuanbaoResolveFixture.Module("3141592653", "2718281828", "1618033988")));
            SetAddition(Addition);
        }

        internal string[] Arguments() => [Command, "7937", InfoPath, Pin(Info), AdditionPath, Pin(Packed)];
        internal void SetInfo(byte[] bytes) { Info = bytes.ToArray(); File.WriteAllBytes(InfoPath, Info); }
        internal void SetAddition(string decoded)
        { Packed = ClientItemCatalogCodec.Encode(Encoding.ASCII.GetBytes(decoded)); File.WriteAllBytes(AdditionPath, Packed); }

        internal TreeImage Capture()
        {
            var files = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).ToDictionary(
                path => Path.GetRelativePath(Root, path), path => new FileImage(File.ReadAllBytes(path), File.GetLastWriteTimeUtc(path)),
                StringComparer.Ordinal);
            return new(files, Directory.GetDirectories(Root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Root, path)).Order(StringComparer.Ordinal).ToArray());
        }

        internal void AssertUnchanged(TreeImage before)
        {
            var after = Capture();
            Assert.Equal(before.Directories, after.Directories);
            Assert.Equal(before.Files.Keys.Order(StringComparer.Ordinal), after.Files.Keys.Order(StringComparer.Ordinal));
            foreach (var pair in before.Files)
            {
                Assert.Equal(pair.Value.Bytes, after.Files[pair.Key].Bytes);
                Assert.Equal(pair.Value.LastWriteUtc, after.Files[pair.Key].LastWriteUtc);
            }
        }

        internal async Task<Result> RunUnchanged(params string[] args)
        {
            var before = Capture(); var result = await RunProcess(Root, args); AssertUnchanged(before); return result;
        }

        internal void Cleanup()
        {
            var full = Path.GetFullPath(Root); var name = Path.GetFileName(full);
            if (!string.Equals(Path.GetDirectoryName(full), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                    StringComparison.OrdinalIgnoreCase) || !name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name[Prefix.Length..], "N", out _)) throw new InvalidOperationException("Invalid owned CLI fixture root.");
            if (!Directory.Exists(full)) return;
            var pending = new Stack<string>(); pending.Push(full);
            while (pending.TryPop(out var directory))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Owned CLI fixture contains a reparse directory.");
                foreach (var entry in Directory.GetFileSystemEntries(directory))
                {
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Owned CLI fixture contains a reparse entry.");
                    if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
                }
            }
            Directory.Delete(full, recursive: true);
        }
    }
}
