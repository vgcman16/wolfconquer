using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(8, 0)]
    [InlineData(1003, 1)]
    [InlineData(1008, 0)]
    [InlineData(65535, 1)]
    public Task GenderUsesActualBodyLastDigitWithoutAnyMeshOrNobilityOwnerRequirement(int body, int expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var player = scope.Native.Owner.Player;
            player.Body = (ushort)body; player.Face = 65535; player.TransformationID = 65535;
            Assert.Equal((byte)expected, player.GetGender);
            Assert.Null(player.Nobility); Assert.Null(player.UnionMemeber);
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task PlayerBirthLeavesNobilityAndUnionSlotsNullAndRankSetterDoesNotInventOwners() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var player = scope.Native.Owner.Player;
            Assert.Null(player.Nobility); Assert.Null(player.UnionMemeber);
            Assert.Null(player.MyGuild); Assert.Null(player.MyGuildMember);
            Assert.Equal(LegacyNativeNobilityRank.Serf, player.NobilityRank);
            player.NobilityRank = LegacyNativeNobilityRank.Prince;
            Assert.Equal(LegacyNativeNobilityRank.Prince, player.NobilityRank);
            Assert.Null(player.Nobility); Assert.Null(player.UnionMemeber);
            Assert.Null(player.MyGuild); Assert.Null(player.MyGuildMember);
            Assert.Empty(scope.Native.Queue);
        });

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public Task RankSetterUpdatesActualGuildMemberOnlyWithBothGuildSlotsAndUnionIndependently(bool guild, bool member, bool union) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var player = scope.Native.Owner.Player;
            var guildMember = new LegacyNativeGuildRuntime.Member { UID = 999, NobilityRank = 77 };
            var unionMember = new LegacyNativeUnionMember { UID = 998, NobilityRank = LegacyNativeNobilityRank.Baron };
            player.MyGuild = guild ? LegacyNativeGuildRuntime.CreateLoadedGuild("Other physical guild") : null;
            player.MyGuildMember = member ? guildMember : null;
            player.UnionMemeber = union ? unionMember : null;
            var separateNobility = Row(999, 0, -1);
            player.Nobility = separateNobility;

            player.NobilityRank = (LegacyNativeNobilityRank)255;

            Assert.Equal((LegacyNativeNobilityRank)255, player.NobilityRank);
            Assert.Equal(guild && member ? 255u : 77u, guildMember.NobilityRank);
            Assert.Equal(union ? (LegacyNativeNobilityRank)255 : LegacyNativeNobilityRank.Baron, unionMember.NobilityRank);
            Assert.Equal(999u, guildMember.UID); Assert.Equal(998u, unionMember.UID);
            Assert.Same(separateNobility, player.Nobility);
            Assert.Equal(LegacyNativeNobilityRank.Serf, separateNobility.Rank);
            Assert.Empty(scope.Native.Queue);
        });
}
