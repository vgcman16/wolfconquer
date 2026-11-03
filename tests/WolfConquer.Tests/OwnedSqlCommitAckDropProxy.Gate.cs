namespace WolfConquer.Tests;

internal sealed partial class OwnedSqlCommitAckDropProxy
{
    private sealed class GatedClientEofException : EndOfStreamException { }

    private sealed class CommitAckGate(bool holdRequest)
    {
        internal bool HoldRequest { get; } = holdRequest;
        internal readonly TaskCompletionSource<CommitRequestGateObservation> RequestObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> RequestRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Forwarding, QuitForwarded;
        internal readonly TaskCompletionSource<CommitAckGateObservation> Observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Forwarded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

internal sealed record CommitAckGateObservation(int ObservedCommitQueries, byte AckSequence, int AckPayloadBytes, ushort StatusFlags);

internal sealed record CommitRequestGateObservation(int ObservedCommitQueries, byte Sequence, int PayloadBytes);
