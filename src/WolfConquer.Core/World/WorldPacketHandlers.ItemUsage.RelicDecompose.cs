using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRelicDecomposeSource = "MsgItemUsuagePacket.DecomposeRelics";

    private static async ValueTask HandleLegacyDecomposeRelicsAsync(WorldPacketHandlerContext context,
        WorldSession session, MsgItemUsagePacket packet, PacketFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!LegacyRelicDecomposeRequest.TryParse(packet, frame, out var request, out var reason) || request is null)
        {
            await context.Log.WriteLineAsync($"WorldServer held DecomposeRelics reason={reason}; no success was published.")
                .ConfigureAwait(false);
            return;
        }
        if (!TryCommitOwnedRelicDecompose(context, session, request, cancellationToken,
                out var committed, out var wire, out reason))
        {
            await context.Log.WriteLineAsync($"WorldServer held DecomposeRelics reason={reason}; no success was published.")
                .ConfigureAwait(false);
            return;
        }
        await PublishOwnedRelicWireAsync(context, wire!, cancellationToken).ConfigureAwait(false);
        await context.Log.WriteLineAsync("WorldServer committed MsgItemUsage decompose relics " +
            $"{FormatSession(context with { Session = committed })} source={LegacyRelicDecomposeSource} " +
            $"selected={request.SelectedUids.Length} rewardItemId={LegacyRelicDecomposeRecipe.RewardItemId} " +
            $"rewardRows={request.SelectedUids.Length * LegacyRelicDecomposeRecipe.RewardsPerInput}.").ConfigureAwait(false);
    }
}
