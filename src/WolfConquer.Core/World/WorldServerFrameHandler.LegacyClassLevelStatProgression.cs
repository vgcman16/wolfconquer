using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask<CharacterSnapshot> EnsureLegacyClassLevelStatStateAsync(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        CombatGearSnapshot combatGear,
        CancellationToken cancellationToken)
    {
        if (_worldData is null)
        {
            return snapshot;
        }

        var progressed = LegacyClassLevelStatProgression.Apply(
            snapshot,
            _worldData,
            snapshot.Level,
            out var change);
        if (!change.AppliedSourceStats)
        {
            return snapshot;
        }

        var updated = LegacyClassLevelStatProgression.RefreshVitals(
            progressed,
            playerItems,
            combatGear,
            _worldData);
        if (!CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(snapshot, updated))
        {
            return snapshot;
        }

        if (_snapshotLoader is not null &&
            !CharacterSnapshotScalarPersistence.TryPersist(
                _snapshotLoader.UsersRoot,
                updated,
                out var path,
                out var reason))
        {
            LogCharacterScalarPersistenceGap(updated.Uid, updated, path, reason);
        }

        await _log.WriteLineAsync(
                "WorldServer repaired legacy class level stats " +
                $"uid={updated.Uid.ToString(CultureInfo.InvariantCulture)} " +
                $"class={updated.Class.ToString(CultureInfo.InvariantCulture)} " +
                $"sourceClass={change.ClassName} " +
                $"level={updated.Level.ToString(CultureInfo.InvariantCulture)} " +
                $"strength={updated.Strength.ToString(CultureInfo.InvariantCulture)} " +
                $"vitality={updated.Vitality.ToString(CultureInfo.InvariantCulture)} " +
                $"agility={updated.Agility.ToString(CultureInfo.InvariantCulture)} " +
                $"spirit={updated.Spirit.ToString(CultureInfo.InvariantCulture)} " +
                "source=AtributesStatus.GetStatus.")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return updated;
    }
}
