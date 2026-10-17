using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPersistLegacyFullAccountClassMutation(
        WorldPacketHandlerContext context,
        WorldSession updatedSession,
        IReadOnlyDictionary<string, string> characterValues,
        out string reason)
    {
        reason = string.Empty;
        var characterPath = Path.Combine(
            updatedSession.UsersRoot!,
            updatedSession.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!File.Exists(characterPath))
        {
            reason = "snapshot-missing";
            return false;
        }

        string[] characterLines;
        try
        {
            characterLines = File.ReadAllLines(characterPath, LegacyCharacterSnapshotEncoding);
        }
        catch (IOException)
        {
            reason = "snapshot-read-failed";
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            reason = "snapshot-read-denied";
            return false;
        }

        if (!TryValidateLegacyCharacterUid(characterLines, updatedSession.Character.Uid, out reason) ||
            !TryApplyLegacyCharacterAttributeLines(characterLines, characterValues, out var updatedCharacterLines))
        {
            if (reason.Length == 0)
            {
                reason = "character-section-missing";
            }

            return false;
        }

        var transactionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var characterTempPath = characterPath + ".full-account-class-" + transactionId + ".tmp";
        var characterBackupPath = characterPath + ".full-account-class-" + transactionId + ".bak";
        var spellPath = Path.Combine(
            context.PlayerSpellsRoot!,
            updatedSession.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        var spellTempPath = spellPath + ".full-account-class-" + transactionId + ".tmp";
        var spellBackupPath = spellPath + ".full-account-class-" + transactionId + ".bak";
        var spellExisted = File.Exists(spellPath);
        var characterCommitted = false;
        var spellCommitted = false;

        try
        {
            File.WriteAllLines(characterTempPath, updatedCharacterLines, LegacyCharacterSnapshotEncoding);
            Directory.CreateDirectory(context.PlayerSpellsRoot!);
            File.WriteAllBytes(spellTempPath, PlayerSpellSnapshotLoader.ToLegacyBytes(updatedSession.PlayerSpells));

            File.Replace(
                characterTempPath,
                characterPath,
                characterBackupPath,
                ignoreMetadataErrors: true);
            characterCommitted = true;

            if (spellExisted)
            {
                File.Replace(spellTempPath, spellPath, spellBackupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(spellTempPath, spellPath);
            }

            spellCommitted = true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            OverflowException)
        {
            var spellRestored = !spellCommitted ||
                TryRestoreLegacyRewardPointsFile(spellBackupPath, spellPath, spellExisted);
            var characterRestored = !characterCommitted ||
                TryRestoreLegacyRewardPointsFile(characterBackupPath, characterPath, existed: true);
            reason = spellRestored && characterRestored
                ? "transaction-write-failed:" + ex.GetType().Name
                : "transaction-rollback-failed:" + ex.GetType().Name;
            return false;
        }
        finally
        {
            TryDeleteLegacyRewardPointsFile(characterTempPath);
            TryDeleteLegacyRewardPointsFile(spellTempPath);
        }

        TryDeleteLegacyRewardPointsFile(characterBackupPath);
        TryDeleteLegacyRewardPointsFile(spellBackupPath);
        return true;
    }
}
