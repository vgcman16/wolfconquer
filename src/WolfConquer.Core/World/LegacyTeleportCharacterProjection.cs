using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyTeleportCharacterProjection
{
    internal static byte[] Project(byte[] before, CharacterSnapshot expected, CharacterSnapshot proposed)
    {
        if (!LegacyPairMovementCharacterProjection.TryProjectCoordinates(before, checked((uint)expected.Uid),
            checked((uint)expected.MapId), checked((ushort)expected.X), checked((ushort)expected.Y),
            checked((ushort)expected.X), checked((ushort)expected.Y), out var neutral, out var reason) ||
            !before.AsSpan().SequenceEqual(neutral)) throw new InvalidDataException("teleportCharacterBeforeInvalid:" + reason);
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Map"] = proposed.MapId.ToString(CultureInfo.InvariantCulture),
            ["X"] = proposed.X.ToString(CultureInfo.InvariantCulture), ["Y"] = proposed.Y.ToString(CultureInfo.InvariantCulture),
            ["PMap"] = proposed.PreviousMapId.ToString(CultureInfo.InvariantCulture),
            ["PMapX"] = proposed.PreviousMapX.ToString(CultureInfo.InvariantCulture),
            ["PMapY"] = proposed.PreviousMapY.ToString(CultureInfo.InvariantCulture),
        };
        var afterText = LegacyOrdinaryAdmissionCharacterText.Project(text, fields, paid: null);
        var after = encoding.GetPreamble().Concat(encoding.GetBytes(afterText)).ToArray();
        if (after.Length > CharacterSnapshotFileBaseline.MaximumBytes ||
            !LegacyPairMovementCharacterProjection.TryProjectCoordinates(after, checked((uint)proposed.Uid),
                checked((uint)proposed.MapId), checked((ushort)proposed.X), checked((ushort)proposed.Y),
                checked((ushort)proposed.X), checked((ushort)proposed.Y), out neutral, out reason) ||
            !after.AsSpan().SequenceEqual(neutral)) throw new InvalidDataException("teleportCharacterAfterInvalid:" + reason);
        var parsed = CharacterSnapshotParser.ParseText(afterText);
        if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(parsed, proposed) ||
            CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(parsed, proposed) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(parsed, proposed)) throw new InvalidDataException("teleportCharacterProjectionIncomplete");
        return after;
    }
}
