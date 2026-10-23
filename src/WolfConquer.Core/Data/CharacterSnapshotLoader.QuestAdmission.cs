namespace WolfConquer.Core.Data;

public sealed partial class CharacterSnapshotLoader
{
    private CharacterSnapshotLoadResult LoadCharacterWithQuests(
        long accountId, string characterPath, CharacterSnapshot snapshot)
    {
        PlayerQuestSnapshot quests;
        try
        {
            quests = LoadPlayerQuestsByCharacterUid(snapshot.Uid);
        }
        catch (UnauthorizedAccessException error)
        {
            return QuestLoadFailure(accountId, characterPath,
                CharacterSnapshotLoadStatus.SnapshotReadDenied,
                "questSnapshotReadDenied", error);
        }
        catch (IOException error)
        {
            return QuestLoadFailure(accountId, characterPath,
                CharacterSnapshotLoadStatus.SnapshotReadFailed,
                "questSnapshotReadFailed", error);
        }
        catch (FormatException error)
        {
            return QuestLoadFailure(accountId, characterPath,
                CharacterSnapshotLoadStatus.SnapshotParseFailed,
                "questSnapshotParseFailed", error);
        }

        // No expiry persistence occurs until the required quest image is known
        // to load. UID mismatch has already been rejected by the caller.
        var complete = snapshot with { PlayerQuests = quests };
        complete = RestoreExpiredPaidNobility(characterPath, complete);
        return new CharacterSnapshotLoadResult(
            complete, CharacterSnapshotLoadStatus.Success, accountId,
            characterPath, "success", string.Empty);
    }

    private static CharacterSnapshotLoadResult QuestLoadFailure(
        long accountId, string characterPath, CharacterSnapshotLoadStatus status,
        string reason, Exception error) =>
        new(null, status, accountId, characterPath, reason, error.Message);
}
