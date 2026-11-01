using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedPairMovementWrite
{
    private void CaptureImages()
    {
        foreach (var member in new[] { actor, partner })
            member.Path = Path.Combine(UsersRoot, member.Binding.AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
        SnapshotFileLockCoordinator.ExecuteLocked(new[] { actor.Path, partner.Path }, () =>
        {
            if (!IsCurrent()) throw new InvalidOperationException("pairMovementOwnerChangedBeforeCapture");
            Capture(actor); Capture(partner);
            if (!IsCurrent()) throw new InvalidOperationException("pairMovementOwnerChangedDuringCapture");
            return true;
        });
    }

    private void Capture(Member member)
    {
        if (!CharacterSnapshotFileBaseline.TryReadBytes(member.Path, "pairMovement", out var bytes, out var reason))
            throw new IOException("pairMovementCharacterUnavailable:" + reason);
        var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(bytes, out _));
        if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(disk, member.Before.Character) ||
            CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, member.Before.Character) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(disk, member.Before.Character))
            throw new InvalidDataException("pairMovementCharacterBaselineStale");
        if (!LegacyPairMovementCharacterProjection.TryProject(bytes, Operation, checked((uint)member.Binding.Uid),
                out var after, out reason)) throw new InvalidDataException("pairMovementCharacterProjection:" + reason);
        var parsedAfter = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(after, out _));
        if (!WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(parsedAfter, member.Proposed.Character) ||
            !PersistedMovementFieldsEqual(parsedAfter, member.Proposed.Character) ||
            !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(parsedAfter, member.Proposed.Character))
            throw new InvalidDataException("pairMovementCharacterProjectionIncomplete");
        member.BeforeImage = bytes; member.AfterImage = after;
    }

    private LegacyTradeJournalChange[] CopyChanges() => new[] { actor, partner }
        .OrderBy(member => member.Binding.AccountId)
        .Select(member => new LegacyTradeJournalChange(LegacyTradeJournalFileKind.Character, member.Binding.AccountId,
            member.BeforeImage.ToArray(), member.AfterImage.ToArray())).ToArray();

    private bool ImagesMatch(bool after)
    {
        foreach (var member in new[] { actor, partner })
            if (!CharacterSnapshotFileBaseline.TryReadBytes(member.Path, "pairMovement", out var actual, out _) ||
                !actual.AsSpan().SequenceEqual(after ? member.AfterImage : member.BeforeImage)) return false;
        return true;
    }

    internal bool ContextCoordinatesMatch()
    {
        uint Read(LegacyNativeAttributeMapProof proof)
        {
            if (!proof.Fields.TryGetValue("DynamicID", out var raw)) return 0;
            return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : throw new InvalidDataException("pairMovementRuntimeDynamicInvalid");
        }
        return Read(actor.beforeAttributes) == Operation.DynamicId && Read(partner.beforeAttributes) == Operation.DynamicId;
    }
}
