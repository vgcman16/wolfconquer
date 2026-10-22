using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityTests
{
    [Fact]
    public void ScalarConstructorPreservesEverySuppliedWidthReferenceAndNullWithoutNormalizing()
    {
        string name = new("Wolf".ToCharArray());
        var row = new LegacyNativeNobility(uint.MaxValue, name, ulong.MaxValue, uint.MaxValue, byte.MaxValue);
        Assert.Equal(uint.MaxValue, row.UID); Assert.Same(name, row.Name);
        Assert.Equal(ulong.MaxValue, row.Donation); Assert.Equal(uint.MaxValue, row.Mesh);
        Assert.Equal(byte.MaxValue, row.Gender); Assert.Equal(-1, row.Position);
        Assert.Null(new LegacyNativeNobility(0, null!, 0, 0, 0).Name);
        row.UID = 1; row.Name = "Changed"; row.Donation = 2; row.Mesh = 3; row.Gender = 4; row.Position = 5;
        Assert.Equal(1u, row.UID); Assert.Equal("Changed", row.Name); Assert.Equal(2UL, row.Donation);
        Assert.Equal(3u, row.Mesh); Assert.Equal((byte)4, row.Gender); Assert.Equal(5, row.Position);
    }

    [Fact]
    public Task ClientConstructorCopiesActualCurrentPlayerIdentityMeshAndBodyGenderButNotDonationOrRank() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var player = scope.Native.Owner.Player;
            string name = new("Source Player".ToCharArray());
            player.UID = uint.MaxValue; player.Name = name; player.Face = 123; player.Body = 1008;
            player.TransformationID = 2; player.NobilityRank = LegacyNativeNobilityRank.King;
            player.Nobility = Row(99, ulong.MaxValue, 0);

            var row = new LegacyNativeNobility(scope.Native.Owner);

            Assert.Equal(uint.MaxValue, row.UID); Assert.Same(name, row.Name);
            Assert.Equal(21_231_008u, row.Mesh); Assert.Equal((byte)0, row.Gender);
            Assert.Equal(0UL, row.Donation); Assert.Equal(-1, row.Position);
            Assert.Equal(LegacyNativeNobilityRank.Serf, row.Rank);
            Assert.NotSame(player.Nobility, row);
            player.UID = 4; player.Name = "Later"; player.Body = 1003;
            Assert.Equal(uint.MaxValue, row.UID); Assert.Same(name, row.Name);
            Assert.Equal(21_231_008u, row.Mesh); Assert.Equal((byte)0, row.Gender);
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task NullClientOrActualNullPlayerFaultsWithoutManufacturingAReplacement() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Throws<NullReferenceException>(() => new LegacyNativeNobility((LegacyNativeGameClientRuntime)null!));
            scope.Native.Owner.Player = null!;
            Assert.Throws<NullReferenceException>(() => new LegacyNativeNobility(scope.Native.Owner));
            Assert.Null(scope.Native.Owner.Player); Assert.Empty(scope.Native.Queue);
        });

    [Theory]
    [InlineData(0, 0UL, 12)]
    [InlineData(2, 0UL, 12)]
    [InlineData(3, 0UL, 9)]
    [InlineData(16, 0UL, 9)]
    [InlineData(17, 0UL, 7)]
    [InlineData(49, 0UL, 7)]
    [InlineData(-1, 0UL, 0)]
    [InlineData(-1, 9999UL, 0)]
    [InlineData(-1, 10000UL, 3)]
    [InlineData(-1, 19999UL, 3)]
    [InlineData(-1, 20000UL, 5)]
    [InlineData(-1, 29999UL, 5)]
    [InlineData(-1, 30000UL, 5)]
    [InlineData(50, 0UL, 0)]
    [InlineData(int.MinValue, ulong.MaxValue, 5)]
    [InlineData(int.MaxValue, 10000UL, 3)]
    public void RankRetainsPositionPrecedenceExactBoundsAndNativeUnreachableKnightBranch(int position, ulong donation, int expected)
    {
        var row = Row(donation: donation, position: position);
        Assert.Equal((LegacyNativeNobilityRank)expected, row.Rank);
        Assert.Equal(position, row.Position); Assert.Equal(donation, row.Donation);
    }
}
