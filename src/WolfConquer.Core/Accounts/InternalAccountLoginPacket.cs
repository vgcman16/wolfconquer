using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Accounts;

public sealed record InternalAccountLoginPacket(string Username, string Password)
{
    private static readonly Encoding WireEncoding = Encoding.UTF8;

    public static bool TryParse(PacketFrame frame, out InternalAccountLoginPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != AccountPacketOpcodes.InternalLoginRequest)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var usernameEnd = Array.IndexOf(payload, (byte)0);
        if (usernameEnd < 0)
        {
            return false;
        }

        var passwordStart = usernameEnd + 1;
        var passwordEnd = Array.IndexOf(payload, (byte)0, passwordStart);
        if (passwordEnd < 0)
        {
            return false;
        }

        var username = WireEncoding.GetString(payload.AsSpan(0, usernameEnd));
        var password = WireEncoding.GetString(payload.AsSpan(passwordStart, passwordEnd - passwordStart));
        packet = new InternalAccountLoginPacket(username, password);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        EnsureNoNull(Username, nameof(Username));
        EnsureNoNull(Password, nameof(Password));

        var usernameBytes = WireEncoding.GetBytes(Username);
        var passwordBytes = WireEncoding.GetBytes(Password);
        var writer = new PacketWriter(
            AccountPacketOpcodes.InternalLoginRequest,
            usernameBytes.Length + 1 + passwordBytes.Length + 1);

        usernameBytes.CopyTo(writer.Payload);
        writer.Payload[usernameBytes.Length] = 0;
        passwordBytes.CopyTo(writer.Payload[(usernameBytes.Length + 1)..]);
        writer.Payload[^1] = 0;
        return writer.ToArray();
    }

    private static void EnsureNoNull(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("Login packet fields cannot contain embedded null characters.", parameterName);
        }
    }
}
