namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyDragonIslandNavigationAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId == LegacyDragonIslandConductressNpcId)
        {
            if (optionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.DragonIslandConductress",
                        LegacyDragonIslandConductressDialogTexts,
                        LegacyDragonIslandConductressOptions,
                        avatarId: null,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (TryGetLegacyDragonIslandConductressDestination(optionId, out var destination))
            {
                await SendLegacyNpcTeleportAsync(context, session, npcId, optionId, destination, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        if (IsLegacyGlowtownPortalNpc(npcId))
        {
            var sourceMethod = GetLegacyGlowtownPortalSource(npcId);
            if (optionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        sourceMethod,
                        [LegacyGlowtownPortalDialogText],
                        LegacyGlowtownPortalOptions,
                        LegacyGlowtownPortalAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (optionId == 1)
            {
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(10_137, 93, 408, sourceMethod),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        if (npcId == LegacyCobbWindNpcId)
        {
            if (optionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.CobbWind",
                        CreateLegacyCobbWindDialogTexts(),
                        LegacyCobbWindOptions,
                        LegacyCobbWindAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (optionId == 1)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        "NpcHandler.CobbWind",
                        LegacyCobbWindDetailsDialogTexts,
                        LegacyCobbWindDetailsOptions,
                        LegacyCobbWindAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> CreateLegacyCobbWindDialogTexts()
    {
        var texts = new List<string>(LegacyCobbWindDialogBaseTexts);
        var minute = DateTime.Now.Minute;
        if (minute is >= 0 and <= 15)
        {
            texts.Add("Howling Sea Giant appears near Dragon Island (568,372) at minute 15 each hour.\n");
            texts.Add(LegacyCobbWindCurrentBossTransferText);
        }
        else if (minute is >= 16 and <= 30)
        {
            texts.Add("Forbidden Heaven Beast appears near Dragon Island (568,372) at minute 30 each hour.\n");
            texts.Add(LegacyCobbWindCurrentBossTransferText);
        }
        else if (minute is >= 31 and <= 57)
        {
            texts.Add("Crystal Snow Demon appears near Dragon Island (568,372) at minute 57 each hour.\n");
            texts.Add(LegacyCobbWindCurrentBossTransferText);
        }

        return texts;
    }

    private static bool TryGetLegacyDragonIslandConductressDestination(
        byte optionId,
        out LegacyNpcTeleportDestination destination)
    {
        destination = optionId switch
        {
            1 => new LegacyNpcTeleportDestination(1_002, 410, 354, "NpcHandler.DragonIslandConductress"),
            20 => new LegacyNpcTeleportDestination(1_002, 346, 420, "NpcHandler.DragonIslandConductress"),
            2 => new LegacyNpcTeleportDestination(1_002, 345, 441, "NpcHandler.DragonIslandConductress"),
            3 => new LegacyNpcTeleportDestination(10_137, 453, 483, "NpcHandler.DragonIslandConductress"),
            _ => default,
        };
        return !string.IsNullOrEmpty(destination.SourceMethod);
    }

    private static bool IsLegacyGlowtownPortalNpc(uint npcId)
    {
        return npcId is LegacyGlowtownPortal1NpcId or LegacyGlowtownPortal2NpcId or LegacyGlowtownPortal3NpcId;
    }

    private static string GetLegacyGlowtownPortalSource(uint npcId)
    {
        return npcId switch
        {
            LegacyGlowtownPortal1NpcId => "NpcHandler.GlowtownPortal1",
            LegacyGlowtownPortal2NpcId => "NpcHandler.GlowtownPortal2",
            LegacyGlowtownPortal3NpcId => "NpcHandler.GlowtownPortal3",
            _ => "NpcHandler.GlowtownPortal",
        };
    }
}
