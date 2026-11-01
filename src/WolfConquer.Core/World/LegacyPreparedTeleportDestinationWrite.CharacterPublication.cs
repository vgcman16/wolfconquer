using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedTeleportDestinationWrite
{
    internal sealed class CharacterPublication
    {
        private readonly LegacyPreparedTeleportDestinationWrite writer;
        private CharacterPublication(LegacyPreparedTeleportDestinationWrite writer) => this.writer = writer;
        internal static CharacterPublication Prepare(LegacyPreparedTeleportDestinationWrite writer)
        {
            if (!writer.IsCurrent()) throw Unavailable();
            return new(writer);
        }
        internal void RequireCurrent(WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected, WorldSession proposed)
        {
            var target = writer.Proposed;
            if (!ReferenceEquals(writer.store, store) || !ReferenceEquals(writer.binding.Connection, connection) ||
                !ReferenceEquals(writer.plan.Expected, expected) || !writer.IsCurrent() ||
                (target with { Character = proposed.Character, PlayerItems = proposed.PlayerItems,
                    PlayerSpells = proposed.PlayerSpells, PlayerProficiencies = proposed.PlayerProficiencies }) != proposed ||
                !SameCharacter(target.Character, proposed.Character) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(target.PlayerItems).AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems)) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(target.PlayerSpells).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(proposed.PlayerSpells)) ||
                !PlayerProficiencySnapshotLoader.ToLegacyBytes(target.PlayerProficiencies).AsSpan().SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(proposed.PlayerProficiencies))) throw Unavailable();
        }
        internal byte[] RequireAfterImage(LegacyOrdinarySpellBinding binding, WorldSession expected, CharacterSnapshot proposed, byte[] actualBefore)
        {
            if (!ReferenceEquals(writer.binding, binding) || !ReferenceEquals(writer.plan.Expected, expected) || !writer.IsCurrent() ||
                !actualBefore.AsSpan().SequenceEqual(writer.before) || !SameCharacter(writer.Proposed.Character, proposed)) throw Unavailable();
            return writer.after.ToArray();
        }
        internal bool RequireCommittedCharacter() => SnapshotFileLockCoordinator.ExecuteLocked(writer.path, () =>
            writer.IsCurrent() && ReferenceEquals(writer.committed, writer.plan) && !LegacyTradeJournal.HasPending(writer.binding.UsersRoot) &&
            CharacterSnapshotFileBaseline.TryReadBytes(writer.path, "teleportDestination", out var actual, out _) &&
            actual.AsSpan().SequenceEqual(writer.after));
        private static bool SameCharacter(CharacterSnapshot a, CharacterSnapshot b)
        {
            if ((a with { Attributes = b.Attributes, PaidNobility = b.PaidNobility }) != b) return false;
            return SameFields(a.Attributes, b.Attributes) && SameFields(a.PaidNobility, b.PaidNobility);
        }
        private static bool SameFields(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
        {
            var first = new LegacyNativeAttributeMapProof(a); var second = new LegacyNativeAttributeMapProof(b);
            return first.Fields.Count == second.Fields.Count && first.Fields.All(row =>
                second.Fields.TryGetValue(row.Key, out var value) && value == row.Value) && first.IsCurrentPlain() && second.IsCurrentPlain();
        }
    }
    internal CharacterPublication PreparePublication() => CharacterPublication.Prepare(this);
}
