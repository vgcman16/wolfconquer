using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask<bool> TryHandleLegacyRegionalConductressAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (TryGetLegacyRegionalConductressDialog(npcId, out var source, out var text, out var options, out var avatarId))
        {
            if (optionId == 0)
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [text],
                        options,
                        avatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }

            if (TryGetLegacyRegionalConductressDestination(npcId, optionId, session.Character, out var destination))
            {
                if (IsLegacyPaidRegionalConductress(npcId))
                {
                    return await SendLegacyPaidRegionalConductressTeleportAsync(
                            context,
                            session,
                            npcId,
                            optionId,
                            destination,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await SendLegacyNpcTeleportAsync(context, session, npcId, optionId, destination, cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<bool> SendLegacyPaidRegionalConductressTeleportAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        LegacyNpcTeleportDestination destination,
        CancellationToken cancellationToken)
    {
        if (session.Character.Money < LegacyTwinCityConductressTeleportCost)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    destination.SourceMethod,
                    [LegacyRegionalConductressNoMoneyText],
                    LegacyRegionalConductressNoMoneyOptions,
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
                npcId,
                optionId,
                destination,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static bool TryGetLegacyRegionalConductressDialog(
        uint npcId,
        out string source,
        out string text,
        out IReadOnlyList<LegacyNpcDialogOption> options,
        out ushort avatarId)
    {
        source = string.Empty;
        text = string.Empty;
        options = [];
        avatarId = LegacyTwinCityConductressAvatarId;

        switch (npcId)
        {
            case LegacyPheonixCityConductressNpcId:
                source = "NpcHandler.PheonixCityConductress";
                text = LegacyRegionalConductressDialogText;
                options = LegacyPheonixCityConductressOptions;
                return true;
            case LegacyDesertCityConductressNpcId:
                source = "NpcHandler.DesertCityConductress";
                text = LegacyRegionalConductressDialogText;
                options = LegacyDesertCityConductressOptions;
                return true;
            case LegacyApeCityConductressNpcId:
                source = "NpcHandler.ApeCityConductress";
                text = LegacyRegionalConductressDialogText;
                options = LegacyApeCityConductressOptions;
                return true;
            case LegacyBirdCityConductressNpcId:
                source = "NpcHandler.BirdCityConductress";
                text = LegacyRegionalConductressDialogText;
                options = LegacyBirdCityConductressOptions;
                return true;
            case LegacyMarketConductressNpcId:
                source = "NpcHandler.MarketConductress";
                text = LegacyMarketConductressDialogText;
                options = LegacyMarketConductressOptions;
                avatarId = 105;
                return true;
            case LegacyBoxerConductorNpcId:
                source = "NpcHandler.BoxerConductor";
                text = LegacyBoxerConductorDialogText;
                options = LegacyBoxerConductorOptions;
                avatarId = 15;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetLegacyRegionalConductressDestination(
        uint npcId,
        byte optionId,
        CharacterSnapshot character,
        out LegacyNpcTeleportDestination destination)
    {
        destination = default!;

        if (npcId == LegacyMarketConductressNpcId && optionId == 1)
        {
            destination = GetLegacyMarketReturnDestination(character, "NpcHandler.MarketConductress");
            return true;
        }

        if (npcId == LegacyBoxerConductorNpcId && optionId == 1)
        {
            destination = GetLegacyMarketReturnDestination(character, "NpcHandler.BoxerConductor");
            return true;
        }

        return npcId switch
        {
            LegacyPheonixCityConductressNpcId =>
                LegacyPheonixCityConductressDestinations.TryGetValue(optionId, out destination),
            LegacyDesertCityConductressNpcId =>
                LegacyDesertCityConductressDestinations.TryGetValue(optionId, out destination),
            LegacyApeCityConductressNpcId =>
                LegacyApeCityConductressDestinations.TryGetValue(optionId, out destination),
            LegacyBirdCityConductressNpcId =>
                LegacyBirdCityConductressDestinations.TryGetValue(optionId, out destination),
            _ => false,
        };
    }

    private static LegacyNpcTeleportDestination GetLegacyMarketReturnDestination(
        CharacterSnapshot character,
        string sourceMethod)
    {
        return character.PreviousMapId switch
        {
            1_000 => new LegacyNpcTeleportDestination(1_000, 500, 650, sourceMethod),
            1_020 => new LegacyNpcTeleportDestination(1_020, 565, 562, sourceMethod),
            1_011 => new LegacyNpcTeleportDestination(1_011, 188, 264, sourceMethod),
            1_015 => new LegacyNpcTeleportDestination(1_015, 717, 571, sourceMethod),
            _ when sourceMethod == "NpcHandler.MarketConductress" =>
                new LegacyNpcTeleportDestination(1_002, 313, 331, sourceMethod),
            _ => new LegacyNpcTeleportDestination(1_002, 410, 354, sourceMethod),
        };
    }

    private static bool IsLegacyPaidRegionalConductress(uint npcId)
    {
        return npcId is LegacyPheonixCityConductressNpcId or
            LegacyDesertCityConductressNpcId or
            LegacyApeCityConductressNpcId or
            LegacyBirdCityConductressNpcId;
    }
}
