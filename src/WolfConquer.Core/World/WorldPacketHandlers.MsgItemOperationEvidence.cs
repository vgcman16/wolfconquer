using System.Buffers;
using System.Security.Cryptography;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const int MsgItemOperationEvidencePayloadHexLimit = 256;
    private const string MsgItemOperationEvidencePrefix = "WIRE-EVIDENCE MsgItemOperationRaw";

    [PacketHandler(WorldPacketOpcodes.MsgItemOperation)]
    private static async ValueTask HandleMsgItemOperationAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!MsgItemOperationPacket.TryParse(frame, out var packet))
        {
            await LogMsgItemOperationEvidenceAsync(
                    context,
                    frame,
                    "active 7899 CMsgItemPB payload is malformed or missing common required fields 1,6,7")
                .ConfigureAwait(false);
            return;
        }

        var validShape = packet.Action switch
        {
            MsgItemOperationAction.AddVendingItemGold => packet.HasPrice && !packet.HasSecondaryValue,
            MsgItemOperationAction.AddVendingItemConquerPoints or
            MsgItemOperationAction.RemoveVendingItem or
            MsgItemOperationAction.BuyVendingItem or
            MsgItemOperationAction.EmbedAnima or
            MsgItemOperationAction.RemoveAnima => !packet.HasPrice && packet.HasSecondaryValue,
            _ => true,
        };
        if (!validShape)
        {
            await LogMsgItemOperationEvidenceAsync(
                    context,
                    frame,
                    FormattableString.Invariant(
                        $"active 7899 CMsgItemPB action {(uint)packet.Action} has invalid field2/field3 presence"))
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is not (
                MsgItemOperationAction.AddVendingItemGold or
                MsgItemOperationAction.AddVendingItemConquerPoints or
                MsgItemOperationAction.RemoveVendingItem or
                MsgItemOperationAction.BuyVendingItem or
                MsgItemOperationAction.EmbedAnima or
                MsgItemOperationAction.RemoveAnima))
        {
            await LogMsgItemOperationEvidenceAsync(
                    context,
                    frame,
                    FormattableString.Invariant(
                        $"active 7899 CMsgItemPB action {(uint)packet.Action} is not modeled"))
                .ConfigureAwait(false);
            return;
        }

        if (context.Session is not { } session)
        {
            await LogMsgItemOperationEvidenceAsync(
                    context,
                    frame,
                    FormattableString.Invariant(
                        $"active 7899 CMsgItemPB action {(uint)packet.Action} has no character session"))
                .ConfigureAwait(false);
            return;
        }

        if (packet.Action is
            MsgItemOperationAction.AddVendingItemGold or
            MsgItemOperationAction.AddVendingItemConquerPoints or
            MsgItemOperationAction.RemoveVendingItem or
            MsgItemOperationAction.BuyVendingItem)
        {
            await HandleMsgItemOperationVendorAsync(
                    context,
                    session,
                    packet,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        session = LegacyAccountSecurityResolveAnimaSession(context, session);
        context = context with { Session = session };
        if (packet.Action == MsgItemOperationAction.EmbedAnima)
        {
            await HandleMsgItemOperationEmbedAnimaAsync(
                    context,
                    session,
                    packet,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await HandleMsgItemOperationRemoveAnimaAsync(
                context,
                session,
                packet,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask LogMsgItemOperationEvidenceAsync(
        WorldPacketHandlerContext context,
        PacketFrame frame,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var payloadLength = checked((int)frame.Payload.Length);
        var payloadHexLength = Math.Min(payloadLength, MsgItemOperationEvidencePayloadHexLimit);
        var payloadHex = payloadHexLength == 0
            ? string.Empty
            : Convert.ToHexString(frame.Payload.Slice(0, payloadHexLength).ToArray());
        var fullPayloadSha256 = ComputeMsgItemOperationPayloadSha256(frame.Payload);
        var payloadHexTruncated = payloadLength > payloadHexLength ? "true" : "false";
        var diagnostic = LegacyParityGapDiagnostics.UnsupportedPacket(
            WorldPacketOpcodes.MsgItemOperation,
            packet: "MsgItemOperation",
            reason);

        var evidence = string.Join(
            ' ',
            [
                MsgItemOperationEvidencePrefix,
                FormattableString.Invariant($"opcode={frame.Header.Opcode}"),
                FormatSession(context),
                FormattableString.Invariant(
                    $"payloadLength={payloadLength} payloadHexBytes={payloadHexLength} payloadHexTruncated={payloadHexTruncated}"),
                $"payloadHex={payloadHex}",
                $"fullPayloadSha256={fullPayloadSha256}",
                diagnostic.FormatLogMessage(),
            ]);

        await context.Log.WriteLineAsync(evidence).ConfigureAwait(false);
    }

    private static string ComputeMsgItemOperationPayloadSha256(ReadOnlySequence<byte> payload)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var segment in payload)
        {
            hash.AppendData(segment.Span);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
