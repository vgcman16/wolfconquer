using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacySecurityAnimaSellItemSource = "MsgItemUsuagePacket.SellItem";
    private const string LegacySecurityAnimaDropItemSource = "MsgItemUsuagePacket.DropItem";
    private const string LegacySecurityAnimaItemNpcSource = "NpcHandler.Animas";
    private const string LegacySecurityAnimaActivationRequiredMessage = "Please Active Pincode";

    private static bool IsLegacySecurityAnimaProtectedItem(PlayerItemSnapshotItem item)
    {
        return item.AnimaItemId != 0 ||
            item.ItemId >= LegacyAnimaFirstItemNpcId && item.ItemId <= LegacyAnimaLastItemNpcId;
    }

    private static bool LegacySecurityAnimaItemOperationNeedsPin(WorldSession session) =>
        ReadAttributeString(session.Character.Attributes, LegacyAccountSecurityAnimaPinAttribute).Length != 0 &&
        !ReadAttributeBool(session.Character.Attributes, LegacyAccountSecurityAnimaActivatedAttribute);

    private static async ValueTask<bool> TryBlockLegacySecurityAnimaItemOperationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        string source,
        CancellationToken cancellationToken)
    {
        var currentSession = LegacyAccountSecurityResolveAnimaSession(context, session);
        if (!LegacySecurityAnimaItemOperationNeedsPin(currentSession))
        {
            return false;
        }

        var currentContext = context with { Session = currentSession };
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacySecurityAnimaActivationRequiredMessage).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer blocked SecurityAnima item operation " +
                $"{FormatSession(currentContext)} source={source} reason=anima-pin-not-activated " +
                $"wire=7899 npcReplyOpcode={LegacyNpcReplyPacket.Opcode}.")
            .ConfigureAwait(false);
        return true;
    }
}
