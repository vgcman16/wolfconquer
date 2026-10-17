using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask PersistLegacyFirstCharacterCreationSidecarsAsync(
        CharacterSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (_snapshotLoader is null)
        {
            return;
        }

        await PersistLegacyFirstCharacterEmptyCombatGearAsync(snapshot, cancellationToken)
            .ConfigureAwait(false);
        await PersistLegacyFirstCharacterEmptyQuestsAsync(snapshot, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask PersistLegacyFirstCharacterEmptyCombatGearAsync(
        CharacterSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var playersCombatGearRoot = _snapshotLoader?.PlayersCombatGearRoot;
        if (string.IsNullOrWhiteSpace(playersCombatGearRoot))
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy first-character combat gear persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason=playersCombatGearRootMissing.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new CombatGearSnapshotLoader(playersCombatGearRoot)
                .SaveByCharacterUid(snapshot.Uid, CombatGearSnapshot.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _log.WriteLineAsync(
                    "WorldServer failed legacy first-character combat gear persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason={ex.GetType().Name}.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask PersistLegacyFirstCharacterEmptyQuestsAsync(
        CharacterSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var playerQuestsRoot = _snapshotLoader?.PlayerQuestsRoot;
        if (string.IsNullOrWhiteSpace(playerQuestsRoot))
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy first-character quest persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason=playerQuestsRootMissing.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerQuestSnapshotLoader(playerQuestsRoot)
                .SaveByCharacterUid(snapshot.Uid, PlayerQuestSnapshot.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _log.WriteLineAsync(
                    "WorldServer failed legacy first-character quest persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason={ex.GetType().Name}.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
