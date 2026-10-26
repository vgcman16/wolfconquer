using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    internal static bool TryCaptureMailboxLoadProof(out MailboxLoadProof? proof)
    {
        lock (MailboxLoadGate)
        {
            proof = ReadMailboxGraph().Proof;
            if (proof is not null && proof.IsCurrent()) return true;
            proof = null;
            return false;
        }
    }

    // Concrete completion can only be constructed inside the actual staged
    // LoadAndPublish path below, never from a flag, existence check or callback.
    internal sealed class MailboxLoadProof
    {
        private readonly MailboxMetadataState metadata;
        private readonly string location;
        private MailboxLoadProof(MailboxMetadataState metadata, string location)
        { this.metadata = metadata; this.location = location; }

        internal static void LoadAndPublish(string prefix)
        {
            lock (MailboxLoadGate)
            {
                var rankPath = LegacyNativeServerConfig.DbLocation + "CombatGearRank.txt";
                if (!MailboxLoadStage.TryCreate(prefix, rankPath, out var stage, out var reason))
                    throw new InvalidDataException(reason);
                RegisterMessages();
                ProbeMailboxSerializer();
                if (!stage!.IsCurrentInputs()) throw new IOException("Gear metadata inputs changed before publication.");
                var prepared = new MailboxGraph(stage.Gear, stage.Jade, stage.Lottery, stage.Collection,
                    stage.Ranks, prefix, true, true, null);
                var state = MailboxMetadataState.Capture(prepared);
                if (!state.Matches(prepared) || !MailboxContractsCurrent())
                    throw new InvalidDataException("Complete gear metadata changed before publication.");
                var proof = new MailboxLoadProof(state, prefix);
                Volatile.Write(ref mailboxGraph, prepared with { Proof = proof });
            }
        }

        internal bool IsCurrent()
        {
            lock (MailboxLoadGate)
            {
                try
                {
                    var graph = ReadMailboxGraph();
                    return graph.CompleteReady && graph.RawGate && ReferenceEquals(graph.Proof, this) &&
                        graph.Location == location && metadata.Matches(graph) && MailboxContractsCurrent() &&
                        ReferenceEquals(ReadMailboxGraph(), graph);
                }
                catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
                { return false; }
            }
        }

        internal bool TryGetGear(uint id, uint level, out CQ? found)
        {
            lock (MailboxLoadGate)
            {
                found = null;
                if (!IsCurrent()) return false;
                var graph = ReadMailboxGraph();
                if (!graph.Gear!.TryGetValue(id, out var levels) || !levels.TryGetValue(level, out var row)) return false;
                if (!IsCurrent()) return false;
                found = row;
                return true;
            }
        }

        internal bool TryGetJade(uint id, out LegacyNativeDaoqiDictTypeTable.CQ? found)
        {
            lock (MailboxLoadGate)
            {
                found = null;
                if (!IsCurrent() || !ReadMailboxGraph().Jade!.TryGetValue(id, out var row) || !IsCurrent()) return false;
                found = row;
                return true;
            }
        }
    }
}
