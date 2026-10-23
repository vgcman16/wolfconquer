using System.Text;

namespace WolfConquer.Tests;

public sealed partial class ClientAuditPrefixQualityTests
{
    private sealed class ClientFiles : IDisposable
    {
        private const string Prefix = "wolf-client-prefix-quality-";
        private readonly string _temporaryParent = Path.GetFullPath(Path.GetTempPath());
        private readonly string _reference;
        private readonly string _resource;
        private readonly byte[] _referenceBytes = Encoding.UTF8.GetBytes(
            "return { 'STR_KNOWN', 'STR_MISSING' }\r\n");
        private readonly byte[] _resourceBytes = Encoding.UTF8.GetBytes("STR_KNOWN=Known text\r\n");

        internal ClientFiles()
        {
            Root = Path.GetFullPath(Path.Combine(_temporaryParent, Prefix + Guid.NewGuid().ToString("N")));
            ValidateRoot();
            RequireNoReparsePoint(_temporaryParent);
            Assert.False(Directory.Exists(Root));
            Directory.CreateDirectory(Root);
            RequireNoReparsePoint(Root);
            _reference = Path.Combine(Root, "ref.lua");
            _resource = Path.Combine(Root, "res.ini");
            File.WriteAllBytes(_reference, _referenceBytes);
            File.WriteAllBytes(_resource, _resourceBytes);
        }

        internal string Root { get; }

        internal void AssertUnchanged()
        {
            Assert.Equal(_referenceBytes, File.ReadAllBytes(_reference));
            Assert.Equal(_resourceBytes, File.ReadAllBytes(_resource));
        }

        private void ValidateRoot()
        {
            Assert.Equal(Path.TrimEndingDirectorySeparator(_temporaryParent), Path.GetDirectoryName(Root));
            Assert.StartsWith(Prefix, Path.GetFileName(Root));
            Assert.True(Guid.TryParseExact(Path.GetFileName(Root)[Prefix.Length..], "N", out _));
        }

        private static void RequireNoReparsePoint(string path)
        {
            if (File.Exists(path) || Directory.Exists(path))
                Assert.Equal((FileAttributes)0, File.GetAttributes(path) & FileAttributes.ReparsePoint);
        }

        public void Dispose()
        {
            ValidateRoot();
            RequireNoReparsePoint(_temporaryParent);
            RequireNoReparsePoint(Root);
            foreach (var file in new[] { _reference, _resource })
            {
                Assert.Equal(Root, Path.GetDirectoryName(Path.GetFullPath(file)));
                RequireNoReparsePoint(file);
                if (File.Exists(file)) File.Delete(file);
            }
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: false);
        }
    }
}
