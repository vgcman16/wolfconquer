using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyPromotionEmeraldItemId = 1_080_001;
    private const uint LegacyPromotionMeteorItemId = 1_088_001;
    private const uint LegacyPromotionMoonBoxItemId = 721_080;
    private const uint LegacyPromotionDragonBallItemId = 1_088_000;

    private static async ValueTask<WorldSession> ApplyLegacyPromotionMasterItemsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerItemSnapshot updatedItems,
        byte optionId,
        string sourceMethod,
        string sourceRange,
        string mutationSummary)
    {
        var updatedSession = session with { PlayerItems = updatedItems };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            try
            {
                new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                    updatedSession.Character.Uid,
                    updatedItems);
                await context.Log.WriteLineAsync(
                        "WorldServer persisted LegacyNpc PromotionMaster item mutation " +
                        $"{FormatSession(updatedContext)} source={sourceMethod} sourceRange={sourceRange} " +
                        $"option={optionId} path={updatedSession.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin " +
                        $"{mutationSummary}.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is ArgumentException ||
                ex is NotSupportedException)
            {
                await context.Log.WriteLineAsync(
                        "PARITY-GAP LegacyNpc PromotionMaster item persistence " +
                        $"{FormatSession(updatedContext)} source={sourceMethod} sourceRange={sourceRange} " +
                        $"option={optionId} {mutationSummary} reason={ex.Message}.")
                    .ConfigureAwait(false);
            }

            return updatedSession;
        }

        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc PromotionMaster item mutation missing PlayerItemsRoot " +
                $"{FormatSession(updatedContext)} source={sourceMethod} sourceRange={sourceRange} " +
                $"option={optionId} {mutationSummary}; inventory update is session-only.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask SendLegacyPromotionMasterItemFramesAsync(
        WorldPacketHandlerContext context,
        IEnumerable<byte[]> frames,
        CancellationToken cancellationToken)
    {
        foreach (var frame in frames)
        {
            await context.Connection.SendAsync(frame, cancellationToken).ConfigureAwait(false);
        }
    }
}
