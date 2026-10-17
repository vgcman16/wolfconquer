namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyItemPackServicesSarckSoldierNpcId = 16_833;
    private const uint LegacyItemPackServicesSarckSoldierEntranceNpcId = 16_834;
    private const uint LegacyItemPackServicesSarckSoldierSupplyPoint1NpcId = 16_852;
    private const uint LegacyItemPackServicesSarckSoldierSupplyPoint2NpcId = 16_853;
    private const uint LegacyItemPackServicesSarckSoldierSupplyPoint3NpcId = 16_854;
    private const uint LegacyItemPackServicesSarckSoldierSupplyPoint4NpcId = 16_855;
    private const uint LegacyItemPackServicesSarckSoldierSupplyPoint5NpcId = 16_856;

    private const string LegacyItemPackServicesSarckSoldierSource = "NpcHandler.SarckSoldier";
    private const string LegacyItemPackServicesInvokeSarckSoldierSource = "NpcHandler.InvokeSarckSoldier";
    private const string LegacyItemPackServicesSarckSoldierEntranceSource = "NpcHandler.SarckSoldierEnctrance";
    private const string LegacyItemPackServicesSarckSoldierSupplyPoint1Source = "NpcHandler.SarckSoldierSuppluPoint1";
    private const string LegacyItemPackServicesSarckSoldierSupplyPoint2Source = "NpcHandler.SarckSoldierSuppluPoint2";
    private const string LegacyItemPackServicesSarckSoldierSupplyPoint3Source = "NpcHandler.SarckSoldierSuppluPoint3";
    private const string LegacyItemPackServicesSarckSoldierSupplyPoint4Source = "NpcHandler.SarckSoldierSuppluPoint4";
    private const string LegacyItemPackServicesSarckSoldierSupplyPoint5Source = "NpcHandler.SarckSoldierSuppluPoint5";

    private static readonly LegacyNpcDialogOption[] LegacyItemPackServicesSarckSoldierRootOptions =
    [
        new("Send~me~to~the~Bloodshed~Sea!", 1),
        new("Holy...So~scary!", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyItemPackServicesInvokeSarckSoldierRootOptions =
    [
        new("Tell~me~about~the~sea.", 1),
        new("God!~Send~me~back!", 3),
        new("Alright.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyItemPackServicesInvokeSarckSoldierSupplyOptions =
    [
        new("Tell~me~about~the~sea.", 1),
        new("I~wanna~buy~some~Sea~Potions.", 2),
        new("God!~Send~me~back!", 3),
        new("Alright.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleSarckSoldierAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId == LegacyItemPackServicesSarckSoldierNpcId)
        {
            return await LegacyItemPackServicesTryHandleSarckSoldierEntryAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return await LegacyItemPackServicesTryHandleInvokeSarckSoldierAsync(
                context,
                session,
                npcId,
                optionId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleSarckSoldierEntryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyItemPackServicesSarckSoldierSource,
                        [
                            "There is a monster called Nemesis Tyrant hiding at the bottom of the Bloodshed Sea.",
                            "Anyone who kills it may receive a Savage Bone for the P7 Sacred Treasures.",
                        ],
                        LegacyItemPackServicesSarckSoldierRootOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await LegacyItemPackServicesSendMessageBoxAsync(
                        context,
                        LegacyItemPackServicesSarckSoldierSource,
                        "You~arrived~at~the~Bloodshed~Sea.",
                        cancellationToken)
                    .ConfigureAwait(false);
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(3_846, 381, 271, LegacyItemPackServicesSarckSoldierSource),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async ValueTask<bool> LegacyItemPackServicesTryHandleInvokeSarckSoldierAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        var source = LegacyItemPackServicesGetInvokeSarckSoldierSource(npcId);
        switch (optionId)
        {
            case 0:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "This is where the Nemesis Tyrant is hiding.",
                            "I can tell you about the Bloodshed Sea or send you back.",
                        ],
                        LegacyItemPackServicesInvokeSarckSoldierRootOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 1:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "You will not get PK points here and cannot use City Gates.",
                            "Nemesis Tyrant appears at minutes 15 and 45. Gold Boxes and Nightmare Captain can provide Sea Potions.",
                        ],
                        [
                            new("Tell~me~something~else.", 4),
                            new("Got~you.", LegacyNpcClosedOption),
                        ],
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 2:
                await SendLegacyNpcOpenGuiActionAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        source,
                        useMapIdField: false,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 3:
                await LegacyItemPackServicesSendMessageBoxAsync(
                        context,
                        source,
                        "You~arrived~at~the~Gale~Shallow.",
                        cancellationToken)
                    .ConfigureAwait(false);
                await SendLegacyNpcTeleportAsync(
                        context,
                        session,
                        npcId,
                        optionId,
                        new LegacyNpcTeleportDestination(3_055, 62, 89, source),
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 4:
                await SendLegacyNpcDialogAsync(
                        context,
                        source,
                        [
                            "This is where the Nemesis Tyrant is hiding.",
                            "I can tell you about the Bloodshed Sea, sell Sea Potions, or send you back.",
                        ],
                        LegacyItemPackServicesInvokeSarckSoldierSupplyOptions,
                        avatarId: null,
                        cancellationToken)
                    .ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static string LegacyItemPackServicesGetInvokeSarckSoldierSource(uint npcId)
    {
        return npcId switch
        {
            LegacyItemPackServicesSarckSoldierEntranceNpcId => LegacyItemPackServicesSarckSoldierEntranceSource,
            LegacyItemPackServicesSarckSoldierSupplyPoint1NpcId => LegacyItemPackServicesSarckSoldierSupplyPoint1Source,
            LegacyItemPackServicesSarckSoldierSupplyPoint2NpcId => LegacyItemPackServicesSarckSoldierSupplyPoint2Source,
            LegacyItemPackServicesSarckSoldierSupplyPoint3NpcId => LegacyItemPackServicesSarckSoldierSupplyPoint3Source,
            LegacyItemPackServicesSarckSoldierSupplyPoint4NpcId => LegacyItemPackServicesSarckSoldierSupplyPoint4Source,
            LegacyItemPackServicesSarckSoldierSupplyPoint5NpcId => LegacyItemPackServicesSarckSoldierSupplyPoint5Source,
            _ => LegacyItemPackServicesInvokeSarckSoldierSource,
        };
    }
}
