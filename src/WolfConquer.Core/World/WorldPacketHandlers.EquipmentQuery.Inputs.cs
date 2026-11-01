using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static WorldSession CaptureEquipmentQueryInputs(WorldSession source)
    {
        var attributes = LegacyNativeAttributeMapProof.CaptureRaw(source.Character.Attributes);
        var paid = LegacyNativeAttributeMapProof.CaptureRaw(source.Character.PaidNobility);
        var spells = new LegacyPerformerSpellSource(source.PlayerSpells.Spells);
        if (!LegacyItemSaveGraph.TryCapture(source.PlayerItems, out var items) || items is null)
            throw new InvalidDataException("equipmentQueryItemsInvalid");
        var copy = source with
        {
            Character = source.Character with
            {
                Attributes = new Dictionary<string, string>(attributes.Fields, attributes.CapturedComparer),
                PaidNobility = new Dictionary<string, string>(paid.Fields, paid.CapturedComparer),
            },
            PlayerSpells = new(new LegacyPerformerSpellRows(spells.Rows)),
        };
        copy = items.WithOwnedItemsForPersistence(copy);
        if (!attributes.IsCurrentPlain() || !paid.IsCurrentPlain() || !spells.IsCurrent() || !items.IsCurrent(source.PlayerItems))
            throw new InvalidDataException("equipmentQueryInputsChanged");
        return copy;
    }

    private static CharacterSnapshot WithEquipmentQueryVigor(CharacterSnapshot character, ushort vigor)
    {
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
        var keys = proof.Fields.Keys.Where(key => key.Equals("Vigor", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (keys.Length > 1) throw new InvalidDataException("equipmentQueryVigorAmbiguous");
        var copy = new Dictionary<string, string>(proof.Fields, proof.CapturedComparer)
        { [keys.Length == 0 ? "Vigor" : keys[0]] = vigor.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        if (!proof.IsCurrentPlain()) throw new InvalidDataException("equipmentQueryVigorChanged");
        return character with { Attributes = copy };
    }
}
