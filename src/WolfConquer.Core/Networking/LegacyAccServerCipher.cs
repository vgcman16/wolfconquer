using System.Buffers.Binary;

namespace WolfConquer.Core.Networking;

public sealed class LegacyAccServerCipher
{
    private const byte PacketXor = 0xAB;
    private const int KeyLength = 256;

    private static readonly byte[] BaseKeyLow;
    private static readonly byte[] BaseKeyHigh;
    private static readonly byte[] PasswordKey1 =
    [
        20, 87, 129, 96, 229, 235, 151, 20, 169, 166, 88, 219, 93, 86, 200, 80,
        163, 238, 127, 46, 10, 56, 158, 187, 64, 26, 88, 207, 239, 167, 133, 88,
    ];
    private static readonly byte[] PasswordKey2 =
    [
        88, 254, 246, 238, 126, 236, 64, 156, 140, 89, 94, 28, 108, 196, 28, 62,
        23, 151, 92, 192, 64, 253, 224, 74, 138, 224, 88, 171, 233, 26, 227, 189,
    ];

    private readonly byte[] _generatedDecryptKeyLow = new byte[KeyLength];
    private readonly byte[] _generatedDecryptKeyHigh = new byte[KeyLength];
    private ushort _decryptCounter;
    private ushort _encryptCounter;
    private bool _useGeneratedDecryptKeys;

    static LegacyAccServerCipher()
    {
        BaseKeyLow = new byte[KeyLength];
        BaseKeyHigh = new byte[KeyLength];

        byte low = 157;
        byte high = 98;
        for (int i = 0; i < KeyLength; i++)
        {
            BaseKeyLow[i] = low;
            BaseKeyHigh[i] = high;
            low = unchecked((byte)((15 + (byte)(low * 250)) * low + 19));
            high = unchecked((byte)((121 - (byte)(high * 92)) * high + 109));
        }
    }

    public ushort DecryptCounter => _decryptCounter;

    public ushort EncryptCounter => _encryptCounter;

    public void Encrypt(Span<byte> buffer)
    {
        ApplyPacketTransform(buffer, ref _encryptCounter, BaseKeyLow, BaseKeyHigh);
    }

    public void DecryptFromServer(Span<byte> buffer)
    {
        ApplyPacketInverseTransform(buffer, ref _encryptCounter, BaseKeyLow, BaseKeyHigh);
    }

    public void Decrypt(Span<byte> buffer)
    {
        byte[] keyLow = _useGeneratedDecryptKeys ? _generatedDecryptKeyLow : BaseKeyLow;
        byte[] keyHigh = _useGeneratedDecryptKeys ? _generatedDecryptKeyHigh : BaseKeyHigh;
        ApplyPacketTransform(buffer, ref _decryptCounter, keyLow, keyHigh);
    }

    public void EncryptForServer(Span<byte> buffer)
    {
        byte[] keyLow = _useGeneratedDecryptKeys ? _generatedDecryptKeyLow : BaseKeyLow;
        byte[] keyHigh = _useGeneratedDecryptKeys ? _generatedDecryptKeyHigh : BaseKeyHigh;
        ApplyPacketInverseTransform(buffer, ref _decryptCounter, keyLow, keyHigh);
    }

    public void ResetCounters(ushort encryptCounter = 0, ushort decryptCounter = 0)
    {
        _encryptCounter = encryptCounter;
        _decryptCounter = decryptCounter;
    }

    public void UseBaseDecryptKeys()
    {
        _useGeneratedDecryptKeys = false;
    }

    public void GenerateDecryptKeys(uint cryptoKey, uint accountId)
    {
        uint key = unchecked((cryptoKey + accountId) ^ 0x4321u ^ cryptoKey);
        uint squaredKey = unchecked(key * key);

        for (int i = 0; i < KeyLength; i++)
        {
            int leftShift = (3 - (i % 4)) * 8;
            int rightShift = (i % 4 * 8) + leftShift;
            _generatedDecryptKeyLow[i] = unchecked((byte)(BaseKeyLow[i] ^ (key << leftShift >> rightShift)));
            _generatedDecryptKeyHigh[i] = unchecked((byte)(BaseKeyHigh[i] ^ (squaredKey << leftShift >> rightShift)));
        }

        _useGeneratedDecryptKeys = true;
    }

