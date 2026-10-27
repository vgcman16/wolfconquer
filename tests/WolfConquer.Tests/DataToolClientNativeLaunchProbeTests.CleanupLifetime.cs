namespace WolfConquer.Tests;

public sealed partial class DataToolClientNativeLaunchProbeTests
{
    [Theory]
    [InlineData(-1, null, false)]
    [InlineData(0, null, true)]
    [InlineData(100, null, true)]
    [InlineData(101, null, false)]
    [InlineData(51, 50, false)]
    [InlineData(50, 50, true)]
    [InlineData(0, -1, false)]
    public void NativeCleanupCreationMustBelongToTheExactParentLifetimeAndSnapshot(int childOffset, int? exitOffset, bool accepted)
    {
        var parentBorn = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        DateTime? parentExit = exitOffset is int value ? parentBorn.AddMilliseconds(value) : null;
        Assert.Equal(accepted, global::Program.IsClientNativeCleanupCreationWithinLifetime(
            parentBorn.AddMilliseconds(childOffset), parentBorn, parentExit, parentBorn.AddMilliseconds(100)));
    }

    [Fact]
    public void NativeCleanupDoesNotCompareUnspecifiedClockDataAsAnOwnershipWitness()
    {
        var value = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(global::Program.IsClientNativeCleanupCreationWithinLifetime(
            DateTime.SpecifyKind(value, DateTimeKind.Unspecified), value, null, value));
    }
}
