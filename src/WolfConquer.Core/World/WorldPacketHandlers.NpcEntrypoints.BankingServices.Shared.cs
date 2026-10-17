namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyBankCpsNpcId = 102_177;
    private const uint LegacyMarketCpAdminNpcId = 2_071;
    private const uint LegacyJailCpAdminNpcId = 1_000;

    private const string LegacyBankCpsSource = "NpcHandler.BankCps";
    private const string LegacyMarketCpAdminSource = "NpcHandler.MarketCpAdmin";
    private const string LegacyJailCpAdminSource = "NpcHandler.JailCPAdmin";

    private static async ValueTask SendLegacyBankInputDialogAsync(
        WorldPacketHandlerContext context,
        string sourceMethod,
        string text,
        string inputLabel,
        byte inputOptionId,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForInput(inputLabel, inputOptionId, maxLength: 10).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForClosedOption("Later.").ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForAvatar(183).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForFinish().ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                $"WorldServer sent LegacyNpc bank input dialog {FormatSession(context)} source={sourceMethod} option={inputOptionId}.")
            .ConfigureAwait(false);
    }
}
