using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyBoundHitpointsScalarWrite
{
    private static (string Path, byte[] Before, byte[] After) PrepareImages(LegacyOrdinarySpellBinding binding,
        WorldSession expected, WorldPacketHandlers.BoundHitpointsInputs inputs)
    {
        var path = Path.Combine(binding.UsersRoot, expected.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        return SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            if (!binding.Recovery.TryRequireLiveJournalClear(out var readiness))
                throw new LegacyNativeHitpointsRecoveryRequiredException("ordinaryHitpointsRecoveryRequired:" + readiness);
            if (!CharacterSnapshotFileBaseline.TryRead(path, "ordinaryHitpoints", out var baseline, out _) || baseline is null)
                throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsCharacterUnavailable");
            var text = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out var encoding);
            if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(baseline.Bytes))
                throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsEncodingUnproven");
            var disk = CharacterSnapshotParser.ParseText(text);
            if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(disk, inputs.Before) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, inputs.Before) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(disk, inputs.Before) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(inputs.Before, inputs.After))
                throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsCharacterBaselineStale");
            var bytes = ProjectAfterImage(baseline.Bytes, inputs.After);
            return (path, baseline.Bytes, bytes);
        });
    }
    // Shared pure serializer for genuine external journal producers/tests. It
    // grants no owner, persistence result, callback or publication permission.
    internal static byte[] ProjectAfterImage(byte[] before, CharacterSnapshot target)
    {
        if (before.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes)
            throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsImageBoundsInvalid");
        var stable = new WorldPacketHandlers.BoundHitpointsInputs(target, target).After;
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(before))
            throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsEncodingUnproven");
        var projected = LegacyOrdinaryAdmissionCharacterText.Project(text,
            CharacterSnapshotScalarPersistence.BuildPersistedValues(stable), paid: null);
        var parsed = CharacterSnapshotParser.ParseText(projected);
        if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(parsed, stable) ||
            CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(parsed, stable))
            throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsScalarProjectionIncomplete");
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(projected)).ToArray();
        if (bytes.Length > CharacterSnapshotFileBaseline.MaximumBytes)
            throw new LegacyNativeHitpointsRefusalException("ordinaryHitpointsImageTooLarge");
        return bytes;
    }

}
