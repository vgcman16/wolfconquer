using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;

namespace WolfConquer.Core.Networking;

public sealed class LegacyGameServerCipher
{
    public const int BlockSize = 8;
    public const int KeyMaterialSize = 16;
    public const int DiffieHellmanProcessedKeySize = 64;
    public const string LoginKey = "AS19RayF54ZO1D6P";
    public const string DiffieHellmanPrimeHex = "E7A69EBDF105F2A6BBDEAD7E798F76A209AD73FB466431E2E7352ED262F8C558F10BEFEA977DE9E21DCEE9B04D245F300ECCBBA03E72630556D011023F9E857F";
    public const string DiffieHellmanGeneratorHex = "05";

    private readonly Cast5Block _cast5 = new();
    private readonly byte[] _encryptIv = new byte[BlockSize];
    private readonly byte[] _decryptIv = new byte[BlockSize];
    private int _encryptIndex;
    private int _decryptIndex;
    private bool _hasKey;

    public LegacyGameServerCipher()
    {
    }

    public LegacyGameServerCipher(ReadOnlySpan<byte> keyMaterial)
    {
        SetKey(keyMaterial);
    }

    public void SetKey(ReadOnlySpan<byte> keyMaterial)
    {
        if (keyMaterial.Length < KeyMaterialSize)
        {
            throw new ArgumentException("Legacy GameServer cipher key material must contain at least 16 bytes.", nameof(keyMaterial));
        }

        _cast5.SetKey(keyMaterial[..KeyMaterialSize]);
        _hasKey = true;
    }

    public void Reset()
    {
        Array.Clear(_encryptIv);
        Array.Clear(_decryptIv);
        _encryptIndex = 0;
        _decryptIndex = 0;
    }

    public void Encrypt(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ValidateTransformBuffers(source, destination);
        EnsureKeyed();

        for (int i = 0; i < source.Length; i++)
        {
            if (_encryptIndex == 0)
            {
                _cast5.EncryptBlock(_encryptIv);
            }

            byte encrypted = (byte)(source[i] ^ _encryptIv[_encryptIndex]);
            destination[i] = encrypted;
            _encryptIv[_encryptIndex] = encrypted;
            _encryptIndex = (_encryptIndex + 1) & 7;
        }
    }

    public void Decrypt(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        ValidateTransformBuffers(source, destination);
        EnsureKeyed();

        for (int i = 0; i < source.Length; i++)
        {
            if (_decryptIndex == 0)
            {
                _cast5.EncryptBlock(_decryptIv);
            }

            byte encrypted = source[i];
            byte decrypted = (byte)(_decryptIv[_decryptIndex] ^ encrypted);
            destination[i] = decrypted;
            _decryptIv[_decryptIndex] = encrypted;
            _decryptIndex = (_decryptIndex + 1) & 7;
        }
    }

    public void EncryptInPlace(Span<byte> buffer)
    {
        Encrypt(buffer, buffer);
    }

    public void DecryptInPlace(Span<byte> buffer)
    {
        Decrypt(buffer, buffer);
    }

    public static int GetFixedDiffieHellmanKeyLength(ReadOnlySpan<byte> sharedSecret)
    {
        int zeroIndex = sharedSecret.IndexOf((byte)0);
        return zeroIndex < 0 ? sharedSecret.Length : zeroIndex;
    }

    public static int PostProcessDiffieHellmanKey(ReadOnlySpan<byte> sharedSecret, Span<byte> destination)
    {
        if (destination.Length < DiffieHellmanProcessedKeySize)
        {
            throw new ArgumentException("Destination must have room for the 64-byte processed Diffie-Hellman key.", nameof(destination));
        }

        int fixedLength = GetFixedDiffieHellmanKeyLength(sharedSecret);
        Span<byte> firstHash = stackalloc byte[16];
        MD5.HashData(sharedSecret[..fixedLength], firstHash);

        Span<byte> firstHex = destination[..32];
        WriteLowerHexAscii(firstHash, firstHex);

        Span<byte> doubledHex = stackalloc byte[64];
        firstHex.CopyTo(doubledHex);
        firstHex.CopyTo(doubledHex[32..]);

        Span<byte> secondHash = stackalloc byte[16];
        MD5.HashData(doubledHex, secondHash);
        WriteLowerHexAscii(secondHash, destination[32..DiffieHellmanProcessedKeySize]);

        return DiffieHellmanProcessedKeySize;
    }

    public static int DeriveKeyMaterialFromDiffieHellmanSharedSecret(ReadOnlySpan<byte> sharedSecret, Span<byte> destination)
    {
        if (destination.Length < KeyMaterialSize)
        {
            throw new ArgumentException("Destination must have room for the 16-byte packet cipher key material.", nameof(destination));
        }

        Span<byte> processed = stackalloc byte[DiffieHellmanProcessedKeySize];
        PostProcessDiffieHellmanKey(sharedSecret, processed);
        processed[..KeyMaterialSize].CopyTo(destination);
        return KeyMaterialSize;
    }

    private void EnsureKeyed()
    {
        if (!_hasKey)
        {
            throw new InvalidOperationException("Legacy GameServer cipher key has not been set.");
        }
    }

    private static void ValidateTransformBuffers(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination must be at least as long as the source.", nameof(destination));
        }

