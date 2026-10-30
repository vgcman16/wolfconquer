using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeUpdateSpell
{
    private sealed partial class TrainingPlan
    {
        private bool HasBinding() => ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) && player.UID == uid &&
            player.Level == playerLevel && attack.SpellID == spellId && attack.AtkType == attackType &&
            ReferenceEquals(client.Equipment, equipment) && equipment.IsOwnedBy(client) &&
            (right == 0 && left == 0 || ReferenceEquals(client.MyProfs, profs) && profs?.IsOwnedBy(client) == true) &&
            ReferenceEquals(client.Socket, socket) && client.Fake == fake &&
            (trainingWeapons is not null
                ? trainingWeapons.MatchesCurrent(client, player, equipment)
                : equipment.RightWeapon / 1000 == right && equipment.LeftWeapon / 1000 == left);

        private bool HasMetadata()
        {
            if (metadata is null || selected is null) return true;
            if (metadata.Count != metadataCount) return false;
            if (row is null) return originalLevel >= metadataCount - 1;
            foreach (var pair in metadata)
                if (pair.Key == originalLevel)
                    return ReferenceEquals(pair.Value, row) && row.ID == spellId && row.Level == originalLevel &&
                        row.Experience == threshold && row.NeedLevel == needLevel && row.isRuneSkill == rune;
            return false;
        }

    }
}
