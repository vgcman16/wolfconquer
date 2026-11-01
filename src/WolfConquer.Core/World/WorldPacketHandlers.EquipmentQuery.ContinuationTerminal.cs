namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class PreparedLevelUpRebuild
    {
        // A read-only successor check, not a query issuer. Only the driver's
        // actual final-clear branch registers the enclosing terminal receipt.
        internal bool TryReadTerminalMaximums(CompletedContinuationQuery query, WorldSession ready,
            WorldSession cleared, out LegacyNativeMagicQueryClient.CompletedMaximums? maximums)
        {
            maximums = null;
            if (!ReferenceEquals(query, continuationQuery) || !ReferenceEquals(ready, continuationSession) ||
                !ReferenceEquals(query.Session, ready) || continuationMarker is not { Phase: LegacyExperienceContinuationPhase.Ready } pending ||
                completion is null || !Equals(pending, LegacyExperienceContinuationCodec.ReadCharacter(ready.Character, ready.AccountId)) ||
                LegacyExperienceContinuationCodec.ReadCharacter(cleared.Character, cleared.AccountId) is not null ||
                !IsQueryOwnerCurrent(context, cleared, cleared)) return false;
            var character = LegacyExperienceContinuationCodec.WithState(ready.Character, ready.AccountId, null);
            character = new BoundHitpointsInputs(ready.Character, character).After;
            if ((cleared with { Character = ready.Character }) != ready ||
                (cleared.Character with { Attributes = character.Attributes, PaidNobility = character.PaidNobility }) != character) return false;
            var expected = LegacyNativeAttributeMapProof.CaptureRaw(character.Attributes);
            var actual = LegacyNativeAttributeMapProof.CaptureRaw(cleared.Character.Attributes);
            if (!ReferenceEquals(expected.CapturedComparer, actual.CapturedComparer) ||
                !expected.Fields.SequenceEqual(actual.Fields) || !actual.IsCurrentPlain() || !expected.IsCurrentPlain()) return false;
            var expectedPaid = LegacyNativeAttributeMapProof.CaptureRaw(character.PaidNobility);
            var actualPaid = LegacyNativeAttributeMapProof.CaptureRaw(cleared.Character.PaidNobility);
            if (!ReferenceEquals(expectedPaid.CapturedComparer, actualPaid.CapturedComparer) ||
                !expectedPaid.Fields.SequenceEqual(actualPaid.Fields) || !actualPaid.IsCurrentPlain() || !expectedPaid.IsCurrentPlain()) return false;
            maximums = completion;
            return true;
        }
    }

    internal sealed partial class CompletedContinuationQuery
    {
        internal bool TryReadTerminalMaximums(WorldSession ready, WorldSession cleared,
            out LegacyNativeMagicQueryClient.CompletedMaximums? maximums) =>
            owner.TryReadTerminalMaximums(this, ready, cleared, out maximums);
    }
}
