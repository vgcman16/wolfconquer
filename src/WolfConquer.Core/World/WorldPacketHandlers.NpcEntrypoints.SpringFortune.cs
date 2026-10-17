using System.Globalization;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySpringFortuneNianShouChestNpcId = 4_503;
    private const string LegacySpringFortuneNianShouChestSourceMethod = "NpcID.AuntZhang";
    private const string LegacySpringFortuneNianShouChestSourceEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgNpc\\NpcID.cs:266 declares AuntZhang = 4503u; source searches found no [Npc(NpcID.AuntZhang)] handler in NpcHandler.cs or Procesor.cs, while the patched client labels NpcType4503 as Nian Shou Chest / [Spring Fortune Gifts].";

    private static readonly LegacyNpcDialogOption[] LegacySpringFortuneNianShouChestOptions =
    [
        new("I~understand.", LegacyNpcClosedOption),
    ];

    private static async ValueTask<bool> TryHandleLegacySpringFortuneNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        byte interactType,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacySpringFortuneNianShouChestNpcId)
        {
            return false;
        }

        if (optionId == LegacyNpcClosedOption)
        {
            await context.Log.WriteLineAsync(
                    "WorldServer ignored LegacyNpc SpringFortuneNianShouChest closed option " +
                    $"{FormatSession(context)} npcId={npcId} source={LegacySpringFortuneNianShouChestSourceMethod}.")
                .ConfigureAwait(false);
            return true;
        }

        await LogLegacySpringFortuneNianShouChestGapAsync(context, session, optionId, interactType)
            .ConfigureAwait(false);

        if (optionId == 0)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacySpringFortuneNianShouChestSourceMethod,
                    [
                        "The Nian Shou Chest belongs to the Spring Fortune Gifts event.",
                        "The available source only identifies NPC 4503 as AuntZhang and does not include a reward or open branch, so nothing was granted.",
                    ],
                    LegacySpringFortuneNianShouChestOptions,
                    avatarId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP LegacyNpc SpringFortuneNianShouChest reward unavailable " +
                    $"{FormatSession(context)} npcId={npcId} option={optionId.ToString(CultureInfo.InvariantCulture)} " +
                    $"source={LegacySpringFortuneNianShouChestSourceMethod} client=NianShouChest; " +
                    LegacySpringFortuneNianShouChestSourceEvidence)
                .ConfigureAwait(false);
            return true;
        }

        await context.Connection.SendAsync(
                LegacyNpcReplyPacket.ForMessageBox(
                        "This Spring Fortune chest option is not available yet. No reward was granted.")
                    .ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "PARITY-GAP LegacyNpc SpringFortuneNianShouChest unsupported option " +
                $"{FormatSession(context)} npcId={npcId} option={optionId.ToString(CultureInfo.InvariantCulture)} " +
                $"source={LegacySpringFortuneNianShouChestSourceMethod} client=NianShouChest; " +
                LegacySpringFortuneNianShouChestSourceEvidence)
            .ConfigureAwait(false);
        return true;
    }

    private static async ValueTask LogLegacySpringFortuneNianShouChestGapAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        byte interactType)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.ClickedNpc(
                    LegacySpringFortuneNianShouChestNpcId,
                    opcode: WorldPacketOpcodes.LegacyNpcServer,
                    mapId: checked((uint)session.Character.MapId),
                    optionId: optionId,
                    interactType: interactType,
                    action: LegacyNpcServerAction.Script.ToString(),
                    source: LegacySpringFortuneNianShouChestSourceMethod,
                    reason: "Spring Fortune Nian Shou Chest reward/open behavior is absent from the provided source dump; fenced dialog sent without changing economy state"))
            .ConfigureAwait(false);
    }
}
