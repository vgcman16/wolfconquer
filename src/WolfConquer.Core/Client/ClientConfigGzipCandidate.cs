using System.Buffers.Binary;

namespace WolfConquer.Core.Client;

/// <summary>Exposes the observed header/footer fields without validating or expanding a gzip stream.</summary>
public static class ClientConfigGzipCandidate
{
    public static uint ReadDeclaredOutputLength(ReadOnlySpan<byte> transformedContents)
    {
        if (transformedContents.Length <= sizeof(uint) || transformedContents[0] != 0x1F || transformedContents[1] != 0x8B)
        {
            throw new InvalidDataException("The transformed contents do not meet the observed gzip-candidate prefix and length checks.");
        }

        // This is a raw trailer value, not a verified output length, allocation
        // authorization, encoding declaration or proof of a complete stream.
        return BinaryPrimitives.ReadUInt32LittleEndian(transformedContents[^sizeof(uint)..]);
    }
}
