using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed class TitlePersistenceFileFixture : IDisposable
{
    internal const long Uid = 1_296_887;
    private readonly string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wolf-title-persistence-" + Guid.NewGuid().ToString("N")));
    private readonly string baseText;
    internal string Root => root;
    internal string PathName => Path.Combine(root, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");

    internal TitlePersistenceFileFixture()
    {
        ValidateRoot();
        if (Directory.Exists(root)) throw new IOException("Owned fixture root already exists.");
        Directory.CreateDirectory(root);
        new CharacterSnapshotLoader(root, null, null, null, null, null)
            .CreateLegacyFirstCharacterSnapshot(Uid, "Crème€", 1008, 10);
        baseText = File.ReadAllText(PathName);
    }

    internal WorldSession Seed(int encodingKind = 0)
    {
        var text = baseText.Replace("\r\n", "\n").Replace("\n", "\r\n")
            .Replace("[PaidNobility]",
                "SpecialTitles=2,2149,0,4001,0,\r\nSpecialTitleID=0\r\nSpecialWingID=0\r\nSpecialTitleScore=0\r\n" +
                "SecurityPass=91357,0,0,\r\nManagedTradeEscrowMoney=17\r\n[Unrelated]\r\nSentinel=café€\r\n" +
                "[PaidNobility]", StringComparison.Ordinal);
        File.WriteAllText(PathName, text, EncodingFor(encodingKind));
        return new(Uid, "owned-fixture", CharacterSnapshotParser.ParseText(text),
            PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: root);
    }

    internal static WorldSession Updated(WorldSession session)
    {
        var fields = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["SpecialTitles"] = "2,2149,1,4001,0,",
            ["SpecialTitleID"] = "21492149",
            ["SpecialTitleScore"] = "150",
        };
        return session with { Character = session.Character with { Attributes = fields } };
    }

    internal static Dictionary<string, string> Values(WorldSession session)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "SpecialTitleID", "SpecialWingID", "SpecialTitleScore" })
            result.Add(key, session.Character.Attributes.TryGetValue(key, out var value)
                ? ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : "0");
        if (session.Character.Attributes.TryGetValue("SpecialTitles", out var titles)) result.Add("SpecialTitles", titles);
        return result;
    }

    internal static Encoding EncodingFor(int kind)
    {
        if (kind != 2) return new UTF8Encoding(kind == 1);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    internal void AssertRejected(WorldSession original, WorldSession updated,
        IReadOnlyDictionary<string, string> values, string prefix)
    {
        var before = File.ReadAllBytes(PathName);
        Assert.False(LegacyTitleStoragePersistence.TryPersist(original, updated, values, out var reason));
        Assert.StartsWith(prefix, reason);
        Assert.Equal(before, File.ReadAllBytes(PathName));
    }

    public void Dispose()
    {
        ValidateRoot();
        if (!Directory.Exists(root)) return;
        var directory = new DirectoryInfo(root);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Owned fixture root changed to a reparse point.");
        var entries = Directory.GetFileSystemEntries(root);
        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            var prefix = Uid.ToString(CultureInfo.InvariantCulture) + ".ini.character-file-";
            var transaction = name.StartsWith(prefix, StringComparison.Ordinal)
                ? name[prefix.Length..] : string.Empty;
            var ownTransaction = transaction.Length == 36 && Guid.TryParseExact(transaction[..32], "N", out _) &&
                (transaction.EndsWith(".tmp", StringComparison.Ordinal) || transaction.EndsWith(".bak", StringComparison.Ordinal));
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(entry)), Path.GetFullPath(root),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                (name != Uid.ToString(CultureInfo.InvariantCulture) + ".ini" && !ownTransaction))
                throw new IOException("Unexpected owned fixture entry; preserved for inspection.");
            if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                throw new IOException("Unexpected owned fixture directory; preserved for inspection.");
        }
        foreach (var entry in entries) File.Delete(entry);
        Directory.Delete(root, recursive: false);
    }

    private void ValidateRoot()
    {
        var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        const string prefix = "wolf-title-persistence-";
        var name = Path.GetFileName(root);
        if (!string.Equals(Path.GetDirectoryName(root), parent, comparison) ||
            !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..], "N", out _) ||
            (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Owned fixture boundary changed; preserved.");
    }
}
