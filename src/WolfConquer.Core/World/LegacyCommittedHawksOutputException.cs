using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// A historical outcome diagnostic only. It cannot resume output or a cast.
internal sealed class LegacyCommittedHawksOutputException : InvalidOperationException
{
    private readonly WorldSessionStore.OwnedHawksOutputReceipt receipt;

    private LegacyCommittedHawksOutputException(WorldSessionStore.OwnedHawksOutputReceipt receipt, Exception failure)
        : base("committedHawksAmbitionOutputInterrupted", failure) => this.receipt = receipt;

    internal static Exception FromFailure(WorldSessionStore.OwnedHawksOutputReceipt receipt, Exception failure) =>
        receipt.HasCommittedOutcome ? new LegacyCommittedHawksOutputException(receipt, failure) : failure;

    internal bool IsFor(WorldServerFrameHandler frame, WorldSessionStore store, ConquerTcpConnection connection) =>
        receipt.IsHistoricalOutcomeFor(frame, store, connection);
}