    public void GenerateDecryptKeys(ReadOnlySpan<byte> inputKey1, ReadOnlySpan<byte> inputKey2)
    {
        if (inputKey1.Length < 4)
        {
            throw new ArgumentException("Input key 1 must contain at least four bytes.", nameof(inputKey1));
        }

        if (inputKey2.Length < 4)
        {
            throw new ArgumentException("Input key 2 must contain at least four bytes.", nameof(inputKey2));
        }

        Span<byte> key1 = stackalloc byte[4];
        Span<byte> key2 = stackalloc byte[4];
        Span<byte> work1 = stackalloc byte[4];
        Span<byte> work2 = stackalloc byte[4];

        for (int i = 0; i < 4; i++)
        {
            key1[i] = inputKey1[3 - i];
            key2[i] = inputKey2[3 - i];
        }

        uint combined = unchecked(ReadLegacyUInt32(key1) + ReadLegacyUInt32(key2));
        WriteLegacyUInt32Bytes(combined, work1);
        Reverse4(work1, work2);

        work2[2] ^= 0x43;
        work2[3] ^= 0x21;
        for (int i = 0; i < 4; i++)
        {
            work2[i] ^= inputKey1[i];
        }

        for (int i = 0; i < KeyLength; i++)
        {
            _generatedDecryptKeyLow[i] = (byte)(work2[3 - i % 4] ^ BaseKeyLow[i]);
        }

        Reverse4(work2, work1);
        uint squared = unchecked(ReadLegacyUInt32(work1) * ReadLegacyUInt32(work1));
        WriteLegacyUInt32Bytes(squared, work1);
        Reverse4(work1, work2);

        for (int i = 0; i < KeyLength; i++)
        {
            _generatedDecryptKeyHigh[i] = (byte)(work2[3 - i % 4] ^ BaseKeyHigh[i]);
        }

        _useGeneratedDecryptKeys = true;
    }

    public static void DecryptPasswordInPlace(Span<byte> passwordBytes)
    {
        int length = passwordBytes.Length;
        if (length > PasswordKey1.Length)
        {
            throw new ArgumentException("Legacy account passwords cannot exceed 32 encrypted bytes.", nameof(passwordBytes));
        }

        for (int i = 0; i < length; i++)
        {
            passwordBytes[i] ^= PasswordKey1[i % length];
            passwordBytes[i] ^= PasswordKey1[i * 24 % 32 % length];
            passwordBytes[i] ^= PasswordKey2[i * 48 % 64 % length];
        }
    }

    private static void ApplyPacketTransform(
        Span<byte> buffer,
        ref ushort counter,
        ReadOnlySpan<byte> keyLow,
        ReadOnlySpan<byte> keyHigh)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            int counterLow = counter & 0xFF;
            int counterHigh = counter >> 8;
            byte value = (byte)(buffer[i] ^ PacketXor);
            value = NibbleSwap(value);
            value ^= (byte)(keyLow[counterLow] ^ keyHigh[counterHigh]);
            buffer[i] = value;
            counter++;
        }
    }

    private static void ApplyPacketInverseTransform(
        Span<byte> buffer,
        ref ushort counter,
        ReadOnlySpan<byte> keyLow,
        ReadOnlySpan<byte> keyHigh)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            int counterLow = counter & 0xFF;
            int counterHigh = counter >> 8;
            byte key = (byte)(keyLow[counterLow] ^ keyHigh[counterHigh]);
            buffer[i] = (byte)(NibbleSwap((byte)(buffer[i] ^ key)) ^ PacketXor);
            counter++;
        }
    }

    private static byte NibbleSwap(byte value)
    {
        return (byte)((value >> 4) | (value << 4));
    }

    private static uint ReadLegacyUInt32(ReadOnlySpan<byte> value)
    {
        return (uint)((value[3] << 24) | (value[2] << 16) | (value[1] << 8) | value[0]);
    }

    private static void WriteLegacyUInt32Bytes(uint value, Span<byte> destination)
    {
        destination[0] = (byte)(value & 0xFF);
        destination[1] = (byte)((value >> 8) & 0xFF);
        destination[2] = (byte)((value >> 16) & 0xFF);
        destination[3] = (byte)((value >> 24) & 0xFF);
    }

    private static void Reverse4(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        destination[0] = source[3];
        destination[1] = source[2];
        destination[2] = source[1];
        destination[3] = source[0];
    }
}

public sealed class LegacyAccServerRivestCipher5
{
    private const int KeySize = 4;
    private const int SeedSize = 16;
    private const int SubstitutionSize = 26;
    private const int Rounds = 12;

    private static ReadOnlySpan<byte> DefaultInitializationVector =>
    [
        60, 220, 254, 232, 196, 84, 214, 126, 22, 166, 248, 26, 232, 208, 56, 190,
    ];

