using System.Globalization;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class CharacterSnapshotFileCoordinator
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
        => TryUpdateSectionsCore(path, expectedCharacterUid, sections, detailedIoFailures, null, out reason);

    internal static bool TryUpdateExperienceEscrowRecovery(string path, long expectedCharacterUid,
        IReadOnlyDictionary<string, string> values, LegacyTradeRecoveryState.ExperienceEscrowRecoveryPermit permit,
        out string reason)
    {
        string[] allowed = ["Money", "ConquerPoints", LegacyTradeEscrowClaims.IdKey,
            LegacyTradeEscrowClaims.MoneyKey, LegacyTradeEscrowClaims.CpKey];
        if (values.Count != allowed.Length || allowed.Any(key => !values.ContainsKey(key)) ||
            values.Keys.Any(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase)))
        { reason = "experienceEscrowRecoveryFieldsInvalid"; return false; }
        return TryUpdateSectionsCore(path, expectedCharacterUid,
            [new CharacterSnapshotSectionUpdate(CharacterSection, values, false, "characterSectionMissing")], false, permit, out reason);
    }

    private static bool TryUpdateSectionsCore(string path, long? expectedCharacterUid,
        IReadOnlyList<CharacterSnapshotSectionUpdate> sections, bool detailedIoFailures,
        LegacyTradeRecoveryState.ExperienceEscrowRecoveryPermit? recovery, out string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(sections);
        foreach (var section in sections)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(section.SectionName);
            ArgumentNullException.ThrowIfNull(section.Values);
            ArgumentException.ThrowIfNullOrWhiteSpace(section.MissingReason);
            if (section.SectionName.Equals(CharacterSection, StringComparison.OrdinalIgnoreCase) &&
                section.Values.Keys.Any(key => key.Equals(LegacyExperienceContinuationCodec.Key, StringComparison.OrdinalIgnoreCase)))
            { reason = "experienceContinuationPendingRequiresOwner"; return false; }
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

                bool pending = LegacyExperienceContinuationFence.ContainsCharacterMarker(lines);
                byte[]? exactRecoveryBefore = null;
                LegacyExperienceContinuation? capturedPending = null;
                long recoveryAccount = 0; uint recoveryUid = 0;
                if (pending)
                {
                    if (recovery is null || !recovery.IsCurrentFor(path) || expectedCharacterUid is null ||
                        expectedCharacterUid.Value <= 0 || expectedCharacterUid.Value > uint.MaxValue ||
                        sections.Count != 1 || !LegacyTradeEscrowClaims.MatchesExperienceEscrowRefund(path, expectedCharacterUid.Value, sections[0].Values) ||
                        !long.TryParse(Path.GetFileNameWithoutExtension(path), NumberStyles.None, CultureInfo.InvariantCulture, out var account) ||
                        !CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceEscrowRecovery", out var beforeImage, out _))
                    { operationReason = "experienceContinuationPendingRequiresOwner"; return false; }
                    try
                    {
                        capturedPending = LegacyExperienceContinuationCodec.ReadCharacterImage(beforeImage, account, checked((uint)expectedCharacterUid.Value));
                        if (capturedPending is null)
                        { operationReason = "experienceContinuationPendingRequiresOwner"; return false; }
                        exactRecoveryBefore = beforeImage; recoveryAccount = account; recoveryUid = checked((uint)expectedCharacterUid.Value);
                    }
                    catch (Exception error) when (error is InvalidDataException or ArgumentException or OverflowException)
                    { operationReason = "experienceContinuationPendingInvalid"; return false; }
                }

                if (expectedCharacterUid is { } expectedUid &&
                    !TryValidateCharacterUid(lines, expectedUid, out operationReason))
                {
                    return false;
                }

                // The transformer mutates its input array in place. Retain the
                // original ordered protected rows for the recovery-only proof.
                var originalLines = pending ? lines.ToArray() : lines;
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

                if (pending && (recovery is null || !recovery.IsCurrentFor(path) ||
                    !LegacyExperienceContinuationFence.RecoveryProtectedLinesUnchanged(originalLines, updatedLines)))
                { operationReason = "experienceContinuationRecoveryProjectionChanged"; return false; }

                try
                {
                    if (pending)
                    {
                        if (exactRecoveryBefore is null || capturedPending is null || recovery is null || !recovery.IsCurrentFor(path) ||
                            !CharacterSnapshotFileBaseline.TryReadBytes(path, "experienceEscrowRecoveryBeforeWrite", out var currentImage, out _) ||
                            !currentImage.AsSpan().SequenceEqual(exactRecoveryBefore))
                        { operationReason = "experienceContinuationRecoveryBaselineChanged"; return false; }
                        var originalText = CharacterSnapshotFileEncoding.Decode(exactRecoveryBefore, out var originalEncoding);
                        var projected = ProjectExperienceEscrow(originalText, sections[0].Values);
                        var afterImage = originalEncoding.GetPreamble().Concat(originalEncoding.GetBytes(projected)).ToArray();
                        if (!Equals(capturedPending, LegacyExperienceContinuationCodec.ReadCharacterImage(afterImage, recoveryAccount, recoveryUid)))
                        { operationReason = "experienceContinuationRecoveryProjectionChanged"; return false; }
                        WriteAtomically(path, temporary => File.WriteAllBytes(temporary, afterImage), retainBackup: false, out _);
                    }
                    else WriteAllLinesAtomically(path, updatedLines, encoding);
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
                catch (Exception error) when (pending && error is (InvalidDataException or ArgumentException or OverflowException or NotSupportedException))
                {
                    operationReason = "experienceContinuationRecoveryProjectionInvalid:" + error.GetType().Name;
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
        => WriteAtomically(path, temporary => File.WriteAllLines(temporary, lines, encoding), retainBackup: false, out _);

    private static void WriteAtomically(string path, Action<string> writeTemporary, bool retainBackup, out string backupPath)
        => WriteAtomically(path, writeTemporary, retainBackup, createOnly: false, out backupPath, out _);

    private static void WriteAtomically(string path, Action<string> writeTemporary, bool retainBackup,
        bool createOnly, out string backupPath, out bool published)
    {
        published = false;
        WolfConquer.Core.World.LegacyMailboxPackageMutationFence.RequireFileMutation(path);
        var transactionId = Guid.NewGuid().ToString("N");
        var tempPath = path + TransactionMarker + transactionId + ".tmp";
        backupPath = createOnly ? string.Empty : path + TransactionMarker + transactionId + ".bak";
        var keepBackup = retainBackup;

        try
        {
            writeTemporary(tempPath);
            if (createOnly) File.Move(tempPath, path, overwrite: false);
            else File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
            published = true;
        }
        catch
        {
            if (!createOnly && File.Exists(backupPath))
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
