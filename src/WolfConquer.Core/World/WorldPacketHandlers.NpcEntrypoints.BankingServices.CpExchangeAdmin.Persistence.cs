using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPersistLegacyCpExchangeAdminMutation(
        WorldSession originalSession,
        WorldSession updatedSession,
        string? playerItemsRoot,
        IReadOnlyDictionary<string, string> characterValues,
        bool persistPlayerItems,
        out string characterPath,
        out string playerItemsPath,
        out string reason)
    {
        characterPath = string.Empty;
        playerItemsPath = string.Empty;
        reason = string.Empty;

        if (!TryResolveLegacyCpExchangeRoot(
                originalSession.UsersRoot,
                "usersRootMissing",
                "usersRootInvalid",
                out var usersRoot,
                out reason))
        {
            return false;
        }

        if (originalSession.AccountId != updatedSession.AccountId ||
            originalSession.Character.Uid != updatedSession.Character.Uid)
        {
            reason = "sessionBindingMismatch";
            return false;
        }

        characterPath = Path.Combine(
            usersRoot,
            originalSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var lockedPaths = new List<string> { characterPath };
        if (persistPlayerItems)
        {
            if (!TryResolveLegacyCpExchangeRoot(
                    playerItemsRoot,
                    "playerItemsRootMissing",
                    "playerItemsRootInvalid",
                    out var resolvedPlayerItemsRoot,
                    out reason))
            {
                return false;
            }

            playerItemsPath = Path.Combine(
                resolvedPlayerItemsRoot,
                originalSession.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            lockedPaths.Add(playerItemsPath);
        }

        try
        {
            var resolvedCharacterPath = characterPath;
            var resolvedPlayerItemsPath = playerItemsPath;
            var lockedReason = string.Empty;
            var persisted = SnapshotFileLockCoordinator.ExecuteLocked(
                lockedPaths,
                () => TryPersistLegacyCpExchangeAdminMutationLocked(
                    originalSession,
                    updatedSession,
                    characterValues,
                    resolvedCharacterPath,
                    resolvedPlayerItemsPath,
                    persistPlayerItems,
                    out lockedReason));
            reason = lockedReason;
            return persisted;
        }
        catch (Exception ex) when (IsLegacyCpExchangePersistenceException(ex))
        {
            reason = "persistenceCoordinatorFailed:" + ex.GetType().Name;
            return false;
        }
    }

    private static bool TryPersistLegacyCpExchangeAdminMutationLocked(
        WorldSession originalSession,
        WorldSession updatedSession,
        IReadOnlyDictionary<string, string> characterValues,
        string characterPath,
        string playerItemsPath,
        bool persistPlayerItems,
        out string reason)
    {
        reason = string.Empty;
        byte[]? originalPlayerItemsBytes = null;

        if (persistPlayerItems)
        {
            if (!TryPersistLegacyCpExchangePlayerItems(
                    originalSession,
                    updatedSession,
                    playerItemsPath,
                    out originalPlayerItemsBytes,
                    out reason))
            {
                return false;
            }
        }

        if (!TryValidateLegacyCpExchangeCharacterBaseline(
                characterPath,
                originalSession,
                characterValues.Keys,
                out var baselineReason))
        {
            return TryRollbackLegacyCpExchangePlayerItemsAfterFailure(
                playerItemsPath,
                originalPlayerItemsBytes,
                baselineReason,
                out reason);
        }

        if (!TryPersistLegacyCharacterAttributes(
                originalSession.UsersRoot,
                originalSession.AccountId,
                originalSession.Character.Uid,
                characterValues,
                out _,
                out var characterReason))
        {
            return TryRollbackLegacyCpExchangePlayerItemsAfterFailure(
                playerItemsPath,
                originalPlayerItemsBytes,
                "characterPersistenceFailed:" + characterReason,
                out reason);
        }

        return true;
    }

    private static bool TryPersistLegacyCpExchangePlayerItems(
        WorldSession originalSession,
        WorldSession updatedSession,
        string playerItemsPath,
        out byte[]? originalBytes,
        out string reason)
    {
        originalBytes = null;
        reason = string.Empty;
        byte[] expectedOriginalBytes;
        byte[] updatedBytes;
        try
        {
            originalBytes = PlayerItemSnapshotFilePersistence.ReadAllBytesIfExists(playerItemsPath);
            expectedOriginalBytes = PlayerItemSnapshotLoader.ToLegacyBytes(originalSession.PlayerItems);
            updatedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(updatedSession.PlayerItems);
        }
        catch (Exception ex) when (IsLegacyCpExchangePersistenceException(ex))
        {
            reason = "itemPersistenceReadFailed:" + ex.GetType().Name;
            return false;
        }

        if (originalBytes is null)
        {
            reason = "playerItemsSnapshotMissing";
            return false;
        }

        if (!originalBytes.AsSpan().SequenceEqual(expectedOriginalBytes))
        {
            reason = "playerItemsBaselineMismatch";
            return false;
        }

        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(playerItemsPath, updatedBytes);
            return true;
        }
        catch (Exception ex) when (IsLegacyCpExchangePersistenceException(ex))
        {
            reason = "itemPersistenceFailed:" + ex.GetType().Name;
            return false;
        }
    }

    private static bool TryValidateLegacyCpExchangeCharacterBaseline(
        string characterPath,
        WorldSession originalSession,
        IEnumerable<string> persistedKeys,
        out string reason)
    {
        reason = string.Empty;
        if (!File.Exists(characterPath))
        {
            reason = "characterPersistenceFailed:snapshotMissing";
            return false;
        }

        IReadOnlyDictionary<string, string> persisted;
        try
        {
            var document = IniDocument.Parse(
                CharacterSnapshotFileEncoding.ReadAllText(characterPath, out _));
            if (!document.Sections.TryGetValue("Character", out persisted!))
            {
                reason = "characterPersistenceFailed:characterSectionMissing";
                return false;
            }
        }
        catch (Exception ex) when (IsLegacyCpExchangePersistenceException(ex))
        {
            reason = "characterBaselineReadFailed:" + ex.GetType().Name;
            return false;
        }

        if (!TryReadLegacyCpExchangeCharacterBaselineValue(
                persisted,
                "UID",
                out var persistedUid,
                out var uidReason))
        {
            reason = "characterPersistenceFailed:" + uidReason;
            return false;
        }

        if (persistedUid != originalSession.Character.Uid)
        {
            reason = "characterPersistenceFailed:uidMismatch";
            return false;
        }

        foreach (var key in persistedKeys)
        {
            long expectedValue;
            if (key.Equals("ConquerPoints", StringComparison.OrdinalIgnoreCase))
            {
                expectedValue = originalSession.Character.ConquerPoints;
            }
            else if (key.Equals("Money", StringComparison.OrdinalIgnoreCase))
            {
                expectedValue = originalSession.Character.Money;
            }
            else if (key.Equals("BankCps", StringComparison.OrdinalIgnoreCase))
            {
                expectedValue = originalSession.Character.BankConquerPoints;
            }
            else if (key.Equals("ChiPoints", StringComparison.OrdinalIgnoreCase))
            {
                expectedValue = LegacyChiState.FromSnapshot(originalSession.Character).ChiPoints;
            }
            else
            {
                continue;
            }

            if (!TryReadLegacyCpExchangeCharacterBaselineValue(
                    persisted,
                    key,
                    out var persistedValue,
                    out var valueReason))
            {
                reason = "characterBaselineReadFailed:" + valueReason;
                return false;
            }

            if (persistedValue != expectedValue)
            {
                reason = "characterBaselineMismatch:" + key;
                return false;
            }
        }

        return true;
    }

    private static bool TryReadLegacyCpExchangeCharacterBaselineValue(
        IReadOnlyDictionary<string, string> values,
        string key,
        out long value,
        out string reason)
    {
        value = 0;
        reason = string.Empty;
        if (!values.TryGetValue(key, out var rawValue))
        {
            reason = key.Equals("UID", StringComparison.OrdinalIgnoreCase)
                ? "uidMissing"
                : key + "Missing";
            return false;
        }

        if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            reason = key.Equals("UID", StringComparison.OrdinalIgnoreCase)
                ? "uidInvalid"
                : key + "Invalid";
            return false;
        }

        return true;
    }

    private static bool TryRollbackLegacyCpExchangePlayerItemsAfterFailure(
        string playerItemsPath,
        byte[]? originalPlayerItemsBytes,
        string failureReason,
        out string reason)
    {
        reason = failureReason;
        if (originalPlayerItemsBytes is null)
        {
            return false;
        }

        try
        {
            PlayerItemSnapshotFilePersistence.WriteAllBytesAtomically(
                playerItemsPath,
                originalPlayerItemsBytes);
            reason += ";itemRollback=none";
        }
        catch (Exception ex) when (IsLegacyCpExchangePersistenceException(ex))
        {
            reason += ";itemRollback=" + ex.GetType().Name;
        }

        return false;
    }

    private static bool TryResolveLegacyCpExchangeRoot(
        string? root,
        string missingReason,
        string invalidReason,
        out string resolvedRoot,
        out string reason)
    {
        resolvedRoot = string.Empty;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
        {
            reason = missingReason;
            return false;
        }

        try
        {
            resolvedRoot = Path.GetFullPath(root);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            reason = invalidReason;
            return false;
        }
    }

    private static bool IsLegacyCpExchangePersistenceException(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            FormatException or
            OverflowException;
    }
}
