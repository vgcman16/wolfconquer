using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private const string GmRefusal = "GM command refused or unavailable. Use an explicitly authorized test character and Talk chat.";

    private async ValueTask SendGmRefusalAsync(ConquerTcpConnection connection, CancellationToken cancellationToken)
    {
        // Fixed, non-sensitive refusal goes only to the requesting transport.
        // It contains no command text, account identity, policy or paths.
        lock (_tradeStates.SyncRoot)
            if (!connection.HasSendRoute || _tradeStates.HasConnectionEnded(connection)) return;
        await connection.SendAsync(LegacyMsgMessagePacket.SystemWhiteTalk(GmRefusal).ToFrameBytes(),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SendGmLinesAsync(GmCommandActor actor, IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count is < 1 or > 12 || lines.Any(line => line.Length is < 1 or > 180 ||
                line.Any(value => value is < ' ' or > '~')))
            throw new InvalidDataException("gmResponseBoundsInvalid");
        foreach (var line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_tradeStates.SyncRoot)
                if (!GmResponseActorCurrent(actor, out _)) return;
            await actor.Connection.SendAsync(LegacyMsgMessagePacket.SystemWhiteTalk(line).ToFrameBytes(),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string[] GmMailOutcomeLines(LegacyOperatorGearMailResult result) =>
    [
        $"GM kit operation={result.OperationId:N} status={result.Status}.",
        $"Mailbox entries={result.MailIds.Length}; item UIDs={result.ItemUids.Length}. Keep the same operation ID for retry; never remint after an uncertain outcome."
    ];
}
