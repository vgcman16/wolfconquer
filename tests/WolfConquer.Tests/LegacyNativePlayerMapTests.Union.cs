using WolfConquer.Core.World;
using Rank = WolfConquer.Core.World.LegacyNativeUnionMember.MilitaryRanks;
using Exploit = WolfConquer.Core.World.LegacyNativePlayerExploitsRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePlayerMapTests
{
    [Fact]
    public Task UnionBirthAndQueriesKeepRealReferenceOwnersArrayCopiesAndNativeOrdering() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var union = new LegacyNativeUnionRuntime(); var other = new LegacyNativeUnionRuntime();
        Assert.NotSame(union.Members, other.Members); Assert.NotSame(union.UnionRanks, other.UnionRanks);
        Assert.Equal("", union.Name); Assert.Equal("None", union.Bulletin); Assert.Equal("None", union.RecruitDeclaration);
        Assert.True(union.CanSave); Assert.Equal(0u, union.UID); Assert.Equal((byte)0, union.IsKingdom);
        Assert.Empty(LegacyNativeUnionRuntime.UnionPoll); // Genuine implicit birth never publishes itself.
        var lower = new LegacyNativeUnionMember { UID = 1, Rank = Rank.LeftMar };
        var higher = new LegacyNativeUnionMember { UID = 2, Rank = Rank.LeftPre, Owner = scope.Client };
        union.Members[1] = lower; union.Members[2] = higher;
        Assert.Equal(new[] { higher, lower }, union.GetOnlineMembers);
        union.UnionRanks.Add(Rank.LeftMar, lower); union.UnionRanks.Add(Rank.LeftPre, higher);
        Assert.Equal(new[] { higher, lower }, union.UnionOfficials);
        Assert.Same(lower, union.GetOfficialMemberByUID(1)); Assert.Null(union.GetOfficialMemberByUID(99));
        higher.Rank = Rank.Conc; union.ImperialHarem[2] = higher;
        Assert.Same(higher, union.GetImperialHaremMemberByUID(2)); Assert.Equal(1, union.GetImperialHaremCount(Rank.Conc));
        Assert.True(union.IsImperialHaremRank(Rank.Conc)); Assert.True(union.IsImperialHaremRank(Rank.SeniorConc));
        Assert.True(union.IsImperialHaremRank(Rank.Empress)); Assert.False(union.IsImperialHaremRank(Rank.Emperor));
        var first = union.GetImperialHarem; var second = union.GetImperialHarem;
        Assert.NotSame(first, second); Assert.Same(higher, Assert.Single(second));
        for (uint index = 1; index <= 4; index++) LegacyNativeUnionRuntime.UnionPoll[index] = new() { UID = index, GoldBrick = index * 10 };
        Assert.Equal(new uint[] { 4, 3, 2, 1 }, LegacyNativeUnionRuntime.GetOrderyUnions.Select(value => value.UID));
        Assert.Equal(new uint[] { 4, 3, 2 }, LegacyNativeUnionRuntime.Top3Unions.Select(value => value.UID));
        var guild = LegacyNativeGuildRuntime.CreateLoadedGuild("Large"); guild.Members[1] = new();
        var small = LegacyNativeGuildRuntime.CreateLoadedGuild("Small"); union.Guilds[1] = guild; union.Guilds[2] = small;
        Assert.Equal(new[] { small, guild }, union.GetOrderGuilds);
    });

    [Fact]
    public Task ClientMemberFactoryUsesGenuinePlayerFieldsAndMembershipRequiresBothActualReferences() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.Name = "Member"; Set(scope.Player, "_level", (ushort)130);
        Set(scope.Player, "_class", 1005u); scope.Player.NobilityRank = LegacyNativeNobilityRank.Duke;
        var member = LegacyNativeUnionMember.CreateMember(scope.Client, Rank.Emperor);
        Assert.Equal("Member", member.Name); Assert.Equal((ushort)130, member.Level); Assert.Equal(1005u, member.Class);
        Assert.Equal(scope.Player.Mesh, member.Mesh); Assert.Equal(scope.Player.UID, member.UID);
        Assert.Equal(LegacyNativeNobilityRank.Duke, member.NobilityRank); Assert.Same(scope.Client, member.Owner);
        Assert.False(scope.Player.InUnion); scope.Player.UnionMemeber = member; Assert.False(scope.Player.InUnion);
        scope.Player.MyUnion = new(); Assert.True(scope.Player.InUnion); Assert.True(scope.Player.IsUnionEmperror);
        member.Rank = Rank.LeftPre; Assert.False(scope.Player.IsUnionEmperror);
    });

    [Theory]
    [InlineData(199u, 0u)]
    [InlineData(200u, 1u)]
    [InlineData(499u, 1u)]
    [InlineData(500u, 2u)]
    [InlineData(1000u, 3u)]
    [InlineData(1500u, 4u)]
    [InlineData(2100u, 5u)]
    [InlineData(2800u, 6u)]
    [InlineData(3700u, 7u)]
    [InlineData(4700u, 8u)]
    [InlineData(6000u, 9u)]
    [InlineData(7500u, 10u)]
    [InlineData(10000u, 11u)]
    [InlineData(15000u, 12u)]
    [InlineData(23000u, 13u)]
    [InlineData(uint.MaxValue, 13u)]
    public Task ExploitSetterUsesEveryNativeThresholdUpdatesActualMemberAndEmitsRealUpdate(uint value, uint rank) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.MyUnion = new(); scope.Player.UnionMemeber = new();
        scope.Player.KingDomExploits = value;
        Assert.Equal(value, scope.Player.KingDomExploits); Assert.Equal(value, scope.Player.UnionMemeber.Exploits);
        Assert.Equal((Exploit)rank, scope.Player.ExploitsRank);
        if (rank == 0) Assert.Empty(scope.Native.Queue);
        else
        {
            byte[] frame = Assert.Single(scope.Native.Queue);
            Assert.Equal((ushort)2176, BitConverter.ToUInt16(frame, 2));
            var scalars = Scalars(frame); Assert.Equal(new ulong[] { scope.Player.UID }, scalars[1]);
            // Independent exact nested protobuf field: Type82 and the selected rank.
            Assert.Equal(new byte[] { 0x08, 0x52, 0x10, (byte)rank }, frame.AsSpan(frame.Length - 4).ToArray());
        }
    });

    [Fact]
    public Task ExploitRankDoesNotResetBelowThresholdAndMutationPrecedesRecycleFault() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); scope.Player.ExploitsRank = Exploit.GeneralinChief;
        scope.Player.UnionMemeber = new() { Exploits = 7 }; // Missing union: member is not authoritative.
        scope.Player.KingDomExploits = 199;
        Assert.Equal(Exploit.GeneralinChief, scope.Player.ExploitsRank); Assert.Equal(7u, scope.Player.UnionMemeber.Exploits);
        scope.Player.MyUnion = new(); scope.NullBin();
        Assert.Throws<NullReferenceException>(() => scope.Player.KingDomExploits = 500);
        Assert.Equal(500u, scope.Player.KingDomExploits); Assert.Equal(500u, scope.Player.UnionMemeber.Exploits);
        Assert.Equal(Exploit.Decurion, scope.Player.ExploitsRank); Assert.Empty(scope.Native.Queue);
    });
}
