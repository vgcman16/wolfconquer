using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

// Every positive-length raw-pointer call stays inside this fresh child and a fixed managed array.
internal static class LegacyNativeTQCastChildScenarios
{
    public static string Execute(string scenario, string guard, string coreHash, string testsHash)
    {
        var thread = Environment.CurrentManagedThreadId;
        object data = scenario switch
        {
            "offsets" => Offsets(),
            "streaming" => Streaming(),
            "reset" => Reset(),
            "rekey" => Rekey(),
            "unkeyed-directions" => UnkeyedDirections(),
            "managed-faults" => ManagedFaults(),
            _ => throw new InvalidOperationException("Unknown bounded CAST scenario."),
        };
        return JsonSerializer.Serialize(new
        {
            Guard = guard, Scenario = scenario, CoreSha256 = coreHash, TestsSha256 = testsHash,
            Completed = true, SameSynchronousThread = thread == Environment.CurrentManagedThreadId,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), Data = data,
        });
    }

    private static object Offsets()
    {
        var plain = Bytes(32, 7);
        var cipher = new LegacyNativeTQCast5();
        cipher.GenerateKey(Bytes(16, 3));
        var encrypted = Encrypt(cipher, plain, 3, 13, 11);
        var input = new byte[17];
        encrypted.CopyTo(input, 2);
        var other = new LegacyNativeTQCast5();
        other.GenerateKey(Bytes(16, 3));
        var recovered = Decrypt(other, input, 2, 13, 7);
        Need(recovered.SequenceEqual(plain.AsSpan(3, 13).ToArray()));
        return new { Case = "offsets", Checked = true, Count = 13, IgnoredEncryptOutOffset = 11,
            DecryptOutOffset = 7, CiphertextHex = Convert.ToHexString(encrypted) };
    }

    private static object Streaming()
    {
        var plain = Bytes(27, 11);
        var whole = Keyed(5);
        var expected = Encrypt(whole, plain, 2, 23);
        var parts = Keyed(5);
        var first = Encrypt(parts, plain, 2, 7);
        var second = Encrypt(parts, plain, 9, 16, 5);
        Need(first.Concat(second).SequenceEqual(expected));
        var receiver = Keyed(5);
        var recovered = Decrypt(receiver, expected, 0, 5, 3)
            .Concat(Decrypt(receiver, expected, 5, 18, 2)).ToArray();
        Need(recovered.SequenceEqual(plain.AsSpan(2, 23).ToArray()));
        Need(Field<int[]>(parts, "EncNum")[0] == 7);
        Need(Field<int[]>(receiver, "DecNum")[0] == 7);
        return new { Case = "streaming", Checked = true, Total = 23, EncryptCuts = "7+16",
            DecryptCuts = "5+18", CiphertextHex = Convert.ToHexString(expected) };
    }

    private static object Reset()
    {
        var cipher = Keyed(13);
        var plain = Bytes(20, 17);
        var initial = Encrypt(cipher, plain, 0, 20);
        var implementation = Field<LegacyNativeTQCast5Impl>(cipher, "Impl");
        var masks = Field<uint[]>(implementation, "_Km");
        var maskCopy = masks.ToArray();
        var oldIv = Field<byte[]>(cipher, "EncIvec");
        var oldNum = Field<int[]>(cipher, "EncNum");
        cipher.Reset();
        Need(ReferenceEquals(implementation, Field<LegacyNativeTQCast5Impl>(cipher, "Impl")));
        Need(ReferenceEquals(masks, Field<uint[]>(implementation, "_Km")) && masks.SequenceEqual(maskCopy));
        Need(!ReferenceEquals(oldIv, Field<byte[]>(cipher, "EncIvec")));
        Need(!ReferenceEquals(oldNum, Field<int[]>(cipher, "EncNum")));
        Need(Field<byte[]>(cipher, "EncIvec").Length == 16 && Field<int[]>(cipher, "EncNum").Length == 8);
        Need(Field<byte[]>(cipher, "EncIvec").All(value => value == 0));
        Need(Field<int[]>(cipher, "EncNum").All(value => value == 0));
        Need(Encrypt(cipher, plain, 0, 20).SequenceEqual(initial));
        return new { Case = "reset", Checked = true, KeyScheduleRetained = true,
            NewIvBytes = 16, NewCounterInts = 8, CiphertextHex = Convert.ToHexString(initial) };
    }

    private static object Rekey()
    {
        var cipher = Keyed(19);
        _ = Encrypt(cipher, Bytes(8, 23), 0, 5);
        var iv = Field<byte[]>(cipher, "EncIvec");
        var oldBytes = iv.ToArray();
        var counter = Field<int[]>(cipher, "EncNum");
        var implementation = Field<LegacyNativeTQCast5Impl>(cipher, "Impl");
        cipher.GenerateKey(Bytes(16, 29));
        Need(ReferenceEquals(iv, Field<byte[]>(cipher, "EncIvec")) && iv.SequenceEqual(oldBytes));
        Need(ReferenceEquals(counter, Field<int[]>(cipher, "EncNum")) && counter[0] == 5);
        Need(ReferenceEquals(implementation, Field<LegacyNativeTQCast5Impl>(cipher, "Impl")));
        var next = Bytes(3, 31);
        var actual = Encrypt(cipher, next, 0, 3);
        var expected = next.Select((value, index) => (byte)(value ^ oldBytes[5 + index])).ToArray();
        Need(actual.SequenceEqual(expected) && counter[0] == 0);
        return new { Case = "rekey", Checked = true, PriorIndex = 5, FinalIndex = 0,
            ExistingFeedbackTailHex = Convert.ToHexString(actual) };
    }

    private static object UnkeyedDirections()
    {
        var cipher = new LegacyNativeTQCast5();
        var plain = Bytes(20, 37);
        var encrypted = Encrypt(cipher, plain, 0, 20);
        Need(Field<int[]>(cipher, "EncNum")[0] == 4 && Field<int[]>(cipher, "DecNum")[0] == 0);
        var recovered = Decrypt(cipher, encrypted, 0, 20, 4);
        Need(recovered.SequenceEqual(plain));
        Need(Field<int[]>(cipher, "EncNum")[0] == 4 && Field<int[]>(cipher, "DecNum")[0] == 4);
        Need(!ReferenceEquals(Field<byte[]>(cipher, "EncIvec"), Field<byte[]>(cipher, "DecIvec")));
        return new { Case = "unkeyed-directions", Checked = true, NoGenerateKeyCall = true,
            IndependentDirections = true, CiphertextHex = Convert.ToHexString(encrypted) };
    }

    private static unsafe object ManagedFaults()
    {
        var cipher = Keyed(41);
        var source = Bytes(8, 43);
        var shortOutput = new byte[2];
        var encryptedFault = false;
        fixed (byte* input = source)
        {
            try { cipher.Encrypt(input, 0, shortOutput, 0, 3); }
            catch (IndexOutOfRangeException) { encryptedFault = true; }
        }
        Need(encryptedFault && Field<int[]>(cipher, "EncNum")[0] == 2);
        var shortInput = Bytes(2, 47);
        var destination = Enumerable.Repeat((byte)0xCC, 8).ToArray();
        var decryptedFault = false;
        fixed (byte* output = destination)
        {
            try { cipher.Decrypt(shortInput, 0, output, 0, 3); }
            catch (IndexOutOfRangeException) { decryptedFault = true; }
        }
        Need(decryptedFault && Field<int[]>(cipher, "DecNum")[0] == 2);
        Need(destination.Skip(2).All(value => value == 0xCC));
        return new { Case = "managed-faults", Checked = true, EncryptCommitted = 2,
            DecryptCommitted = 2, UnmanagedInputCapacity = 8, UnmanagedOutputCapacity = 8 };
    }

    private static unsafe byte[] Encrypt(LegacyNativeTQCast5 cipher, byte[] source, int offset, int count, int ignoredOut = 0)
    {
        Need(offset >= 0 && count > 0 && count <= 23 && offset + count <= source.Length);
        var output = Enumerable.Repeat((byte)0xCC, count + 4).ToArray();
        fixed (byte* input = source) { cipher.Encrypt(input, offset, output, ignoredOut, count); }
        Need(output.Skip(count).All(value => value == 0xCC));
        return output.AsSpan(0, count).ToArray();
    }

    private static unsafe byte[] Decrypt(LegacyNativeTQCast5 cipher, byte[] source, int offset, int count, int outOffset)
    {
        Need(offset >= 0 && count > 0 && count <= 23 && offset + count <= source.Length && outOffset >= 0 && outOffset <= 7);
        var output = Enumerable.Repeat((byte)0xCC, outOffset + count + 4).ToArray();
        fixed (byte* destination = output) { cipher.Decrypt(source, offset, destination, outOffset, count); }
        Need(output.Take(outOffset).All(value => value == 0xCC));
        Need(output.Skip(outOffset + count).All(value => value == 0xCC));
        return output.AsSpan(outOffset, count).ToArray();
    }

    private static LegacyNativeTQCast5 Keyed(int salt)
    {
        var result = new LegacyNativeTQCast5();
        result.GenerateKey(Bytes(16, salt));
        return result;
    }

    private static byte[] Bytes(int length, int salt) => Enumerable.Range(0, length).Select(index => (byte)(index * 7 + salt)).ToArray();
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Need(bool condition) { if (!condition) throw new InvalidOperationException("Bounded native CAST control failed."); }
}
