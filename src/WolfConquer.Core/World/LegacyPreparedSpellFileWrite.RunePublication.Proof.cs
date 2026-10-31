using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedSpellFileWrite
{
    // Issued by the actual prepared spell writer, never by a detached image or
    // callback. Only that writer's successful matching Rune journal completes it.
    internal sealed partial class RuneCharacterPublication
    {
        private readonly LegacyPreparedSpellFileWrite writer;
        private readonly CharacterSnapshot character;
        private readonly LegacyTradeJournalChange[] changes;
        private readonly LegacyRuneOperationEvidence? rune;
        private readonly LegacyRuneContainerEvidence? container;
        private readonly LegacyRuneComposeEvidence? compose;
        private readonly LegacyRuneChanceEvidence? chance;
        private readonly LegacyRuneDecomposeEvidence? decompose;

        private RuneCharacterPublication(LegacyPreparedSpellFileWrite writer, CharacterSnapshot character,
            LegacyTradeJournalChange[] changes, LegacyRuneOperationEvidence? rune, LegacyRuneContainerEvidence? container,
            LegacyRuneComposeEvidence? compose, LegacyRuneChanceEvidence? chance, LegacyRuneDecomposeEvidence? decompose)
        { this.writer = writer; this.character = character; this.changes = changes; this.rune = rune; this.container = container; this.compose = compose; this.chance = chance; this.decompose = decompose; }

        internal void RequireCurrent(WorldSessionStore store, ConquerTcpConnection connection,
            WorldSession expected, WorldSession proposed)
        {
            if (!ReferenceEquals(writer.store, store) || !ReferenceEquals(writer.connection, connection) ||
                !ReferenceEquals(writer.expected, expected) || !ReferenceEquals(proposed.Character, character) ||
                proposed.AccountId != expected.AccountId || proposed.Character.Uid != expected.Character.Uid ||
                !ReferenceEquals(proposed.NativeGameClient, writer.binding!.Client) ||
                !PlayerItemSnapshotLoader.ToLegacyBytes(proposed.PlayerItems).AsSpan().SequenceEqual(changes[1].After) ||
                !LegacyRuneSpellOrigins.ProjectSpellImage(changes[2].Before, proposed.PlayerSpells).AsSpan().SequenceEqual(changes[2].After) ||
                !writer.OwnerIsCurrentPlain() || rune is not null && !rune.IsCurrent() ||
                container is not null && !container.IsCurrent() || compose is not null && !compose.IsCurrent() ||
                chance is not null && !chance.IsCurrent() || decompose is not null && !decompose.IsCurrent()) throw Unavailable();
            decompose?.RequireProposedCharacter(proposed.Character);
        }

        internal byte[] RequireAfterImage(LegacyOrdinarySpellBinding binding, WorldSession expected,
            CharacterSnapshot target, byte[] actualBefore)
        {
            if (!ReferenceEquals(writer.binding, binding) || !ReferenceEquals(writer.expected, expected) ||
                !writer.OwnerIsCurrentPlain() || !actualBefore.AsSpan().SequenceEqual(changes[0].Before) ||
                !WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(character, target) ||
                !WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(character, target) ||
                !LegacyRuneSpellOrigins.Read(character).SequenceEqual(LegacyRuneSpellOrigins.Read(target))) throw Unavailable();
            return changes[0].After.ToArray();
        }

        internal void RequireBeforeCommit(LegacyPreparedSpellFileWrite actual,
            IReadOnlyList<LegacyTradeJournalChange> owned, LegacyRuneOperation? operation, LegacyRuneContainerOperation? containerOperation,
            LegacyRuneComposeOperation? composeOperation, LegacyRuneChanceOperation? chanceOperation,
            LegacyRuneDecomposeOperation? decomposeOperation)
        {
            if (!ReferenceEquals(writer, actual) || !writer.OwnerIsCurrentPlain() ||
                !ReferenceEquals(rune?.Operation, operation) || !ReferenceEquals(container?.Operation, containerOperation) ||
                !ReferenceEquals(compose?.Operation, composeOperation) ||
                !ReferenceEquals(chance?.Operation, chanceOperation) ||
                !ReferenceEquals(decompose?.Operation, decomposeOperation) ||
                rune is not null && !rune.IsCurrent() || container is not null && !container.IsCurrent() || owned.Count != changes.Length)
                throw Unavailable();
            if (compose is not null && !compose.IsCurrent() || chance is not null && !chance.IsCurrent() ||
                decompose is not null && !decompose.IsCurrent()) throw Unavailable();
            for (var index = 0; index < changes.Length; index++)
            {
                var captured = changes[index]; var current = owned[index];
                if (captured.Kind != current.Kind || captured.FileId != current.FileId ||
                    !Same(captured.Before, current.Before) || !captured.After.AsSpan().SequenceEqual(current.After)) throw Unavailable();
            }
        }

        internal bool RequireCommittedCharacter()
        {
            try
            {
                var paths = decompose is null ? new[] { writer.CharacterPath } : new[] { writer.CharacterPath, decompose.ItemPath, writer.SpellsPath };
                return SnapshotFileLockCoordinator.ExecuteLocked(paths, () =>
                    writer.committed && ReferenceEquals(writer.committedRuneCharacter, this) &&
                    ReferenceEquals(writer.binding!.SavedSpells, writer.prepared) && !writer.Recovery.IsBlocked &&
                    writer.store.IsSpellFileWriteOwnerCurrent(writer.connection, writer.expected, writer.identity, writer.binding) &&
                    !LegacyTradeJournal.HasPending(writer.UsersRoot) &&
                    CharacterSnapshotFileBaseline.TryReadBytes(writer.CharacterPath, "runeCharacterPublication", out var actual, out _) &&
                    actual.AsSpan().SequenceEqual(changes[0].After) && DecomposeCommittedSidecarsMatch());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
                NotSupportedException or System.Security.SecurityException or FormatException or OverflowException) { return false; }
        }

        private static bool Same(byte[]? first, byte[]? second) => first is null ? second is null :
            second is not null && first.AsSpan().SequenceEqual(second);
    }
}
