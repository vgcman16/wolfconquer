using System.Buffers.Binary;
using System.Numerics;

namespace WolfConquer.Core.Client;

/// <summary>
/// The inspected D13 default public transform with exponent 65537 and type-1
/// unpacking. The caller selects this profile explicitly; this class does not
/// establish which native global/provider/method was selected at runtime.
/// </summary>
public static class ClientConfigDefaultPublicTransform
{
    private const uint PublicExponent = 65537;

    public static byte[] DecodeBlock(ReadOnlySpan<byte> encodedBlock, ReadOnlySpan<byte> modulusBigEndian,
        int outputCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(outputCapacity);
        // The native constructor shifts by 32 and adds each word. Its integer
        // import/export are unsigned big-endian; padding is checked on the
        // minimal recovered representation, never by adding missing zeroes.
        while (!modulusBigEndian.IsEmpty && modulusBigEndian[0] == 0)
        {
            modulusBigEndian = modulusBigEndian[1..];
        }

        if (modulusBigEndian.Length > ClientConfigType1Block.MaximumModulusBytes)
        {
            throw new ArgumentException("The modulus exceeds the inspected default profile.", nameof(modulusBigEndian));
        }

        if (modulusBigEndian.Length < 11 || (modulusBigEndian[^1] & 1) == 0)
        {
            throw new ArgumentException("The default transform requires an odd modulus of at least 11 bytes.", nameof(modulusBigEndian));
        }

        if (encodedBlock.IsEmpty || encodedBlock.Length > modulusBigEndian.Length)
        {
            throw new InvalidDataException("Encoded block length exceeds the modulus width or is empty.");
        }

        var modulus = new BigInteger(modulusBigEndian, isUnsigned: true, isBigEndian: true);
        var encoded = new BigInteger(encodedBlock, isUnsigned: true, isBigEndian: true);
        if (modulus <= PublicExponent || encoded >= modulus)
        {
            throw new InvalidDataException("The default public-transform integer bounds are invalid.");
        }

        // The selected one-bit power loop is repeated square/multiply modulo n.
        // Use managed arithmetic for that value contract, not a native provider,
        // a caller-supplied transform callback or a private-key operation.
        var recovered = BigInteger.ModPow(encoded, PublicExponent, modulus)
            .ToByteArray(isUnsigned: true, isBigEndian: true);
        return ClientConfigType1Block.Decode(recovered, modulusBigEndian.Length, outputCapacity);
    }

    public static byte[] DecodeWordBlock(ReadOnlySpan<byte> encodedBlock, ReadOnlySpan<uint> encodedModulusWords,
        int outputCapacity)
    {
        var words = ClientConfigWordRecipe.Decode(encodedModulusWords);
        var modulus = new byte[ClientConfigWordRecipe.WordCount * sizeof(uint)];
        for (var index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(modulus.AsSpan(index * sizeof(uint)), words[index]);
        }

        return DecodeBlock(encodedBlock, modulus, outputCapacity);
    }
}
