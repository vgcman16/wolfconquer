using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed partial class FuturePlayerDeathData
    {
        internal sealed class Images
        {
            private readonly byte[] character, spells;
            internal LegacyPhysicalDeathOperation Operation { get; }
            internal int EnvelopeBytes { get; }
            internal byte[] CopyCharacterAfter() => character.ToArray();
            internal byte[] CopySpellsAfter() => spells.ToArray();
            internal Images(LegacyPhysicalDeathOperation operation, byte[] character, byte[] spells, int envelopeBytes)
            { Operation = operation; this.character = character; this.spells = spells; EnvelopeBytes = envelopeBytes; }
        }

        internal Images ProjectImages(long targetAccountId, byte[] characterBefore, byte[]? spellsBefore,
            byte[] itemsBefore, string usersRoot, string itemsRoot, string spellsRoot)
        {
            if (targetAccountId is <= 0 or > uint.MaxValue || targetAccountId != before.AccountId ||
                characterBefore is null || characterBefore.Length is 0 or > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes ||
                spellsBefore is { Length: > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes } ||
                itemsBefore is null || itemsBefore.Length is 0 or > LegacyTradeJournal.PhysicalDeathMaximumReferenceBytes)
                throw InvalidFutureDeath();
            var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterBefore, out _));
            var savedSpells = spellsBefore is null ? PlayerSpellSnapshot.Empty : PlayerSpellSnapshotLoader.Parse(spellsBefore);
            if (!BoundHitpointsInputs.ScalarsEqual(disk, before.Character) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(disk, before.Character) ||
                !BoundHitpointsInputs.PaidEqual(disk, before.Character) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems).AsSpan()
                    .SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(itemsBefore))) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(savedSpells).AsSpan()
                    .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(before.PlayerSpells))) throw InvalidFutureDeath();
            if (!LegacyExperienceContinuationFence.AllowsOrdinaryCharacterImage(characterBefore, targetAccountId,
                    checked((uint)before.Character.Uid)) ||
                !LegacyExperienceContinuationFence.AllowsOrdinaryPublication(before, Session)) throw InvalidFutureDeath();
            var operation = new LegacyPhysicalDeathOperation(checked((uint)before.Character.Uid), QueryAdmitted,
                before.AlternateEquipmentActive, itemsBefore, ranks, changes, grants);
            var afterCharacter = operation.ProjectCharacter(characterBefore, spellsBefore);
            var afterSpells = operation.ProjectSpells(spellsBefore, disk);
            var projected = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(afterCharacter, out _));
            if (!BoundHitpointsInputs.ScalarsEqual(projected, Session.Character) ||
                CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(projected, Session.Character) ||
                !BoundHitpointsInputs.PaidEqual(projected, Session.Character) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(PlayerSpellSnapshotLoader.Parse(afterSpells)).AsSpan()
                    .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(Session.PlayerSpells))) throw InvalidFutureDeath();
            RequireDeathDataOrigins(projected, Session.Character, Session.PlayerSpells);
            LegacyTradeJournalChange[] changesToWrite =
            [
                new(LegacyTradeJournalFileKind.Character, targetAccountId, characterBefore, afterCharacter),
                new(LegacyTradeJournalFileKind.Spells, before.Character.Uid, spellsBefore, afterSpells),
            ];
            int envelope = LegacyTradeJournal.ValidatePhysicalDeathDataEnvelope(usersRoot, itemsRoot, spellsRoot,
                operation, changesToWrite);
            return new(operation, afterCharacter, afterSpells, envelope);
        }

        private static void RequireDeathDataOrigins(CharacterSnapshot recorded, CharacterSnapshot proposed,
            PlayerSpellSnapshot spells)
        {
            // Same DATA condition as the actual journal22 writer. Runtime
            // ThundercloudSight/AttackHit and status attributes are not saved
            // scalar equality, while the exact derived origin is required.
            LegacyDerivedSpellOrigins.RequireRows(recorded, spells);
            LegacyDerivedSpellOrigins.RequireRows(proposed, spells);
            var left = new LegacyNativeAttributeMapProof(recorded.Attributes).Fields;
            var right = new LegacyNativeAttributeMapProof(proposed.Attributes).Fields;
            bool hasLeft = left.TryGetValue(LegacyDerivedSpellOrigins.Key, out var leftValue);
            bool hasRight = right.TryGetValue(LegacyDerivedSpellOrigins.Key, out var rightValue);
            if (hasLeft != hasRight || leftValue != rightValue) throw InvalidFutureDeath();
        }
    }
}
