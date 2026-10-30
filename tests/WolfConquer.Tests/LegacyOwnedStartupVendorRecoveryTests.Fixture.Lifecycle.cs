using System.Buffers;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class LegacyOwnedStartupVendorRecoveryFixture
{
    private async Task StartAndListAsync()
    {
        await SendAsync(SellerUid, new MsgActionPacket { ObjId = SellerUid, Type = MsgActionType.StartVendor }.ToFrameBytes());
        Assert.True(uint.TryParse(Current(SellerUid).Character.Attributes["RuntimeVendorUid"], out var booth));
        BoothUid = booth; Assert.NotEqual(0U, BoothUid);
        var action = IsCp ? MsgItemUsageAction.AddVendingItemConquerPoints : MsgItemUsageAction.AddVendingItemGold;
        var request = new MsgItemUsagePacket(SoldUid, checked((ulong)Amount), action, 3210, 0, 0, 0);
        var first = frames[SellerUid].Count;
        await SendAsync(SellerUid, request.ToFrameBytes());
        Assert.Equal(request.ToFrameBytes(), Assert.Single(frames[SellerUid].Skip(first)));
        AssertListing(); AssertNoEscrow(Current(SellerUid).Character); AssertNoEscrow(Current(BuyerUid).Character);
    }

    internal Task StageAsync(int boundary) => ProducePendingAsync(LegacyJournalInterruption.StopAfter(boundary), boundary);
    internal Task StopAfterStageFlushedAsync(int ordinal) =>
        ProducePendingAsync(LegacyJournalInterruption.StopAfterStageFlushed(ordinal), null);
    private MsgItemUsagePacket BuyRequest() => new(SoldUid, BoothUid, MsgItemUsageAction.BuyVendingItem, 3456, 2, 3, 4);

    private async Task ProducePendingAsync(LegacyJournalInterruption interruption, int? boundary)
    {
        var buyer = Current(BuyerUid); var seller = Current(SellerUid);
        int buyerFrames = frames[BuyerUid].Count, sellerFrames = frames[SellerUid].Count;
        using (interruption)
        {
            Handler.VendorJournalInterruption = interruption;
            try { await SendAsync(BuyerUid, BuyRequest().ToFrameBytes()); }
            finally { Handler.VendorJournalInterruption = null; }
            Assert.True(interruption.PendingObserved, Log.ToString());
            if (boundary is { } replaced) Assert.Equal(Enumerable.Range(0, replaced + 1), interruption.Visited);
        }
        Assert.True(Runtime.Recovery.IsBlocked);
        Assert.Same(buyer, Current(BuyerUid)); Assert.Same(seller, Current(SellerUid));
        Assert.Equal(buyerFrames, frames[BuyerUid].Count); Assert.Equal(sellerFrames, frames[SellerUid].Count);
        AssertListing();
        var record = JsonNode.Parse(File.ReadAllBytes(PendingPath))!.AsObject();
        Assert.Equal(1, record["Version"]!.GetValue<int>());
        After = new(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var change in record["Changes"]!.AsArray())
        {
            var kind = (LegacyTradeJournalFileKind)change!["Kind"]!.GetValue<int>();
            var uid = checked((uint)change["FileId"]!.GetValue<long>());
            var path = kind == LegacyTradeJournalFileKind.Character ? CharacterPath(uid) : ItemsPath(uid);
            Assert.Equal(Before[path], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.True(After.TryAdd(path, Convert.FromBase64String(change["After"]!.GetValue<string>())));
            order.Add(path);
        }
        Assert.Equal(new[] { CharacterPath(BuyerUid), CharacterPath(SellerUid), ItemsPath(BuyerUid), ItemsPath(SellerUid) }, order);
        OrderedTargets = order;
        Assert.Equal(RegistryState, File.ReadAllBytes(Data.State));
        AssertExpectedCapturedImages();
        if (boundary is { } count)
            for (var index = 0; index < order.Count; index++)
                Assert.Equal(index < count ? After[order[index]] : Before[order[index]], File.ReadAllBytes(order[index]));
    }

    private async Task LoginAsync(uint uid)
    {
        var lifetime = new CancellationTokenSource(); lifetimes.Add(uid, lifetime); resources.Add(lifetime);
        var sent = new List<byte[]>(); frames[uid] = sent;
        var native = new LegacyNativeGameClientRuntime();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, token) => { token.ThrowIfCancellationRequested(); sent.Add(bytes.ToArray()); return ValueTask.CompletedTask; },
            _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, native);
        connections.Add(uid, connection);
        await SendAsync(uid, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
        var current = Current(uid); Assert.Equal(uid, current.Character.Uid);
        Assert.Same(native, current.NativeGameClient); Assert.NotNull(native.OrdinarySpellBinding);
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, current));
    }

    private async Task SendAsync(uint uid, byte[] bytes) => await Handler.HandleFrameAsync(
        connections[uid], ReadFrame(bytes), lifetimes[uid].Token);
    private WorldSession Current(uint uid) => Assert.IsType<WorldSession>(Store.TryGet(connections[uid], out var current) ? current : null);
    private static PacketFrame ReadFrame(byte[] bytes)
    { Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame; }

    internal async Task AssertFreshFrameAsync(LegacyOwnedAdmissionRuntime runtime)
    {
        resources.Add(new NativeLoginBeastMetadataScope(1000));
        Handler = new WorldServerFrameHandler(Data.Loader, null, Catalog(), LegacyRuntimeUpdateLoopOptions.Disabled,
            TextWriter.Null, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
        await LoginAsync(SellerUid); await LoginAsync(BuyerUid);
        foreach (var uid in new[] { SellerUid, BuyerUid })
        {
            var current = Current(uid); var saved = ReadCharacter(After[CharacterPath(uid)]);
            Assert.Equal(saved.Money, current.Character.Money); Assert.Equal(saved.ConquerPoints, current.Character.ConquerPoints);
            Assert.Equal(saved.Money, current.NativeGameClient!.Player!.Money);
            Assert.Equal(checked((uint)saved.ConquerPoints), current.NativeGameClient.Player.ConquerPoints);
            Assert.Equal(After[ItemsPath(uid)], PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Null(current.LegacyVendorListings);
            Assert.False(current.Character.Attributes.ContainsKey("RuntimeVendorUid"));
            Assert.False(current.LegacyTradeActive);
        }
    }

    internal async Task AssertReplayRefusedAsync()
    {
        var files = Data.Files(); var buyer = Current(BuyerUid); var seller = Current(SellerUid);
        int buyerFrames = frames[BuyerUid].Count, sellerFrames = frames[SellerUid].Count;
        await SendAsync(BuyerUid, BuyRequest().ToFrameBytes());
        Assert.Same(buyer, Current(BuyerUid)); Assert.Same(seller, Current(SellerUid));
        Assert.Equal(buyerFrames, frames[BuyerUid].Count); Assert.Equal(sellerFrames, frames[SellerUid].Count);
        Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(Data.Users));
    }

    internal async Task CloseAsync()
    {
        var errors = new List<Exception>();
        foreach (var connection in connections.Values)
        {
            try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
            try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
        }
        connections.Clear();
        for (var index = runtimes.Count - 1; index >= 0; index--)
            try { await runtimes[index].DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        runtimes.Clear();
        for (var index = resources.Count - 1; index >= 0; index--)
            try { resources[index].Dispose(); } catch (Exception error) { errors.Add(error); }
        resources.Clear(); lifetimes.Clear();
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
}
