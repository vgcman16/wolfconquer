using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Acquire at ingress, before targets, resource costs, damage or repeat
    // tickets are consumed. The XP continuation never upgrades a shared lease.
    private static bool CombatRequiresExclusiveOperation(PacketFrame frame, ConquerProtocolProfile profile) =>
        profile == ConquerProtocolProfile.VisibleDx8D13 &&
        (frame.Header.Opcode == MsgAttackPacket.Opcode ||
         frame.Header.Opcode == WorldPacketOpcodes.MsgAction &&
         MsgActionPacket.TryParse(frame, out var action) && action.Type == MsgActionType.PetAttack);
}
