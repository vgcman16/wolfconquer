using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedMovementWrite
{
    internal sealed class CharacterPublication
    {
        private readonly LegacyPreparedMovementWrite writer;
        private readonly WorldSession target;
        private CharacterPublication(LegacyPreparedMovementWrite writer) { this.writer = writer; target = writer.Proposed; }
        internal static CharacterPublication Prepare(LegacyPreparedMovementWrite writer)
        {
            if (!writer.IsCurrent()) throw new InvalidOperationException("ordinaryMovementCharacterOwnerChanged");
            return new(writer);
        }

        internal void RequireCurrent(WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected, WorldSession proposed)
        {
            if (!ReferenceEquals(writer.store, store) || !ReferenceEquals(writer.binding.Connection, connection) ||
                !ReferenceEquals(writer.plan.Expected, expected) || !writer.IsCurrent() ||
                (target with { Character = proposed.Character, PlayerItems = proposed.PlayerItems,
                    PlayerSpells = proposed.PlayerSpells, PlayerProficiencies = proposed.PlayerProficiencies }) != proposed ||
                !SameCharacter(target.Character, proposed.Character) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(target.PlayerItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems)) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(target.PlayerSpells).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(proposed.PlayerSpells)) ||
                !PlayerProficiencySnapshotLoader.ToLegacyBytes(target.PlayerProficiencies).AsSpan().SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(proposed.PlayerProficiencies)))
                throw new InvalidOperationException("ordinaryMovementCharacterPublicationMismatch");
        }

        internal byte[] RequireAfterImage(LegacyOrdinarySpellBinding binding, WorldSession expected,
            CharacterSnapshot proposed, byte[] actualBefore)
        {
            if (!ReferenceEquals(writer.binding, binding) || !ReferenceEquals(writer.plan.Expected, expected) ||
                !writer.IsCurrent() || !actualBefore.AsSpan().SequenceEqual(writer.before) || !SameCharacter(target.Character, proposed))
                throw new InvalidOperationException("ordinaryMovementCharacterImageMismatch");
            return writer.after.ToArray();
        }

        internal bool RequireCommittedCharacter() => SnapshotFileLockCoordinator.ExecuteLocked(writer.path, () =>
            writer.IsCurrent() && ReferenceEquals(writer.committed, writer.plan) && !LegacyTradeJournal.HasPending(writer.binding.UsersRoot) &&
            CharacterSnapshotFileBaseline.TryReadBytes(writer.path, "ordinaryMovement", out var bytes, out _) &&
            bytes.AsSpan().SequenceEqual(writer.after));

        private static bool SameCharacter(CharacterSnapshot expected, CharacterSnapshot proposed)
        {
            if ((expected with { Attributes = proposed.Attributes, PaidNobility = proposed.PaidNobility }) != proposed) return false;
            return SameFields(expected.Attributes, proposed.Attributes) && SameFields(expected.PaidNobility, proposed.PaidNobility);
        }
        private static bool SameFields(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second)
        {
            var a = LegacyNativeAttributeMapProof.CaptureRaw(first);
            var b = LegacyNativeAttributeMapProof.CaptureRaw(second);
            if (a.Fields.Count != b.Fields.Count) return false;
            var exact = new Dictionary<string, string>(b.Fields, StringComparer.Ordinal);
            foreach (var row in a.Fields) if (!exact.TryGetValue(row.Key, out var value) || value != row.Value) return false;
            return a.IsCurrentPlain() && b.IsCurrentPlain();
        }
    }

    internal CharacterPublication PreparePublication() => CharacterPublication.Prepare(this);
}
