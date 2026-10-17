using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private async ValueTask<PlayerSpellSnapshot> EnsureLegacyPlayerSpellClassGatesAsync(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells,
        CancellationToken cancellationToken)
    {
        if (!ShouldApplyLegacyPlayerSpellClassGateSanitization(snapshot, playerItems, playerSpells))
        {
            return playerSpells;
        }

        if (!LegacyPlayerSpellClassGateSanitizer.TrySanitize(
                snapshot,
                playerSpells,
                _worldData?.MagicTypeOperationsByReferencedMagicTypeId,
                out var sanitized,
                out var removedSpellIds))
        {
            return playerSpells;
        }

        await PersistLegacyPlayerSpellClassGateSanitizationAsync(snapshot, sanitized, removedSpellIds, cancellationToken)
            .ConfigureAwait(false);
        return sanitized;
    }

    private static bool ShouldApplyLegacyPlayerSpellClassGateSanitization(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        PlayerSpellSnapshot playerSpells)
    {
        _ = playerItems;
        if (playerSpells.Spells.Count <= 1 ||
            !HasOnlyLegacyZeroProgressSpells(playerSpells) ||
            !TryGetLegacyFirstCharacterStarterSpellProfile(snapshot.Class, out var spellProfile))
        {
            return false;
        }

        return HasLegacyFirstCharacterStarterSpellAnchor(playerSpells, spellProfile) &&
            HasLegacyFirstCharacterStarterSpellPollution(playerSpells, spellProfile.TunedSpellIds);
    }

    private static bool HasLegacyFirstCharacterStarterSpellAnchor(
        PlayerSpellSnapshot playerSpells,
        LegacyFirstCharacterStarterSpellProfile spellProfile)
    {
        foreach (var spell in playerSpells.Spells)
        {
            if (spellProfile.TunedSpellIds.Contains(spell.Id) ||
                spellProfile.SourceSpellIds.Contains(spell.Id))
            {
                return true;
            }
        }

        return false;
    }

    private async ValueTask PersistLegacyPlayerSpellClassGateSanitizationAsync(
        CharacterSnapshot snapshot,
        PlayerSpellSnapshot playerSpells,
        IReadOnlyList<ushort> removedSpellIds,
        CancellationToken cancellationToken)
    {
        if (_snapshotLoader is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_snapshotLoader.PlayerSpellsRoot))
        {
            await _log.WriteLineAsync(
                    "WorldServer skipped legacy player spell class-gate persistence " +
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
                    "WorldServer failed legacy player spell class-gate persistence " +
                    $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} reason={ex.GetType().Name}.")
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await _log.WriteLineAsync(
                "WorldServer removed legacy player spells rejected by source class gates " +
                $"uid={snapshot.Uid.ToString(CultureInfo.InvariantCulture)} " +
                $"class={snapshot.Class.ToString(CultureInfo.InvariantCulture)} " +
                $"removedSpellIds={FormatLegacyClassGateSpellIds(removedSpellIds)}.")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static string FormatLegacyClassGateSpellIds(IReadOnlyList<ushort> spellIds)
    {
        return string.Join(",", spellIds.Select(spellId => spellId.ToString(CultureInfo.InvariantCulture)));
    }
}
