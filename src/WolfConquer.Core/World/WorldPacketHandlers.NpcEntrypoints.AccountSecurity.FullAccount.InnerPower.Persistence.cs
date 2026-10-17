using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static readonly Encoding LegacyInnerPowerTextEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private static readonly object LegacyFullAccountInnerPowerPersistenceSync = new();

    private static bool TryPersistLegacyFullAccountInnerPowerMutation(
        WorldSession updatedSession,
        InnerPowerRankEntry updatedRow,
        out string reason)
    {
        lock (LegacyFullAccountInnerPowerPersistenceSync)
        {
            return TryPersistLegacyFullAccountInnerPowerMutationLocked(
                updatedSession,
                updatedRow,
                out reason);
        }
    }

    private static bool TryPersistLegacyFullAccountInnerPowerMutationLocked(
        WorldSession updatedSession,
        InnerPowerRankEntry updatedRow,
        out string reason)
    {
        reason = string.Empty;
        var dbRoot = TryDeriveLegacyDbRoot(updatedSession.UsersRoot);
        if (dbRoot is null)
        {
            reason = "db-root-missing";
            return false;
        }

        var characterPath = Path.Combine(
            updatedSession.UsersRoot!,
            updatedSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        var innerPowerPath = Path.Combine(dbRoot, "InnerPower.txt");
        if (!File.Exists(characterPath) || !File.Exists(innerPowerPath))
        {
            reason = !File.Exists(characterPath)
                ? "snapshot-missing"
                : "inner-power-file-missing";
            return false;
        }

        string[] characterLines;
        string[] innerPowerLines;
        IReadOnlyList<InnerPowerRankEntry> persistedRows;
        try
        {
            characterLines = File.ReadAllLines(characterPath, LegacyCharacterSnapshotEncoding);
            var innerPowerText = File.ReadAllText(innerPowerPath, LegacyInnerPowerTextEncoding);
            innerPowerLines = File.ReadAllLines(innerPowerPath, LegacyInnerPowerTextEncoding);
            persistedRows = RankingSnapshotParser.ParseInnerPowerRanks(
                innerPowerText,
                Path.GetFileName(innerPowerPath));
        }
        catch (FormatException)
        {
            reason = "inner-power-file-invalid";
            return false;
        }
        catch (DecoderFallbackException)
        {
            reason = "inner-power-file-encoding-invalid";
            return false;
        }
        catch (IOException)
        {
            reason = "persistence-read-failed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "persistence-read-denied";
            return false;
        }

        if (!TryValidateLegacyCharacterUid(characterLines, updatedSession.Character.Uid, out reason) ||
            !TryApplyLegacyCharacterAttributeLines(
                characterLines,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = updatedSession.Character.ConquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                out var updatedCharacterLines))
        {
            if (reason.Length == 0)
            {
                reason = "character-section-missing";
            }

            return false;
        }

        var matches = persistedRows
            .Where(row => row.CharacterId == updatedRow.CharacterId)
            .ToArray();
        if (matches.Length != 1 ||
            matches[0].LineNumber <= 0 ||
            matches[0].LineNumber > innerPowerLines.Length)
        {
            reason = matches.Length == 0
                ? "inner-power-row-missing-on-disk"
                : "inner-power-row-ambiguous-on-disk";
            return false;
        }

        innerPowerLines[matches[0].LineNumber - 1] = updatedRow.RawLine;
        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var characterTempPath = characterPath + ".full-account-inner-power-" + transactionId + ".tmp";
        var characterBackupPath = characterPath + ".full-account-inner-power-" + transactionId + ".bak";
        var innerPowerTempPath = innerPowerPath + ".full-account-inner-power-" + transactionId + ".tmp";
        var innerPowerBackupPath = innerPowerPath + ".full-account-inner-power-" + transactionId + ".bak";
        var characterCommitted = false;
        var innerPowerCommitted = false;

        try
        {
            File.WriteAllLines(characterTempPath, updatedCharacterLines, LegacyCharacterSnapshotEncoding);
            File.WriteAllLines(innerPowerTempPath, innerPowerLines, LegacyInnerPowerTextEncoding);
            File.Replace(
                characterTempPath,
                characterPath,
                characterBackupPath,
                ignoreMetadataErrors: true);
            characterCommitted = true;
            File.Replace(
                innerPowerTempPath,
                innerPowerPath,
                innerPowerBackupPath,
                ignoreMetadataErrors: true);
            innerPowerCommitted = true;
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            OverflowException or
            EncoderFallbackException)
        {
            var innerPowerRestored = !innerPowerCommitted ||
                TryRestoreLegacyRewardPointsFile(
                    innerPowerBackupPath,
                    innerPowerPath,
                    existed: true);
            var characterRestored = !characterCommitted ||
                TryRestoreLegacyRewardPointsFile(
                    characterBackupPath,
                    characterPath,
                    existed: true);
            reason = innerPowerRestored && characterRestored
                ? "transaction-write-failed:" + exception.GetType().Name
                : "transaction-rollback-failed:" + exception.GetType().Name;
            return false;
        }
        finally
        {
            TryDeleteLegacyRewardPointsFile(characterTempPath);
            TryDeleteLegacyRewardPointsFile(innerPowerTempPath);
        }

        TryDeleteLegacyRewardPointsFile(characterBackupPath);
        TryDeleteLegacyRewardPointsFile(innerPowerBackupPath);
        return true;
    }
}
