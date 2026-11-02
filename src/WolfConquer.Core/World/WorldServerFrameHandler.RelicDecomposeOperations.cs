using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private static bool LegacyRelicDecomposeRequiresExclusiveOperation(PacketFrame frame, ConquerProtocolProfile profile) =>
        profile == ConquerProtocolProfile.VisibleDx8D13 && frame.Header.Opcode == WorldPacketOpcodes.MsgItemUsage &&
        MsgItemUsagePacket.TryParse(frame, out var packet) && packet.Action == MsgItemUsageAction.DecomposeRelics &&
        LegacyRelicDecomposeRequest.TryParse(packet, frame, out _, out _);
}
