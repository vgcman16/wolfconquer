using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public static class VisibleDx8D13TrainingVitalityInfoPacket
{
    // Exact visible-DX8 D13 dispatcher case 0x8FB, CMsgTrainingVitalityInfo.
    public const ushort Opcode = 2299;

    public static bool TryParse(PacketFrame frame, out LegacyChiInfoPacket packet)
    {
        return LegacyChiInfoPacket.TryParse(frame, Opcode, out packet);
    }
}

public static class ChiInfoPacketProfileSelector
{
    public static byte[] CreateStartupFrame(
        ConquerProtocolProfile profile,
        long uid,
        LegacyChiState state)
    {
        return CreateFrame(profile, uid, state, LegacyChiInfoAction.Upgrade);
    }

    public static byte[] CreateFrame(
        ConquerProtocolProfile profile,
        CharacterSnapshot snapshot,
        LegacyChiInfoAction action)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return CreateFrame(profile, snapshot.Uid, LegacyChiState.FromSnapshot(snapshot), action);
    }

    public static byte[] CreateFrame(
        ConquerProtocolProfile profile,
        long uid,
        LegacyChiState state,
        LegacyChiInfoAction action)
    {
        var packet = LegacyChiInfoPacket.Create(uid, state, action);
        return profile switch
        {
            ConquerProtocolProfile.VisibleDx8D13 =>
                packet.ToFrameBytes(VisibleDx8D13TrainingVitalityInfoPacket.Opcode),
            ConquerProtocolProfile.RootD11_7899 => packet.ToFrameBytes(),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unknown protocol profile."),
        };
    }
}
