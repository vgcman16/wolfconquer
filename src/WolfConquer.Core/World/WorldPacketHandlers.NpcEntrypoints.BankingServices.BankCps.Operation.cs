using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool IsBankCpsMutationOption(byte option) => option is 2 or 3 or 10 or 20;

    // A routing hint only. The actual current NPC/session/confirmation owner is
    // revalidated after Exclusive; a numeric option alone grants no bypass.
    internal static bool IsLegacyBankCpsMutationFrame(PacketFrame frame, uint? activeNpcId = null)
    {
        if (frame.Header.Opcode == WorldPacketOpcodes.LegacyNpcServer &&
            LegacyNpcServerRequestPacket.TryParse(frame, out var server))
            return server.NpcId == LegacyBankCpsNpcId && IsBankCpsMutationOption(server.OptionId) &&
                (server.Action == LegacyNpcServerAction.Script || IsLegacyCursorScriptNpc(server));
        return frame.Header.Opcode == WorldPacketOpcodes.LegacyNpcReply &&
            LegacyNpcDialogRequestPacket.TryParse(frame, out var reply) && reply.IsScriptReply &&
            reply.ResolveNpcId(activeNpcId ?? 0) == LegacyBankCpsNpcId && IsBankCpsMutationOption(reply.OptionId);
    }
}
