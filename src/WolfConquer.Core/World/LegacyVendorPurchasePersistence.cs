using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyVendorPurchasePersistence
{
    private const string CharacterSection = "Character";
    private const string TransactionMarker = ".vendor-purchase-";

    private static readonly object PersistenceGate = new();

    // Trade reuses this transaction, with validation inside the existing gate -> file-lock
    // order. Taking those file locks outside TryPersist would invert vendor lock ordering.
    internal static bool TryPersistWithBaselineValidation(
        WorldSession originalFirst,
        WorldSession updatedFirst,
        WorldSession originalSecond,
        WorldSession updatedSecond,
        string? playerItemsRoot,
        Func<bool> validateLocked,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(validateLocked);
        lock (PersistenceGate)
        {
            return TryPersistCore(originalFirst, updatedFirst, originalSecond, updatedSecond,
                playerItemsRoot, out reason, validateLocked, mode: PersistenceMode.Trade);
        }
    }

    public static bool TryPersist(
        WorldSession originalBuyer,
        WorldSession updatedBuyer,
        WorldSession originalSeller,
        WorldSession updatedSeller,
        string? playerItemsRoot,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(originalBuyer);
        ArgumentNullException.ThrowIfNull(updatedBuyer);
        ArgumentNullException.ThrowIfNull(originalSeller);
        ArgumentNullException.ThrowIfNull(updatedSeller);

        lock (PersistenceGate)
        {
            return TryPersistCore(
                originalBuyer,
                updatedBuyer,
                originalSeller,
                updatedSeller,
                playerItemsRoot,
                out reason);
        }
    }

    private static bool TryPersistCore(
        WorldSession originalBuyer,
        WorldSession updatedBuyer,
        WorldSession originalSeller,
        WorldSession updatedSeller,
        string? playerItemsRoot,
        out string reason,
        Func<bool>? validateLocked = null,
        PersistenceMode mode = PersistenceMode.Compatibility)
    {
        reason = string.Empty;
        if (!TryValidateSessionPair(
                "buyer",
                originalBuyer,
                updatedBuyer,
                out var buyerUsersRoot,
                out reason) ||
            !TryValidateSessionPair(
                "seller",
                originalSeller,
                updatedSeller,
                out var sellerUsersRoot,
                out reason))
        {
            return false;
        }

        if (originalBuyer.Character.Uid == originalSeller.Character.Uid)
        {
            reason = "buyerSellerCharacterNotDistinct";
            return false;
        }

        if (!TryNormalizeRoot(
                playerItemsRoot,
                "playerItemsRootMissing",
                "playerItemsRootInvalid",
                out var normalizedPlayerItemsRoot,
                out reason))
        {
            return false;
        }

        if (File.Exists(normalizedPlayerItemsRoot))
        {
            reason = "playerItemsRootNotDirectory";
            return false;
        }

        var buyerCharacterPath = GetCharacterPath(buyerUsersRoot, updatedBuyer.AccountId);
        var sellerCharacterPath = GetCharacterPath(sellerUsersRoot, updatedSeller.AccountId);
        if (PathsEqual(buyerCharacterPath, sellerCharacterPath))
        {
            reason = "buyerSellerCharacterNotDistinct";
            return false;
        }

        var buyerItemPath = GetItemPath(normalizedPlayerItemsRoot, updatedBuyer.Character.Uid);
        var sellerItemPath = GetItemPath(normalizedPlayerItemsRoot, updatedSeller.Character.Uid);
        if (PathsEqual(buyerItemPath, sellerItemPath))
        {
            reason = "buyerSellerCharacterNotDistinct";
            return false;
        }

        var lockedReason = string.Empty;
        var persisted = SnapshotFileLockCoordinator.ExecuteLocked(
            [buyerCharacterPath, sellerCharacterPath, buyerItemPath, sellerItemPath],
            () =>
            {
                if (validateLocked is not null && !validateLocked())
                {
                    lockedReason = "snapshotBaselineMismatch";
                    return false;
                }
                return TryPersistLocked(
                buyerCharacterPath,
                sellerCharacterPath,
                buyerItemPath,
                sellerItemPath,
                updatedBuyer,
                updatedSeller,
                normalizedPlayerItemsRoot,
                out lockedReason,
                mode);
            });
        reason = lockedReason;
        return persisted;
    }

    private static bool TryPersistLocked(
        string buyerCharacterPath,
        string sellerCharacterPath,
        string buyerItemPath,
        string sellerItemPath,
        WorldSession updatedBuyer,
        WorldSession updatedSeller,
        string normalizedPlayerItemsRoot,
        out string reason,
        PersistenceMode mode)
    {
        reason = string.Empty;
        if (!TryPrepareCharacterUpdate(
                "buyer",
                buyerCharacterPath,
                updatedBuyer,
                out var buyerCharacterLines,
                out var buyerCharacterEncoding,
                out reason,
                clearTradeClaim: mode == PersistenceMode.Trade) ||
            !TryPrepareCharacterUpdate(
                "seller",
                sellerCharacterPath,
                updatedSeller,
                out var sellerCharacterLines,
                out var sellerCharacterEncoding,
                out reason,
                clearTradeClaim: mode == PersistenceMode.Trade))
        {
            return false;
        }

        byte[] buyerItemBytes;
        byte[] sellerItemBytes;
        try
        {
            buyerItemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(updatedBuyer.PlayerItems);
            sellerItemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(updatedSeller.PlayerItems);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            reason = "itemSnapshotSerializationFailed:" + ex.GetType().Name;
            return false;
        }

        if (mode != PersistenceMode.Compatibility)
        {
            return TryPersistJournal(
                new(buyerCharacterPath, buyerItemPath, updatedBuyer, buyerCharacterLines,
                    buyerCharacterEncoding, buyerItemBytes),
                new(sellerCharacterPath, sellerItemPath, updatedSeller, sellerCharacterLines,
                    sellerCharacterEncoding, sellerItemBytes),
                normalizedPlayerItemsRoot, mode, out reason);
        }

        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var targets = new[]
        {
            new TransactionFile(
                buyerCharacterPath,
                transactionId,
                tempPath => File.WriteAllLines(tempPath, buyerCharacterLines, buyerCharacterEncoding)),
            new TransactionFile(
                sellerCharacterPath,
                transactionId,
                tempPath => File.WriteAllLines(tempPath, sellerCharacterLines, sellerCharacterEncoding)),
            new TransactionFile(
                buyerItemPath,
                transactionId,
                tempPath => File.WriteAllBytes(tempPath, buyerItemBytes)),
            new TransactionFile(
                sellerItemPath,
                transactionId,
                tempPath => File.WriteAllBytes(tempPath, sellerItemBytes)),
        };

        var committed = false;
        var rollbackSucceeded = false;
        try
        {
            Directory.CreateDirectory(normalizedPlayerItemsRoot);
            foreach (var target in targets)
            {
                target.Stage();
            }

            foreach (var target in targets)
            {
                target.Commit();
            }

            committed = true;
        }
        catch (Exception ex) when (IsPersistenceException(ex))
        {
            rollbackSucceeded = TryRollback(targets);
            reason = rollbackSucceeded
                ? "transactionWriteFailed:" + ex.GetType().Name
                : "transactionRollbackFailed:" + ex.GetType().Name;
        }
        finally
        {
            foreach (var target in targets)
            {
                target.DeleteTemp();
                if (committed || rollbackSucceeded)
                {
                    target.DeleteBackup();
                }
            }
        }

        return committed;
    }

    private static bool TryValidateSessionPair(
        string role,
        WorldSession original,
        WorldSession updated,
        out string usersRoot,
        out string reason)
    {
        usersRoot = string.Empty;
        reason = string.Empty;
        if (original.AccountId != updated.AccountId ||
            original.Character.Uid != updated.Character.Uid)
        {
            reason = role + "SessionBindingMismatch";
            return false;
        }

        if (!TryNormalizeRoot(
                original.UsersRoot,
                role + "UsersRootMissing",
                role + "UsersRootInvalid",
                out var originalUsersRoot,
                out reason) ||
            !TryNormalizeRoot(
                updated.UsersRoot,
                role + "UsersRootMissing",
                role + "UsersRootInvalid",
                out var updatedUsersRoot,
                out reason))
        {
            return false;
        }

        if (!PathsEqual(originalUsersRoot, updatedUsersRoot))
        {
            reason = role + "UsersRootMismatch";
            return false;
        }

        usersRoot = updatedUsersRoot;
        return true;
    }

    private static bool TryPrepareCharacterUpdate(
        string role,
        string path,
        WorldSession updatedSession,
        out string[] updatedLines,
        out Encoding encoding,
        out string reason,
        bool clearTradeClaim)
    {
        updatedLines = [];
        encoding = CharacterSnapshotFileEncoding.DefaultWriteEncoding;
        reason = string.Empty;
        if (!File.Exists(path))
        {
            reason = role + "SnapshotMissing";
            return false;
        }

        string[] lines;
        try
        {
            lines = CharacterSnapshotFileEncoding.ReadAllLines(path, out encoding);
        }
        catch (IOException)
        {
            reason = role + "SnapshotReadFailed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = role + "SnapshotReadDenied";
            return false;
        }

        if (!TryValidateCharacterUid(lines, updatedSession.Character.Uid, out var validationReason))
        {
            reason = RoleReason(role, validationReason);
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = updatedSession.Character.Money.ToString(CultureInfo.InvariantCulture),
            ["ConquerPoints"] = updatedSession.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
        };
        if (clearTradeClaim)
            foreach (var value in LegacyTradeEscrowClaims.ClearValues) values[value.Key] = value.Value;
        if (!TryApplyCharacterValues(lines, values, out updatedLines))
        {
            reason = role + "CharacterSectionMissing";
            return false;
        }

        return true;
    }

    private static byte[] EncodeTradeCharacterLines(string[] lines, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        var text = encoding.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        var bytes = new byte[preamble.Length + text.Length];
        preamble.CopyTo(bytes, 0);
        text.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static bool TryValidateCharacterUid(
        IReadOnlyList<string> lines,
        long expectedCharacterUid,
        out string reason)
    {
        reason = string.Empty;
        var inCharacterSection = false;
        var foundCharacterSection = false;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var sectionName = trimmed[1..^1].Trim();
                inCharacterSection = string.Equals(
                    sectionName,
                    CharacterSection,
                    StringComparison.OrdinalIgnoreCase);
                foundCharacterSection |= inCharacterSection;
                continue;
            }

            if (!inCharacterSection ||
                trimmed.Length == 0 ||
                trimmed.StartsWith(';') ||
                trimmed.StartsWith('#'))
            {
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0 ||
                !string.Equals(
                    rawLine[..equalsIndex].Trim(),
                    "UID",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rawUid = rawLine[(equalsIndex + 1)..].Trim();
            if (!long.TryParse(rawUid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid))
            {
                reason = "uidInvalid";
                return false;
            }

            if (uid != expectedCharacterUid)
            {
                reason = "uidMismatch";
                return false;
            }

            return true;
        }

        reason = foundCharacterSection ? "uidMissing" : "characterSectionMissing";
        return false;
    }

    private static bool TryApplyCharacterValues(
        string[] lines,
        IReadOnlyDictionary<string, string> values,
        out string[] updatedLines)
    {
        updatedLines = lines;
        var inCharacterSection = false;
        var foundCharacterSection = false;
        var insertIndex = lines.Length;
        var remaining = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < lines.Length; index++)
        {
            var rawLine = lines[index];
            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (inCharacterSection)
                {
                    insertIndex = index;
                }

                var sectionName = trimmed[1..^1].Trim();
                inCharacterSection = string.Equals(
                    sectionName,
                    CharacterSection,
                    StringComparison.OrdinalIgnoreCase);
                foundCharacterSection |= inCharacterSection;
                if (inCharacterSection)
                {
                    insertIndex = index + 1;
                }

                continue;
            }

            if (!inCharacterSection)
            {
                continue;
            }

            insertIndex = index + 1;
            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 0 ||
                !remaining.Remove(rawLine[..equalsIndex].Trim(), out var value))
            {
                continue;
            }

            lines[index] = rawLine[..(equalsIndex + 1)] + value;
        }

        if (!foundCharacterSection)
        {
            return false;
        }

        if (remaining.Count > 0)
        {
            updatedLines = lines
                .Take(insertIndex)
                .Concat(remaining.Select(pair => pair.Key + "=" + pair.Value))
                .Concat(lines.Skip(insertIndex))
                .ToArray();
        }

        return true;
    }

    private static bool TryNormalizeRoot(
        string? root,
        string missingReason,
        string invalidReason,
        out string normalizedRoot,
        out string reason)
    {
        normalizedRoot = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
        {
            reason = missingReason;
            return false;
        }

        try
        {
            normalizedRoot = Path.GetFullPath(root);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            reason = invalidReason;
            return false;
        }
    }

    private static bool TryRollback(IReadOnlyList<TransactionFile> targets)
    {
        var restored = true;
        for (var index = targets.Count - 1; index >= 0; index--)
        {
            restored &= targets[index].TryRollback();
        }

        return restored;
    }

    private static bool IsPersistenceException(Exception ex)
    {
        return ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            OverflowException;
    }

    private static string GetCharacterPath(string usersRoot, long accountId)
    {
        return Path.Combine(usersRoot, accountId.ToString(CultureInfo.InvariantCulture) + ".ini");
    }

    private static string GetItemPath(string playerItemsRoot, long characterUid)
    {
        return Path.Combine(playerItemsRoot, characterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string RoleReason(string role, string reason)
    {
        return reason.Length == 0
            ? role
            : role + char.ToUpperInvariant(reason[0]) + reason[1..];
    }

    private sealed class TransactionFile
    {
        private readonly Action<string> _writeTemp;

        public TransactionFile(string path, string transactionId, Action<string> writeTemp)
        {
            Path = path;
            TempPath = path + TransactionMarker + transactionId + ".tmp";
            BackupPath = path + TransactionMarker + transactionId + ".bak";
            Existed = File.Exists(path);
            _writeTemp = writeTemp;
        }

        public string Path { get; }

        public string TempPath { get; }

        public string BackupPath { get; }

        public bool Existed { get; }

        public bool Committed { get; private set; }

        public void Stage()
        {
            _writeTemp(TempPath);
        }

        public void Commit()
        {
            if (Existed)
            {
                File.Replace(TempPath, Path, BackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(TempPath, Path);
            }

            Committed = true;
        }

        public bool TryRollback()
        {
            if (!Committed)
            {
                return true;
            }

            try
            {
                if (!Existed)
                {
                    if (File.Exists(Path))
                    {
                        File.Delete(Path);
                    }

                    return true;
                }

                if (!File.Exists(BackupPath))
                {
                    return false;
                }

                if (File.Exists(Path))
                {
                    File.Replace(BackupPath, Path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(BackupPath, Path);
                }

                return true;
            }
            catch (Exception ex) when (IsPersistenceException(ex))
            {
                return false;
            }
        }

        public void DeleteTemp()
        {
            TryDelete(TempPath);
        }

        public void DeleteBackup()
        {
            TryDelete(BackupPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (IsPersistenceException(ex))
            {
            }
        }
    }
}
