using WolfConquer.Core.World;
using Storage = WolfConquer.Core.World.LegacyNativeAnimaSkinRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAnimaSkinTests
{
    [Fact]
    public Task LoadingWithoutEquippedSkinSendsPinnedRestoreAndDisposesActualPacket() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Records.Row(17, 17, false);
        scope.Storage.Loading();
        byte[] literal = Convert.FromHexString("0A006F09080510112001");
        Assert.Equal(literal, Frame(17, restore: true));
        scope.Sent(literal);
        Assert.Single(scope.Queue);
        Assert.Equal(2, scope.Available);
        Assert.Same(row, scope.Storage.Items[17]);
        Assert.False(scope.Storage.Load);
        Assert.Null(scope.Client.MyAnimaSkin);
    });

    [Fact]
    public Task LoadingEquippedSkinSendsPinnedMyShowWithDefaultTypeAndOkOmitted() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Records.Row(100, 9, true);
        scope.Storage.Load = true;
        scope.Storage.Loading();
        byte[] literal = Convert.FromHexString("08006F0910111809");
        Assert.Equal(literal, Frame(17, 9));
        scope.Sent(literal);
        Assert.Single(scope.Queue);
        Assert.Same(row, scope.Storage.Items[100]);
        Assert.True(row.isEquip);
        Assert.True(scope.Storage.Load);
        Assert.False(scope.Storage.Items.ContainsKey(9));
    });

    [Fact]
    public Task EquippedPhysicalZeroIdStillTakesRestoreBranchWithoutClearingItsFlag() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Records.Row(100, 0, true);
        scope.Storage.Loading();
        scope.Sent(Frame(17, restore: true));
        Assert.Single(scope.Queue);
        RowIs(row, 0, true);
        Assert.Same(row, Assert.Single(scope.Storage.Items).Value);
    });

    [Fact]
    public Task LoadingUsesCurrentPhysicalUserForRecipientAndUidNotClientMemberSlot() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var foreignQueue = new Queue<byte[]>();
        LegacyNativeGameClientRuntime foreign = NativeSpellTestFixture.CreateOwner(foreignQueue);
        foreign.Player.UID = 19;
        scope.Client.MyAnimaSkin = new Storage(scope.Client);
        scope.Client.MyAnimaSkin.AddSkin(99, true);
        scope.Records.Row(100, 17, true);
        scope.Storage.user = foreign;
        scope.Storage.Loading();
        Assert.Empty(scope.Queue);
        Assert.Equal(Frame(19, 17), Assert.Single(foreignQueue));
        scope.Serialized(Frame(19, 17));
        Assert.Same(foreign, scope.Storage.user);
        Assert.Null(foreign.MyAnimaSkin);
        Assert.Equal(99u, scope.Client.MyAnimaSkin.isEquip);
    });

    [Fact]
    public Task RepeatedLoadingReadsCurrentRowsUidAndLoadFlagWithoutMakingItReady() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var row = scope.Records.Row(100, 9, true);
        scope.Storage.Loading();
        Assert.False(scope.Storage.Load);
        row.ID = uint.MaxValue;
        scope.Client.Player.UID = 0;
        scope.Storage.Load = true;
        scope.Storage.Loading();
        scope.Sent(Frame(17, 9));
        scope.Sent(Frame(0, uint.MaxValue), 1);
        Assert.Equal(2, scope.Queue.Count);
        Assert.Equal(1, scope.Available);
        Assert.True(scope.Storage.Load);
        Assert.Same(row, scope.Storage.Items[100]);
        RowIs(row, uint.MaxValue, true);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task BrokenDictionaryOrRowFaultStillDisposesPacketTakenBeforeEquippedScan(bool nullRow) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (nullRow) scope.Storage.Items[17] = null!;
        else scope.Storage.Items = null!;
        Assert.Throws<NullReferenceException>(() => scope.Storage.Loading());
        scope.FaultDisposed();
        Assert.False(scope.Storage.Load);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullPhysicalUserFaultInEitherBranchStillDisposesUnserializedPacket(bool equipped) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        if (equipped) scope.Records.Row(100, 9, true);
        scope.Storage.user = null!;
        Assert.Throws<NullReferenceException>(() => scope.Storage.Loading());
        scope.FaultDisposed();
        Assert.False(scope.Storage.Load);
        if (equipped) RowIs(scope.Storage.Items[100], 9, true);
    });

    [Fact]
    public Task FakeRecipientSuppressesOnlyQueueAfterRealSerializationAndDisposal() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Fake = true;
        scope.Storage.Loading();
        Assert.Empty(scope.Queue);
        scope.Serialized(Frame(17, restore: true));
        Assert.Equal(2, scope.Available);
        Assert.False(scope.Storage.Load);
        Assert.Empty(scope.Storage.Items);
    });
}
