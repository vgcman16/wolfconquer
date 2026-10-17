using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask<PlayerSpellSnapshot> EnsureLegacyClassOwnedSpellsAsync(
        CharacterSnapshot snapshot,
        PlayerSpellSnapshot playerSpells,
        CancellationToken cancellationToken)
    {
        var matchingLoginSpellIds = LegacyClassOwnedSpellGrantProfile.GetSpellIds(snapshot);
        if (matchingLoginSpellIds.Count == 0)
        {
            return playerSpells;
        }

        var updatedSpells = playerSpells;
        List<ushort>? grantedSpellIds = null;
        foreach (var spellId in matchingLoginSpellIds)
        {
            if (updatedSpells.ContainsSpell(spellId))
            {
                continue;
            }

            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(
                updatedSpells,
                CreateLegacyClassOwnedSpellEntry(spellId));
            grantedSpellIds ??= [];
            grantedSpellIds.Add(spellId);
        }

        if (grantedSpellIds is null)
        {
            return playerSpells;
        }

        await PersistLegacyClassOwnedSpellsAsync(snapshot, updatedSpells, grantedSpellIds, cancellationToken)
            .ConfigureAwait(false);
        return updatedSpells;
    }

    private async ValueTask PersistLegacyClassOwnedSpellsAsync(
        CharacterSnapshot snapshot,
        PlayerSpellSnapshot playerSpells,
        IReadOnlyList<ushort> grantedSpellIds,
        CancellationToken cancellationToken)
    {
        if (_snapshotLoader is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader.PlayerSpellsRoot))
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy Trojan-lineage login spell persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason=playerSpellsRootMissing.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerSpellSnapshotLoader(_snapshotLoader.PlayerSpellsRoot)
                .SaveByCharacterUid(snapshot.Uid, playerSpells);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _log.WriteLineAsync(
                    "WorldServer failed legacy Trojan-lineage login spell persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason={ex.GetType().Name}.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _log.WriteLineAsync(
                "WorldServer applied legacy Trojan-lineage login spell grant " +
                $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} " +
                $"class={snapshot.Class.ToString(CultureInfo.InvariantCulture)} " +
                $"firstClass={snapshot.FirstClass.ToString(CultureInfo.InvariantCulture)} " +
                $"secondClass={snapshot.SecondClass.ToString(CultureInfo.InvariantCulture)} " +
                $"level={snapshot.Level.ToString(CultureInfo.InvariantCulture)} " +
                $"spellIds={FormatLegacyClassOwnedSpellIds(grantedSpellIds)}.")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static PlayerSpellSnapshotEntry CreateLegacyClassOwnedSpellEntry(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static string FormatLegacyClassOwnedSpellIds(IReadOnlyList<ushort> spellIds)
    {
        return string.Join(",", spellIds.Select(spellId => spellId.ToString(CultureInfo.InvariantCulture)));
    }
}
