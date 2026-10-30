using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    internal sealed partial class RuneCharacterPublication
    {
        private static (CharacterSnapshot Character, PlayerItemSnapshot Items, PlayerSpellSnapshot Spells) ProjectRecordedImages(
            CharacterSnapshot character, PlayerItemSnapshot items, PlayerSpellSnapshot spells,
            LegacyRuneOperationEvidence? rune, LegacyRuneContainerEvidence? container, LegacyRuneComposeEvidence? compose,
            LegacyRuneChanceEvidence? chance)
        {
            if (chance is not null)
            {
                var result = WorldPacketHandlers.ProjectRecordedRuneChance(character, items, spells, chance.Operation);
                return (result.Character, result.Items, result.Spells);
            }
            if (compose is not null)
            {
                var result = WorldPacketHandlers.ProjectRecordedRuneCompose(character, items, spells, compose.Operation);
                return (result.Character, result.Items, result.Spells);
            }
            var other = rune is not null
                ? WorldPacketHandlers.ProjectRecordedRuneOperation(character, items, spells, rune.Operation)
                : WorldPacketHandlers.ProjectRecordedRuneContainer(character, items, spells, container!.Operation);
            return (other.Character, other.Items, other.Spells);
        }
    }
}