    private readonly uint[] _keyBuffer = new uint[KeySize];
    private readonly uint[] _substitutionBuffer = new uint[SubstitutionSize];

    public LegacyAccServerRivestCipher5()
    {
        GenerateKeys(DefaultInitializationVector);
    }

    public LegacyAccServerRivestCipher5(int seed)
    {
        Span<byte> initializationVector = stackalloc byte[SeedSize];
        GenerateSeed(seed, initializationVector);
        GenerateKeys(initializationVector);
    }

    public void GenerateKeys(ReadOnlySpan<byte> initializationVector)
    {
        if (initializationVector.Length != SeedSize)
        {
            throw new ArgumentException("The RC5 initialization vector must be exactly 16 bytes.", nameof(initializationVector));
        }

        for (int i = 0; i < KeySize; i++)
        {
            _keyBuffer[i] = BinaryPrimitives.ReadUInt32LittleEndian(initializationVector[(i * sizeof(uint))..]);
        }

        _substitutionBuffer[0] = 3_084_996_963u;
        for (int i = 1; i < SubstitutionSize; i++)
        {
            _substitutionBuffer[i] = unchecked(_substitutionBuffer[i - 1] + 2_654_435_769u);
        }

        uint previousSubstitution = 0;
        uint previousKey = 0;
        int substitutionIndex = 0;
        int keyIndex = 0;
        for (int i = 0; i < 78; i++)
        {
            _substitutionBuffer[substitutionIndex] = RotateLeft(
                unchecked(_substitutionBuffer[substitutionIndex] + previousSubstitution + previousKey),
                3);
            previousSubstitution = _substitutionBuffer[substitutionIndex];
            substitutionIndex = (substitutionIndex + 1) % SubstitutionSize;

            _keyBuffer[keyIndex] = RotateLeft(
                unchecked(_keyBuffer[keyIndex] + previousSubstitution + previousKey),
                (int)unchecked(previousSubstitution + previousKey));
            previousKey = _keyBuffer[keyIndex];
            keyIndex = (keyIndex + 1) % KeySize;
        }
    }

    public bool Encrypt(Span<byte> input)
    {
        if (input.Length % 8 != 0)
        {
            return false;
        }

        int blockCount = input.Length / 8;
        for (int i = 0; i < blockCount; i++)
        {
            Span<byte> block = input.Slice(i * 8, 8);
            uint left = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(block) + _substitutionBuffer[0]);
            uint right = unchecked(BinaryPrimitives.ReadUInt32LittleEndian(block[4..]) + _substitutionBuffer[1]);

            for (int round = 1; round <= Rounds; round++)
            {
                left = unchecked(RotateLeft(left ^ right, (int)right) + _substitutionBuffer[2 * round]);
                right = unchecked(RotateLeft(right ^ left, (int)left) + _substitutionBuffer[(2 * round) + 1]);
            }

            BinaryPrimitives.WriteUInt32LittleEndian(block, left);
            BinaryPrimitives.WriteUInt32LittleEndian(block[4..], right);
        }

        return true;
    }

    public bool Decrypt(Span<byte> input)
    {
        if (input.Length % 8 != 0)
        {
            return false;
        }

        int blockCount = input.Length / 8;
        for (int i = 0; i < blockCount; i++)
        {
            Span<byte> block = input.Slice(i * 8, 8);
            uint left = BinaryPrimitives.ReadUInt32LittleEndian(block);
            uint right = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);

            for (int round = Rounds; round > 0; round--)
            {
                right = RotateRight(unchecked(right - _substitutionBuffer[(2 * round) + 1]), (int)left) ^ left;
                left = RotateRight(unchecked(left - _substitutionBuffer[2 * round]), (int)right) ^ right;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(block, unchecked(left - _substitutionBuffer[0]));
            BinaryPrimitives.WriteUInt32LittleEndian(block[4..], unchecked(right - _substitutionBuffer[1]));
        }

        return true;
    }

    public static void GenerateSeed(int seed, Span<byte> destination)
    {
        if (destination.Length < SeedSize)
        {
            throw new ArgumentException("The seed destination must contain at least 16 bytes.", nameof(destination));
        }

        for (int i = 0; i < SeedSize; i++)
        {
            seed = unchecked((seed * 214_013) + 2_531_011);
            destination[i] = (byte)((seed >> 16) & 0x7FFF);
        }
    }

    public static uint RotateLeft(uint value, int count)
    {
        count %= 32;
        return (value << count) | (value >> (32 - count));
    }

    public static uint RotateRight(uint value, int count)
    {
        count %= 32;
        return (value >> count) | (value << (32 - count));
    }
}