        if (source.Overlaps(destination, out int elementOffset) && elementOffset != 0)
        {
            throw new ArgumentException("Source and destination must be the same span or non-overlapping.", nameof(destination));
        }
    }

    private static void WriteLowerHexAscii(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        for (int i = 0; i < source.Length; i++)
        {
            byte value = source[i];
            destination[i * 2] = ToLowerHexAscii(value >> 4);
            destination[(i * 2) + 1] = ToLowerHexAscii(value & 0xF);
        }
    }

    private static byte ToLowerHexAscii(int value)
    {
        return (byte)(value < 10 ? '0' + value : 'a' + value - 10);
    }

    private sealed class Cast5Block
    {
        private const int Rounds = 16;

        private static readonly uint[] S1 = DecodeSBox(S1Data);
        private static readonly uint[] S2 = DecodeSBox(S2Data);
        private static readonly uint[] S3 = DecodeSBox(S3Data);
        private static readonly uint[] S4 = DecodeSBox(S4Data);
        private static readonly uint[] S5 = DecodeSBox(S5Data);
        private static readonly uint[] S6 = DecodeSBox(S6Data);
        private static readonly uint[] S7 = DecodeSBox(S7Data);
        private static readonly uint[] S8 = DecodeSBox(S8Data);

        private readonly int[] _kr = new int[Rounds + 1];
        private readonly uint[] _km = new uint[Rounds + 1];

        public void SetKey(ReadOnlySpan<byte> key)
        {
            Span<int> x = stackalloc int[16];
            Span<int> z = stackalloc int[16];

            for (int i = 0; i < key.Length; i++)
            {
                z[i] = key[i] & 0xFF;
            }

            uint x0x1x2x3 = IntsTo32Bits(z, 0);
            uint x4x5x6x7 = IntsTo32Bits(z, 4);
            uint x8x9xAxB = IntsTo32Bits(z, 8);
            uint xCxDxExF = IntsTo32Bits(z, 12);

            uint z0z1z2z3 = x0x1x2x3 ^ S5[z[13]] ^ S6[z[15]] ^ S7[z[12]] ^ S8[z[14]] ^ S7[z[8]];
            Bits32ToInts(z0z1z2z3, x, 0);
            uint z4z5z6z7 = x8x9xAxB ^ S5[x[0]] ^ S6[x[2]] ^ S7[x[1]] ^ S8[x[3]] ^ S8[z[10]];
            Bits32ToInts(z4z5z6z7, x, 4);
            uint z8z9zAzB = xCxDxExF ^ S5[x[7]] ^ S6[x[6]] ^ S7[x[5]] ^ S8[x[4]] ^ S5[z[9]];
            Bits32ToInts(z8z9zAzB, x, 8);
            uint zCzDzEzF = x4x5x6x7 ^ S5[x[10]] ^ S6[x[9]] ^ S7[x[11]] ^ S8[x[8]] ^ S6[z[11]];
            Bits32ToInts(zCzDzEzF, x, 12);
            _km[1] = S5[x[8]] ^ S6[x[9]] ^ S7[x[7]] ^ S8[x[6]] ^ S5[x[2]];
            _km[2] = S5[x[10]] ^ S6[x[11]] ^ S7[x[5]] ^ S8[x[4]] ^ S6[x[6]];
            _km[3] = S5[x[12]] ^ S6[x[13]] ^ S7[x[3]] ^ S8[x[2]] ^ S7[x[9]];
            _km[4] = S5[x[14]] ^ S6[x[15]] ^ S7[x[1]] ^ S8[x[0]] ^ S8[x[12]];

            x0x1x2x3 = IntsTo32Bits(x, 0);
            x4x5x6x7 = IntsTo32Bits(x, 4);
            x8x9xAxB = IntsTo32Bits(x, 8);
            xCxDxExF = IntsTo32Bits(x, 12);
            z0z1z2z3 = x8x9xAxB ^ S5[x[5]] ^ S6[x[7]] ^ S7[x[4]] ^ S8[x[6]] ^ S7[x[0]];
            Bits32ToInts(z0z1z2z3, z, 0);
            z4z5z6z7 = x0x1x2x3 ^ S5[z[0]] ^ S6[z[2]] ^ S7[z[1]] ^ S8[z[3]] ^ S8[x[2]];
            Bits32ToInts(z4z5z6z7, z, 4);
            z8z9zAzB = x4x5x6x7 ^ S5[z[7]] ^ S6[z[6]] ^ S7[z[5]] ^ S8[z[4]] ^ S5[x[1]];
            Bits32ToInts(z8z9zAzB, z, 8);
            zCzDzEzF = xCxDxExF ^ S5[z[10]] ^ S6[z[9]] ^ S7[z[11]] ^ S8[z[8]] ^ S6[x[3]];
            Bits32ToInts(zCzDzEzF, z, 12);
            _km[5] = S5[z[3]] ^ S6[z[2]] ^ S7[z[12]] ^ S8[z[13]] ^ S5[z[8]];
            _km[6] = S5[z[1]] ^ S6[z[0]] ^ S7[z[14]] ^ S8[z[15]] ^ S6[z[13]];
            _km[7] = S5[z[7]] ^ S6[z[6]] ^ S7[z[8]] ^ S8[z[9]] ^ S7[z[3]];
            _km[8] = S5[z[5]] ^ S6[z[4]] ^ S7[z[10]] ^ S8[z[11]] ^ S8[z[7]];

            x0x1x2x3 = IntsTo32Bits(z, 0);
            x4x5x6x7 = IntsTo32Bits(z, 4);
            x8x9xAxB = IntsTo32Bits(z, 8);
            xCxDxExF = IntsTo32Bits(z, 12);
            z0z1z2z3 = x0x1x2x3 ^ S5[z[13]] ^ S6[z[15]] ^ S7[z[12]] ^ S8[z[14]] ^ S7[z[8]];
            Bits32ToInts(z0z1z2z3, x, 0);
            z4z5z6z7 = x8x9xAxB ^ S5[x[0]] ^ S6[x[2]] ^ S7[x[1]] ^ S8[x[3]] ^ S8[z[10]];
            Bits32ToInts(z4z5z6z7, x, 4);
            z8z9zAzB = xCxDxExF ^ S5[x[7]] ^ S6[x[6]] ^ S7[x[5]] ^ S8[x[4]] ^ S5[z[9]];
            Bits32ToInts(z8z9zAzB, x, 8);
            zCzDzEzF = x4x5x6x7 ^ S5[x[10]] ^ S6[x[9]] ^ S7[x[11]] ^ S8[x[8]] ^ S6[z[11]];
            Bits32ToInts(zCzDzEzF, x, 12);
            _km[9] = S5[x[3]] ^ S6[x[2]] ^ S7[x[12]] ^ S8[x[13]] ^ S5[x[9]];
            _km[10] = S5[x[1]] ^ S6[x[0]] ^ S7[x[14]] ^ S8[x[15]] ^ S6[x[12]];
            _km[11] = S5[x[7]] ^ S6[x[6]] ^ S7[x[8]] ^ S8[x[9]] ^ S7[x[2]];
            _km[12] = S5[x[5]] ^ S6[x[4]] ^ S7[x[10]] ^ S8[x[11]] ^ S8[x[6]];

            x0x1x2x3 = IntsTo32Bits(x, 0);
            x4x5x6x7 = IntsTo32Bits(x, 4);
            x8x9xAxB = IntsTo32Bits(x, 8);
            xCxDxExF = IntsTo32Bits(x, 12);
            z0z1z2z3 = x8x9xAxB ^ S5[x[5]] ^ S6[x[7]] ^ S7[x[4]] ^ S8[x[6]] ^ S7[x[0]];
            Bits32ToInts(z0z1z2z3, z, 0);
            z4z5z6z7 = x0x1x2x3 ^ S5[z[0]] ^ S6[z[2]] ^ S7[z[1]] ^ S8[z[3]] ^ S8[x[2]];
            Bits32ToInts(z4z5z6z7, z, 4);
            z8z9zAzB = x4x5x6x7 ^ S5[z[7]] ^ S6[z[6]] ^ S7[z[5]] ^ S8[z[4]] ^ S5[x[1]];
            Bits32ToInts(z8z9zAzB, z, 8);
            zCzDzEzF = xCxDxExF ^ S5[z[10]] ^ S6[z[9]] ^ S7[z[11]] ^ S8[z[8]] ^ S6[x[3]];
            Bits32ToInts(zCzDzEzF, z, 12);
            _km[13] = S5[z[8]] ^ S6[z[9]] ^ S7[z[7]] ^ S8[z[6]] ^ S5[z[3]];
            _km[14] = S5[z[10]] ^ S6[z[11]] ^ S7[z[5]] ^ S8[z[4]] ^ S6[z[7]];
            _km[15] = S5[z[12]] ^ S6[z[13]] ^ S7[z[3]] ^ S8[z[2]] ^ S7[z[8]];
            _km[16] = S5[z[14]] ^ S6[z[15]] ^ S7[z[1]] ^ S8[z[0]] ^ S8[z[13]];

            x0x1x2x3 = IntsTo32Bits(z, 0);
            x4x5x6x7 = IntsTo32Bits(z, 4);
            x8x9xAxB = IntsTo32Bits(z, 8);
            xCxDxExF = IntsTo32Bits(z, 12);
            z0z1z2z3 = x0x1x2x3 ^ S5[z[13]] ^ S6[z[15]] ^ S7[z[12]] ^ S8[z[14]] ^ S7[z[8]];
            Bits32ToInts(z0z1z2z3, x, 0);
            z4z5z6z7 = x8x9xAxB ^ S5[x[0]] ^ S6[x[2]] ^ S7[x[1]] ^ S8[x[3]] ^ S8[z[10]];
            Bits32ToInts(z4z5z6z7, x, 4);
            z8z9zAzB = xCxDxExF ^ S5[x[7]] ^ S6[x[6]] ^ S7[x[5]] ^ S8[x[4]] ^ S5[z[9]];
            Bits32ToInts(z8z9zAzB, x, 8);
            zCzDzEzF = x4x5x6x7 ^ S5[x[10]] ^ S6[x[9]] ^ S7[x[11]] ^ S8[x[8]] ^ S6[z[11]];
            Bits32ToInts(zCzDzEzF, x, 12);
            _kr[1] = (int)((S5[x[8]] ^ S6[x[9]] ^ S7[x[7]] ^ S8[x[6]] ^ S5[x[2]]) & 0x1F);
            _kr[2] = (int)((S5[x[10]] ^ S6[x[11]] ^ S7[x[5]] ^ S8[x[4]] ^ S6[x[6]]) & 0x1F);
            _kr[3] = (int)((S5[x[12]] ^ S6[x[13]] ^ S7[x[3]] ^ S8[x[2]] ^ S7[x[9]]) & 0x1F);
            _kr[4] = (int)((S5[x[14]] ^ S6[x[15]] ^ S7[x[1]] ^ S8[x[0]] ^ S8[x[12]]) & 0x1F);

            x0x1x2x3 = IntsTo32Bits(x, 0);
            x4x5x6x7 = IntsTo32Bits(x, 4);
            x8x9xAxB = IntsTo32Bits(x, 8);
            xCxDxExF = IntsTo32Bits(x, 12);
            z0z1z2z3 = x8x9xAxB ^ S5[x[5]] ^ S6[x[7]] ^ S7[x[4]] ^ S8[x[6]] ^ S7[x[0]];
            Bits32ToInts(z0z1z2z3, z, 0);
            z4z5z6z7 = x0x1x2x3 ^ S5[z[0]] ^ S6[z[2]] ^ S7[z[1]] ^ S8[z[3]] ^ S8[x[2]];
            Bits32ToInts(z4z5z6z7, z, 4);
            z8z9zAzB = x4x5x6x7 ^ S5[z[7]] ^ S6[z[6]] ^ S7[z[5]] ^ S8[z[4]] ^ S5[x[1]];
            Bits32ToInts(z8z9zAzB, z, 8);
            zCzDzEzF = xCxDxExF ^ S5[z[10]] ^ S6[z[9]] ^ S7[z[11]] ^ S8[z[8]] ^ S6[x[3]];
            Bits32ToInts(zCzDzEzF, z, 12);
            _kr[5] = (int)((S5[z[3]] ^ S6[z[2]] ^ S7[z[12]] ^ S8[z[13]] ^ S5[z[8]]) & 0x1F);
            _kr[6] = (int)((S5[z[1]] ^ S6[z[0]] ^ S7[z[14]] ^ S8[z[15]] ^ S6[z[13]]) & 0x1F);
            _kr[7] = (int)((S5[z[7]] ^ S6[z[6]] ^ S7[z[8]] ^ S8[z[9]] ^ S7[z[3]]) & 0x1F);
            _kr[8] = (int)((S5[z[5]] ^ S6[z[4]] ^ S7[z[10]] ^ S8[z[11]] ^ S8[z[7]]) & 0x1F);

            x0x1x2x3 = IntsTo32Bits(z, 0);
            x4x5x6x7 = IntsTo32Bits(z, 4);
            x8x9xAxB = IntsTo32Bits(z, 8);
            xCxDxExF = IntsTo32Bits(z, 12);
            z0z1z2z3 = x0x1x2x3 ^ S5[z[13]] ^ S6[z[15]] ^ S7[z[12]] ^ S8[z[14]] ^ S7[z[8]];
            Bits32ToInts(z0z1z2z3, x, 0);
            z4z5z6z7 = x8x9xAxB ^ S5[x[0]] ^ S6[x[2]] ^ S7[x[1]] ^ S8[x[3]] ^ S8[z[10]];
            Bits32ToInts(z4z5z6z7, x, 4);
            z8z9zAzB = xCxDxExF ^ S5[x[7]] ^ S6[x[6]] ^ S7[x[5]] ^ S8[x[4]] ^ S5[z[9]];
            Bits32ToInts(z8z9zAzB, x, 8);
            zCzDzEzF = x4x5x6x7 ^ S5[x[10]] ^ S6[x[9]] ^ S7[x[11]] ^ S8[x[8]] ^ S6[z[11]];
            Bits32ToInts(zCzDzEzF, x, 12);
            _kr[9] = (int)((S5[x[3]] ^ S6[x[2]] ^ S7[x[12]] ^ S8[x[13]] ^ S5[x[9]]) & 0x1F);
            _kr[10] = (int)((S5[x[1]] ^ S6[x[0]] ^ S7[x[14]] ^ S8[x[15]] ^ S6[x[12]]) & 0x1F);
            _kr[11] = (int)((S5[x[7]] ^ S6[x[6]] ^ S7[x[8]] ^ S8[x[9]] ^ S7[x[2]]) & 0x1F);
            _kr[12] = (int)((S5[x[5]] ^ S6[x[4]] ^ S7[x[10]] ^ S8[x[11]] ^ S8[x[6]]) & 0x1F);

            x0x1x2x3 = IntsTo32Bits(x, 0);
            x4x5x6x7 = IntsTo32Bits(x, 4);
            x8x9xAxB = IntsTo32Bits(x, 8);
            xCxDxExF = IntsTo32Bits(x, 12);
            z0z1z2z3 = x8x9xAxB ^ S5[x[5]] ^ S6[x[7]] ^ S7[x[4]] ^ S8[x[6]] ^ S7[x[0]];
            Bits32ToInts(z0z1z2z3, z, 0);
            z4z5z6z7 = x0x1x2x3 ^ S5[z[0]] ^ S6[z[2]] ^ S7[z[1]] ^ S8[z[3]] ^ S8[x[2]];
            Bits32ToInts(z4z5z6z7, z, 4);
            z8z9zAzB = x4x5x6x7 ^ S5[z[7]] ^ S6[z[6]] ^ S7[z[5]] ^ S8[z[4]] ^ S5[x[1]];
            Bits32ToInts(z8z9zAzB, z, 8);
            zCzDzEzF = xCxDxExF ^ S5[z[10]] ^ S6[z[9]] ^ S7[z[11]] ^ S8[z[8]] ^ S6[x[3]];
            Bits32ToInts(zCzDzEzF, z, 12);
            _kr[13] = (int)((S5[z[8]] ^ S6[z[9]] ^ S7[z[7]] ^ S8[z[6]] ^ S5[z[3]]) & 0x1F);
            _kr[14] = (int)((S5[z[10]] ^ S6[z[11]] ^ S7[z[5]] ^ S8[z[4]] ^ S6[z[7]]) & 0x1F);
            _kr[15] = (int)((S5[z[12]] ^ S6[z[13]] ^ S7[z[3]] ^ S8[z[2]] ^ S7[z[8]]) & 0x1F);
            _kr[16] = (int)((S5[z[14]] ^ S6[z[15]] ^ S7[z[1]] ^ S8[z[0]] ^ S8[z[13]]) & 0x1F);
        }

        public void EncryptBlock(Span<byte> block)
        {
            uint left = BinaryPrimitives.ReadUInt32BigEndian(block[..4]);
            uint right = BinaryPrimitives.ReadUInt32BigEndian(block[4..8]);

            Encipher(left, right, out uint resultLeft, out uint resultRight);

            BinaryPrimitives.WriteUInt32BigEndian(block[..4], resultLeft);
            BinaryPrimitives.WriteUInt32BigEndian(block[4..8], resultRight);
        }

        private void Encipher(uint left, uint right, out uint resultLeft, out uint resultRight)
        {
            uint previousLeft = left;
            uint previousRight = right;
            uint nextLeft = left;
            uint nextRight = right;

            for (int round = 1; round <= Rounds; round++)
            {
                previousLeft = nextLeft;
                previousRight = nextRight;
                nextLeft = previousRight;
                nextRight = previousLeft ^ RoundFunction(round, previousRight);
            }

            resultLeft = nextRight;
            resultRight = nextLeft;
        }

        private uint RoundFunction(int round, uint data)
        {
            return round switch
            {
                1 or 4 or 7 or 10 or 13 or 16 => F1(data, _km[round], _kr[round]),
                2 or 5 or 8 or 11 or 14 => F2(data, _km[round], _kr[round]),
                _ => F3(data, _km[round], _kr[round])
            };
        }

        private static uint F1(uint data, uint mask, int rotate)
        {
            uint value = BitOperations.RotateLeft(mask + data, rotate);
            return (S1[(value >> 24) & 0xFF] ^ S2[(value >> 16) & 0xFF]) - S3[(value >> 8) & 0xFF] + S4[value & 0xFF];
        }

        private static uint F2(uint data, uint mask, int rotate)
        {
            uint value = BitOperations.RotateLeft(mask ^ data, rotate);
            return (S1[(value >> 24) & 0xFF] - S2[(value >> 16) & 0xFF] + S3[(value >> 8) & 0xFF]) ^ S4[value & 0xFF];
        }

        private static uint F3(uint data, uint mask, int rotate)
        {
            uint value = BitOperations.RotateLeft(mask - data, rotate);
            return ((S1[(value >> 24) & 0xFF] + S2[(value >> 16) & 0xFF]) ^ S3[(value >> 8) & 0xFF]) - S4[value & 0xFF];
        }

        private static void Bits32ToInts(uint value, Span<int> destination, int offset)
        {
            destination[offset + 3] = (int)(value & 0xFF);
            destination[offset + 2] = (int)((value >> 8) & 0xFF);
            destination[offset + 1] = (int)((value >> 16) & 0xFF);
            destination[offset] = (int)((value >> 24) & 0xFF);
        }

        private static uint IntsTo32Bits(ReadOnlySpan<int> source, int offset)
        {
            return ((uint)(source[offset] & 0xFF) << 24)
                | ((uint)(source[offset + 1] & 0xFF) << 16)
                | ((uint)(source[offset + 2] & 0xFF) << 8)
                | (uint)(source[offset + 3] & 0xFF);
        }

        private static uint[] DecodeSBox(string data)
        {
            byte[] bytes = Convert.FromBase64String(data);
            if (bytes.Length != 256 * sizeof(uint))
            {
                throw new InvalidOperationException("Legacy CAST5 S-box data is not the expected length.");
            }

            var values = new uint[256];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * sizeof(uint), sizeof(uint)));
            }

            return values;
        }

        private const string S1Data =
            "v2WBXNRA+zAL/6CfL83sa3qMJT8vPyEe000AnEDlA2BJyZ/PJ6/Uv7W9u4iQQAPidZbQmOCgY24ZAYUZHWbnwo7/1CJvO2go" +
            "WdB/wMh5I//iUF9300DDQ1aGL98apHyILb3SotbgyaEZSGw0h223YS8PVCLhMr4qaxZUqjqOViLQQdOiyEDbZi85hKcv/00A" +
            "3tK5Law/lJfYwZdKt0R2Uqc39LXvuiy4WdFR1+3w928fegla0Gh7gi717JBUwLAiNVmOvH8vbUuiZLtQEElm0i2B5b6QIjO3" +
            "nxU76RHkjrRdNP9LQMJF/T+XMa0u0PbEZYH8Va3KsdWuLayhbbfUolAMm8HyQCKIOE9uDNe/5KRyoltPLx1MVhlTnMVU40m5" +
            "/mlGsIqrtrHdWBPHRcWFY12TDxHVilNXkwQ5auA3Peaz9lQqX314OrWgdmLf/KYZaiBCetXU+SmRGBv2Xidyu2eBUKqREJA4" +
            "6wW1xozLx4QPWtcqJxRKh2uT0aKvhtIqkdJWqmBDidcNdVxCJp6zk8mEcRgtswBsFLviczy8vqB5N2JUq55FZIKLMj+Czxh3" +
            "ps6iWS4A7gTmeP6JUAmrP8L2XzIFPziByMVjadZay3bJdJnUzw0YytWCBzj2XPrHERXDihOe5zXQkdpHhpAP9J5B4qdBYjYx" +
            "lfQeBQQ7V6qNXYBK0ACDVDwqMgDfzWS/jqZXuis3xnVB069QdTLBp/ULWpGrv1RrJhQLK9fJTKuCzZxEZfL79/PFhauU21Ub" +
            "JOPUqj+9pM/io+otAk0gnqwlvcizVd/qmJ691bIxEuNsrdUq3ilDlShFvq0iARMgD8lRqvZreKoeP1Eim6dRqsxE0yrwQVp7" +
            "rft80wWVBhuR5OxB5jLDtNRoIgPMCmDJbX44zmyxa794+3BqydkDDd4539TaYxDgZPQ2R9go01qWzEezww+7dfsbUZg1zPtP" +
            "as+LtbwKH+FK/sW/EOwKpwpXOawvRAQ/U7GIYS56OeB5yydXj0HrnI3WrByWfNMqnct1AQn/ncbwZVvH2EDb2Xl3DuzU6kRH" +
            "dDIcsZ7LJN29VBx++UQR8LEOJNL9s3WWVTeso68nfNRNX8hRlnWQVuYVu6XwBANY8SwEyuo3GgHbqr+NSj66NaD/JjUJTXvD" +
            "2W4wvGYmpZgl90hWnVZe/9Bj7QzPsmN84UULcPFQ6tVyKKmFp70fr3BII9TzC4eneU07LZhB4ELn7dAMuA1HJkyBgfjXak1H" +
            "XF4MfFkZI9GYchs42/TS9VOGg6sjHi9unpxxg0bgkb1uRVaaDCA53HHFyCAc2iuW/5bm4QirQbG5icp8g+dpGkNIzAJ5xfei" +
            "ffSeQpwWe0JJ8MlaAA+P3Q==";

        private const string S2Data =
            "8ewjRZQQIB9bpwvvfs/jaYBDPzl6z2H+eiDF7pSciFVRBvxyee+nrTVyHU7OY1rVujYE3u8wxJmUBwxffdvcGPPv1qF7L7Wg" +
            "BTboWZSwFe4J2f/phgBE3FlElO+zzIO6+83D4IFB2tGxKgk7wfGX+XvP5qXbDUIBW+/n5EH/oSUG+IDhgBDEH3rumxepxnrT" +
            "pDBY/n+L3phOP+h3aZKSeXuf+iRbyBPhgwDErCU1UNdfYer3VDEUYmNLVQ0hEWhdWcNmyHPPYz3ANOLOh37Y1CErZ1yBYR8H" +
            "f2L3OYQwHjY7V+vkpGQvYJzNOtY1RrwbLQOBngz1ASe0eoSZed/joIzzbLqUMIQQXqk3Jf5vb/QfO/+havuMIHSMRY8nouDZ" +
            "NDrHTmlPiPzf6E0+iAAO741kWTWMOEWKZkOAHf2bHXK7hIalM2Ml6BKCToSYgI0StD/T/uEKKM6lm+EnUsKm1b1Ul+TdVdbF" +
            "ZHBm600LhHcBqLahqSbbhBkBhRm3Q/AhYFjQ5YQw8FRy9G8GU6Eao1VH3Nq/XWK15htWaJRryoM70m4t2wHP7LrQ06ZcPYC2" +
            "Cad3r0yjtDPWyHs5lSviXgRTDl9hb+2BZEPnIHgTXrSbYxjeIqEciNEmZ7nop0mAe9q3IiUtVV430nJSHJXSeUyJDcYCtIxI" +
            "W/6kG2ufsKTPFagcBTAMomPfcYjLL9656cnGDFP/7gsXRSHjNShUtDwpY58p50HufC0dboZSBFDzhWYexgE085UsojBQCKcx" +
            "Ew+TYBeE+XNZmCahRFxk7Kl3yFKmM//NQRcroKLZunxvA4AhCJzZUGFIP8tl12vCq/ajZHYmNIAiARMg/NHm5OYQxyCAtvDN" +
            "O02EF0347jHkJAh+60nLLK47aoSIePeP9mBd7nNW93rbXN0vwTEWoUNv9jBU7Pqz+td/Fcx5he9Y3lLRXv0v2xnOMo96+Wow" +
            "+D7wAtWaMZkP+kLCsOvjpwZJjsYMI9q4KDCCgMjz3txxsV/TyBuKCGDFwL7oyaNhTfWovPrvL8eZLoIitHDFgolO2di8NByL" +
            "5hYeMHnpOyem6v+wxrjZYWlIsgA/zv+3OyjcCFr22kOYl+H3L7cZdqSbHI+gN4bcsdOnFreTw5/rbhOnPsa8xkI3URq8KGjv" +
            "1mUDUqt3ai1L7Sc1FtIfgi5uXAn78pLbyynqXvWSWBR/T1iRe2mDVMyoZyZIYBmF6qxLjNRgOIP54CMNin44bEnS5goMYISy" +
            "HXM12EfGsdzqVkyss4G9PrCrDiOHvDhk+rG18LOiXo9CRhj8emsDCr2JsE+JpZ1kXkFFoyODA1y5O10+cpXXQ3zQbX4e398G" +
            "78RsbDmlYHFwvr9zBXaHgw==";

        private const string S3Data =
            "gzc17kDC742fXfolvz2Q6wfJEOj/f2BHS+SfNkTGH4yQys6uv/mxvurK++5QGc/orgffUQaIDpJIBa3wg4084dUQcJKffRAR" +
            "uX1kB9Tk47JeKE89IKivueCC3vqLJmegLnlygsCyP1Ur4ppIlJfv1Lw/XhLu/P8h/Rtbgu3FVZJAolcSAoMaTv9/4LrnRoJS" +
            "DhRXjr/3czOIgZ+M6E78pqW1gsm3HcCoZMKfVzFPCWdfP73ywff/QPyNtx/B0muOm+V7Q789sJkZAYUZ5sCNY5mdgVUcyJeh" +
            "bi0BSihKiMVxb8PME8JDuPFDB2w8iQmDX93tD1Dofy9+f8DXv39QAgSa+1rQ0kenLhlRFj6/cK+AE8NYLjCYX8TDfHICtA8K" +
            "gu9/D639loyuKixdSZrpjriI2lCg9CeEkFesHiIBEyAV3FKCm329731ZcqbYQKitBEX1RQN0XfoFwz7oGnWRT8JpVpJB6e8j" +
            "LvEDqfINJ2C25HYCdGX9lLKFeZLL23aCdoF3Ao2Rr/ie90hO321hjw6EneKDfS+EyOUMNIK2u5ZIsbSTqzww7yivT5ibr593" +
            "DVbckiAeTSKIqjeEltwpfdzTVifufJCLQNIfteN8wOehtGblXmHpw50g+Dzj0ZRgQaOczQ5Gdlw7mOoAgXjW1CxXR/3Z7Wz3" +
            "nCKovaqtfRJOB4pDkMCXH4rbGwi+fqCTFco4uf88sJf4wMI97LIajVEOOGT7e8xoiCcP2YEBSRLU/+Vdavh+3RTionZoA6S5" +
            "j5Vdkvr/OUvprjm6C9P/pDuT9/ojhklt+rw8GUV1Yid69FyCoIu9YdFCHtH0BK3OkqN+EreNQhByqXKCqMRwkgvlfRLIoVso" +
            "T/RiPKXqwDUx0gXo+ymJQoLf/LRTarZPW8F9DqsfCB+uGIYQbQj9/Iko//kRzEtprlxqI03K3hLFjD8s/i3Q0pZY7/jaUs/k" +
            "Z1sVlYxISkkMqLa5vIKPXEVr04k3lGA6qckA7FNScURJS4cKQLxz1xxnNHz2fnECNlXrT/8v0KLEYL/SwAM/1G3vtFDRjEcH" +
            "iBhuAFU/5aK81Oa5FoAEojM4V5dnfSDXPY8P3jN7+HIzT8yrXcWIdrCmAHsBAHuU0nUAV/iIu/meAUKJ/6VkQuACY4Ur2dty" +
            "aRuX7t4vom4rrghfbWF6r2eHyeXS6x/PwsjvYXElrPHCOYLMuEwhZ9GD5bFiPty3zr0QfzhcCvk9RPAPxm1uYEk6VGBIwSdX" +
            "HYrpKzgXtIokvuEgD9qWryWERWjlO4OZfUUNYFCTLyhiszSDIBEd2aCNbSsxHitkAFownIjmvFKKWAMb1e+695ztQkERXDGk" +
            "xT4ygzZG798BxTOhHFPT6Q==";

        private const string S4Data =
            "0n7vCiAEs53e6bYf73u+p5iic9Lbe09KV4ytZEMEUYXRDgL6/3oofmO2D+ahNV8JIPHreUOdBf2xt5dkYx9k899KHiRffxQo" +
            "zbiiT0AAQ8kgIsMMMAvT/U83pcDZAC0dFXsUJBoRTe5nUcoPTJD/cf5fGS1fZAUa/v4TDMoIGwghARcFAAFTgP5ePuj49Jqs" +
            "ASfnf1/uuNJhQt8GipueuyXqk3Lf/4TOAYhx9QRL1j07Jm+iAITUfubrflSgTG1E9dbzbN+rSSb1x6CuwYwzNpN+P1BhIHfT" +
            "4Ti2EQMOUHK7sg74LlDgq953jeyBHpdXRmdP4QBUM8mPMSBpmbsdCKUEw/8FGDVN41w9fxkBhRmpzFtd6m/s2pFvkp8vIkaf" +
            "fUaROY5tv6VPxEMRAoOVQ+tOIdC4gyACDBi2Px6T+BjmWBYoPm5IJnCK14vB5Hd0fOAGtSUKLfMCiwl5gbvq5CM7Eig4rd5p" +
            "Fsp0FWIbh9+3QBwh+Z4apXs3FADIih4EA0ARCdLkWb3VVtHj1XboT0CjkS/e6HtVp+TqAOzC5Qymu7RN/71W56xpM901sBfs" +
            "JyNXBrDIr5mRw8hWHIFlaxlhFF51y4VuAsAHvndVMsLs9D+JLcm/WyU77NC3GoC3JDttje9jxyD8pWbDgCg4nAUyzgqKVMmq" +
            "IgETIDL6GgRaYhYdLJABZ1R6dZv3d9QxMbAmkdtvzDZGiwvHSGrm2Xla5VbrTGoC/35DUrR2jy+lgPkN4810husE2u0EvqkX" +
            "3/QYLJ1/dLe09yqrIE3D73xrCS5UokEXNaC25fZCPSEmfBwsD/XCYfnaUmX4McLSaQ8TJaJ/FtjI8hgEppYaAKsmFQ0hXDFj" +
            "7HIKXv3+uknZCHkYhr0NjadwETEMZJs+1xA+zLbTytWIw64M4QEw9/+Kcmyh4upxbvOaHy/Ry88XhN7Ba74HrNihRMtWD5uL" +
            "w4g5AcovxbHNMb60Bih42OKkoxIy5X1vtn79WADpHtDC/60kxQ+Z9MWqEZeVex0A0uflgvZzmBCWMGEAIZUtw/8hoa0VhJAp" +
            "f5e7f9uznq8q7ckpZaTiXCzzMKfoP6rQkcBciucsntSpVOQMhs0K1hkZXwEDkQd39jqg3l5WqHjfVuPevlzwIYfjdYtRBsWz" +
            "78OluNK27th3viPlKUUVwt/vaS/7euavssRw9Fvr4PN2mMzWDEbkOTiF2h8vg4cZZ3MAyvhEkameKWsplcIvSau+ZpJpbme1" +
            "2t3Tmy8Fft8ccCXb7lFeG+YkU/Zs4/xqBMwWAz4hRIbQWdy3HylleUP91sx5OYJB9s0rk03DV7aC0t9ODCnlemtTuTz+IB6F" +
            "flUzmLDw7BNys//TwcWFPw==";

        private const string S5Data =
            "1Ofp7wQMyX65dG4s32YOmxF5M6b/f2q49VjTHUSd3UR/FjEX+vH7CMwR9ecAGwXSALpac9gityrLgWM4OiT2rHr9vml/56Lm" +
            "zSDH8BZIScSAwfXMQBaFOEiosBXLGIvm/96qTAEKSF+qshIE/BSYJeLv0EGNtEBO+7aOJP4cuo0Cm6lBBApVGstlj7rn9FFy" +
            "JRelldfsBsEKmKWXqrk5xWr+eU1j9/PyQICvaFaeDO2LlbQRiFrr4bDmCYdWceDXp/4pTi3lZmMAwNECBY6sxHH1d5MqNwUM" +
            "8jWFVwK+YSLJoELWgKIT39JbtXTAmSFo7OUh1Og8+1MZAYUZyX+oKIGZlT0A+R9cmdM4/gv/TgzqByQGsU8vqnZpuU8FlceQ" +
            "dKeosP+hVe/CopzlJy22pmNCauYfAGXfZgnFDrxV3d9VBt4pmnMekXWJrxciARMgaJT4iYDpAQ30VUdSyTy2A7JEyAyq8PO8" +
            "6TashyZ0OuUr2LMBSXSeGn4t7mTasdvNEEnJAYC/aLj98yYN5+1Ck4TCpQS2N2djFrb1UONmR/LBNsqO2wVuE5GD8f43eoj7" +
            "1Pfn1sl9+8ff/GMw3on1ttpBKeyVZuQmGWRWt8XvVPa3WI3QAVSSSH/LusEPVf/lSTAItujQtVtaLteH4W5qq85mOiLN8yvG" +
            "+YUInkc+y2gPAWwIIOgdot5pi9F3V/bz9sMC+sPafkBQ1bPLTQiTF7oO17DVeLMKDPtR2Vba197kuyRBVgvKlNFVVw9u5eHg" +
            "vrWEYZ8kCljAS/eUjogn42FVe5+AAtzDFXdoBddrbGSzTZBEo/C0Zopk8cCv1X5p9i/pSU83njBqNbYsc4WAhUD4kUkCrvB2" +
            "Teg7CJocQigGlEhEuExucxApCcHGX8mL9JyGfW9hTxONEXcu4Ssbs3K0kKoX16U8uhsWfRCQrZyiK0av0lnkn1lF00UT2vLZ" +
            "h1TG20755PNvSG0X6hN8CcelHWOCc19E9INWF5dqxs2IAr5wcs/Ns/PSXW55YJMgpYCbRdviYL4BMcKpXDGl6/JCTiJyFVwc" +
            "LBty9vP/0hpOQCWML9dOMv23Z0COEyMFeLyjXG7WD9yDIpJ1F2tNeG6x61iFTwlEhx1IP3uu/vx2/7V3vwIjjFZ19KoqsEZf" +
            "ASgJK/f1OD02H6gMikqvUsDn1WZ0CDvfEFEFlajXWhut1R72eeT2bISBdSBl+s7QWL73iCZoBErz+PYPcH+coKCrRlMobOlc" +
            "o+124X8wrGvSKWg3qQ82hSr+4xdnl7ckIGup9ZUlzda/Hv9oLERVdb4Gn/GaZeD5HUm57hgHATS4yjC7Ff4i6IMJV4hJYg51" +
            "VX5i2qj/dl5GRVOxCN5HbQ==";

        private const string S6Data =
            "L8911p2P+vbhbKwsZ0ijTHx/M+LnCNuVtENoAbxc7eysU1UyYAmfv+3iod+dV/CDuYbtY7imtho5vl7eMveP8zixiYlhSfEz" +
            "vTcZwNrGBvV+XmLkmeoIozzjI07M18t5Z0OhSBmWFKPVS8n+ShcUoWYYoOot24Sgb0ioCUphiKiYrwApkVlmAWMomeFgDPPI" +
            "PO94LjIZ1dAU7A/P0gfK93IgqNB+GUH9sKYFk9rja+jN0750PKUtN0hEf0xA1LXaww66bacZOQjZ7rqfsM/bSVMMZ04BnD1c" +
            "Qbm9ZGpjDizN2X26iHNv6mLHC+fbmvI1jd1MXIyN1PDiU4G4GQGFGcjq4hqJr0woI4KSqlO+NJO/ITo740tDFgY56ppuw+jv" +
            "2c2Q+K5tIoCjpEDDCZx+3weolKbMXnxbIgETIC+gaZpUioFobymyzjqEwFNVNon+iua/JbyKYrS/LiLPSG+sJYeTqall271T" +
            "5/tv53j9Z+ljNakLwSs0jukboegNdIBJ/H0IyJm/5I2gARGhdXnTf8AmWtpPmR/oic0ole2fM/2/NHi4bUUEX5iGJSI7yMTJ" +
            "vlbBLaqNYk/FXvVXvgoi4r9ukdKVW8dOwMPyJJld0UKgfw3N/ydue/CK3KgGwUVzLyMe9IYjFjUmiermlLAzM/LGfhWvdCs3" +
            "5HMlaUjYqemJAhbzHe9iOjjih6d29qXzU0g2dGMQlSCNaXZFB9T6tlD5KlkjNfc2h277TMDOpH2qLRVsqJYDy13+DcWrB9f8" +
            "L8QhCbvw34l4vuJfM0+PRMkTRnWN0AUrhdW5SEGUBNybjwnIhuftfXMzmsMFAEFCURcJaqbI8w7WcgCJgnYgKL73qamdZzK/" +
            "dVtb1AD9U7NY47DLCiIPgxSyjx8Iz3LTE0o8zGYx9oy+hxwGiI/JiJfjYmB6js9Hg1LItvuswjx2acA/UgKPTk0x2GTjcDja" +
            "WVRmHvAICcGlITBRt2hbbKCKL4I+zQcw755xdIEmh9zUQDMH2S9DfkHCXgxsKAmIkdiS9fYwqQgF836Vvf/7t2/pZsKYrORv" +
            "wOxzsSq0YLzamDSVEq6h+zbXSy2r+iUP6/zzpCORluI9DH8lSa9Ik7wAFDZKb4HoAPIUOENA+aPCVHqcV09wvPnnQdo601rC" +
            "hKD0VAVVf7G+fDVZyBW97avFl3+1x1q6r972tjqcRzol2gJTan49ZUmNJlTqd6RRW9UXUIhd0td2bBNEyKgEBCGh5biKkhq4" +
            "aVjtYJZbxZcbmezqE1mTKfG3/QH6jY4I9fa2mp+/TDur411KNR0F5lXY4aDxTGvT6+1E9SQ16bC9j7u+z2LXolQvyUkx87U4" +
            "VKQocQUpOUi4HVumvZcchQ==";

        private const string S7Data =
            "o4pLlRlA4IVn9Ssz/78tZpNWxs9vf40qEsmbq6EIYN4f2igg57wnAhYpZE0Aw/oYgovxUBHLsixc5zKy8pU2S94Hh7L2vF+g" +
            "6YFBzQwhUOG98U7igcNosYnn5P3YsHlcQ/2LHgFQSU1BQ744He48kT+cp5K+ZpcI9K3uus++hhIZy+q2AMJgJuS9ZXV6HyRk" +
            "qdxIgmats8OGYBMoqN/YC/IcbTW+iXcQzumys4+qAgUeNcALKvVrFoL/EusRaUjjFnVN0/86e04ZAYUZN+D2nIOsgUnOZkIz" +
            "t0GTjMBU2NCIbDrLKSi8Rze6JUcr0mqmHh/Wevq6XAwH8TdEYpnnthbY0kKIEpYKbsCl4WeedBMaCPxy9znRsUU3WPlY3xnP" +
            "VvfDvjC6bsAkGyEHKYjCRSIBEyARxY686Ua8OBT65sZKWOi6RrxOrYtQj0ZfQyl4Oxgk8Z+6HYL0D/avbU4s6mSS4xaLSlSS" +
            "w0+bAO2Mpqt4b8mamrelBm5uhbKpPOwaiIaDvukECA5WvvFVOzbl513yobOFu973PAP+YTNidBYoTAM8dAxt2mzFqnmt4eQ8" +
            "AsjwUVrz+JifpCYWKSvY7uMvOB2auU8MeFcyu3vZxj6ppnduXItly8cwUtSLQNErtz7AYHiNBrn0VDejfcgw9AITp8gyjG25" +
            "vufU6y2di74G+3l5CFMi53fPdYukje8RWMiD4G94a42mF2NaoPdc+jMA2l2wv47yEMO59YDC6qB6drkIsNLZoxdC03mNcRoC" +
            "ajPGmmD9ESfjUIBDqAiZBsTtfz3vK22CdoTrTiXPjUhm1ck2QU7nKMoKYcLPqUk937njuuaNX7Zkr66S5tXHOgkFqJ59ASvy" +
            "cD8XpMMWHt351+AVh7ixUNVPnyuCulpiYnkBapwbwC6pikgVQOcW1yxaBUAimtKTmr8t47lFhwUe3FM0bimZ1m//bEmGSZ8c" +
            "B+3i39FCcriuft4ZGlY+BYxvrRUcbGJmTMJUcSorCOo5KeuT8LDcF67y1Fj7lKKeTFbPUmb+g5iBBcQuw1M5di5p1gEIwaDT" +
            "Dhbnoabf8uSF0j5pmEaQdN0OK0xWdnVPeDM5XU8jMqFdHDI9lOH1wwGTJksvAp/Hfn6ZPASVT169+/o/Dq33dvSTZilvzh89" +
            "vkUexjSrtdO3+Sv3wDQEG2e1ck49o5JVAZMitX+o0s9nt65gazgUGD3DvDB9wKA48gYW/ZtRY8OQ051Y5vh5VEfWuBypYf2X" +
            "9Fl36p1TVy3PWJpWrWNO6HgbLkZ++IBlFHmB8/RV2pHzMKJANY+Y0dIY47a8UPo/IfBAPa69wMNMwlhJsjaPUXDTsYSDzu0P" +
            "2tqNh8d5ovLoG+CUS29xkA==";

        private const string S8Data =
            "nvWL6g0wFuL8/927vdrrp5WAZDW3+Il3GxLB5gAWJA616CwFsM+pEREvleUKmefsdNGGkxyTQioRgeN2Ou8tsfzd3Tex3pre" +
            "LMMMCilwGb5ACaCEDzoku8830bTweU60/e2eBF2hFQtoMQ1IWt67i0LtnWYx6OzH55WPPxsZ33INM4B1UUIHlPrNfVxjbb6r" +
            "ZCFAqgrUAbPK0ecCrh1XU6KCMXrs3agSXTOq/ehDbxfURvtxIpASONSalM6taUe4YthbllXQ84Jnl/tmTgu4FaBHWx1v4P1M" +
            "uMSOwm5y6Ff8eHpkRF2GmZPVi2ADDiBs9l/cOaMAC13yr2OuMtaLfgyMEHBJUNO7BN+YKSr0DJiR9G2bU91+nkiFkQYHfstY" +
            "Lu90O7H/L1LMCEfSzSd+HFsh66Ti0vE8OHq0GRkBhRk5YIU1594XneY16yd79q/JuPW6Ns1nxAmxEInBe78d4fgazQYIxnBx" +
            "VDNeLVpJ3tQG0MZkLMbAvLMN0D00j49wQhvVdw9iTya/0rgknrfBFWQlpUZO5df4YIE3PqXNlXilFZyFiJdF5l/He8MMugfb" +
            "q6N2Bh6bIn97LoQx158lJHL0vvi4/F+D8sH0bZWx9Zb88Ar9TBP+sD1tUOLqEptPJfIV8m9zI6IoxLSfeUnQJfgTxzSHgWHE" +
            "mG566vxu0XxshzYUB0FU8RTu3r4nr+lWQaRKoJnI9zzmuuySbQFn3euCFhXf7kKoIgETIHV7kPEPA+MgnsLYJDtnOeG4P6bv" +
            "VDCHcTvP8rZCZDKfzKQVywRFGrCNfeTx5RtKhNzf57pw2stCCq59zXpb6Ff2Wj/VjE3PICjUpM6kMNF5++uGNNzN0zNTO4V3" +
            "tfzvN3iHBsXms4Dl9LhoTn6zyMWinoANfOuPOZRPKhMOlbdDHH3uL70TNiKiygbdK5PfN4mCJMTD6/Ost/YVV914NO9vYWfy" +
            "5MtIwV6BUpCrD0FeZSSKtKR/2i7kQHvohKCO6eHpiVj8kNPvW9MH3ZRWSNuy5dc4AQFyV7zeDnMTMWRbT36RlLovPFCCEm9k" +
            "StIjdZWWd+CPesH5ISFbepa4h9FNOiYp3wxRup989IHtYxGtZVl76m5yABqSMEARd23aAGHdDEoDRh+tsN9bYGTD7Z6o5usi" +
            "itLnzqA256C5pmRVCTKFEDeP68fKBectD1dRiSuCCd9sGmm98uQSqg8cRYd6ovbgGUjaOk928UwrHHcNVrHNZ4SDDTUP+jhZ" +
            "8545Qgd7mTY9CYQOYT6pSnvYYIMMi6kfLDhJEaUldum30RQGSyQlDkeDdgyCjZ5Y0VkgDR67ZqSCCtr4MJHxBMBObrpkUSaZ" +
            "DSPnHoCtslABaO7qg6KyjQ==";
    }
}
