using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalWrite
{
    internal sealed class CharacterPublication
    {
        private readonly LegacyPreparedBasicPhysicalWrite writer;
        private CharacterPublication(LegacyPreparedBasicPhysicalWrite writer) => this.writer = writer;
        internal static CharacterPublication Prepare(LegacyPreparedBasicPhysicalWrite writer)
        {
            if (!writer.IsCurrent()) throw Unavailable();
            return new(writer);
        }
        internal void RequireCurrent(WorldSessionStore store, ConquerTcpConnection connection, WorldSession expected, WorldSession proposed)
        {
            if (!ReferenceEquals(store, writer.Store) || !ReferenceEquals(connection, writer.Binding.Connection) ||
                !ReferenceEquals(expected, writer.Plan.ExpectedTarget) || !writer.IsCurrent() ||
                !writer.Plan.CanonicalOutcomesMatch(writer.Plan.Actor, proposed) ||
                !writer.Plan.Death!.TryReadNativeQueryProposal(writer.Binding, proposed, out _)) throw Unavailable();
        }
        internal byte[] RequireAfterImage(LegacyOrdinarySpellBinding binding, WorldSession expected,
            CharacterSnapshot target, byte[] actualBefore)
        {
            if (!ReferenceEquals(binding, writer.Binding) || !ReferenceEquals(expected, writer.Plan.ExpectedTarget) ||
                !writer.IsCurrent() || !actualBefore.AsSpan().SequenceEqual(writer.characterBefore) ||
                !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(writer.Proposed.Character, target) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(writer.Proposed.Character, target)) throw Unavailable();
            return writer.characterAfter.ToArray();
        }
        internal bool RequireCommittedCharacter() => SnapshotFileLockCoordinator.ExecuteLocked(writer.Paths, () =>
            writer.Plan.IsCurrentPlain() && ReferenceEquals(writer.committed, writer.Operation) &&
            writer.spells.HasCommittedPhysicalDeathAnchor(writer) && !LegacyTradeJournal.HasPending(writer.Roots.Users) &&
            writer.ImagesMatch(after: true));
    }
    internal CharacterPublication PreparePublication() => CharacterPublication.Prepare(this);
}
