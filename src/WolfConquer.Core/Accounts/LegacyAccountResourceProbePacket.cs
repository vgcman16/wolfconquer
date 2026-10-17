using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Accounts;

public sealed record LegacyAccountResourceProbePacket(string ResourceName)
{
    private const string KnownResourceName = "res.dat";
    private const int MaxZeroPrefixLength = 8;
    private const int MaxHeaderPrefixLength = 16;

    private static ReadOnlySpan<byte> KnownResourceNameBytes => "res.dat"u8;

    public static bool TryParse(PacketFrame frame, out LegacyAccountResourceProbePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != AccountPacketOpcodes.LegacyResourceProbe)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        if (!TryFindKnownResourceName(payload))
        {
            return false;
        }

        packet = new LegacyAccountResourceProbePacket(KnownResourceName);
        return true;
    }

    private static bool TryFindKnownResourceName(ReadOnlySpan<byte> payload)
    {
        var maxOffset = Math.Min(MaxHeaderPrefixLength, payload.Length - KnownResourceNameBytes.Length);
        for (var offset = 0; offset <= maxOffset; offset++)
        {
            if (!payload[offset..].StartsWith(KnownResourceNameBytes))
            {
                continue;
            }

            var suffixOffset = offset + KnownResourceNameBytes.Length;
            if (IsLegacyResourceNamePrefix(payload[..offset]) &&
                IsZeroPadded(payload[suffixOffset..]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLegacyResourceNamePrefix(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length == 0)
        {
            return true;
        }

        if (prefix.Length <= MaxZeroPrefixLength &&
            IsZeroPadded(prefix))
        {
            return true;
        }

        if (prefix.Length <= MaxHeaderPrefixLength &&
            HasLegacyResourceProbeHeaderShape(prefix))
        {
            return true;
        }

        return prefix.Length switch
        {
            1 => prefix[0] == KnownResourceNameBytes.Length,
            sizeof(ushort) => BinaryPrimitives.ReadUInt16LittleEndian(prefix) == KnownResourceNameBytes.Length,
            sizeof(uint) => BinaryPrimitives.ReadUInt32LittleEndian(prefix) == KnownResourceNameBytes.Length,
            _ => false,
        };
    }

    private static bool HasLegacyResourceProbeHeaderShape(ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length < sizeof(uint) ||
            prefix.Length % sizeof(uint) != 0)
        {
            return false;
        }

        var identifier = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        return identifier > 0;
    }

    private static bool IsZeroPadded(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            if (item != 0)
            {
                return false;
            }
        }

        return true;
    }
}
