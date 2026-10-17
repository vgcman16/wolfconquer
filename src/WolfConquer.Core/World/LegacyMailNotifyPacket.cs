using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyMailNotifyMode : byte
{
    NoMail = 2,
    HasMail = 3,
}

public readonly record struct LegacyMailNotifyPacket(LegacyMailNotifyMode Mode)
{
    public const ushort Opcode = WorldPacketOpcodes.LegacyMailNotify;
    public const int PayloadLength = 1;

    public static LegacyMailNotifyPacket StartupNoMail { get; } =
        new(LegacyMailNotifyMode.NoMail);

    public static LegacyMailNotifyPacket Startup(bool hasMail)
    {
        return new LegacyMailNotifyPacket(
            hasMail ? LegacyMailNotifyMode.HasMail : LegacyMailNotifyMode.NoMail);
    }

    public static bool TryParse(PacketFrame frame, out LegacyMailNotifyPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var mode = (LegacyMailNotifyMode)frame.Payload.ToArray()[0];
        if (mode is not LegacyMailNotifyMode.NoMail and not LegacyMailNotifyMode.HasMail)
        {
            return false;
        }

        packet = new LegacyMailNotifyPacket(mode);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.Payload[0] = (byte)Mode;
        return writer.ToArray();
    }
}
