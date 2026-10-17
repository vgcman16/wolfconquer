using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

internal static class CharacterSnapshotFileCoordinator
{
    private const string CharacterSection = "Character";
    private const int PersistenceAttemptCount = 5;
    private const int RetryDelayMilliseconds = 10;
    private const string TransactionMarker = ".character-file-";

    internal static TResult ExecuteLocked<TResult>(string path, Func<TResult> action)
    {
        return SnapshotFileLockCoordinator.ExecuteLocked(path, action);
    }

    internal static TResult ExecuteLocked<TResult>(
        IReadOnlyCollection<string> paths,
        Func<TResult> action)
    {
        return SnapshotFileLockCoordinator.ExecuteLocked(paths, action);
    }

    internal static bool TryUpdateCharacterAttributes(
        string path,
        long? expectedCharacterUid,
        IReadOnlyDictionary<string, string> values,
        bool detailedIoFailures,
        out string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(values);

        return TryUpdateSections(
            path,
            expectedCharacterUid,
            [
                new CharacterSnapshotSectionUpdate(
                    CharacterSection,
                    values,
                    CreateIfMissing: false,
                    MissingReason: "characterSectionMissing"),
            ],
            detailedIoFailures,
            out reason);
    }

    internal static bool TryUpdateSections(
        string path,
        long? expectedCharacterUid,
        IReadOnlyList<CharacterSnapshotSectionUpdate> sections,
        bool detailedIoFailures,
        out string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(sections);
        foreach (var section in sections)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(section.SectionName);
            ArgumentNullException.ThrowIfNull(section.Values);
            ArgumentException.ThrowIfNullOrWhiteSpace(section.MissingReason);
        }

        reason = string.Empty;

        if (!File.Exists(path))
        {
            reason = "snapshotMissing";
            return false;
        }

        var operationReason = string.Empty;
        var succeeded = ExecuteLocked(path, () =>
        {
            for (var attempt = 1; attempt <= PersistenceAttemptCount; attempt++)
            {
                string[] lines;
                Encoding encoding;
                try
                {
                    lines = CharacterSnapshotFileEncoding.ReadAllLines(path, out encoding);
                }
                catch (IOException exception)
                {
                    if (attempt < PersistenceAttemptCount)
                    {
                        WaitBeforeRetry(attempt);
                        continue;
                    }

                    operationReason = BuildIoFailureReason(
                        "snapshotReadFailed",
                        exception,
                        attempt,
                        detailedIoFailures);
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    operationReason = "snapshotReadDenied";
                    return false;
                }

                if (expectedCharacterUid is { } expectedUid &&
                    !TryValidateCharacterUid(lines, expectedUid, out operationReason))
                {
                    return false;
                }

                var updatedLines = lines;
                foreach (var section in sections)
                {
                    if (CharacterSnapshotSectionLineTransformer.TryApply(
                            updatedLines,
                            section.SectionName,
                            section.Values,
                            section.CreateIfMissing,
                            out updatedLines))
                    {
                        continue;
                    }

                    operationReason = section.MissingReason;
                    return false;
                }

                try
                {
                    WriteAllLinesAtomically(path, updatedLines, encoding);
                    return true;
                }
                catch (IOException exception)
                {
                    if (attempt < PersistenceAttemptCount)
                    {
                        WaitBeforeRetry(attempt);
                        continue;
                    }

                    operationReason = BuildIoFailureReason(
                        "snapshotWriteFailed",
                        exception,
                        attempt,
                        detailedIoFailures);
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    operationReason = "snapshotWriteDenied";
                    return false;
                }
            }

            throw new InvalidOperationException(
                "Character snapshot persistence retry loop exited unexpectedly.");
        });
        reason = operationReason;
        return succeeded;
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

    private static void WriteAllLinesAtomically(string path, string[] lines, Encoding encoding)
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var tempPath = path + TransactionMarker + transactionId + ".tmp";
        var backupPath = path + TransactionMarker + transactionId + ".bak";
        var keepBackup = false;

        try
        {
            File.WriteAllLines(tempPath, lines, encoding);
            File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
        }
        catch
        {
            if (File.Exists(backupPath))
            {
                keepBackup = !TryRestoreBackup(backupPath, path);
            }

            throw;
        }
        finally
        {
            TryDeleteTransactionFile(tempPath);
            if (!keepBackup)
            {
                TryDeleteTransactionFile(backupPath);
            }
        }
    }

    private static bool TryRestoreBackup(string backupPath, string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Replace(backupPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(backupPath, path);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteTransactionFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void WaitBeforeRetry(int attempt)
    {
        Thread.Sleep(RetryDelayMilliseconds * attempt);
    }

    private static string BuildIoFailureReason(
        string reason,
        IOException exception,
        int attempts,
        bool detailed)
    {
        if (!detailed)
        {
            return reason;
        }

        return reason +
            ":attempts=" + attempts.ToString(CultureInfo.InvariantCulture) +
            ":hresult=0x" + exception.HResult.ToString("X8", CultureInfo.InvariantCulture);
    }
}
