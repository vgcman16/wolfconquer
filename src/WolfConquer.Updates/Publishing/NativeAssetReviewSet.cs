using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Publishing;

internal sealed class NativeAssetReviewSet
{
    internal const int MaximumReviews = 4096;
    internal const string ObjectDirectory = "native-map-object-directory";
    internal const string LogsBitmap = "native-logs-bitmap";
    internal const string ServerTypeLua = "native-server-type-lua";
    internal const string LuaConfigCatalog = "native-luacfg-userdata-catalog";
    private readonly Dictionary<string, NativeAssetReview> reviews;
    internal PublishEntry[] Entries { get; }
    internal IReadOnlyCollection<NativeAssetReview> Reviews => reviews.Values;

    private NativeAssetReviewSet(PublishEntry[] entries, Dictionary<string, NativeAssetReview> rows)
    {
        Entries = entries;
        reviews = rows;
    }

    internal NativeAssetReview? For(string path) => reviews.GetValueOrDefault(path);

    internal static NativeAssetReviewSet Create(IEnumerable<PublishEntry> plan,
        IEnumerable<NativeAssetReview>? nativeAssetReviews)
    {
        ArgumentNullException.ThrowIfNull(plan);
        PublishEntry[] entries = plan.Take(SignedUpdateManifest.MaximumFiles + 1).ToArray();
        if (entries.Length > SignedUpdateManifest.MaximumFiles || entries.Any(row => row is null))
            throw Invalid("Publication plan is invalid or too large.");
        NativeAssetReview[] supplied = nativeAssetReviews?.Take(MaximumReviews + 1).ToArray() ?? [];
        if (supplied.Length > MaximumReviews || supplied.Any(row => row is null) ||
            nativeAssetReviews is not null && supplied.Length == 0)
            throw Invalid("Native asset reviews are invalid or too large.");
        var rows = new Dictionary<string, NativeAssetReview>(StringComparer.OrdinalIgnoreCase);
        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (NativeAssetReview suppliedRow in supplied)
        {
            var row = suppliedRow with { };
            ValidateRow(row);
            if (!rows.TryAdd(row.Path, row)) throw Invalid("Native asset reviews collide.");
            if (roots.TryGetValue(row.CollisionPath, out string? previous) && previous != row.CollisionPath)
                throw Invalid("Native asset collision roots differ in case.");
            roots[row.CollisionPath] = row.CollisionPath;
        }
        var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < entries.Length; index++)
        {
            PublishEntry entry = entries[index] with { };
            entries[index] = entry;
            UpdatePathPolicy.ValidateRelativePath(entry.Path);
            NativeAssetReview? row = rows.GetValueOrDefault(entry.Path);
            if (row is not null && !entry.Path.Equals(row.Path, StringComparison.Ordinal))
                throw Invalid("Native asset review spelling differs from its plan entry.");
            PublicationPathPolicy.ValidateRelativePath(entry.Path, row);
            if (!planned.Add(entry.Path)) throw Invalid("Publication paths collide.");
        }
        foreach (string path in planned)
            for (int slash = path.IndexOf('/'); slash >= 0; slash = path.IndexOf('/', slash + 1))
                if (planned.Contains(path[..slash])) throw Invalid("A publication file is also a parent directory.");
        if (rows.Keys.Any(path => !planned.Contains(path))) throw Invalid("A native asset review is unused.");
        return new NativeAssetReviewSet(entries, rows);
    }

    private static void ValidateRow(NativeAssetReview row)
    {
        UpdatePathPolicy.ValidateRelativePath(row.Path);
        UpdatePathPolicy.ValidateRelativePath(row.CollisionPath);
        if (row.Length is < 0 or > 4294967296L || row.ExpectedSourceSha256 is null ||
            row.ExpectedSourceSha256.Length != 64 || row.ExpectedSourceSha256.Any(c => !Uri.IsHexDigit(c)) ||
            string.IsNullOrEmpty(row.Evidence) || row.Evidence.Length > 128 ||
            row.Evidence.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_' and not '-'))
            throw Invalid("Native asset review metadata is invalid.");
        if (row.Category == ObjectDirectory)
        {
            string[] parts = row.CollisionPath.Split('/');
            string extension = System.IO.Path.GetExtension(row.Path);
            if (row.CollisionNodeType != "directory" || parts.Length < 5 ||
                parts[0] != "data" || parts[1] != "map" || parts[2] != "mapobj" || parts[^1] != "obj" ||
                !row.Path.StartsWith(row.CollisionPath + "/", StringComparison.Ordinal) ||
                !new[] { ".dds", ".bmp", ".png", ".tga", ".msk" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
                throw Invalid("Native object-directory review shape is invalid.");
        }
        else if (row.Category == LogsBitmap)
        {
            if (row.CollisionNodeType != "file" || row.Path != "data/main/LogS.BMP" || row.CollisionPath != row.Path)
                throw Invalid("Native bitmap review shape is invalid.");
        }
        else if (row.Category == ServerTypeLua || row.Category == LuaConfigCatalog)
        {
            string expected = row.Category == ServerTypeLua ? "ini/userdata.lua" : "ini/luacfg/userdata.dat";
            long maximum = row.Category == ServerTypeLua ? 65536 : 1048576;
            if (row.CollisionNodeType != "file" || row.Path != expected || row.CollisionPath != expected ||
                row.Length < 1 || row.Length > maximum)
                throw Invalid("Native data review shape is invalid.");
        }
        else throw Invalid("Unsupported native asset review category.");
    }

    private static InvalidDataException Invalid(string message) => new(message);
}
