using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeEscrowClaims
{
    private sealed record DiskWallet(long Uid, long Money, long ConquerPoints, LegacyTradeEscrowClaim? Claim);

    // Unlike the permissive legacy loader, recovery must never pick the first/last of duplicate fields.
    private static bool TryReadDisk(string path, out DiskWallet disk, out string reason)
    {
        disk = null!;
        reason = string.Empty;
        if (!File.Exists(path)) { reason = "tradeEscrowSnapshotMissing"; return false; }
        var lines = CharacterSnapshotFileEncoding.ReadAllLines(path, out _);
        return TryParseDisk(lines, out disk, out reason);
    }

    // Recovery is not a new global character-schema validator. Native wallet loading supplies
    // defaults; an unrelated no-claim legacy file must not block every other account's admission.
    // Any known claim field, even a key missing its '=', opts the record into strict validation.
    private static bool TryReadRecoveryDisk(string path, out DiskWallet? disk, out string reason)
    {
        disk = null;
        reason = string.Empty;
        if (!File.Exists(path)) { reason = "tradeEscrowSnapshotMissing"; return false; }
        var lines = CharacterSnapshotFileEncoding.ReadAllLines(path, out _);
        var inCharacter = false;
        foreach (var raw in lines)
        {
            var text = raw.Trim();
            if (text.StartsWith('[') && text.EndsWith(']'))
            {
                inCharacter = text[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inCharacter || text.Length == 0 || text.StartsWith(';') || text.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            var key = equals >= 0 ? raw[..equals].Trim() : text;
            if (!IsClaimKey(key)) continue;
            if (!TryParseDisk(lines, out var strictDisk, out reason)) return false;
            disk = strictDisk;
            return true;
        }
        return true;
    }

    private static bool TryParseDisk(IReadOnlyList<string> lines, out DiskWallet disk, out string reason)
    {
        disk = null!;
        reason = string.Empty;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inCharacter = false;
        var characterSections = 0;
        foreach (var raw in lines)
        {
            var text = raw.Trim();
            if (text.StartsWith('[') && text.EndsWith(']'))
            {
                inCharacter = text[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (inCharacter && ++characterSections != 1)
                { reason = "tradeEscrowCharacterSectionAmbiguous"; return false; }
                continue;
            }
            if (!inCharacter || text.Length == 0 || text.StartsWith(';') || text.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            if (equals <= 0)
            { reason = "tradeEscrowCharacterLineInvalid"; return false; }
            var key = raw[..equals].Trim();
            if (!IsClaimKey(key) && !key.Equals("UID", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("Money", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("ConquerPoints", StringComparison.OrdinalIgnoreCase)) continue;
            // Keep raw values: whitespace/signs/leading zeroes are not canonical recovery amounts.
            if (!fields.TryAdd(key, raw[(equals + 1)..]))
            { reason = "tradeEscrowDiskFieldsAmbiguous"; return false; }
        }
        if (characterSections != 1 || !fields.TryGetValue("UID", out var uidText) ||
            !TryCanonicalLong(uidText, out var uid) || uid == 0 ||
            !fields.TryGetValue("Money", out var moneyText) || !TryCanonicalLong(moneyText, out var money) ||
            !fields.TryGetValue("ConquerPoints", out var cpText) || !TryCanonicalLong(cpText, out var cp) ||
            cp > uint.MaxValue)
        { reason = "tradeEscrowDiskWalletInvalid"; return false; }
        if (!TryRead(fields, out var claim, out reason)) return false;
        disk = new(uid, money, cp, claim);
        return true;
    }

    private static bool TryValidatePath(string path, bool directory, out string reason)
    {
        reason = string.Empty;
        FileSystemInfo entry = directory ? new DirectoryInfo(Path.GetFullPath(path)) : new FileInfo(Path.GetFullPath(path));
        entry.Refresh();
        if (!entry.Exists)
        { reason = directory ? "tradeEscrowUsersRootMissing" : "tradeEscrowSnapshotMissing"; return false; }
        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
        { reason = "tradeEscrowReparsePathRejected"; return false; }
        var parent = directory ? ((DirectoryInfo)entry).Parent : ((FileInfo)entry).Directory;
        while (parent is not null)
        {
            parent.Refresh();
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
            { reason = "tradeEscrowReparsePathRejected"; return false; }
            parent = parent.Parent;
        }
        return true;
    }
}
