using System.Buffers.Binary;

internal static partial class Program
{
    static IReadOnlyList<TcpPayloadCandidate> ExtractTcpPayloadCandidates(ReadOnlySpan<byte> bytes)
    {
        const int MinimumSectionHeaderBlockLength = 28;
        const int MinimumBlockLength = 12;
        const uint SimplePacketBlockType = 0x00000003;
        const uint EnhancedPacketBlockType = 0x00000006;
        const int MaxPayloadSamples = 16;

        var candidates = new List<TcpPayloadCandidate>();
        bool? currentSectionLittleEndian = null;
        var packetBlockIndex = 0;
        var offset = 0;

        while (offset <= bytes.Length - MinimumBlockLength)
        {
            var isSectionHeader = MatchesBytes(bytes.Slice(offset, 4), 0x0A, 0x0D, 0x0D, 0x0A);
            bool littleEndian;
            uint blockLength;

            if (isSectionHeader)
            {
                if (bytes.Length - offset < MinimumSectionHeaderBlockLength)
                {
                    break;
                }

                var byteOrderMagic = bytes.Slice(offset + 8, 4);
                var sectionLittleEndian = MatchesBytes(byteOrderMagic, 0x4D, 0x3C, 0x2B, 0x1A);
                var sectionBigEndian = MatchesBytes(byteOrderMagic, 0x1A, 0x2B, 0x3C, 0x4D);
                if (!sectionLittleEndian && !sectionBigEndian)
                {
                    break;
                }

                littleEndian = sectionLittleEndian;
                blockLength = ReadUInt32(bytes.Slice(offset + 4, 4), littleEndian);
                currentSectionLittleEndian = littleEndian;
            }
            else
            {
                if (currentSectionLittleEndian is null)
                {
                    break;
                }

                littleEndian = currentSectionLittleEndian.Value;
                var blockType = ReadUInt32(bytes.Slice(offset, 4), littleEndian);
                blockLength = ReadUInt32(bytes.Slice(offset + 4, 4), littleEndian);
                if (blockLength < MinimumBlockLength || blockLength > bytes.Length - offset)
                {
                    break;
                }

                var blockBody = bytes.Slice(offset + 8, (int)blockLength - MinimumBlockLength);
                if (blockType == EnhancedPacketBlockType || blockType == SimplePacketBlockType)
                {
                    packetBlockIndex++;
                    var packetBytes = TryGetPcapngPacketBytes(blockBody, blockType, littleEndian);
                    if (!packetBytes.IsEmpty &&
                        TryExtractTcpPayload(packetBytes, out var sourcePort, out var destinationPort, out var payload) &&
                        payload.Length > 0)
                    {
                        var sampleBytes = payload[..Math.Min(payload.Length, 32)].ToArray();
                        candidates.Add(new TcpPayloadCandidate(
                            blockType == EnhancedPacketBlockType ? "enhanced" : "simple",
                            packetBlockIndex,
                            packetBytes.Length,
                            sourcePort,
                            destinationPort,
                            payload.Length,
                            Convert.ToHexString(sampleBytes).ToLowerInvariant(),
                            Convert.ToBase64String(sampleBytes)));

                        if (candidates.Count >= MaxPayloadSamples)
                        {
                            break;
                        }
                    }
                }
            }

            if (blockLength < MinimumBlockLength || blockLength > bytes.Length - offset)
            {
                break;
            }

            offset += (int)blockLength;
        }

        return candidates;
    }

    static ReadOnlySpan<byte> TryGetPcapngPacketBytes(ReadOnlySpan<byte> blockBody, uint blockType, bool littleEndian)
    {
        const uint SimplePacketBlockType = 0x00000003;
        const uint EnhancedPacketBlockType = 0x00000006;

        if (blockType == EnhancedPacketBlockType)
        {
            if (blockBody.Length < 20)
            {
                return [];
            }

            var capturedLength = ReadUInt32(blockBody.Slice(12, 4), littleEndian);
            if (capturedLength > int.MaxValue || blockBody.Length - 20 < capturedLength)
            {
                return [];
            }

            return blockBody.Slice(20, (int)capturedLength);
        }

        if (blockType == SimplePacketBlockType)
        {
            if (blockBody.Length < 4)
            {
                return [];
            }

            var packetLength = ReadUInt32(blockBody[..4], littleEndian);
            var capturedLength = Math.Min((int)Math.Min(packetLength, int.MaxValue), blockBody.Length - 4);
            return blockBody.Slice(4, capturedLength);
        }

        return [];
    }

    static bool TryExtractTcpPayload(
        ReadOnlySpan<byte> packetBytes,
        out int sourcePort,
        out int destinationPort,
        out ReadOnlySpan<byte> payload)
    {
        sourcePort = 0;
        destinationPort = 0;
        payload = [];
        if (packetBytes.Length < 20)
        {
            return false;
        }

        var ipOffset = -1;
        if ((packetBytes[0] >> 4) == 4)
        {
            ipOffset = 0;
        }
        else if (packetBytes.Length >= 34 &&
            packetBytes[12] == 0x08 &&
            packetBytes[13] == 0x00 &&
            (packetBytes[14] >> 4) == 4)
        {
            ipOffset = 14;
        }

        if (ipOffset < 0 || packetBytes.Length - ipOffset < 20)
        {
            return false;
        }

        var ipBytes = packetBytes[ipOffset..];
        var ipHeaderLength = (ipBytes[0] & 0x0F) * 4;
        if (ipHeaderLength < 20 || ipBytes.Length < ipHeaderLength || ipBytes[9] != 6)
        {
            return false;
        }

        var flagsAndFragmentOffset = BinaryPrimitives.ReadUInt16BigEndian(ipBytes.Slice(6, 2));
        if ((flagsAndFragmentOffset & 0x1FFF) != 0)
        {
            return false;
        }

        var totalLength = (int)BinaryPrimitives.ReadUInt16BigEndian(ipBytes.Slice(2, 2));
        if (totalLength < ipHeaderLength || totalLength > ipBytes.Length)
        {
            totalLength = ipBytes.Length;
        }

        var tcpBytes = ipBytes.Slice(ipHeaderLength, totalLength - ipHeaderLength);
        if (tcpBytes.Length < 20)
        {
            return false;
        }

        var tcpHeaderLength = (tcpBytes[12] >> 4) * 4;
        if (tcpHeaderLength < 20 || tcpBytes.Length < tcpHeaderLength)
        {
            return false;
        }

        sourcePort = BinaryPrimitives.ReadUInt16BigEndian(tcpBytes[..2]);
        destinationPort = BinaryPrimitives.ReadUInt16BigEndian(tcpBytes.Slice(2, 2));
        payload = tcpBytes[tcpHeaderLength..];
        return true;
    }
}
