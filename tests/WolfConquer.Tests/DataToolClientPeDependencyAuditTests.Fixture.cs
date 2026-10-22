using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace WolfConquer.Tests;

public sealed partial class DataToolClientPeDependencyAuditTests
{
    private const string Command = "client-pe-dependency-audit";
    private const int Optional = 0x98, Normal = 0x500, Delay = 0x700, Names = 0xA00, RawEnd = 0x1400;
    private static uint Rva(int raw) => checked((uint)(0x2000 + raw - 0x400));
    private static int Directories(bool plus) => Optional + (plus ? 112 : 96);
    private static int Section(bool plus) => Optional + (plus ? 0xF0 : 0xE0);
    private static void U16(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
    private static void U32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);

    private static byte[] Image(string[]? normal = null, string[]? delayed = null, bool plus = false, bool legacyVa = false)
    {
        normal ??= []; delayed ??= [];
        var bytes = new byte[0x1600]; // A deliberate unmapped overlay follows the raw section.
        bytes[0] = 0x4D; bytes[1] = 0x5A; U32(bytes, 0x3C, 0x80); U32(bytes, 0x80, 0x4550);
        U16(bytes, 0x84, (ushort)(plus ? 0x8664 : 0x14C)); U16(bytes, 0x86, 1);
        U16(bytes, 0x94, (ushort)(plus ? 0xF0 : 0xE0)); U16(bytes, Optional, (ushort)(plus ? 0x20B : 0x10B));
        if (plus) BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(Optional + 24), legacyVa ? 0x400000UL : 0x140000000UL);
        else U32(bytes, Optional + 28, 0x400000);
        U32(bytes, Optional + 32, 0x1000); U32(bytes, Optional + 36, 0x200);
        U32(bytes, Optional + 56, 0x4000); U32(bytes, Optional + 60, 0x400);
        U32(bytes, Optional + (plus ? 108 : 92), 16);
        int section = Section(plus);
        Encoding.ASCII.GetBytes(".rdata\0\0").CopyTo(bytes, section);
        U32(bytes, section + 8, 0x1000); U32(bytes, section + 12, 0x2000);
        U32(bytes, section + 16, 0x1000); U32(bytes, section + 20, 0x400);
        int cursor = Names;
        if (normal.Length != 0)
        {
            U32(bytes, Directories(plus) + 8, Rva(Normal));
            U32(bytes, Directories(plus) + 12, checked((uint)((normal.Length + 1) * 20)));
            for (int i = 0; i < normal.Length; i++)
            {
                U32(bytes, Normal + i * 20 + 12, Rva(cursor));
                WriteName(normal[i]);
            }
        }
        if (delayed.Length != 0)
        {
            U32(bytes, Directories(plus) + 13 * 8, Rva(Delay));
            U32(bytes, Directories(plus) + 13 * 8 + 4, checked((uint)((delayed.Length + 1) * 32)));
            for (int i = 0; i < delayed.Length; i++)
            {
                U32(bytes, Delay + i * 32, legacyVa ? 0U : 1U);
                U32(bytes, Delay + i * 32 + 4, Rva(cursor) + (legacyVa ? 0x400000U : 0U));
                WriteName(delayed[i]);
            }
        }
        return bytes;

        void WriteName(string name)
        {
            byte[] text = Encoding.ASCII.GetBytes(name);
            text.CopyTo(bytes, cursor); cursor += text.Length;
            bytes[cursor++] = 0;
        }
    }

    private sealed class AuditFixture : IDisposable
    {
        internal string Root { get; } = Directory.CreateTempSubdirectory("wolf-pe-dependencies-" + Guid.NewGuid().ToString("N") + "-").FullName;
        internal string Path { get; }
        private byte[] Original { get; }
        private DateTime OriginalWrite { get; }
        internal AuditFixture(byte[] bytes, string name = "synthetic.exe")
        {
            Path = System.IO.Path.Combine(Root, name);
            File.WriteAllBytes(Path, bytes);
            Original = (byte[])bytes.Clone(); OriginalWrite = File.GetLastWriteTimeUtc(Path);
        }
        internal async Task<AuditResult> Run(params string[] arguments)
        {
            using var process = TestProjectProcess.Create(RepositoryRoot(), "WolfConquer.DataTool");
            process.StartInfo.WorkingDirectory = Root;
            process.StartInfo.ArgumentList.Add(Command);
            foreach (string argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            Assert.True(process.Start(), "Could not start the actual DataTool CLI.");
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true); await process.WaitForExitAsync();
                Assert.Fail($"Owned DataTool child timed out. stdout: {await stdout} stderr: {await stderr}");
            }
            Assert.Equal(Original, File.ReadAllBytes(Path));
            Assert.Equal(OriginalWrite, File.GetLastWriteTimeUtc(Path));
            return new(process.ExitCode, await stdout, await stderr);
        }
        public void Dispose()
        {
            Assert.Equal(Original, File.ReadAllBytes(Path));
            Assert.Equal(OriginalWrite, File.GetLastWriteTimeUtc(Path));
            Assert.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), Root, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("wolf-pe-dependencies-", System.IO.Path.GetFileName(Root), StringComparison.Ordinal);
            Assert.False((File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0);
            Directory.Delete(Root, recursive: true); // No links are created in these fixtures.
        }
    }

    private static string RepositoryRoot([CallerFilePath] string file = "")
    {
        for (DirectoryInfo? directory = new(System.IO.Path.GetDirectoryName(file)!); directory is not null; directory = directory.Parent)
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "WolfConquer.sln"))) return directory.FullName;
        throw new InvalidOperationException("Could not locate the actual repository test output.");
    }
    private sealed record AuditResult(int ExitCode, string Output, string Error);
}
