using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static partial class Program
{
    private const int ClientPeDependencyMaximumModules = 512;
    private const int ClientPeDependencyMaximumNameBytes = 256; // Includes the required NUL.
    private const string ClientPeDependencyAuditUsage =
        "Usage: WolfConquer.DataTool client-pe-dependency-audit <absoluteLocalPePath>";

    private static int ClientPeDependencyAudit(string[] args)
    {
        try
        {
            if (args.Length != 2) throw new ArgumentException("Supply exactly one explicit PE path.");
            string path = ResolveClientPeDependencyPath(args[1]);
            byte[] bytes;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length is < 64 or > (64L << 20))
                    throw new InvalidDataException("PE input must be 64 bytes through 64 MiB.");
                bytes = new byte[checked((int)input.Length)];
                input.ReadExactly(bytes);
                if (input.ReadByte() != -1 || input.Length != bytes.Length)
                    throw new IOException("PE input changed while reading.");
                ValidateClientPeDependencyOrdinary(path);
            }
            var pe = ReadClientPeDependencyHeaders(bytes);
            var modules = new List<ClientPeDependencyModule>();
            ReadClientPeDependencyDirectory(bytes, pe, delayed: false, modules);
            ReadClientPeDependencyDirectory(bytes, pe, delayed: true, modules);
            var report = new
            {
                schemaVersion = 1, path, length = bytes.Length,
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                peKind = pe.IsPe32Plus ? "PE32+" : "PE32", modules,
                limitation = "Static normal/delay import module names only. No dependency loading/search, symbol checks, " +
                    "LoadLibrary/runtime imports, forwarded exports or SxS resolution; no OS/project classification or approval."
            };
            var json = JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true
            });
            if (json.Length > 256 * 1024) throw new InvalidDataException("Dependency report exceeds its output bound.");
            Console.WriteLine(Encoding.UTF8.GetString(json));
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or OverflowException)
        {
            Console.Error.WriteLine("Client PE dependency audit refused: " + error.Message);
            Console.Error.WriteLine(ClientPeDependencyAuditUsage);
            return 1;
        }
    }

    private static string ResolveClientPeDependencyPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || !Path.IsPathFullyQualified(path) ||
            path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidDataException("Use an explicit local absolute PE path, never UNC/device paths.");
        string root = Path.GetPathRoot(path)!;
        foreach (string segment in path[root.Length..].Replace('\\', '/').Split('/'))
        {
            string stem = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (segment.Length is 0 or > 255 || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ') ||
                segment.Any(character => char.IsControl(character) || ":<>\"|?*".Contains(character)) || IsClientPeDependencyDevice(stem))
                throw new InvalidDataException("PE input contains an unsafe Windows path segment.");
        }
        string full = Path.GetFullPath(path);
        string extension = Path.GetExtension(full);
        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("PE input must name an .exe or .dll file.");
        ValidateClientPeDependencyOrdinary(full);
        return full;
    }

    private static void ValidateClientPeDependencyOrdinary(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            var attributes = File.GetAttributes(current);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0 ||
                ((attributes & FileAttributes.Directory) != 0) == (current == path))
                throw new InvalidDataException("PE input must be an ordinary file with ordinary directory ancestors.");
        }
    }

    private static bool IsClientPeDependencyDevice(string stem) =>
        stem is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$" or "CONIN$" or "CONOUT$" ||
        ((stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            stem.Length == 4 && "123456789¹²³".Contains(stem[3]));

    private static void ReadClientPeDependencyDirectory(byte[] bytes, ClientPeSymbolPeHeaders pe,
        bool delayed, List<ClientPeDependencyModule> modules)
    {
        uint rva = delayed ? pe.DelayImportTableRva : pe.ImportTableRva;
        uint size = delayed ? pe.DelayImportTableSize : pe.ImportTableSize;
        uint stride = delayed ? 32U : 20U;
        if ((rva | size) == 0) return;
        if (rva == 0 || size < stride)
            throw new InvalidDataException("Invalid import directory RVA/size or incomplete first descriptor.");
        var data = bytes.AsSpan();
        int start = MapClientPeDependencyRange(data, pe, rva, size);
        for (uint index = 0; index < size / stride; index++)
        {
            int offset = checked(start + (int)(index * stride));
            bool zero = true;
            for (int field = 0; field < stride; field += 4)
                zero &= ReadClientLaunchPeUInt32(data, offset + field) == 0;
            if (zero) return; // Later bytes are outside the terminated descriptor array.
            if (modules.Count == ClientPeDependencyMaximumModules)
                throw new InvalidDataException("Normal/delay import descriptors exceed the combined 512-module bound.");
            uint nameRva;
            if (delayed)
            {
                uint attributes = ReadClientLaunchPeUInt32(data, offset);
                if ((attributes & ~1U) != 0) throw new InvalidDataException("Reserved delay-import attribute bits are set.");
                nameRva = ResolveClientPeSymbolDelayedAddress(pe, ReadClientLaunchPeUInt32(data, offset + 4),
                    (attributes & 1) != 0, "dependency-dll-name");
            }
            else nameRva = ReadClientLaunchPeUInt32(data, offset + 12);
            if (nameRva == 0) throw new InvalidDataException("An import descriptor has no module name.");
            string name = ReadClientPeDependencyName(data, pe, nameRva);
            modules.Add(new(name, delayed ? "delay" : "normal"));
            // INT/IAT/module handles/bound/unload tables are deliberately not
            // dereferenced: this command inventories names, not symbol readiness.
        }
        throw new InvalidDataException("Import descriptors lack an all-zero terminator inside their declared directory.");
    }

    private static string ReadClientPeDependencyName(ReadOnlySpan<byte> data, ClientPeSymbolPeHeaders pe, uint rva)
    {
        int start = MapClientPeDependencyRange(data, pe, rva, 1);
        for (uint length = 0; length < ClientPeDependencyMaximumNameBytes; length++)
        {
            // Mapping the complete prefix also refuses cross-section strings.
            int mapped = MapClientPeDependencyRange(data, pe, rva, length + 1);
            if (mapped != start) throw new InvalidDataException("Import name has a noncontiguous file mapping.");
            byte value = data[start + (int)length];
            if (value != 0)
            {
                if (value is < 0x20 or > 0x7E || "/\\:<>\"|?*".Contains((char)value))
                    throw new InvalidDataException("Import name is not a safe printable ASCII module basename.");
                continue;
            }
            string name = Encoding.ASCII.GetString(data.Slice(start, (int)length));
            if (name.Length == 0 || name is "." or ".." || name.StartsWith(' ') || name.EndsWith(' ') || name.EndsWith('.') ||
                IsClientPeDependencyDevice(name.Split('.')[0].TrimEnd(' ').ToUpperInvariant()))
                throw new InvalidDataException("Import name is not a supported single Windows module basename.");
            return name;
        }
        throw new InvalidDataException("Import name is not NUL-terminated inside its 256-byte bound.");
    }

    private sealed record ClientPeDependencyModule(string Name, string Kind);
}
