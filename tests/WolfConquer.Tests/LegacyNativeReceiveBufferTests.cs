using System.Reflection;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

public sealed class LegacyNativeReceiveBufferTests
{
    [Fact]
    public void CompleteOwnerKeepsItsNativeMutableStorageConstantsAndSixMethods()
    {
        var type = typeof(LegacyNativeReceiveBuffer);
        Assert.False(type.IsSealed);
        Assert.False(typeof(IDisposable).IsAssignableFrom(type));
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "HeadSize", "RECV_BUFFER_SIZE", "buffer" }, fields.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal));
        foreach (var name in new[] { "HeadSize", "RECV_BUFFER_SIZE" })
        {
            var field = type.GetField(name)!;
            Assert.True(field.IsPublic && field.IsStatic && field.IsLiteral);
            Assert.Equal(typeof(int), field.FieldType);
            Assert.Equal(16384, Assert.IsType<int>(field.GetRawConstantValue()));
        }
        var buffer = type.GetField("buffer")!;
        Assert.Equal(typeof(byte[]), buffer.FieldType);
        Assert.False(buffer.IsStatic || buffer.IsInitOnly);
        var length = Assert.Single(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("nLen", length.Name); Assert.Equal(typeof(int), length.FieldType);
        Assert.Equal(new[] { "AddLength", "DelLength", "Length", "MaxLength", "ReadHead", "Reset" },
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Empty(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    [Fact]
    public void ConstructorKeepsTheLiteralIntBoolSignatureAndFalseOptionalDefault()
    {
        var ctor = Assert.Single(typeof(LegacyNativeReceiveBuffer).GetConstructors());
        var parameters = ctor.GetParameters();
        Assert.Equal(new[] { typeof(int), typeof(bool) }, parameters.Select(p => p.ParameterType));
        Assert.Equal(new[] { "ReceiveBufferSize", "changesize" }, parameters.Select(p => p.Name));
        Assert.False(parameters[0].IsOptional);
        Assert.True(parameters[1].IsOptional);
        Assert.Equal(false, parameters[1].DefaultValue);
    }

    [Fact]
    public void DefaultSizeBranchIgnoresAllRequestedSizesIncludingNegativeAndHugeValues()
    {
        foreach (var requested in new[] { int.MinValue, -1, 0, 1, 32, int.MaxValue })
        {
            var owner = new LegacyNativeReceiveBuffer(requested);
            Assert.Equal(16384, owner.buffer.Length);
            Assert.Equal(16384, owner.MaxLength());
            Assert.Equal(0, owner.Length());
        }
    }

    [Fact]
    public void ChangedSizeBranchAllocatesExactlyTheSmallRequestedManagedArray()
    {
        foreach (var requested in new[] { 0, 1, 2, 7, 16384 })
        {
            var owner = new LegacyNativeReceiveBuffer(requested, changesize: true);
            Assert.Equal(requested, owner.buffer.Length);
            Assert.Equal(requested, owner.MaxLength());
            Assert.Equal(0, owner.Length());
            Assert.All(owner.buffer, value => Assert.Equal((byte)0, value));
        }
    }

    [Fact]
    public void ChangedNegativeSizesKeepTheActualBclArrayAllocationFaultWithoutARepair()
    {
        Assert.Throws<OverflowException>(() => new LegacyNativeReceiveBuffer(-1, changesize: true));
        Assert.Throws<OverflowException>(() => new LegacyNativeReceiveBuffer(int.MinValue, changesize: true));
    }

    [Fact]
    public void AddAndDeleteLengthKeepNegativeAndBeyondCapacityStateWithoutClamping()
    {
        var owner = new LegacyNativeReceiveBuffer(2, changesize: true);
        owner.AddLength(10); Assert.Equal(10, owner.Length());
        owner.DelLength(3); Assert.Equal(7, owner.Length());
        owner.AddLength(-2); Assert.Equal(5, owner.Length());
        owner.DelLength(-4); Assert.Equal(9, owner.Length());
        Assert.Equal(2, owner.MaxLength());
        owner.DelLength(20); Assert.Equal(-11, owner.Length());
        Assert.Equal(2, owner.MaxLength());
    }

    [Fact]
    public void SourceSignedArithmeticWrapsAtIntBoundariesUnderTheUnchangedDefaultCompileContext()
    {
        var owner = new LegacyNativeReceiveBuffer(2, changesize: true);
        owner.AddLength(int.MaxValue); owner.AddLength(1);
        Assert.Equal(int.MinValue, owner.Length());
        owner.AddLength(-1); Assert.Equal(int.MaxValue, owner.Length());
        owner.Reset(); owner.DelLength(int.MinValue);
        Assert.Equal(int.MinValue, owner.Length());
        owner.DelLength(1); Assert.Equal(int.MaxValue, owner.Length());
    }

    [Fact]
    public void ResetOnlyClearsLogicalLengthAndRetainsTheSameBufferAndEveryByte()
    {
        var owner = new LegacyNativeReceiveBuffer(3, changesize: true);
        var same = owner.buffer;
        same[0] = 17; same[1] = 18; same[2] = 19;
        owner.AddLength(999); owner.Reset();
        Assert.Equal(0, owner.Length()); Assert.Same(same, owner.buffer);
        Assert.Equal(new byte[] { 17, 18, 19 }, owner.buffer);
        Assert.Equal(3, owner.MaxLength());
    }

    [Fact]
    public void PublicBufferReplacementIsDirectAliasedStorageWithoutLengthSynchronization()
    {
        var owner = new LegacyNativeReceiveBuffer(9, changesize: true);
        owner.AddLength(100);
        var replacement = new byte[] { 0x34, 0x12 };
        owner.buffer = replacement;
        Assert.Same(replacement, owner.buffer); Assert.Equal(2, owner.MaxLength());
        Assert.Equal(100, owner.Length());
        replacement[0] = 0x78;
        Assert.Equal(BitConverter.ToUInt16(replacement, 0), owner.ReadHead());
        Assert.Equal((byte)0x78, owner.buffer[0]);
    }

    [Fact]
    public void ReadHeadUsesTheFirstTwoPhysicalBytesRegardlessOfLogicalLengthWithoutConsuming()
    {
        var owner = new LegacyNativeReceiveBuffer(3, changesize: true);
        owner.buffer[0] = 0x34; owner.buffer[1] = 0x12; owner.buffer[2] = 0xAB;
        var expected = BitConverter.IsLittleEndian ? (ushort)0x1234 : (ushort)0x3412;
        Assert.Equal(expected, owner.ReadHead()); Assert.Equal(0, owner.Length());
        owner.AddLength(-7);
        Assert.Equal(expected, owner.ReadHead()); Assert.Equal(-7, owner.Length());
        Assert.Equal(new byte[] { 0x34, 0x12, 0xAB }, owner.buffer);
    }

    [Fact]
    public void ZeroAndOneBytePhysicalBuffersKeepTheActualBitConverterFaults()
    {
        var zero = new LegacyNativeReceiveBuffer(0, changesize: true);
        var one = new LegacyNativeReceiveBuffer(1, changesize: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => zero.ReadHead());
        Assert.Throws<ArgumentException>(() => one.ReadHead());
        Assert.Equal(0, zero.Length()); Assert.Equal(0, one.Length());
        Assert.Empty(zero.buffer); Assert.Single(one.buffer);
    }

    [Fact]
    public void NullPublicBufferKeepsNativeFaultsWhileLengthAndResetRemainIndependent()
    {
        var owner = new LegacyNativeReceiveBuffer(2, changesize: true);
        owner.buffer = null!;
        owner.AddLength(5); Assert.Equal(5, owner.Length());
        Assert.Throws<NullReferenceException>(() => owner.MaxLength());
        Assert.Throws<ArgumentNullException>(() => owner.ReadHead());
        owner.Reset(); Assert.Equal(0, owner.Length()); Assert.Null(owner.buffer);
    }

    [Fact]
    public void SeparateConstructedOwnersHaveIndependentArraysAndLogicalLengths()
    {
        var first = new LegacyNativeReceiveBuffer(2, changesize: true);
        var second = new LegacyNativeReceiveBuffer(2, changesize: true);
        Assert.NotSame(first.buffer, second.buffer);
        first.buffer[0] = 9; first.AddLength(8);
        Assert.Equal((byte)0, second.buffer[0]); Assert.Equal(0, second.Length());
        second.AddLength(-3); Assert.Equal(8, first.Length());
    }
}
