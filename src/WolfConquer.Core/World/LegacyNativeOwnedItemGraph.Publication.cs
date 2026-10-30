using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeOwnedItemGraph
{
    internal sealed class PreparedPublication : IDisposable
    {
        internal LegacyNativeOwnedItemGraph Graph { get; }
        internal PlayerItemSnapshot FinalSnapshot => preparation.FinalSnapshot;
        private readonly LegacyOwnedNativeItemImage.Preparation preparation;
        private readonly bool admission, changed;
        private readonly uint beforeUid;
        private readonly LegacyOwnedNativeItemImage? baseImage;
        private readonly PhysicalState before, candidate;
        private readonly StorageLease gates;
        private StateWitness? beforeWriter, afterWriter, candidateBefore, candidateAfter;
        private bool armed, disposed, published;
        private readonly int thread = Environment.CurrentManagedThreadId;

        internal PreparedPublication(
            LegacyNativeOwnedItemGraph graph,
            LegacyOwnedNativeItemImage.Preparation preparation,
            bool admission)
        {
            Graph = graph;
            this.preparation = preparation;
            this.admission = admission;
            beforeUid = graph.Player.UID;
            baseImage = admission ? null : graph.image;
            gates = new StorageLease(graph);
            try
            {
                if (!OriginalOwnerCurrentPlain())
                    throw Invalid();
                before = admission ? graph.CaptureState(null) : graph.current;
                if (!before.ContentsCurrentPlain())
                    throw Invalid();
                // Validate the last published graph before metadata/converter callbacks.
                if (!admission && !graph.IsCurrentPlain())
                    throw Invalid();
                preparation.RequireCurrent();
                if (!OriginalOwnerCurrentPlain() || !before.ContentsCurrentPlain())
                    throw Invalid();
                changed = admission || !ReferenceEquals(preparation.Image, baseImage);
                candidate = changed ? graph.PrepareState(preparation.Image) : before;
                if (!admission && !graph.IsCurrentPlain())
                    throw Invalid();
                if (!candidate.PrivateContentsCurrentPlain() || !preparation.IsCurrentPlain())
                    throw Invalid();
                // Exposing a detached prepared image does not make it a current owner.
                if (admission)
                    graph.image = preparation.Image;
                RequireCurrentBeforeWriter();
            }
            catch
            {
                gates.Dispose();
                throw;
            }
        }

        private bool OriginalOwnerCurrentPlain() =>
            !Graph.retired && !Graph.Client.Fake &&
            Monitor.IsEntered(Graph.Client.SpellAdmissionSyncRoot) &&
            Monitor.IsEntered(Graph.Player.SpellAdmissionSyncRoot) &&
            ReferenceEquals(Graph.Client.Player, Graph.Player) &&
            ReferenceEquals(Graph.Player.Owner, Graph.Client) && Graph.Player.UID == beforeUid &&
            Graph.ConstructedOwnersCurrent() &&
            (admission
                ? !Graph.published && Graph.SlotsAbsent()
                : Graph.published && Graph.SlotsCurrent() &&
                  ReferenceEquals(Graph.image, baseImage) && Graph.current.ReferencesCurrentPlain(Graph));

        internal void RequireCurrentBeforeWriter()
        {
            RequirePlan();
            if (!OriginalOwnerCurrentPlain() || !before.ReferencesCurrentPlain(Graph) ||
                !before.ContentsCurrentPlain() || (!admission && !Graph.image.IsCurrentPlain()))
                throw Invalid();
            preparation.RequireCurrent();
            if (!OriginalOwnerCurrentPlain() || !before.ReferencesCurrentPlain(Graph) ||
                !before.ContentsCurrentPlain() || !candidate.PrivateContentsCurrentPlain() ||
                !preparation.IsCurrentPlain())
                throw Invalid();
        }

        internal void ArmFinalProof()
        {
            RequirePlan();
            if (armed)
                throw Invalid();
            RequireCurrentBeforeWriter();
            beforeWriter = before.CreateWitness();
            afterWriter = before.CreateWitness();
            candidateBefore = candidate.CreateWitness();
            candidateAfter = candidate.CreateWitness();
            armed = true;
        }

        internal bool ConsumeBeforeWriterPlain() => Consume(beforeWriter, candidateBefore);
        internal bool ConsumeCurrentPlain() => Consume(afterWriter, candidateAfter);

        private bool Consume(StateWitness? oldWitness, StateWitness? preparedWitness)
        {
            if (disposed || published || !armed || Environment.CurrentManagedThreadId != thread ||
                !OriginalOwnerCurrentPlain() || !before.ReferencesCurrentPlain(Graph) ||
                (!admission && !Graph.image.IsCurrentPlain()) || !preparation.IsCurrentPlain())
                return false;
            try
            {
                return oldWitness is not null && preparedWitness is not null &&
                    oldWitness.Consume() && preparedWitness.Consume() &&
                    before.PrivateContentsCurrentPlain() && candidate.PrivateContentsCurrentPlain();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private void RequirePlan()
        {
            if (disposed || published || Environment.CurrentManagedThreadId != thread)
                throw Invalid();
        }

        // C calls after actual durable/prefix commitment and the final plain proof.
        // No validation, callback, allocation or gate acquisition follows first assignment.
        internal void PublishPrepared()
        {
            if (changed)
                candidate.PublishUnchecked(Graph);
            if (admission)
            {
                Graph.Client.Inventory = Graph.Inventory;
                Graph.Client.Equipment = Graph.Equipment;
                Graph.Client.Warehouse = Graph.Warehouse;
                Graph.Client.MyWardrobe = Graph.Wardrobe;
                Graph.Client.Rune = Graph.Rune;
                Graph.Client.RelicFuse = Graph.Relic;
                Graph.Client.GearMythsoulBag = Graph.Gear;
            }
            Graph.current = candidate;
            Graph.image = preparation.Image;
            Graph.published = true;
            published = true;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            if (Environment.CurrentManagedThreadId != thread)
                throw Invalid();
            disposed = true;
            gates.Dispose();
        }
    }
}
