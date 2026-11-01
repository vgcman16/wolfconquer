using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPhysicalDeathOperation
{
    internal byte[] ProjectCharacter(byte[] physicalBefore, byte[]? physicalSpellsBefore = null)
    {
        Validate();
        if (physicalBefore is null || physicalBefore.Length is 0 or > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes)
            throw Invalid();
        var before = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(physicalBefore, out _));
        if (before.Uid != TargetUid || before.MinHitPoints is <= 0 or > int.MaxValue || before.MapId is <= 0 or > uint.MaxValue ||
            before.X is < 0 or > ushort.MaxValue || before.Y is < 0 or > ushort.MaxValue) throw Invalid();
        // Reuse strict unique-section/key and exact encoding grammar without
        // changing coordinates or obtaining a movement owner/permission.
        if (!LegacyPairMovementCharacterProjection.TryProjectCoordinates(physicalBefore, TargetUid,
            checked((uint)before.MapId), checked((ushort)before.X), checked((ushort)before.Y),
            checked((ushort)before.X), checked((ushort)before.Y), out _, out _)) throw Invalid();
        var reconciled = ReconcileRecordedSpells(physicalSpellsBefore, before);
        var originImage = LegacyDerivedSpellOrigins.ProjectCharacterImage(physicalBefore, reconciled.Character);
        // This serializer consumes a parsed physical image, not the immutable
        // model's wrapper collections as live runtime ownership inputs.
        var projected = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(originImage, out _));
        try { return LegacyBoundHitpointsScalarWrite.ProjectAfterImage(originImage, projected with { MinHitPoints = 0 }); }
        catch (LegacyNativeHitpointsRefusalException error)
        { throw new InvalidDataException("physicalDeath22CharacterProjectionInvalid", error); }
    }
}
