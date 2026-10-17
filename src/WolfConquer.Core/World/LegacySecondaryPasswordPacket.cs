using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacySecondaryPasswordPacket(
    uint Action,
    uint OldPassword,
    uint NewPassword)
{
    private const int ActionOffset = 0;
    private const int OldPasswordOffset = ActionOffset + sizeof(uint);
    private const int NewPasswordOffset = OldPasswordOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacySecondaryPassword;
    public const uint PasswordCorrectAction = 5;
    public const uint StartupOldPassword = 1;
    public const uint StartupNewPassword = 0;
    public const int PayloadLength = NewPasswordOffset + sizeof(uint);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static LegacySecondaryPasswordPacket StartupPasswordCorrect =>
        new(PasswordCorrectAction, StartupOldPassword, StartupNewPassword);

    public static bool TryCreateStartup(CharacterSnapshot snapshot, out LegacySecondaryPasswordPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        packet = default;

        if (!snapshot.Attributes.TryGetValue("SecurityPass", out var rawSecurityPass) ||
            !TryReadSecurityPassword(rawSecurityPass, out var securityPassword) ||
            securityPassword == 0)
        {
            return false;
        }

        packet = StartupPasswordCorrect;
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacySecondaryPasswordPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacySecondaryPasswordPacket(
            reader.ReadUInt32(ActionOffset),
            reader.ReadUInt32(OldPasswordOffset),
            reader.ReadUInt32(NewPasswordOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(ActionOffset, Action);
        writer.WriteUInt32(OldPasswordOffset, OldPassword);
        writer.WriteUInt32(NewPasswordOffset, NewPassword);
        return writer.ToArray();
    }

    private static bool TryReadSecurityPassword(string rawSecurityPass, out uint securityPassword)
    {
        var firstField = rawSecurityPass.AsSpan();
        var commaIndex = firstField.IndexOf(',');
        if (commaIndex >= 0)
        {
            firstField = firstField[..commaIndex];
        }

        return uint.TryParse(firstField, NumberStyles.Integer, CultureInfo.InvariantCulture, out securityPassword);
    }
}
