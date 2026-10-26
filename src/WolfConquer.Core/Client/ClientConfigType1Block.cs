namespace WolfConquer.Core.Client;

/// <summary>
/// Unpacks an already recovered big-endian type-1 block. This performs no
/// public-key operation, configuration parsing or runtime-provider selection.
/// </summary>
public static class ClientConfigType1Block
{
    public const int MaximumModulusBytes = 2048;

    public static byte[] Decode(ReadOnlySpan<byte> recoveredBlock, int modulusByteCount, int outputCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(modulusByteCount, 11);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(modulusByteCount, MaximumModulusBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(outputCapacity);

        if (recoveredBlock.Length == modulusByteCount)
        {
            if (recoveredBlock[0] != 0)
            {
                throw new InvalidDataException("A full-width recovered block must begin with zero.");
            }

            recoveredBlock = recoveredBlock[1..];
        }
        else if (recoveredBlock.Length != modulusByteCount - 1)
        {
            throw new InvalidDataException("Recovered block length does not match the modulus width.");
        }

        if (recoveredBlock[0] != 1)
        {
            throw new InvalidDataException("Recovered block is not type 1.");
        }

        var index = 1;
        while (index < recoveredBlock.Length && recoveredBlock[index] == 0xFF)
        {
            index++;
        }

        if (index - 1 < 8 || index == recoveredBlock.Length || recoveredBlock[index] != 0)
        {
            throw new InvalidDataException("Recovered block has invalid type-1 padding.");
        }

        var payload = recoveredBlock[(index + 1)..];
        if (payload.Length > outputCapacity)
        {
            throw new InvalidDataException("Recovered block output exceeds the supplied capacity.");
        }

        return payload.ToArray();
    }
}
