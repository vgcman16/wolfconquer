using System.Buffers;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Accounts;

public readonly record struct LegacyAccountPasswordSeedPacket(int Seed)
{
    public const ushort Opcode = AccountPacketOpcodes.LegacyPasswordCryptographySeed;
    public const int PayloadLength = sizeof(int);
    public const int FrameLength = PacketHeader.Size + PayloadLength;

    public static bool TryParse(PacketFrame frame, out LegacyAccountPasswordSeedPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Header.Length != FrameLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyAccountPasswordSeedPacket(reader.ReadInt32(0));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteInt32(0, Seed);
        return writer.ToArray();
    }
}

public sealed record LegacyAccountAuthenticationPacket(
    string Username,
    string Password,
    string Server)
{
    private const int UsernameLengthOffset = 0;
    private const int PasswordLengthOffset = 1;
    private const int ServerLengthOffset = 2;
    private const int ReservedByteOffset = 3;
    private const int ReservedUInt16Offset = 4;
    private const int FieldsOffset = 6;
    private const int MaxPasswordLength = 32;

    private static readonly Encoding WireEncoding = Encoding.Latin1;

    public const ushort Opcode = AccountPacketOpcodes.LegacyAuthentication;
    public const int FrameLength = 472;
    public const int PayloadLength = FrameLength - PacketHeader.Size;

    public IReadOnlyList<string> PasswordCandidates { get; init; } = [Password];

    public static bool TryParse(PacketFrame frame, out LegacyAccountAuthenticationPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Header.Length != FrameLength ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var usernameLength = payload[UsernameLengthOffset];
        var passwordLength = payload[PasswordLengthOffset];
        var serverLength = payload[ServerLengthOffset];
        if (passwordLength > MaxPasswordLength)
        {
            return false;
        }

        var fieldsLength = usernameLength + passwordLength + serverLength;
        if (FieldsOffset > payload.Length - fieldsLength)
        {
            return false;
        }

        var usernameOffset = FieldsOffset;
        var passwordOffset = usernameOffset + usernameLength;
        var serverOffset = passwordOffset + passwordLength;

        var encryptedPasswordBytes = payload.AsSpan(passwordOffset, passwordLength).ToArray();
        var sourcePasswordBytes = ReadLegacyEncryptedPasswordBytes(encryptedPasswordBytes);
        LegacyAccServerCipher.DecryptPasswordInPlace(sourcePasswordBytes);
        var sourcePassword = StripNulls(WireEncoding.GetString(sourcePasswordBytes));

        var rawPasswordBytes = encryptedPasswordBytes.ToArray();
        LegacyAccServerCipher.DecryptPasswordInPlace(rawPasswordBytes);
        var rawPassword = StripNulls(WireEncoding.GetString(rawPasswordBytes));

        packet = new LegacyAccountAuthenticationPacket(
            StripNulls(WireEncoding.GetString(payload.AsSpan(usernameOffset, usernameLength))),
            sourcePassword,
            StripNulls(WireEncoding.GetString(payload.AsSpan(serverOffset, serverLength))))
        {
            PasswordCandidates = CreatePasswordCandidates(sourcePassword, rawPassword),
        };
        return true;
    }

    public byte[] ToFrameBytes()
    {
        EnsureNoNull(Username, nameof(Username));
        EnsureNoNull(Password, nameof(Password));
        EnsureNoNull(Server, nameof(Server));

        var usernameBytes = GetByteCountedField(Username, nameof(Username));
        var passwordBytes = GetByteCountedField(Password, nameof(Password));
        if (passwordBytes.Length > MaxPasswordLength)
        {
            throw new ArgumentOutOfRangeException(nameof(Password), "Legacy account passwords cannot exceed 32 bytes.");
        }

        var serverBytes = GetByteCountedField(Server, nameof(Server));
        var fieldsLength = usernameBytes.Length + passwordBytes.Length + serverBytes.Length;
        if (fieldsLength > PayloadLength - FieldsOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(Server), "Legacy authentication fields exceed the fixed packet frame.");
        }

        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.Payload[UsernameLengthOffset] = (byte)usernameBytes.Length;
        writer.Payload[PasswordLengthOffset] = (byte)passwordBytes.Length;
        writer.Payload[ServerLengthOffset] = (byte)serverBytes.Length;
        writer.Payload[ReservedByteOffset] = 0;
        writer.WriteUInt16(ReservedUInt16Offset, 0);

        var offset = FieldsOffset;
        usernameBytes.CopyTo(writer.Payload[offset..]);
        offset += usernameBytes.Length;

        var encryptedPasswordBytes = passwordBytes.ToArray();
        LegacyAccServerCipher.DecryptPasswordInPlace(encryptedPasswordBytes);
        encryptedPasswordBytes.CopyTo(writer.Payload[offset..]);
        offset += encryptedPasswordBytes.Length;

        serverBytes.CopyTo(writer.Payload[offset..]);
        return writer.ToArray();
    }

    private static byte[] GetByteCountedField(string value, string parameterName)
    {
        var bytes = WireEncoding.GetBytes(value);
        if (bytes.Length > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Legacy authentication fields use one-byte lengths.");
        }

        return bytes;
    }

    private static void EnsureNoNull(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("Legacy authentication fields cannot contain embedded null characters.", parameterName);
        }
    }

    private static string StripNulls(string value)
    {
        return value.Replace("\0", string.Empty, StringComparison.Ordinal);
    }

    private static byte[] ReadLegacyEncryptedPasswordBytes(ReadOnlySpan<byte> encryptedPassword)
    {
        var legacyString = WireEncoding.GetString(encryptedPassword);
        var withoutNulls = StripNulls(legacyString);
        return WireEncoding.GetBytes(withoutNulls);
    }

    private static IReadOnlyList<string> CreatePasswordCandidates(params string[] candidates)
    {
        var unique = new List<string>(candidates.Length);
        foreach (var candidate in candidates)
        {
            if (!unique.Contains(candidate, StringComparer.Ordinal))
            {
                unique.Add(candidate);
            }
        }

        return unique;
    }
}

