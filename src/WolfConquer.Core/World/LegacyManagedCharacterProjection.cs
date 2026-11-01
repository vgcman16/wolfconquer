using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// DATA projection only. This validates the scalar persistence inputs and Vigor;
// other subsystems retain responsibility for their own fields and ambiguity.
// The legacy loader can still collapse case variants before this operation.
internal static class LegacyManagedCharacterProjection
{
    internal static byte[] ProjectAfterImage(byte[] before, CharacterSnapshot target)
    {
        if (before.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes)
            throw new InvalidDataException("managedCharacterImageBoundsInvalid");
        var stable = new WorldPacketHandlers.BoundHitpointsInputs(target, target).After;
        var values = CharacterSnapshotScalarPersistence.BuildPersistedValues(stable);
        var owned = new HashSet<string>(values.Keys, StringComparer.OrdinalIgnoreCase)
        {
            "Name", "Vigor", "MainFlag", "ClaimStateGift", "Phoenix", "Pheonix",
            "EpicTrojan", "StageEpicTrojanQuest", "ChangeEpicTrojan", "ChangeArrayEpicTrojan",
            "EpicTrojanEvilArrayPoints", "EpicTrojanAbysalStage", "ChangeMr_MirrorEpicTrojan",
            "ChangeGeneralPakEpicTrojan", "EpicTrojanMrMirrorPrograss"
        };
        // A runtime alias may be absent from persisted output yet still be an
        // input to BuildPersistedValues; include its full fixed input schema.
        owned.UnionWith(new[] { "CanChangeWindWalkerFree", "ClassExperience", "WHMoney", "WarehouseMoney",
            "VotePoints", "PinCodeAnima", "Dragon", "Tiger", "Turtle", "ChiPoints", "DragonTime",
            "PhoenixTime", "TigerTime", "TurtleTime", "RetreatedDragon", "RetreatedPhoenix",
            "RetreatedTiger", "RetreatedTurtle", "SkyTowerCompletedChallengeId", "SkyTowerAvailableAttempts" });
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in target.Attributes)
            if (owned.Contains(pair.Key) && !seen.Add(pair.Key))
                throw new InvalidDataException("managedCharacterOwnedKeyAmbiguous");
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before))
            throw new InvalidDataException("managedCharacterEncodingUnproven");
        var projected = LegacyOrdinaryAdmissionCharacterText.ProjectManagedScalars(text, values, owned);
        _ = CharacterSnapshotParser.ParseText(text);
        var parsed = CharacterSnapshotParser.ParseText(projected);
        if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(parsed, stable) ||
            CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(parsed, stable))
            throw new InvalidDataException("managedCharacterScalarProjectionIncomplete");
        var result = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (result.Length > CharacterSnapshotFileBaseline.MaximumBytes)
            throw new InvalidDataException("managedCharacterImageBoundsInvalid");
        return result;
    }
}
