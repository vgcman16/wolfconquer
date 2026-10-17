using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask PersistLegacyPlayerQuestsAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string source,
        string action,
        uint questId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.PlayerQuestsRoot))
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyPlayerQuests persistence {FormatSession(context with { Session = session })} " +
                    $"source={source} action={action} questId={questId.ToString(CultureInfo.InvariantCulture)}; " +
                    "no PlayerQuestsRoot is available in this handler context, so the quest update is session-only.")
                .ConfigureAwait(false);
            return;
        }

        try
        {
            new PlayerQuestSnapshotLoader(context.PlayerQuestsRoot).SaveByCharacterUid(
                session.Character.Uid,
                session.Character.PlayerQuests);
            await context.Log.WriteLineAsync(
                    $"WorldServer persisted LegacyPlayerQuests {FormatSession(context with { Session = session })} " +
                    $"source={source} action={action} path={session.Character.Uid.ToString(CultureInfo.InvariantCulture)}.bin " +
                    $"questId={questId.ToString(CultureInfo.InvariantCulture)} " +
                    $"questCount={session.Character.PlayerQuests.Quests.Count.ToString(CultureInfo.InvariantCulture)}.")
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            await context.Log.WriteLineAsync(
                    $"PARITY-GAP LegacyPlayerQuests persistence {FormatSession(context with { Session = session })} " +
                    $"source={source} action={action} questId={questId.ToString(CultureInfo.InvariantCulture)}; " +
                    $"reason={ex.Message}.")
                .ConfigureAwait(false);
        }
    }
}
