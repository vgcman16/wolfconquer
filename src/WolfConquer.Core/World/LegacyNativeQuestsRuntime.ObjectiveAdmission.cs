using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
    private enum ObjectiveMutation { KingdomSet, Set, Increase }

    private void ApplyObjectiveMutation(LegacyNativePacket caller, uint uid, uint[] intentions, ObjectiveMutation operation)
    {
        var player = PersistenceOwner;
        if (player is null || player.Owner is not { } client)
            throw new InvalidDataException("Quest objective storage has no genuine constructor owner.");
        ObjectiveUpdate? update = null;
        lock (client.SpellAdmissionSyncRoot)
        lock (player.SpellAdmissionSyncRoot)
        lock (PersistenceSyncRoot)
        {
            try
            {
                var graph = new ObjectiveGraph(this, client, player);
                if (!graph.TrySelect(uid, operation, out var row)) return;
                if (intentions is null) throw new InvalidDataException("Quest objective input is unavailable.");
                string? seal = LegacyNativePacket.SealString;
                LegacyNativeQuestPacket.GetQuestObjectiveFrameLength(uid, Math.Max(intentions.Length, row!.Values.Length), seal);
                var input = (uint[])intentions.Clone();
                var next = new uint[Math.Max(input.Length, row!.Values.Length)];
                row.Values.CopyTo(next, 0);
                for (int index = 0; index < input.Length; index++)
                    next[index] = operation == ObjectiveMutation.Increase
                        ? checked(next[index] + input[index]) : input[index];

                update = new ObjectiveUpdate(graph, row, next, caller, seal);
                if (!update.CanCommit()) throw new InvalidDataException("Quest objective preparation is stale.");
                update.Commit();
            }
            catch (Exception error) when (error is OverflowException or ArgumentException or InvalidOperationException)
            {
                throw new InvalidDataException("Quest objective preparation was rejected.", error);
            }
        }
        // Delivery is after the accepted row/caller update. A dropped send is
        // not a rejected mutation, and cannot be repaired by rolling back a row.
        update!.Publish();
    }

    private sealed class ObjectiveUpdate
    {
        private readonly ObjectiveGraph graph;
        private readonly ObjectiveRow row;
        private readonly uint[] expectedValues;
        private readonly uint[] committedValues;
        private readonly ObjectivePublication publication;
        private bool committed;

        internal ObjectiveUpdate(ObjectiveGraph graph, ObjectiveRow row, uint[] next, LegacyNativePacket caller, string? seal)
        {
            this.graph = graph; this.row = row;
            expectedValues = next;
            committedValues = (uint[])next.Clone();
            publication = new ObjectivePublication(caller,
                LegacyNativeQuestPacket.PrepareQuestObjectiveFrame(row.Uid, expectedValues, seal));
        }

        internal bool CanCommit() => !committed && graph.IsCurrent() && publication.HasOriginalCaller();

        internal void Commit()
        {
            // All arrays/frame/caller snapshots and validations precede these writes.
            row.Row.Intentions = committedValues;
            publication.CopyCaller();
            committed = true;
        }

        internal void Publish()
        {
            if (!committed) return;
            try
            {
                lock (graph.Client.SpellAdmissionSyncRoot)
                lock (graph.Player.SpellAdmissionSyncRoot)
                lock (graph.Book.PersistenceSyncRoot)
                {
                    if (graph.IsCurrent(row.Row, committedValues, expectedValues) && publication.HasPreparedCaller())
                        graph.Client.Send(publication.Caller);
                }
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or InvalidDataException)
            {
                // This is diagnostic delivery failure after acceptance, not a refund.
                try { WolfConquer.Core.Runtime.LegacyNativeConsole.WriteException(error); }
                catch (Exception) { }
            }
        }
    }
}
