using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacyTowerOfMysteryLayerChangeNpcId = 19_139;
    private const uint LegacyTowerOfMysteryChallenge1NpcId = 200_020;
    private const uint LegacyTowerOfMysteryChallenge2NpcId = 200_025;
    private const uint LegacyTowerOfMysteryChallenge3NpcId = 200_018;
    private const uint LegacyTowerOfMysteryChallenge4NpcId = 200_024;
    private const uint LegacyTowerOfMysteryConductorNpcId = 19_231;
    private const uint LegacyTowerOfMysteryConductor2NpcId = 19_194;
    private const uint LegacyTowerKeeperNpcId = 19_127;
    private const uint LegacyCloudSweeperNpcId = 19_128;
    private const uint LegacyGuidofFieryDragonNpcId = 19_166;
    private const string LegacyTowerOfMysterySourceEvidence =
        "NpcID.cs:199-208 declares the Tower of Mystery NPC ids; NpcHandler.cs has no matching [Npc] handler and Procesor.cs has no special route for this id cluster.";
    private const string LegacyTowerOfMysteryUnsupportedText =
        "The Tower of Mystery challenge manager is not available yet. Your character state was not changed.";
    private const ushort LegacyTowerOfMysteryAvatarId = 85;
    private const uint LegacyTowerOfMysteryDragonIslandReturnMapId = 10_137;
    private const ushort LegacyTowerOfMysteryDragonIslandReturnX = 453;
    private const ushort LegacyTowerOfMysteryDragonIslandReturnY = 483;

    private static readonly LegacyNpcDialogOption[] LegacyTowerOfMysteryConductorOptions =
    [
        new("Enter~the~Tower~of~Mystery.", 1),
        new("Return~to~Dragon~Island.", 2),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTowerOfMysteryLayerOptions =
    [
        new("Move~to~the~next~layer.", 1),
        new("Return~to~Dragon~Island.", 2),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTowerKeeperOptions =
    [
        new("Return~to~Dragon~Island.", 1),
        new("Tell~me~about~the~Tower.", 2),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyCloudSweeperOptions =
    [
        new("Return~to~Dragon~Island.", 1),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyFieryDragonGuideOptions =
    [
        new("Return~to~Dragon~Island.", 1),
        new("Challenge~the~Fiery~Dragon.", 2),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTowerOfMysteryChallengeOptions =
    [
        new("Start~the~challenge.", 1),
        new("No,~thanks.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacyTowerOfMysteryInfoOptions =
    [
        new("I~see.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacyTowerOfMysteryAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (!IsLegacyTowerOfMysteryNpc(npcId))
        {
            return false;
        }

        var sourceMethod = GetLegacyTowerOfMysterySourceMethod(npcId);
        if (optionId == LegacyNpcClosedOption)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc TowerOfMystery closed option " +
                    $"{FormatSession(context)} npcId={npcId} source={sourceMethod}.")
                .ConfigureAwait(false);
            return true;
        }

        if (optionId == 0)
        {
            await SendLegacyTowerOfMysteryRootDialogAsync(
                    context,
                    session,
                    npcId,
                    sourceMethod,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (TryGetLegacyTowerOfMysteryReturnDestination(npcId, optionId, sourceMethod, out var returnDestination))
        {
            await SendLegacyNpcTeleportAsync(
                    context,
                    session,
                    npcId,
                    optionId,
                    returnDestination,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (npcId == LegacyTowerKeeperNpcId && optionId == 2)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        "The Tower of Mystery is an event tower guarded by trial NPCs.",
                        "The provided source declares the NPC ids but does not include the event manager branch that starts or advances the tower.",
                    ],
                    LegacyTowerOfMysteryInfoOptions,
                    LegacyTowerOfMysteryAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        await SendLegacyTowerOfMysteryUnsupportedBranchAsync(
                context,
                session,
                npcId,
                optionId,
                sourceMethod,
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask SendLegacyTowerOfMysteryRootDialogAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        if (npcId is LegacyTowerOfMysteryConductorNpcId or LegacyTowerOfMysteryConductor2NpcId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        "The Tower of Mystery is sealed by a legacy event manager.",
                        "I can send you back to Dragon Island, but entering the tower must wait until that manager is modeled.",
                    ],
                    LegacyTowerOfMysteryConductorOptions,
                    LegacyTowerOfMysteryAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (npcId == LegacyTowerOfMysteryLayerChangeNpcId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        "This stone changes Tower of Mystery layers.",
                        "Layer advancement depends on the missing Tower of Mystery event manager.",
                    ],
                    LegacyTowerOfMysteryLayerOptions,
                    LegacyTowerOfMysteryAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (npcId == LegacyTowerKeeperNpcId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        $"Greetings, {session.Character.Name}. I guard the Tower of Mystery.",
                        "Only the safe guidance paths are modeled until the source event manager is recovered.",
                    ],
                    LegacyTowerKeeperOptions,
                    LegacyTowerOfMysteryAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (npcId == LegacyCloudSweeperNpcId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        "The tower clouds are restless today.",
                        "I can guide you back to Dragon Island while the tower trials are sealed.",
                    ],
                    LegacyCloudSweeperOptions,
                    LegacyTowerOfMysteryAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (npcId == LegacyGuidofFieryDragonNpcId)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    sourceMethod,
                    [
                        "The Fiery Dragon waits behind the Tower of Mystery challenge gate.",
                        "The challenge gate is not modeled yet, but I can guide you back to Dragon Island.",
                    ],
                    LegacyFieryDragonGuideOptions,
                    LegacyTowerOfMysteryAvatarId,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        await SendLegacyNpcDialogAsync(
                context,
                sourceMethod,
                [GetLegacyTowerOfMysteryChallengeText(npcId)],
                LegacyTowerOfMysteryChallengeOptions,
                LegacyTowerOfMysteryAvatarId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacyTowerOfMysteryUnsupportedBranchAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        string sourceMethod,
        CancellationToken cancellationToken)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    npcId,
                    opcode: WorldPacketOpcodes.LegacyNpcReply,
                    mapId: checked((uint)session.Character.MapId),
                    optionId: optionId,
                    interactType: 1,
                    source: sourceMethod,
                    reason: "Tower of Mystery event-manager action is not present in the provided source dump"))
            .ConfigureAwait(false);
        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(LegacyTowerOfMysteryUnsupportedText).ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc TowerOfMystery unsupported event-manager branch " +
                $"{FormatSession(context)} npcId={npcId} option={optionId.ToString(CultureInfo.InvariantCulture)} " +
                $"source={sourceMethod}; {LegacyTowerOfMysterySourceEvidence}")
            .ConfigureAwait(false);
    }

    private static bool IsLegacyTowerOfMysteryNpc(uint npcId)
    {
        return npcId is
            LegacyTowerOfMysteryLayerChangeNpcId or
            LegacyTowerOfMysteryChallenge1NpcId or
            LegacyTowerOfMysteryChallenge2NpcId or
            LegacyTowerOfMysteryChallenge3NpcId or
            LegacyTowerOfMysteryChallenge4NpcId or
            LegacyTowerOfMysteryConductorNpcId or
            LegacyTowerOfMysteryConductor2NpcId or
            LegacyTowerKeeperNpcId or
            LegacyCloudSweeperNpcId or
            LegacyGuidofFieryDragonNpcId;
    }

    private static bool TryGetLegacyTowerOfMysteryReturnDestination(
        uint npcId,
        byte optionId,
        string sourceMethod,
        out LegacyNpcTeleportDestination destination)
    {
        destination = default;
        var shouldReturn = npcId switch
        {
            LegacyTowerOfMysteryLayerChangeNpcId => optionId == 2,
            LegacyTowerOfMysteryConductorNpcId or LegacyTowerOfMysteryConductor2NpcId => optionId == 2,
            LegacyTowerKeeperNpcId or LegacyCloudSweeperNpcId or LegacyGuidofFieryDragonNpcId => optionId == 1,
            _ => false,
        };

        if (!shouldReturn)
        {
            return false;
        }

        destination = new LegacyNpcTeleportDestination(
            LegacyTowerOfMysteryDragonIslandReturnMapId,
            LegacyTowerOfMysteryDragonIslandReturnX,
            LegacyTowerOfMysteryDragonIslandReturnY,
            sourceMethod);
        return true;
    }

    private static string GetLegacyTowerOfMysterySourceMethod(uint npcId)
    {
        return npcId switch
        {
            LegacyTowerOfMysteryLayerChangeNpcId => "NpcHandler.TowerOfMysteryLayerChange",
            LegacyTowerOfMysteryChallenge1NpcId => "NpcHandler.TowerofMysteryChallenge1",
            LegacyTowerOfMysteryChallenge2NpcId => "NpcHandler.TowerofMysteryChallenge2",
            LegacyTowerOfMysteryChallenge3NpcId => "NpcHandler.TowerofMysteryChallenge3",
            LegacyTowerOfMysteryChallenge4NpcId => "NpcHandler.TowerofMysteryChallenge4",
            LegacyTowerOfMysteryConductorNpcId => "NpcHandler.TowerofMysteryConductor",
            LegacyTowerOfMysteryConductor2NpcId => "NpcHandler.TowerofMysteryConductor2",
            LegacyTowerKeeperNpcId => "NpcHandler.TowerKeeper",
            LegacyCloudSweeperNpcId => "NpcHandler.CloudSweeper",
            LegacyGuidofFieryDragonNpcId => "NpcHandler.GuidofFieryDragon",
            _ => "NpcHandler.TowerOfMystery",
        };
    }

    private static string GetLegacyTowerOfMysteryChallengeText(uint npcId)
    {
        return npcId switch
        {
            LegacyTowerOfMysteryChallenge1NpcId => "This is the first Tower of Mystery challenge gate.",
            LegacyTowerOfMysteryChallenge2NpcId => "This is the second Tower of Mystery challenge gate.",
            LegacyTowerOfMysteryChallenge3NpcId => "This is the third Tower of Mystery challenge gate.",
            LegacyTowerOfMysteryChallenge4NpcId => "This is the fourth Tower of Mystery challenge gate.",
            _ => "This Tower of Mystery challenge gate is sealed.",
        };
    }
}
