using WolfConquer.Core.World;
using Storage = WolfConquer.Core.World.LegacyNativeCollectionStorageRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCollectionStorageTests
{
    [Fact]
    public Task LoadSendsEmptyStorageWithEquippedIdentityAndNoOptionalPlayerUserid() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.UID = 123;
        scope.Client.Player.CollectionID = 77;
        Assert.Null(scope.Client.CollectionStorage);
        scope.Storage.Load();
        Assert.Single(scope.Queue);
        // Anchored to Root's observed action-0/Identity77 pinned payload.
        byte[] literal = Convert.FromHexString("080092090800104D");
        Assert.Equal(literal, Frame(77));
        scope.Sent(literal);
        Assert.Equal(2, scope.Available);
        Assert.Equal("0/0/", scope.Storage.ToString());
        Assert.Null(scope.Client.CollectionStorage);
    });

    [Fact]
    public Task LoadKeepsRequiredZeroFieldsInScrapAndItemRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var scrap = scope.Scrap(100, 0, 0);
        var item = scope.Item(200, 0, 0, 0, 0, 0);
        scope.Storage.Load();
        Assert.Single(scope.Queue);
        byte[] literal = Convert.FromHexString("1A009209080010001A0408001000220A08001000180020002800");
        Assert.Equal(literal, Frame(0, [(0, 0)], [(0, 0, 0, 0, 0)]));
        scope.Sent(literal);
        Assert.Same(scrap, scope.Storage.Scraps[100]);
        Assert.Same(item, scope.Storage.Items[200]);
        ItemIs(item, 0, 0, 0, 0, 0);
    });

    [Fact]
    public Task LoadProjectsEveryPhysicalValueInNativeEnumerationOrderWithoutRekeying() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.CollectionID = uint.MaxValue;
        scope.Scrap(91, 17, 19);
        scope.Scrap(3, uint.MaxValue, uint.MaxValue);
        scope.Item(27, 4, 5, 6, 0, 8);
        scope.Item(2, uint.MaxValue, uint.MaxValue, uint.MaxValue, 0, uint.MaxValue);
        var scraps = scope.Storage.Scraps.Values.ToArray();
        var items = scope.Storage.Items.Values.ToArray();
        string record = scope.Storage.ToString();
        scope.Storage.Load();
        scope.Sent(Frame(uint.MaxValue,
            scraps.Select(row => (row.Type, row.Amount)).ToArray(),
            items.Select(row => (row.Type, row.Activate, row.Amount, 0u, row.F5)).ToArray()));
        Assert.Single(scope.Queue);
        Assert.Equal(record, scope.Storage.ToString());
        Assert.Equal(new uint[] { 2, 27 }, scope.Storage.Items.Keys.OrderBy(key => key));
        Assert.Equal(new uint[] { 3, 91 }, scope.Storage.Scraps.Keys.OrderBy(key => key));
    });

    [Fact]
    public Task LoadUsesCurrentPhysicalUserForIdentityAndRecipientNotItsClientMemberSlot() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var foreignQueue = new Queue<byte[]>();
        LegacyNativeGameClientRuntime foreign = NativeSpellTestFixture.CreateOwner(foreignQueue);
        foreign.Player.UID = 999;
        foreign.Player.CollectionID = 41;
        scope.Client.Player.CollectionID = 77;
        scope.Client.CollectionStorage = new Storage(scope.Client);
        scope.Client.CollectionStorage.Items[99] = new Storage.Item { Type = 99 };
        var item = scope.Item(100, 4);
        scope.Storage.user = foreign;
        scope.Storage.Load();
        Assert.Empty(scope.Queue);
        Assert.Equal(Frame(41, items: [(4, 5, 6, 0, 8)]), Assert.Single(foreignQueue));
        Assert.Same(foreign, scope.Storage.user);
        Assert.Null(foreign.CollectionStorage);
        Assert.Single(scope.Client.CollectionStorage.Items);
        Assert.Same(item, Assert.Single(scope.Storage.Items).Value);
        Assert.Equal(0, scope.Used().Position);
        Assert.Equal(2, scope.Available);
    });

    [Fact]
    public Task LoadConvertsAbsoluteExpirationToBoundedRemainingSecondsWithoutChangingRecord() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        int before = LocalSeconds();
        uint expiration = checked((uint)((long)before + 600));
        var item = scope.Item(100, 4, time: expiration);
        string record = scope.Storage.ToString();
        scope.Storage.Load();
        int after = LocalSeconds();
        byte[] actual = Assert.Single(scope.Queue);
        // Only the clock-dependent uint and its enclosing length are read from
        // output. Every fixed field, complete length and final frame are checked.
        Assert.Equal(Convert.FromHexString("0800100022"), actual.AsSpan(4, 5).ToArray());
        int offset = 9;
        uint nestedLength = ReadVarint(actual, ref offset);
        int nestedStart = offset;
        Assert.Equal(Convert.FromHexString("08041005180620"), actual.AsSpan(offset, 7).ToArray());
        offset += 7;
        uint remaining = ReadVarint(actual, ref offset);
        RemainingIsBetween(remaining, expiration, before, after);
        Assert.Equal((byte)0x28, actual[offset++]);
        Assert.Equal((byte)8, actual[offset++]);
        Assert.Equal((uint)(offset - nestedStart), nestedLength);
        Assert.Equal(actual.Length, offset);
        scope.Sent(Frame(0, items: [(4, 5, 6, remaining, 8)]));
        Assert.Same(item, scope.Storage.Items[100]);
        ItemIs(item, 4, 5, 6, expiration, 8);
        Assert.Equal(record, scope.Storage.ToString());
    });

    [Fact]
    public Task RepeatedLoadSendsCurrentRetainedRowsAndCurrentEquippedIdentity() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var scrap = scope.Scrap(100, 2, 3);
        var item = scope.Item(200, 4);
        scope.Client.Player.CollectionID = 7;
        scope.Storage.Load();
        scrap.Amount = 13;
        item.Activate = 0;
        item.Amount = 17;
        item.RemainingTime = 1; // Expired epoch value, not a duration.
        item.F5 = 23;
        scope.Client.Player.CollectionID = 11;
        scope.Storage.Load();
        Assert.Equal(2, scope.Queue.Count);
        scope.Sent(Frame(7, [(2, 3)], [(4, 5, 6, 0, 8)]));
        scope.Sent(Frame(11, [(2, 13)], [(4, 0, 17, 0, 23)]), 1);
        Assert.Equal(1, scope.Available);
        Assert.Same(item, scope.Storage.Items[200]);
        Assert.Same(scrap, scope.Storage.Scraps[100]);
        Assert.Equal(1u, item.RemainingTime);
    });

    [Theory]
    [InlineData("user")]
    [InlineData("scraps")]
    [InlineData("items")]
    [InlineData("scrap-row")]
    [InlineData("item-row")]
    public Task LoadFaultsBeforeAnySerializationOrQueueAdmissionForBrokenPhysicalOwners(string fault) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            if (fault == "user") scope.Storage.user = null!;
            else if (fault == "scraps") scope.Storage.Scraps = null!;
            else if (fault == "items") scope.Storage.Items = null!;
            else if (fault == "scrap-row") scope.Storage.Scraps[7] = null!;
            else scope.Storage.Items[11] = null!;
            Assert.Throws<NullReferenceException>(() => scope.Storage.Load());
            scope.NoSerialization();
            Assert.Null(scope.Client.CollectionStorage);
        });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task RejectedRecipientStillSerializesAndDisposesBeforeGenuineSendAdmission(bool fake) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Fake = fake;
            scope.Client.Socket!.Alive = fake;
            scope.Client.Player.CollectionID = 77;
            scope.Storage.Load();
            Assert.Empty(scope.Queue);
            Assert.Equal(2, scope.Available);
            Assert.Equal(Convert.FromHexString("080092090800104D"), NativeSpellTestFixture.Bytes(scope.Used()));
            Assert.Equal(0, scope.Used().Position);
            Assert.Equal("0/0/", scope.Storage.ToString());
        });
}
