using System.Buffers.Binary;
using System.Numerics;

namespace WolfConquer.Core.Client;

// 3-Way's public-domain round functions originate with Joan Daemen / Wei Dai:
// https://github.com/weidai11/cryptopp/blob/master/3way.cpp
// The fixed key, byte-order adaptation and tail behavior belong to the observed
// client catalog profile; they are not parameters of a general cryptography API.
internal static class ClientItemCatalogThreeWay
{
    private const uint EncryptionWord = 0xDAC0BDDE;
    private const int Rounds = 11;
    private static readonly uint DecryptionWord = DeriveDecryptionWord();

    internal static void Encrypt(ReadOnlySpan<byte> plaintext, Span<byte> ciphertext)
    {
        var first = BinaryPrimitives.ReadUInt32LittleEndian(plaintext);
        var second = BinaryPrimitives.ReadUInt32LittleEndian(plaintext[4..]);
        var third = BinaryPrimitives.ReadUInt32LittleEndian(plaintext[8..]);
        uint roundConstant = 0x0B0B;
        for (var round = 0; round < Rounds; round++)
        {
            first ^= EncryptionWord ^ (roundConstant << 16);
            second ^= EncryptionWord;
            third ^= EncryptionWord ^ roundConstant;
            Theta(ref first, ref second, ref third);
            PiGammaPi(ref first, ref second, ref third);
            roundConstant = NextRoundConstant(roundConstant);
        }

        first ^= EncryptionWord ^ (roundConstant << 16);
        second ^= EncryptionWord;
        third ^= EncryptionWord ^ roundConstant;
        Theta(ref first, ref second, ref third);
        BinaryPrimitives.WriteUInt32LittleEndian(ciphertext, first);
        BinaryPrimitives.WriteUInt32LittleEndian(ciphertext[4..], second);
        BinaryPrimitives.WriteUInt32LittleEndian(ciphertext[8..], third);
    }

    internal static void Decrypt(ReadOnlySpan<byte> ciphertext, Span<byte> plaintext)
    {
        var first = BinaryPrimitives.ReadUInt32BigEndian(ciphertext);
        var second = BinaryPrimitives.ReadUInt32BigEndian(ciphertext[4..]);
        var third = BinaryPrimitives.ReadUInt32BigEndian(ciphertext[8..]);
        uint roundConstant = 0xB1B1;
        Mu(ref first, ref second, ref third);
        for (var round = 0; round < Rounds; round++)
        {
            first ^= DecryptionWord ^ (roundConstant << 16);
            second ^= DecryptionWord;
            third ^= DecryptionWord ^ roundConstant;
            Theta(ref first, ref second, ref third);
            PiGammaPi(ref first, ref second, ref third);
            roundConstant = NextRoundConstant(roundConstant);
        }

        first ^= DecryptionWord ^ (roundConstant << 16);
        second ^= DecryptionWord;
        third ^= DecryptionWord ^ roundConstant;
        Theta(ref first, ref second, ref third);
        Mu(ref first, ref second, ref third);
        BinaryPrimitives.WriteUInt32BigEndian(plaintext, first);
        BinaryPrimitives.WriteUInt32BigEndian(plaintext[4..], second);
        BinaryPrimitives.WriteUInt32BigEndian(plaintext[8..], third);
    }

    private static uint DeriveDecryptionWord()
    {
        var first = EncryptionWord;
        var second = EncryptionWord;
        var third = EncryptionWord;
        Theta(ref first, ref second, ref third);
        Mu(ref first, ref second, ref third);
        first = BinaryPrimitives.ReverseEndianness(first);
        second = BinaryPrimitives.ReverseEndianness(second);
        third = BinaryPrimitives.ReverseEndianness(third);
        // Pin the derived words to the supported fixed transform profile.
        if (first != 0xC5E5239D || second != 0xC5E5239D || third != 0xC5E5239D)
        {
            throw new InvalidOperationException("Fixed client catalog key derivation does not match the native profile.");
        }

        return first;
    }

    private static uint NextRoundConstant(uint value)
    {
        value <<= 1;
        return (value & 0x10000) == 0 ? value : value ^ 0x11011;
    }

    private static void Theta(ref uint first, ref uint second, ref uint third)
    {
        var common = first ^ second ^ third;
        common = BitOperations.RotateLeft(common, 16) ^ BitOperations.RotateLeft(common, 8);
        var left = (first << 24) ^ (third >> 8) ^ (second << 8) ^ (first >> 24);
        var middle = (second << 24) ^ (first >> 8) ^ (third << 8) ^ (second >> 24);
        first ^= common ^ left;
        second ^= common ^ middle;
        third ^= common ^ (left >> 16) ^ (middle << 16);
    }

    private static void PiGammaPi(ref uint first, ref uint second, ref uint third)
    {
        var rotatedLast = BitOperations.RotateLeft(third, 1);
        var rotatedFirst = BitOperations.RotateLeft(first, 22);
        first = BitOperations.RotateLeft(rotatedFirst ^ (second | ~rotatedLast), 1);
        third = BitOperations.RotateLeft(rotatedLast ^ (rotatedFirst | ~second), 22);
        second ^= rotatedLast | ~rotatedFirst;
    }

    private static void Mu(ref uint first, ref uint second, ref uint third)
    {
        var previousFirst = ReverseBitsWithinBytes(first);
        first = ReverseBitsWithinBytes(third);
        second = ReverseBitsWithinBytes(second);
        third = previousFirst;
    }

    private static uint ReverseBitsWithinBytes(uint value)
    {
        value = ((value & 0xAAAAAAAA) >> 1) | ((value & 0x55555555) << 1);
        value = ((value & 0xCCCCCCCC) >> 2) | ((value & 0x33333333) << 2);
        return ((value & 0xF0F0F0F0) >> 4) | ((value & 0x0F0F0F0F) << 4);
    }
}
