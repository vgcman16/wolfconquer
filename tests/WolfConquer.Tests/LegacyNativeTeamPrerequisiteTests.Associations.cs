using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Associate = WolfConquer.Core.World.LegacyNativeAssociateRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeamPrerequisiteTests
{
    [Fact]
    public Task NativeAssociateConstructorKeepsEveryPhysicalDefaultAndDoesNotRegisterOrBindPlayer() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = new Associate.MyAsociats(7);
        Assert.Equal(7u, row.MyUID); Assert.Empty(row.Associat); Assert.Empty(row.OnlineApprentice);
        Assert.Equal(0u, row.Online); Assert.Null(row.MyClient);
        Assert.Equal(0u, row.Mentor_Stones); Assert.Equal(0u, row.Mentor_ExpBalls); Assert.Equal(0u, row.Mentor_Blessing);
        Assert.Empty(Associate.Associates); Assert.Null(scope.Player.MyMentor); Assert.Null(scope.Player.Associate);
        var member = new Associate.Member();
        Assert.Equal(0u, member.UID); Assert.Equal(0u, member.Mesh); Assert.Equal(0u, member.Level);
        Assert.Equal(0u, member.NobilityRank); Assert.Equal(0u, member.Class); Assert.Equal("", member.Signature);
        Assert.Equal(0ul, member.Timer); Assert.Equal(0u, member.ExpBalls); Assert.Equal(0u, member.Stone);
        Assert.Equal(0u, member.KillMap); Assert.Equal(0u, member.Blessing); Assert.Equal("", member.Name); Assert.Equal((ushort)0, member.KillsCount);
        scope.Player.MyMentor = row; row.Mentor_ExpBalls = uint.MaxValue; Assert.Same(row, scope.Player.MyMentor);
    });

    [Fact]
    public Task OnUsePublishesThisOwnerOnlyForNonzeroUidWithoutReplacingAnExistingPhysicalOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var zero = new Associate.MyAsociats(0);
        Assert.False(zero.OnUse()); Assert.Empty(Associate.Associates);
        var first = new Associate.MyAsociats(7); Assert.True(first.OnUse()); Assert.Same(first, Associate.Associates[7]);
        var second = new Associate.MyAsociats(7); Assert.True(second.OnUse()); Assert.Same(first, Associate.Associates[7]);
        var member = new Associate.Member { UID = 90 }; second.Add(5, member);
        Assert.Same(member, second.Associat[5][90]); Assert.Empty(first.Associat);
        first.MyUID = 8; Assert.True(first.OnUse()); Assert.Same(first, Associate.Associates[7]); Assert.Same(first, Associate.Associates[8]);
    });

    [Fact]
    public Task AllowAddPreservesAbsentModeBeforeLimitCheckAndAddDoesNotEnforceThatLimit() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = new Associate.MyAsociats(7);
        Assert.True(row.AllowAdd(5, 1, 0));
        row.Associat[5] = new(); Assert.False(row.AllowAdd(5, 1, 0));
        var member = new Associate.Member { UID = 1 }; row.Add(5, member);
        Assert.Same(member, row.Associat[5][1]); Assert.False(row.AllowAdd(5, 1, 255)); Assert.False(row.AllowAdd(5, 2, 1));
        row.Add(5, new Associate.Member { UID = 2 }); Assert.Equal(2, row.Associat[5].Count);
        Assert.True(row.AllowAdd(6, 1, 0)); Assert.True(row.AllowAdd(5, 3, 3));
    });

    [Fact]
    public Task AddUsesLiveMemberUidAndRetainsFirstDuplicateReferenceWithoutRenamingOldDictionaryKey() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = new Associate.MyAsociats(7);
        var first = new Associate.Member { UID = 1 }; var second = new Associate.Member { UID = 1 };
        row.Add(5, first); row.Add(5, second); Assert.Same(first, row.Associat[5][1]);
        first.UID = 2;
        Assert.True(row.Contain(5, 1)); Assert.False(row.Contain(5, 2));
        row.Add(4, first); Assert.Same(first, row.Associat[4][2]);
        Assert.True(row.Remove(5, 1)); Assert.False(row.Contain(5, 1)); Assert.Same(first, row.Associat[4][2]);
        Assert.False(row.Remove(5, 1)); Assert.False(row.Remove(99, 1)); Assert.True(row.Associat.ContainsKey(5));
    });

    [Fact]
    public Task AddAndOnUseRetainNativeFaultAndPublicationOrderInsteadOfAcceptingNullRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var zero = new Associate.MyAsociats(0);
        zero.Add(5, null!); Assert.Empty(zero.Associat); Assert.Empty(Associate.Associates);
        var row = new Associate.MyAsociats(7);
        Assert.Throws<NullReferenceException>(() => row.Add(5, null!));
        Assert.Same(row, Associate.Associates[7]); Assert.True(row.Associat.ContainsKey(5)); Assert.Empty(row.Associat[5]);
        row.Associat = null!;
        Assert.Throws<NullReferenceException>(() => row.AllowAdd(5, 1, 1));
        Assert.Throws<NullReferenceException>(() => row.Contain(5, 1));
        Assert.Throws<NullReferenceException>(() => row.Remove(5, 1));
        Associate.Associates = null!;
        Assert.False(zero.OnUse()); Assert.Throws<NullReferenceException>(() => row.OnUse());
    });

    [Fact]
    public Task RemoveOfflineUsesActualRegisteredOwnerAndKeepsModeDictionaryAndUnrelatedMembership() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = new Associate.MyAsociats(7);
        var member = new Associate.Member { UID = 90 }; row.Add(5, member); row.Add(4, member);
        Associate.RemoveOffline(5, 999, 90); Assert.True(row.Contain(5, 90));
        Associate.RemoveOffline(5, 7, 90); Assert.False(row.Contain(5, 90)); Assert.True(row.Contain(4, 90));
        Assert.True(row.Associat.ContainsKey(5)); Assert.Same(row, Associate.Associates[7]);
        Associate.Associates = null!; Assert.Throws<NullReferenceException>(() => Associate.RemoveOffline(5, 7, 90));
    });

    [Fact]
    public Task MemberTimerReadsRealWallClockAndClearsOnlyExpiredNativeTickValues() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var member = new Associate.Member();
        Assert.Equal(0, member.GetTimerLeft()); Assert.Equal(0ul, member.Timer);
        member.Timer = (ulong)DateTime.Now.AddMinutes(-5).Ticks;
        Assert.Equal(0, member.GetTimerLeft()); Assert.Equal(0ul, member.Timer);
        ulong future = (ulong)DateTime.Now.AddDays(1).Ticks; member.Timer = future;
        Assert.InRange(member.GetTimerLeft(), 1439, 1440); Assert.Equal(future, member.Timer);
        member.Timer = ulong.MaxValue; Assert.Equal(0, member.GetTimerLeft()); Assert.Equal(0ul, member.Timer);
        member.Timer = long.MaxValue; Assert.Equal(0, member.GetTimerLeft()); Assert.Equal(0ul, member.Timer);
    });

    [Fact]
    public Task ProgressionPrerequisiteFieldsRemainIndependentNativeSlotsAndExactEffectNames() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var player = scope.Player;
        Assert.Equal(0, player.CursedTimer); Assert.False(player.Reincarnation);
        Assert.Equal((byte)0, player.FirstRebornLevel); Assert.Equal((byte)0, player.SecoundeRebornLevel);
        Assert.Equal(0u, player.DExpTime); Assert.Equal(1u, player.RateExp); Assert.Null(player.FocusClientSpell);
        Assert.Equal((ushort)0, player.Atributes); Assert.Equal(0u, player.VirtutePoints); Assert.Equal(0ul, scope.Client.ExpOblivion);
        player.CursedTimer = -1; player.RateExp = uint.MaxValue; player.Atributes = ushort.MaxValue;
        scope.Client.ExpOblivion = ulong.MaxValue;
        Assert.Equal(-1, player.CursedTimer); Assert.Equal(uint.MaxValue, player.RateExp); Assert.Equal(ushort.MaxValue, player.Atributes);
        Assert.Equal(ulong.MaxValue, scope.Client.ExpOblivion); Assert.Equal(0u, player.VirtutePoints);
        Assert.Equal(new[] { "None", "angelwing", "bless" }, Enum.GetNames<LegacyNativeGameItemFlags.ExperienceEffect>());
        Assert.Empty(LegacyNativeDataCoreAttributes.AtributeStatus);
        var actual = LegacyNativeDataCoreAttributes.AtributeStatus;
        actual[1005] = new Dictionary<ushort, LegacyNativeAtributesStatus.Instant>();
        Assert.Same(actual, LegacyNativeDataCoreAttributes.AtributeStatus); Assert.Single(actual);
    });
}
