using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private void PersistChangedSessionPlayerProficiencies(
        WorldSession? previous,
        WorldSession current)
    {
        if (previous is null ||
            ReferenceEquals(previous.PlayerProficiencies, current.PlayerProficiencies) ||
            PlayerProficiencySnapshotsEqual(previous.PlayerProficiencies, current.PlayerProficiencies))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader?.PlayerProficienciesRoot))
        {
            LogPlayerProficiencyPersistenceGap(current, "playerProficienciesRootMissing", string.Empty);
            return;
        }

        try
        {
            var loader = new PlayerProficiencySnapshotLoader(_snapshotLoader.PlayerProficienciesRoot);
            loader.SaveByCharacterUid(current.Character.Uid, current.PlayerProficiencies);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPlayerProficiencyPersistenceGap(
                current,
                ex.GetType().Name,
                _snapshotLoader.PlayerProficienciesRoot);
        }
    }

    private static bool PlayerProficiencySnapshotsEqual(
        PlayerProficiencySnapshot previous,
        PlayerProficiencySnapshot current)
    {
        return PlayerProficiencySnapshotLoader.ToLegacyBytes(previous).AsSpan()
            .SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(current));
    }

    private void PersistChangedSessionPlayerQuests(
        WorldSession? previous,
        WorldSession current)
    {
        if (previous is null ||
            ReferenceEquals(previous.Character.PlayerQuests, current.Character.PlayerQuests) ||
            PlayerQuestSnapshotsEqual(previous.Character.PlayerQuests, current.Character.PlayerQuests))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader?.PlayerQuestsRoot))
        {
            LogPlayerQuestPersistenceGap(current, "playerQuestsRootMissing", string.Empty);
            return;
        }

        try
        {
            var loader = new PlayerQuestSnapshotLoader(_snapshotLoader.PlayerQuestsRoot);
            loader.SaveByCharacterUid(current.Character.Uid, current.Character.PlayerQuests);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPlayerQuestPersistenceGap(current, ex.GetType().Name, _snapshotLoader.PlayerQuestsRoot);
        }
    }

    private static bool PlayerQuestSnapshotsEqual(PlayerQuestSnapshot previous, PlayerQuestSnapshot current)
    {
        return PlayerQuestSnapshotLoader.Serialize(previous).AsSpan()
            .SequenceEqual(PlayerQuestSnapshotLoader.Serialize(current));
    }

    private void LogPlayerProficiencyPersistenceGap(WorldSession session, string reason, string path)
    {
        var file = string.IsNullOrWhiteSpace(path) ? "unknown" : Path.GetFileName(path);
        var diagnostic = LegacyParityGapDiagnostics.Create(
            LegacyParityGapCategory.Runtime,
            "player-proficiency-persistence",
            $"failed to persist legacy player proficiency snapshot reason={reason}",
            LegacyParityGapSeverity.Error,
            new[]
            {
                LegacyParityGapDiagnostics.Fact("accountId", session.AccountId),
                LegacyParityGapDiagnostics.Fact("file", file),
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-player-proficiencies-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("proficiencyCount", session.PlayerProficiencies.Proficiencies.Count),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
                LegacyParityGapDiagnostics.Fact("uid", session.Character.Uid),
            },
            new[]
            {
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-player-proficiencies-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
            });
        _log.WriteLine(diagnostic.FormatLogMessage());
        _log.WriteLine(
            "PARITY-BUG player proficiency persistence failed " +
            $"accountId={session.AccountId.ToString(CultureInfo.InvariantCulture)} " +
            $"uid={session.Character.Uid.ToString(CultureInfo.InvariantCulture)} file={file} reason={reason}");
    }

    private void LogPlayerQuestPersistenceGap(WorldSession session, string reason, string path)
    {
        var file = string.IsNullOrWhiteSpace(path) ? "unknown" : Path.GetFileName(path);
        var diagnostic = LegacyParityGapDiagnostics.Create(
            LegacyParityGapCategory.Runtime,
            "player-quest-persistence",
            $"failed to persist legacy player quest snapshot reason={reason}",
            LegacyParityGapSeverity.Error,
            new[]
            {
                LegacyParityGapDiagnostics.Fact("accountId", session.AccountId),
                LegacyParityGapDiagnostics.Fact("file", file),
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-player-quests-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("questCount", session.Character.PlayerQuests.Quests.Count),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
                LegacyParityGapDiagnostics.Fact("uid", session.Character.Uid),
            },
            new[]
            {
                LegacyParityGapDiagnostics.Fact("nextSeam", "legacy-player-quests-saveclient-parity"),
                LegacyParityGapDiagnostics.Fact("reason", reason),
                LegacyParityGapDiagnostics.Fact("sourceSeam", "WorldSessionStore.Set"),
            });
        _log.WriteLine(diagnostic.FormatLogMessage());
        _log.WriteLine(
            "PARITY-BUG player quest persistence failed " +
            $"accountId={session.AccountId.ToString(CultureInfo.InvariantCulture)} " +
            $"uid={session.Character.Uid.ToString(CultureInfo.InvariantCulture)} file={file} reason={reason}");
    }
}
