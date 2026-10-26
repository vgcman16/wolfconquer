using System.Text.RegularExpressions;
using WolfConquer.Updates.Manifest;

namespace WolfConquer.Updates.Publishing;

// Future publication admission only. Existing signed manifests and repointing
// retain their structural/wire policy. Names cannot establish content safety.
public static partial class PublicationPathPolicy
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex LocalOrControlNames = Pattern(
        @"^(accounts?|profiles?|users?|userdata|user[-_]?data|logs?|cache|caches|screenshots?|screen[-_]?shots|temp|tmp|debug|backups?|crashdumps?|\.git|\.svn|\.hg|\.vs|obj|bin|\.wolfconquer-updates|\.wolfconquer-cache)($|[._-])");
    private static readonly Regex[] PrivateOrGeneratedNames =
    [
        Pattern(@"(\.(pem|key|pfx|p12|ppk|jks|snk|kdbx|wallet|pdb|dmp|dump|ilk|idb|iobj|ipdb|obj|log|bak|old|tmp|temp|orig|sql|db|db3|mdb|sqlite|sqlite3|sav|suo|user)$)|^(\.env($|\.)|id_(rsa|dsa|ecdsa|ed25519)($|\.)|\.git(ignore|attributes|modules)?$)"),
        Pattern(@"\.(bak|old|orig|tmp|temp)[._-]"),
        Pattern(@"^(accounts?|profiles?|credentials?|passwords?|secrets?|private[-_]?keys?|tokens?|cookies|sessions?)([._-].*)?$"),
        Pattern(@"(^|[._-])(secret|credential|password|private[-_]?key|release[-_]?private)([._-]|$)|^(last[-_]?login|login[-_]?(history|records?)|recent[-_]?players?)([._-]|$)"),
        Pattern(@"^(wolfconquer-updates\.json|trusted-release-keys\.json|WolfConquer\.(Updater|Launcher)(\..*)?)$"),
    ];

    public static void ValidateRelativePath(string path) => ValidateRelativePath(path, null);

    internal static void ValidateRelativePath(string path, NativeAssetReview? review)
    {
        // Bounds are established before matching: at most 1024 path characters,
        // 255 per segment. Do not run filename quarantine over absolute roots.
        UpdatePathPolicy.ValidateRelativePath(path);
        string? reason;
        try { reason = QuarantineReason(path, review); }
        catch (RegexMatchTimeoutException)
        {
            // Regex exceptions can retain the input. Keep it out of diagnostics.
            throw new InvalidDataException("Publication filename quarantine did not finish safely.");
        }
        if (reason is not null)
            throw new InvalidDataException("Publication filename quarantine: " + reason + ".");
    }

    private static string? QuarantineReason(string path, NativeAssetReview? review)
    {
        // Same ordered segment categories as Get-CdRisk in the offline tools.
        int prefixLength = 0;
        bool reviewedCollision = false;
        foreach (string segment in path.Split('/'))
        {
            prefixLength += segment.Length;
            bool waived = IsReviewedCollision(path, path[..prefixLength], review);
            prefixLength++;
            string? reason = SegmentReason(segment);
            if (reason is null) continue;
            if (!waived) return reason;
            reviewedCollision = true;
        }
        if (review is not null && !reviewedCollision)
            throw new InvalidDataException("Native asset review does not select a quarantined collision.");
        return null;
    }

    private static string? SegmentReason(string segment)
    {
        if (segment.Equals("AutoP.exe", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("._cache_AutoP.exe", StringComparison.OrdinalIgnoreCase))
            return "legacy-autop-bootstrap-requires-separate-curation";
        if (LocalOrControlNames.IsMatch(segment)) return "private-local-or-control-path";
        foreach (Regex pattern in PrivateOrGeneratedNames)
            if (pattern.IsMatch(segment)) return "secret-private-or-generated-file-name";
        return null;
    }

    private static Regex Pattern(string expression) => new(expression, Options, MatchTimeout);
}
