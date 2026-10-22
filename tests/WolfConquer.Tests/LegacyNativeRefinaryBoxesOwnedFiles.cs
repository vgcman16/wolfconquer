using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.World;
using Rifinery = WolfConquer.Core.World.LegacyNativeRifinery;
using Boxes = WolfConquer.Core.World.LegacyNativeRefinaryBoxes;
using ItemType = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;

namespace WolfConquer.Tests;

// Test-only ownership. Production constructors still read their literal raw paths.
internal sealed class LegacyNativeRefinaryBoxesOwnedFiles : IDisposable
{
    private readonly string oldLocation = LegacyNativeServerConfig.DbLocation;
    private readonly CultureInfo oldCulture = CultureInfo.CurrentCulture;
    private readonly CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
    private readonly Rifinery? oldRifinery = LegacyNativePoolItems.RifineryItems;
    private readonly Boxes? oldBoxes = LegacyNativePoolItems.DBRerinaryBoxes;
    private readonly ItemType? oldItemsBase = LegacyNativePoolItems.ItemsBase;
    private readonly LegacyNativeCounter oldCounter = LegacyNativePoolItems.ItemCounter;
    private readonly Dictionary<uint, Dictionary<uint, Rifinery.Item>> oldRefinary = ItemType.Refinary;
    private readonly Dictionary<string, byte[]> seeds = new(StringComparer.OrdinalIgnoreCase);
    internal string Root { get; }
    internal string BoxesPath { get; }
    internal string RifineryPath { get; }

    internal LegacyNativeRefinaryBoxesOwnedFiles(string boxes, string rifinery = "", bool rawPrefix = false)
    {
        DirectoryInfo? workspace = new(AppContext.BaseDirectory);
        while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, "WolfConquer.sln")))
            workspace = workspace.Parent;
        if (workspace is null) throw new InvalidOperationException("Real workspace required.");
        var parent = Path.GetFullPath(Path.Combine(workspace.FullName, ".artifacts"));
        Ordinary(parent);
        Root = Path.Combine(parent, "goal542-refinary-boxes-owned-" + Guid.NewGuid().ToString("N"));
        if (File.Exists(Root) || Directory.Exists(Root)) throw new IOException("Fresh owned root required.");
        Directory.CreateDirectory(Root);
        Ordinary(Root);
        BoxesPath = Path.Combine(Root, rawPrefix ? "prefixdatabaserefineryboxes.txt" : "databaserefineryboxes.txt");
        RifineryPath = Path.Combine(Root, rawPrefix ? "prefixRifinery.txt" : "Rifinery.txt");
        Seed(BoxesPath, boxes);
        Seed(RifineryPath, rifinery);
        // Explicit MODEL global substitutions, never production startup/publication.
        ItemType.Refinary = new Dictionary<uint, Dictionary<uint, Rifinery.Item>>();
        LegacyNativePoolItems.RifineryItems = null;
        LegacyNativePoolItems.DBRerinaryBoxes = null;
        LegacyNativeServerConfig.DbLocation = rawPrefix
            ? Path.Combine(Root, "prefix") : Root + Path.DirectorySeparatorChar;
    }

    internal Rifinery LoadRifinery()
    {
        var table = new Rifinery();
        LegacyNativePoolItems.RifineryItems = table;
        return table;
    }

    internal static string Row(uint id, string level, string name = "Bow", byte type = 1)
    {
        // Real native Rifinery reads index53:54 tokens, not53 total.
        var tokens = Enumerable.Repeat("0", 54).ToArray();
        tokens[0] = id.ToString(CultureInfo.InvariantCulture);
        tokens[1] = level;
        tokens[2] = "7";
        tokens[3] = type.ToString(CultureInfo.InvariantCulture);
        tokens[4] = "2";
        tokens[5] = "9";
        tokens[53] = name;
        return string.Join(' ', tokens);
    }

    internal static string Rows(params string[] rows) => string.Join('\n', rows) + "\n";

    private void Seed(string path, string text)
    {
        CheckLeaf(path);
        var bytes = new UTF8Encoding(false, true).GetBytes(text);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        Assert.Equal(bytes, File.ReadAllBytes(path));
        seeds.Add(path, bytes);
    }

    private void CheckLeaf(string path)
    {
        if (!Path.IsPathFullyQualified(path) ||
            !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(path), path) ||
            !StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(path), Root) ||
            !(StringComparer.OrdinalIgnoreCase.Equals(path, BoxesPath) ||
              StringComparer.OrdinalIgnoreCase.Equals(path, RifineryPath)))
            throw new IOException("Owned exact leaf required.");
        Ordinary(path);
    }

    private static void Ordinary(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Reparse path preserved.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            current = Path.GetDirectoryName(current);
        }
    }

    public void Dispose()
    {
        LegacyNativeServerConfig.DbLocation = oldLocation;
        CultureInfo.CurrentCulture = oldCulture;
        CultureInfo.CurrentUICulture = oldUiCulture;
        LegacyNativePoolItems.RifineryItems = oldRifinery;
        LegacyNativePoolItems.DBRerinaryBoxes = oldBoxes;
        LegacyNativePoolItems.ItemsBase = oldItemsBase;
        LegacyNativePoolItems.ItemCounter = oldCounter;
        ItemType.Refinary = oldRefinary;
        Ordinary(Root);
        var entries = Directory.GetFileSystemEntries(Root);
        if (entries.Length != seeds.Count || entries.Any(path => !seeds.ContainsKey(path) || Directory.Exists(path)))
            throw new IOException("Unexpected owned entry preserved.");
        // Validate both exact leaves and all bytes before either deletion.
        foreach (var pair in seeds)
        {
            CheckLeaf(pair.Key);
            if (!CryptographicOperations.FixedTimeEquals(
                    SHA256.HashData(pair.Value), SHA256.HashData(File.ReadAllBytes(pair.Key))))
                throw new IOException("Changed owned seed preserved.");
        }
        foreach (var path in seeds.Keys) File.Delete(path);
        Directory.Delete(Root, recursive: false);
    }
}
