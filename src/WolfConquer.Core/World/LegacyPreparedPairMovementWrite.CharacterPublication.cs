using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedPairMovementWrite
{
    // A role of this exact real two-character writer. It cannot independently
    // write a scalar file or authorize a different prepared Store outcome.
    internal sealed class CharacterPublication
    {
        private readonly LegacyPreparedPairMovementWrite writer;
        private readonly Member member;
        private readonly byte[] items, spells, proficiencies;

        private CharacterPublication(LegacyPreparedPairMovementWrite writer, Member member)
        {
            this.writer = writer; this.member = member;
            items = PlayerItemSnapshotLoader.ToLegacyBytes(member.Before.PlayerItems);
            spells = PlayerSpellSnapshotLoader.ToLegacyBytes(member.Before.PlayerSpells);
            proficiencies = PlayerProficiencySnapshotLoader.ToLegacyBytes(member.Before.PlayerProficiencies);
        }

        internal static CharacterPublication ForActor(LegacyPreparedPairMovementWrite writer)
        {
            if (!writer.IsCurrent()) throw Invalid();
            return new(writer, writer.actor);
        }
        internal static CharacterPublication ForPartner(LegacyPreparedPairMovementWrite writer)
        {
            if (!writer.IsCurrent()) throw Invalid();
            return new(writer, writer.partner);
        }

        internal void RequireCurrent(WorldSessionStore store, ConquerTcpConnection connection,
            WorldSession expected, WorldSession proposed)
        {
            var target = member.Proposed;
            if (!ReferenceEquals(writer.Store, store) || !ReferenceEquals(member.Binding.Connection, connection) ||
                !ReferenceEquals(member.Before, expected) || !writer.IsCurrent() ||
                (target with { Character = proposed.Character, PlayerItems = proposed.PlayerItems,
                    PlayerSpells = proposed.PlayerSpells, PlayerProficiencies = proposed.PlayerProficiencies }) != proposed ||
                !SameCharacter(target.Character, proposed.Character) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems).AsSpan().SequenceEqual(items) ||
                !PlayerSpellSnapshotLoader.ToLegacyBytes(proposed.PlayerSpells).AsSpan().SequenceEqual(spells) ||
                !PlayerProficiencySnapshotLoader.ToLegacyBytes(proposed.PlayerProficiencies).AsSpan().SequenceEqual(proficiencies))
                throw Invalid();
        }

        internal byte[] RequireAfterImage(LegacyOrdinarySpellBinding binding, WorldSession expected,
            CharacterSnapshot target, byte[] actualBefore)
        {
            if (!ReferenceEquals(member.Binding, binding) || !ReferenceEquals(member.Before, expected) ||
                !writer.IsCurrent() || !actualBefore.AsSpan().SequenceEqual(member.BeforeImage) ||
                !SameCharacter(member.Proposed.Character, target)) throw Invalid();
            return member.AfterImage.ToArray();
        }

        internal bool RequireCommittedCharacter() => SnapshotFileLockCoordinator.ExecuteLocked(
            new[] { writer.actor.Path, writer.partner.Path }, () => writer.IsCurrent() && writer.RequireCommittedImages());

        private static bool SameCharacter(CharacterSnapshot expected, CharacterSnapshot proposed) =>
            (expected with { Attributes = proposed.Attributes, PaidNobility = proposed.PaidNobility }) == proposed &&
            SameFields(expected.Attributes, proposed.Attributes) && SameFields(expected.PaidNobility, proposed.PaidNobility);

        private static bool SameFields(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> proposed)
        {
            var first = new LegacyNativeAttributeMapProof(expected);
            var second = new LegacyNativeAttributeMapProof(proposed);
            if (first.Fields.Count != second.Fields.Count) return false;
            foreach (var row in first.Fields)
                if (!second.Fields.TryGetValue(row.Key, out var value) || value != row.Value) return false;
            return first.IsCurrentPlain() && second.IsCurrentPlain();
        }

        private static InvalidOperationException Invalid() => new("pairMovementCharacterPublicationMismatch");
    }

    internal CharacterPublication PrepareActorPublication() => CharacterPublication.ForActor(this);
    internal CharacterPublication PreparePartnerPublication() => CharacterPublication.ForPartner(this);
}
