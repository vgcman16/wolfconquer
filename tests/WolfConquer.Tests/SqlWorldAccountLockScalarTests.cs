using WolfConquer.Core.Data.SqlWorld;

namespace WolfConquer.Tests;

public sealed class SqlWorldAccountLockScalarTests
{
    public static IEnumerable<object[]> BinaryIntegerScalars => new object[][]
    {
        [(sbyte)0, false], [(sbyte)1, true],
        [(byte)0, false], [(byte)1, true],
        [(short)0, false], [(short)1, true],
        [(ushort)0, false], [(ushort)1, true],
        [0, false], [1, true],
        [0U, false], [1U, true],
        [0L, false], [1L, true],
        [0UL, false], [1UL, true],
    };

    [Theory]
    [MemberData(nameof(BinaryIntegerScalars))]
    public void ExactIntegralZeroRefusesAndOneSucceedsAtEveryProviderWidth(object value, bool expected) =>
        Assert.Equal(expected, SqlWorldStateRepository.ReadBinaryLockScalar(value, "invalidLockScalar"));

    [Theory]
    [InlineData("sqlWorldAccountLockAcquisitionInvalid")]
    [InlineData("sqlWorldAccountLockVerificationInvalid")]
    [InlineData("sqlWorldCustodyClaimAcquisitionInvalid")]
    public void NonIntegralAndNonBinaryValuesKeepTheExactExistingFailureReason(string reason)
    {
        object?[] invalid =
        [
            null, DBNull.Value, false, true, "0", "1", "1.0", "", '0', '\0', '\u0001',
            0F, 1F, float.NaN, 0D, 1D, double.PositiveInfinity, 0M, 1M, 1.5M,
            (sbyte)-1, (short)-1, -1, -1L,
            (sbyte)2, (byte)2, (short)2, (ushort)2, 2, 2U, 2L, 2UL,
            long.MinValue, long.MaxValue, uint.MaxValue, ulong.MaxValue,
            LockScalarEnum.Zero, LockScalarEnum.One, (Int128)1, new object(),
        ];
        foreach (var value in invalid)
        {
            var error = Assert.Throws<InvalidOperationException>(() => SqlWorldStateRepository.ReadBinaryLockScalar(value, reason));
            Assert.Equal(reason, error.Message);
        }
    }

    private enum LockScalarEnum { Zero, One }
}