public readonly record struct LegacyAccountForwardPacket(
    uint Identifier,
    LegacyAccountForwardType Type,
    ushort Port,
    string IP)
{
    private const int IdentifierOffset = 0;
    private const int TypeOffset = 8;
    private const int PortOffset = 12;
    private const int IPOffset = 20;
    private const int IPLength = 16;

    private static readonly Encoding WireEncoding = Encoding.Latin1;

    public const ushort Opcode = AccountPacketOpcodes.LegacyForward;
    public const int FrameLength = 52;
    public const int PayloadLength = FrameLength - PacketHeader.Size;

    public static bool TryParse(PacketFrame frame, out LegacyAccountForwardPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Header.Length != FrameLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var typeValue = reader.ReadUInt32(TypeOffset);
        if (typeValue > byte.MaxValue ||
            !Enum.IsDefined(typeof(LegacyAccountForwardType), (byte)typeValue))
        {
            return false;
        }

        packet = new LegacyAccountForwardPacket(
            reader.ReadUInt32(IdentifierOffset),
            (LegacyAccountForwardType)(byte)typeValue,
            reader.ReadUInt16(PortOffset),
            ReadNullPaddedString(reader.Payload[IPOffset..(IPOffset + IPLength)]));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(IP);
        var ipBytes = WireEncoding.GetBytes(IP);
        if (ipBytes.Length > IPLength)
        {
            throw new ArgumentOutOfRangeException(nameof(IP), "Legacy forward IP field cannot exceed 16 bytes.");
        }

        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(IdentifierOffset, Identifier);
        writer.WriteUInt32(TypeOffset, (uint)Type);
        writer.WriteUInt16(PortOffset, Port);
        ipBytes.CopyTo(writer.Payload[IPOffset..]);
        return writer.ToArray();
    }

    private static string ReadNullPaddedString(ReadOnlySpan<byte> bytes)
    {
        var length = bytes.IndexOf((byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return WireEncoding.GetString(bytes[..length]);
    }
}

public enum LegacyAccountForwardType : byte
{
    InvalidInfo = 1,
    Ready = 2,
    Banned12 = 12,
    Banned = 25,
    WrongAccount = 57,
    ServersNotConfigured = 59,
    InvalidAuthenticationProtocol = 73,
}
