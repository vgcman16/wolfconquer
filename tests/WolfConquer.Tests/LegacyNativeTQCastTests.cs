using System.Reflection;
using WolfConquer.Core.Networking;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed class LegacyNativeTQCastTests
{
    private readonly ITestOutputHelper output;
    public LegacyNativeTQCastTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void ImplementationRetainsNativeVirtualMetadataAndRoundConstants()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        Assert.Equal("CAST5", implementation.AlgorithmName);
        Assert.False(implementation.IsPartialBlockOkay);
        Assert.Equal(8, implementation.GetBlockSize());
        Assert.Equal(16, LegacyNativeTQCast5Impl.MAX_ROUNDS);
        Assert.Equal(12, LegacyNativeTQCast5Impl.RED_ROUNDS);
        Assert.False(typeof(LegacyNativeTQCast5Impl).IsSealed);
        foreach (var name in new[] { "ProcessBlock", "Reset", "GetBlockSize", "SetKey", "EncryptBlock", "DecryptBlock" })
            Assert.True(typeof(LegacyNativeTQCast5Impl).GetMethod(name)!.IsVirtual);
    }

    [Fact]
    public void NewImplementationKeepsSeventeenZeroKeysAndDefaultDecryptMode()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        Assert.Equal(16, Field<int>(implementation, "_rounds"));
        Assert.False(Field<bool>(implementation, "_encrypting"));
        Assert.Equal(17, Field<int[]>(implementation, "_Kr").Length);
        Assert.Equal(17, Field<uint[]>(implementation, "_Km").Length);
        Assert.All(Field<int[]>(implementation, "_Kr"), value => Assert.Equal(0, value));
        Assert.All(Field<uint[]>(implementation, "_Km"), value => Assert.Equal(0u, value));
    }

    [Fact]
    public void ShortKeyBoundarySelectsTwelveRoundsWithoutRejectingEmptyKeys()
    {
        foreach (var length in new[] { 0, 1, 10 })
        {
            var implementation = new LegacyNativeTQCast5Impl();
            implementation.SetKey(Bytes(length));
            Assert.Equal(12, Field<int>(implementation, "_rounds"));
            Assert.Equal(0u, Field<uint[]>(implementation, "_Km")[0]);
            Assert.Equal(0, Field<int[]>(implementation, "_Kr")[0]);
        }
    }

    [Fact]
    public void ElevenAndSixteenByteFreshKeysRetainSixteenRounds()
    {
        foreach (var length in new[] { 11, 16 })
        {
            var implementation = new LegacyNativeTQCast5Impl();
            implementation.SetKey(Bytes(length));
            Assert.Equal(16, Field<int>(implementation, "_rounds"));
            Assert.All(Field<int[]>(implementation, "_Kr"), value => Assert.InRange(value, 0, 31));
        }
    }

    [Fact]
    public void LongRekeyDoesNotRepairTheNativeStickyReducedRoundState()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        implementation.SetKey(Bytes(10));
        implementation.SetKey(Bytes(16));
        Assert.Equal(12, Field<int>(implementation, "_rounds"));
    }

    [Fact]
    public void NullAndOverlongKeysKeepFaultBeforeScheduleWrites()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        implementation.SetKey(Bytes(16));
        var masks = Field<uint[]>(implementation, "_Km");
        var rotates = Field<int[]>(implementation, "_Kr");
        var oldMasks = masks.ToArray();
        var oldRotates = rotates.ToArray();
        Assert.Throws<NullReferenceException>(() => implementation.SetKey(null!));
        Assert.Throws<IndexOutOfRangeException>(() => implementation.SetKey(Bytes(17)));
        Assert.Same(masks, Field<uint[]>(implementation, "_Km"));
        Assert.Same(rotates, Field<int[]>(implementation, "_Kr"));
        Assert.Equal(oldMasks, masks);
        Assert.Equal(oldRotates, rotates);
        Assert.Equal(16, Field<int>(implementation, "_rounds"));
    }

    [Fact]
    public void ImplementationResetIsTheNativeNoOpForKeysRoundsAndMode()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        implementation.SetKey(Bytes(5));
        implementation.Init(true);
        var masks = Field<uint[]>(implementation, "_Km");
        var rotates = Field<int[]>(implementation, "_Kr");
        var oldMasks = masks.ToArray();
        implementation.Reset();
        Assert.Same(masks, Field<uint[]>(implementation, "_Km"));
        Assert.Same(rotates, Field<int[]>(implementation, "_Kr"));
        Assert.Equal(oldMasks, masks);
        Assert.Equal(12, Field<int>(implementation, "_rounds"));
        Assert.True(Field<bool>(implementation, "_encrypting"));
    }

    [Fact]
    public void ManagedBlockDirectionsRoundTripWithRealKeySchedule()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        implementation.SetKey(Bytes(16));
        var source = Bytes(8);
        var encrypted = new byte[8];
        var recovered = new byte[8];
        implementation.Init(true);
        Assert.Equal(8, implementation.ProcessBlock(source, 0, encrypted, 0));
        implementation.Init(false);
        Assert.Equal(8, implementation.ProcessBlock(encrypted, 0, recovered, 0));
        Assert.Equal(source, recovered);
        var direct = new byte[8];
        implementation.DecryptBlock(encrypted, 0, direct, 0);
        Assert.Equal(recovered, direct);
    }

    [Fact]
    public void ProcessBlockPreservesInputThenOutputValidationAndFaultTypes()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        Assert.Equal("Input buffer too short", Assert.Throws<Exception>(() => implementation.ProcessBlock(new byte[7], 0, null!, 0)).Message);
        Assert.Equal("Output buffer too short", Assert.Throws<Exception>(() => implementation.ProcessBlock(new byte[8], 0, new byte[7], 0)).Message);
        Assert.Throws<NullReferenceException>(() => implementation.ProcessBlock(null!, 0, new byte[8], 0));
        Assert.Throws<NullReferenceException>(() => implementation.ProcessBlock(new byte[8], 0, null!, 0));
        Assert.Throws<IndexOutOfRangeException>(() => implementation.ProcessBlock(new byte[8], -1, new byte[8], 0));
        Assert.Throws<IndexOutOfRangeException>(() => implementation.ProcessBlock(new byte[8], int.MaxValue, new byte[8], 0));
    }

    [Fact]
    public void DirectBlocksReadBothWordsBeforeOverlappingOutputWrites()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        implementation.SetKey(Bytes(16));
        var source = Bytes(16);
        var expected = new byte[8];
        implementation.EncryptBlock(source, 0, expected, 0);
        var overlap = source.ToArray();
        Assert.Equal(8, implementation.EncryptBlock(overlap, 0, overlap, 2));
        Assert.Equal(expected, overlap.AsSpan(2, 8).ToArray());
        var encrypted = expected.Concat(new byte[8]).ToArray();
        Assert.Equal(8, implementation.DecryptBlock(encrypted, 0, encrypted, 1));
        Assert.Equal(source.AsSpan(0, 8).ToArray(), encrypted.AsSpan(1, 8).ToArray());
    }

    [Fact]
    public void DirectShortOutputKeepsItsManagedPrefixBeforeTheFault()
    {
        var implementation = new LegacyNativeTQCast5Impl();
        implementation.SetKey(Bytes(16));
        var source = Bytes(8);
        var complete = new byte[8];
        implementation.EncryptBlock(source, 0, complete, 0);
        var partial = new byte[3];
        Assert.Throws<IndexOutOfRangeException>(() => implementation.EncryptBlock(source, 0, partial, 0));
        Assert.Equal(complete.AsSpan(0, 3).ToArray(), partial);
    }

    [Fact]
    public void IntegerPackingKeepsMasksAndNativeWriteOrder()
    {
        Assert.Equal(0xFF00FF34u, LegacyNativeTQCast5Impl.IntsTo32bits(new[] { -1, 256, 511, 0x1234 }, 0));
        var values = new int[4];
        LegacyNativeTQCast5Impl.Bits32ToInts(0x89ABCDEFu, values, 0);
        Assert.Equal(new[] { 0x89, 0xAB, 0xCD, 0xEF }, values);
        var shortValues = new[] { 5, 6 };
        Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeTQCast5Impl.Bits32ToInts(1, shortValues, 0));
        Assert.Equal(new[] { 5, 6 }, shortValues);
    }

    [Fact]
    public void WrapperConstructionKeepsNativeSixteenByteAndEightIntState()
    {
        var cipher = new LegacyNativeTQCast5();
        Assert.False(typeof(LegacyNativeTQCast5).IsSealed);
        Assert.Equal(16, Field<byte[]>(cipher, "EncIvec").Length);
        Assert.Equal(16, Field<byte[]>(cipher, "DecIvec").Length);
        Assert.Equal(8, Field<int[]>(cipher, "EncNum").Length);
        Assert.Equal(8, Field<int[]>(cipher, "DecNum").Length);
        Assert.NotSame(Field<byte[]>(cipher, "EncIvec"), Field<byte[]>(cipher, "DecIvec"));
        Assert.NotSame(Field<int[]>(cipher, "EncNum"), Field<int[]>(cipher, "DecNum"));
        Assert.All(Field<byte[]>(cipher, "EncIvec"), value => Assert.Equal((byte)0, value));
        Assert.Equal(16, Field<int>(Field<LegacyNativeTQCast5Impl>(cipher, "Impl"), "_rounds"));
    }

    [Fact]
    public void WrapperGenerateKeyCopiesOnlyTheFirstSixteenBytesWithoutReset()
    {
        var cipher = new LegacyNativeTQCast5();
        var iv = Field<byte[]>(cipher, "EncIvec");
        var count = Field<int[]>(cipher, "EncNum");
        var implementation = Field<LegacyNativeTQCast5Impl>(cipher, "Impl");
        cipher.GenerateKey(Bytes(24));
        var control = new LegacyNativeTQCast5Impl();
        control.SetKey(Bytes(16));
        Assert.Equal(Field<uint[]>(control, "_Km"), Field<uint[]>(implementation, "_Km"));
        Assert.Equal(Field<int[]>(control, "_Kr"), Field<int[]>(implementation, "_Kr"));
        Assert.Same(iv, Field<byte[]>(cipher, "EncIvec"));
        Assert.Same(count, Field<int[]>(cipher, "EncNum"));
    }

    [Fact]
    public void WrapperNullAndShortKeyFaultsDoNotEnterTheImplementationSchedule()
    {
        var cipher = new LegacyNativeTQCast5();
        cipher.GenerateKey(Bytes(16));
        var implementation = Field<LegacyNativeTQCast5Impl>(cipher, "Impl");
        var masks = Field<uint[]>(implementation, "_Km").ToArray();
        Assert.Throws<NullReferenceException>(() => cipher.GenerateKey(null!));
        Assert.Throws<IndexOutOfRangeException>(() => cipher.GenerateKey(Bytes(15)));
        Assert.Equal(masks, Field<uint[]>(implementation, "_Km"));
    }

    [Fact]
    public void WrapperResetReplacesAllFeedbackStorageWithoutReplacingTheImplementation()
    {
        var cipher = new LegacyNativeTQCast5();
        var implementation = Field<LegacyNativeTQCast5Impl>(cipher, "Impl");
        var encIv = Field<byte[]>(cipher, "EncIvec");
        var decIv = Field<byte[]>(cipher, "DecIvec");
        var encCount = Field<int[]>(cipher, "EncNum");
        var decCount = Field<int[]>(cipher, "DecNum");
        encIv[0] = 7;
        decIv[0] = 9;
        encCount[0] = 2;
        decCount[0] = 3;
        cipher.Reset();
        Assert.Same(implementation, Field<LegacyNativeTQCast5Impl>(cipher, "Impl"));
        Assert.NotSame(encIv, Field<byte[]>(cipher, "EncIvec"));
        Assert.NotSame(decIv, Field<byte[]>(cipher, "DecIvec"));
        Assert.NotSame(encCount, Field<int[]>(cipher, "EncNum"));
        Assert.NotSame(decCount, Field<int[]>(cipher, "DecNum"));
        Assert.Equal((byte)0, Field<byte[]>(cipher, "EncIvec")[0]);
        Assert.Equal(0, Field<int[]>(cipher, "EncNum")[0]);
    }

    [Fact]
    public void NonPositiveWrapperLengthsSkipInputOutputAccessAndKeepIndices()
    {
        var cipher = new LegacyNativeTQCast5();
        var enc = Field<int[]>(cipher, "EncNum");
        var dec = Field<int[]>(cipher, "DecNum");
        enc[0] = 7;
        dec[0] = 6;
        CallNonPositiveLengths(cipher);
        Assert.Equal(7, enc[0]);
        Assert.Equal(6, dec[0]);
    }

    [Fact]
    public void FreshChildKeepsIgnoredEncryptOffsetAndRealDecryptOffset()
    {
        var result = LegacyNativeTQCastChildFixture.Run("offsets", output);
        Assert.True(result.GetProperty("Checked").GetBoolean());
        Assert.Equal(13, result.GetProperty("Count").GetInt32());
        Assert.Equal(11, result.GetProperty("IgnoredEncryptOutOffset").GetInt32());
        Assert.Equal(7, result.GetProperty("DecryptOutOffset").GetInt32());
    }

    [Fact]
    public void FreshChildKeepsFeedbackAcrossDifferentBoundedChunkCuts()
    {
        var result = LegacyNativeTQCastChildFixture.Run("streaming", output);
        Assert.True(result.GetProperty("Checked").GetBoolean());
        Assert.Equal(23, result.GetProperty("Total").GetInt32());
        Assert.Equal("7+16", result.GetProperty("EncryptCuts").GetString());
        Assert.Equal("5+18", result.GetProperty("DecryptCuts").GetString());
    }

    [Fact]
    public void FreshChildResetRetainsKeysAndReproducesTheInitialFeedbackStream()
    {
        var result = LegacyNativeTQCastChildFixture.Run("reset", output);
        Assert.True(result.GetProperty("Checked").GetBoolean());
        Assert.True(result.GetProperty("KeyScheduleRetained").GetBoolean());
        Assert.Equal(16, result.GetProperty("NewIvBytes").GetInt32());
    }

    [Fact]
    public void FreshChildRekeyKeepsTheExistingPartialFeedbackBlock()
    {
        var result = LegacyNativeTQCastChildFixture.Run("rekey", output);
        Assert.True(result.GetProperty("Checked").GetBoolean());
        Assert.Equal(5, result.GetProperty("PriorIndex").GetInt32());
        Assert.Equal(0, result.GetProperty("FinalIndex").GetInt32());
    }

    [Fact]
    public void FreshChildDefaultUnkeyedScheduleAndDirectionsRemainIndependent()
    {
        var result = LegacyNativeTQCastChildFixture.Run("unkeyed-directions", output);
        Assert.True(result.GetProperty("Checked").GetBoolean());
        Assert.True(result.GetProperty("NoGenerateKeyCall").GetBoolean());
        Assert.True(result.GetProperty("IndependentDirections").GetBoolean());
    }

    [Fact]
    public void FreshChildManagedArrayFaultsRetainOnlyEarlierFeedbackCommits()
    {
        var result = LegacyNativeTQCastChildFixture.Run("managed-faults", output);
        Assert.True(result.GetProperty("Checked").GetBoolean());
        Assert.Equal(2, result.GetProperty("EncryptCommitted").GetInt32());
        Assert.Equal(2, result.GetProperty("DecryptCommitted").GetInt32());
    }

    private static unsafe void CallNonPositiveLengths(LegacyNativeTQCast5 cipher)
    {
        cipher.Encrypt((byte*)0, int.MaxValue, null!, int.MinValue, 0);
        cipher.Encrypt((byte*)0, int.MinValue, null!, int.MaxValue, -1);
        cipher.Decrypt(null!, int.MaxValue, (byte*)0, int.MinValue, 0);
        cipher.Decrypt(null!, int.MinValue, (byte*)0, int.MaxValue, -1);
    }

    private static byte[] Bytes(int length) => Enumerable.Range(0, length).Select(index => (byte)(index * 7 + 3)).ToArray();
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
}
