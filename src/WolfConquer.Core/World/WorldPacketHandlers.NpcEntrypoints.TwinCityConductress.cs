namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyTwinCityConductressAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.TwinCityConductress",
                    [LegacyTwinCityConductressDialogText],
                    LegacyTwinCityConductressOptions,
                    LegacyTwinCityConductressAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!LegacyTwinCityConductressDestinations.TryGetValue(optionId, out var destination))
        {
            return false;
        }

        if (session.Character.Money < LegacyTwinCityConductressTeleportCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    "NpcHandler.TwinCityConductress",
                    [LegacyTwinCityConductressNoMoneyText],
                    LegacyTwinCityConductressNoMoneyOptions,
                    LegacyTwinCityConductressAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var chargedCharacter = session.Character with
        {
            Money = session.Character.Money - LegacyTwinCityConductressTeleportCost,
        };
        var chargedSession = session with { Character = chargedCharacter };
        context.Sessions?.Set(context.Connection, chargedSession);

        await context.Connection.SendAsync(
                new LegacyMsgUpdatePacket(
                        CheckedUInt32(chargedCharacter.Uid, nameof(chargedCharacter.Uid)),
                        [new LegacyMsgUpdateEntry(
                            LegacyMsgUpdateDataType.Money,
                            (ulong)chargedCharacter.Money)])
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await SendLegacyNpcTeleportAsync(
                context,
                chargedSession,
                LegacyTwinCityConductressNpcId,
                optionId,
                destination,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}
