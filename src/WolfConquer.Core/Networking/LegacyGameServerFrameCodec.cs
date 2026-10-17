using System.IO;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Networking;

public enum LegacyGameServerFrameSeal
{
    Server,
    Client,
}

public static class LegacyGameServerFrameCodec
{
    public const string SealString = ServerSealString;
    public const string ServerSealString = "TQServer";
    public const string ClientSealString = "TQClient";
    public const int SealSize = 8;

    private static readonly byte[] ServerSeal =
    [
        (byte)'T',
        (byte)'Q',
        (byte)'S',
        (byte)'e',
        (byte)'r',
        (byte)'v',
        (byte)'e',
        (byte)'r'
    ];

    private static readonly byte[] ClientSeal =
    [
        (byte)'T',
        (byte)'Q',
        (byte)'C',
        (byte)'l',
        (byte)'i',
        (byte)'e',
        (byte)'n',
        (byte)'t'
    ];

    public static ReadOnlySpan<byte> SealBytes => ServerSeal;

    public static ReadOnlySpan<byte> ServerSealBytes => ServerSeal;

    public static ReadOnlySpan<byte> ClientSealBytes => ClientSeal;

    public static byte[] AppendSeal(
        ReadOnlySpan<byte> modernFrame,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        return AppendSeal(modernFrame, LegacyGameServerFrameSeal.Server, maxFrameSize);
    }

    public static byte[] AppendSeal(
        ReadOnlySpan<byte> modernFrame,
        LegacyGameServerFrameSeal seal,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        var header = ReadModernHeader(modernFrame, maxFrameSize);
        if (modernFrame.Length != header.Length)
        {
            throw new InvalidDataException(
                $"Modern packet frame length {modernFrame.Length} does not match header length {header.Length}.");
        }

        var legacyFrame = new byte[checked(header.Length + SealSize)];
        modernFrame.CopyTo(legacyFrame);
        GetSealBytes(seal).CopyTo(legacyFrame.AsSpan(header.Length));
        return legacyFrame;
    }

    public static bool TryStripSeal(
        ReadOnlySpan<byte> legacyWireBuffer,
        out byte[] modernFrame,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        return TryStripSeal(legacyWireBuffer, out modernFrame, out _, maxFrameSize);
    }

    public static bool TryStripSeal(
        ReadOnlySpan<byte> legacyWireBuffer,
        out byte[] modernFrame,
        out int consumed,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        return TryStripSeal(
            legacyWireBuffer,
            LegacyGameServerFrameSeal.Server,
            out modernFrame,
            out consumed,
            maxFrameSize);
    }

    public static bool TryStripSeal(
        ReadOnlySpan<byte> legacyWireBuffer,
        LegacyGameServerFrameSeal seal,
        out byte[] modernFrame,
        out int consumed,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        modernFrame = Array.Empty<byte>();
        consumed = 0;
        ValidateMaxFrameSize(maxFrameSize);

        if (legacyWireBuffer.Length < PacketHeader.Size)
        {
            return false;
        }

        PacketHeader.TryRead(legacyWireBuffer, out var header);
        ValidateHeader(header, maxFrameSize);

        var legacyWireLength = checked(header.Length + SealSize);
        if (legacyWireBuffer.Length < legacyWireLength)
        {
            return false;
        }

        if (!legacyWireBuffer.Slice(header.Length, SealSize).SequenceEqual(GetSealBytes(seal)))
        {
            throw new InvalidDataException(
                $"Legacy GameServer packet frame has an invalid {GetSealString(seal)} seal.");
        }

        modernFrame = legacyWireBuffer[..header.Length].ToArray();
        consumed = legacyWireLength;
        return true;
    }

    public static byte[] EncodeEncrypted(
        ReadOnlySpan<byte> modernFrame,
        LegacyGameServerCipher cipher,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        return EncodeEncrypted(modernFrame, cipher, LegacyGameServerFrameSeal.Server, maxFrameSize);
    }

    public static byte[] EncodeEncrypted(
        ReadOnlySpan<byte> modernFrame,
        LegacyGameServerCipher cipher,
        LegacyGameServerFrameSeal seal,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        ArgumentNullException.ThrowIfNull(cipher);

        var legacyFrame = AppendSeal(modernFrame, seal, maxFrameSize);
        cipher.EncryptInPlace(legacyFrame);
        return legacyFrame;
    }

    public static byte[] DecodeEncrypted(
        ReadOnlySpan<byte> encryptedLegacyWireFrame,
        LegacyGameServerCipher cipher,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        return DecodeEncrypted(encryptedLegacyWireFrame, cipher, LegacyGameServerFrameSeal.Server, maxFrameSize);
    }

    public static byte[] DecodeEncrypted(
        ReadOnlySpan<byte> encryptedLegacyWireFrame,
        LegacyGameServerCipher cipher,
        LegacyGameServerFrameSeal seal,
        int maxFrameSize = PacketHeader.DefaultMaxFrameSize)
    {
        ArgumentNullException.ThrowIfNull(cipher);

        var legacyFrame = encryptedLegacyWireFrame.ToArray();
        cipher.DecryptInPlace(legacyFrame);

        if (!TryStripSeal(legacyFrame, seal, out var modernFrame, out var consumed, maxFrameSize))
        {
            throw new InvalidDataException("Encrypted legacy GameServer packet frame is incomplete.");
        }

        if (consumed != legacyFrame.Length)
        {
            throw new InvalidDataException(
                $"Encrypted legacy GameServer packet frame length {legacyFrame.Length} does not match decrypted wire length {consumed}.");
        }

        return modernFrame;
    }

    private static PacketHeader ReadModernHeader(ReadOnlySpan<byte> modernFrame, int maxFrameSize)
    {
        ValidateMaxFrameSize(maxFrameSize);
        if (modernFrame.Length < PacketHeader.Size)
        {
            throw new InvalidDataException("Modern packet frame is smaller than the packet header.");
        }

        PacketHeader.TryRead(modernFrame, out var header);
        ValidateHeader(header, maxFrameSize);
        return header;
    }

    private static void ValidateHeader(PacketHeader header, int maxFrameSize)
    {
        if (header.Length < PacketHeader.Size)
        {
            throw new InvalidDataException($"Packet frame length {header.Length} is smaller than the header size.");
        }

        if (header.Length > maxFrameSize)
        {
            throw new InvalidDataException(
                $"Packet frame length {header.Length} exceeds the configured maximum size {maxFrameSize}.");
        }
    }

    private static void ValidateMaxFrameSize(int maxFrameSize)
    {
        if (maxFrameSize < PacketHeader.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFrameSize), "Maximum frame size cannot be smaller than the packet header.");
        }
    }

    private static ReadOnlySpan<byte> GetSealBytes(LegacyGameServerFrameSeal seal)
    {
        return seal switch
        {
            LegacyGameServerFrameSeal.Server => ServerSeal,
            LegacyGameServerFrameSeal.Client => ClientSeal,
            _ => throw new ArgumentOutOfRangeException(nameof(seal), seal, "Unknown legacy GameServer seal."),
        };
    }

    private static string GetSealString(LegacyGameServerFrameSeal seal)
    {
        return seal switch
        {
            LegacyGameServerFrameSeal.Server => ServerSealString,
            LegacyGameServerFrameSeal.Client => ClientSealString,
            _ => throw new ArgumentOutOfRangeException(nameof(seal), seal, "Unknown legacy GameServer seal."),
        };
    }
}
