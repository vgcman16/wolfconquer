using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // An empty token has no authority. Only the actual private class plan is
    // registered, after deriving its real clear/preserve projection.
    internal sealed class OwnedFullAccountClassSpellRetirement
    {
        private ClassRetirementData Data => ClassRetirements.TryGetValue(this, out var value) ? value :
            throw InvalidFullAccountClassState();

        internal void RequireImage(LegacyPreparedSpellFileWrite writer, WorldSession expected, PlayerSpellSnapshot spells)
        {
            var data = Data; var plan = data.Plan;
            if (!ReferenceEquals(plan.OriginSpellWriter, writer) || !ReferenceEquals(plan.Owner.Expected, expected) ||
                !data.IsCurrentPlain() || !SameBook(plan.Updated.PlayerSpells, spells)) throw InvalidFullAccountClassState();
            LegacyDerivedSpellOrigins.RequireRows(plan.Updated.Character, spells);
        }

        internal void RequireCurrent(WorldSessionStore store, ConquerTcpConnection connection,
            WorldSession expected, WorldSession proposed)
        {
            var data = Data; var plan = data.Plan;
            if (!ReferenceEquals(plan.Owner.Context.Sessions, store) || !ReferenceEquals(plan.Owner.Context.Connection, connection) ||
                !ReferenceEquals(plan.Owner.Expected, expected) || !data.IsCurrentPlain() ||
                !SameCharacter(data.Proposed.Character, proposed.Character) || !SameBook(data.Proposed.PlayerSpells, proposed.PlayerSpells) ||
                (data.Proposed with { Character = proposed.Character, PlayerSpells = proposed.PlayerSpells,
                    PlayerItems = proposed.PlayerItems, PlayerProficiencies = proposed.PlayerProficiencies }) != proposed ||
                !ReferenceEquals(expected.PlayerItems, proposed.PlayerItems) ||
                !PlayerProficiencySnapshotLoader.ToLegacyBytes(expected.PlayerProficiencies).AsSpan()
                    .SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(proposed.PlayerProficiencies)))
                throw InvalidFullAccountClassState();
        }

        internal byte[] RequireAfterImage(LegacyOrdinarySpellBinding binding, WorldSession expected,
            CharacterSnapshot target, byte[] before)
        {
            var data = Data; var plan = data.Plan;
            if (!ReferenceEquals(expected, plan.Owner.Expected)) throw new FullAccountClassRefusal("fullAccountClassHpExpectedChanged");
            if (!ReferenceEquals(binding.Store, plan.Owner.Context.Sessions)) throw new FullAccountClassRefusal("fullAccountClassHpStoreChanged");
            if (!ReferenceEquals(binding.Connection, plan.Owner.Context.Connection)) throw new FullAccountClassRefusal("fullAccountClassHpConnectionChanged");
            if (!data.IsCurrentPlain()) throw new FullAccountClassRefusal("fullAccountClassHpOwnerChanged");
            if (!before.AsSpan().SequenceEqual(plan.OriginChanges[0].Before)) throw new FullAccountClassRefusal("fullAccountClassHpBeforeChanged");
            // The bound HP writer adds its canonical persisted scalar attributes
            // to its captured After. Compare the complete independently derived
            // same projection, including opaque fields, rather than the raw plan.
            var projected = new BoundHitpointsInputs(data.Proposed.Character, data.Proposed.Character).After;
            if (!SameCharacter(projected, target)) throw new FullAccountClassRefusal("fullAccountClassHpTargetChanged");
            return plan.OriginChanges[0].After.ToArray();
        }

        internal void RequireBeforeCommit(LegacyPreparedSpellFileWrite writer, IReadOnlyList<LegacyTradeJournalChange> changes)
        {
            var data = Data; var plan = data.Plan;
            if (!ReferenceEquals(plan.OriginSpellWriter, writer) || !data.IsCurrentPlain() || changes.Count != 2)
                throw InvalidFullAccountClassState();
            for (int index = 0; index < 2; index++)
            {
                var expected = plan.OriginChanges[index]; var actual = changes[index];
                if (expected.Kind != actual.Kind || expected.FileId != actual.FileId ||
                    !SameBytes(expected.Before, actual.Before) || !expected.After.AsSpan().SequenceEqual(actual.After))
                    throw InvalidFullAccountClassState();
            }
        }

        internal bool IsCurrentPlain() => ClassRetirements.TryGetValue(this, out var data) && data.IsCurrentPlain();
        internal bool CoversAttributes(LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed)
        {
            var data = Data;
            return ReferenceEquals(data.Plan.Owner.Expected, expected) &&
                ReferenceEquals(data.Plan.Owner.Context.Sessions, binding.Store) &&
                ReferenceEquals(data.Plan.Owner.Context.Connection, binding.Connection) && data.IsCurrentPlain() &&
                LegacyNativeAttributeState.From(data.Proposed.Character) == LegacyNativeAttributeState.From(proposed.Character);
        }
        internal bool CoversLevelExperience(LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed) =>
            CoversAttributes(binding, expected, proposed) &&
            Data.Proposed.Character.Level == proposed.Character.Level &&
            Data.Proposed.Character.Experience == proposed.Character.Experience;
        internal bool RequireCommittedPair() => IsCurrentPlain() && Data.Plan.OriginSpellWriter.HasCommittedClassRetirement(this);
        internal IReadOnlyList<LegacyTradeJournalChange> CopyCapturedChanges() => Data.Plan.OriginChanges.Select(change =>
            new LegacyTradeJournalChange(change.Kind, change.FileId, change.Before?.ToArray(), change.After.ToArray())).ToArray();

        private static bool SameBytes(byte[]? first, byte[]? second) => first is null ? second is null :
            second is not null && first.AsSpan().SequenceEqual(second);
        private static bool SameBook(PlayerSpellSnapshot first, PlayerSpellSnapshot second) =>
            PlayerSpellSnapshotLoader.ToLegacyBytes(first).AsSpan().SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(second));
        private static bool SameCharacter(CharacterSnapshot first, CharacterSnapshot second) =>
            (first with { Attributes = second.Attributes, PaidNobility = second.PaidNobility }) == second &&
            SameMap(first.Attributes, second.Attributes) && SameMap(first.PaidNobility, second.PaidNobility);
        private static bool SameMap(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second)
        {
            var a = new LegacyNativeAttributeMapProof(first); var b = new LegacyNativeAttributeMapProof(second);
            return a.Fields.Count == b.Fields.Count && a.Fields.All(pair => b.Fields.TryGetValue(pair.Key, out var value) && value == pair.Value) &&
                a.IsCurrentPlain() && b.IsCurrentPlain();
        }
    }

    private sealed class ClassRetirementData
    {
        internal readonly LegacyFullAccountClassPrepared Plan;
        internal readonly WorldSession Proposed;
        private readonly FullAccountStringMap before, paidBefore, after, paidAfter;
        private readonly Dictionary<string, string>.Enumerator beforeWitness, paidBeforeWitness, afterWitness, paidAfterWitness;
        private readonly LegacyPerformerSpellSource sourceSpells, proposedSpells;

        internal ClassRetirementData(LegacyFullAccountClassPrepared plan)
        {
            Plan = plan; Proposed = plan.Updated;
            before = new(plan.Owner.Expected.Character.Attributes); paidBefore = new(plan.Owner.Expected.Character.PaidNobility);
            after = new(Proposed.Character.Attributes); paidAfter = new(Proposed.Character.PaidNobility);
            // Acquire the real BCL enumerators while constructing the token,
            // before the Store gate and its final callback-free proof.
            beforeWitness = before.Open(); paidBeforeWitness = paidBefore.Open();
            afterWitness = after.Open(); paidAfterWitness = paidAfter.Open();
            sourceSpells = new(plan.Owner.Expected.PlayerSpells.Spells); proposedSpells = new(Proposed.PlayerSpells.Spells);
            LegacyDerivedSpellOrigins.RequireRows(plan.Owner.Expected.Character, plan.Owner.Expected.PlayerSpells);
            LegacyDerivedSpellOrigins.RequireRows(Proposed.Character, Proposed.PlayerSpells);
        }

        internal bool IsCurrentPlain()
        {
            try
            {
                // Enumerator arguments are value copies; each check starts at
                // its captured position without invoking the source again.
                return before.Matches(beforeWitness) && paidBefore.Matches(paidBeforeWitness) &&
                    after.Matches(afterWitness) && paidAfter.Matches(paidAfterWitness) &&
                    sourceSpells.IsCurrent() && proposedSpells.IsCurrent() && Plan.Owner.IsPlainCurrent(Plan.Owner.Expected);
            }
            catch (InvalidOperationException) { return false; }
        }
    }

    private static readonly ConditionalWeakTable<OwnedFullAccountClassSpellRetirement, ClassRetirementData> ClassRetirements = new();

    private static OwnedFullAccountClassSpellRetirement CaptureFullAccountClassRetirement(LegacyFullAccountClassPrepared plan)
    {
        var token = new OwnedFullAccountClassSpellRetirement();
        ClassRetirements.Add(token, new(plan));
        return token;
    }
}
