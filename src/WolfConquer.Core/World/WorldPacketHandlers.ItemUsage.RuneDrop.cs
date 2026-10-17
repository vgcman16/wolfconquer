namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyRuneDropSource = "MsgItemUsuagePacket.DropRune";
    private const string LegacyRuneDropLines = "2070-2073";

    private static async ValueTask HandleLegacyDropRuneAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        MsgItemUsagePacket packet,
        CancellationToken cancellationToken)
    {
        if (!TryGetRuneItem(session.PlayerItems, packet.Id, out var rune))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage drop rune " +
                    $"{FormatSession(context)} source={LegacyRuneDropSource} " +
                    $"sourceLines={LegacyRuneDropLines} itemUid={packet.Id}; rune item was not found.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSession = session with
        {
            PlayerItems = RemoveLegacyRuneItem(session.PlayerItems, rune),
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await TryPersistLegacyItemUsageMutationAsync(
                updatedContext,
                updatedSession,
                "drop rune",
                characterValues: null,
                persistItems: true)
            .ConfigureAwait(false);

        await context.Connection.SendAsync(
                new MsgItemUsagePacket(
                    rune.Uid,
                    DwParam: 0,
                    MsgItemUsageAction.RemoveInventory,
                    Timestamp: 0,
                    DwParam2: 0,
                    DwParam3: 0,
                    DwParam4: 0).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage drop rune " +
                $"{FormatSession(updatedContext)} source={LegacyRuneDropSource} " +
                $"sourceLines={LegacyRuneDropLines} itemUid={rune.Uid} itemId={rune.ItemId} " +
                $"position={rune.Position}.")
            .ConfigureAwait(false);
    }
}
