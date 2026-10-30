using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupItemRecoveryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task RealInterruptedSingleOwnerPrefixRecoversBeforeRuntimePublicationAndReopensWithoutReplay(int boundary) =>
        WithFixture(scope =>
        {
            scope.Stage(boundary);
            var runtime = scope.Open();
            scope.AssertRecoveredImages();
            Assert.Throws<IOException>(() => new FileStream(scope.Data.OwnerLease, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None).Dispose());
            runtime.Dispose();
            scope.AssertLeaseAvailable();
            var recovered = scope.Data.Files();
            scope.Open();
            scope.AssertRecoveredImages();
            scope.Data.AssertFiles(recovered);
        });

    [Fact]
    public Task AccountFilenameMayDifferFromStableCharacterAndItemsUidWithoutCreatingASecondCharacter() =>
        WithFixture(scope =>
        {
            Assert.NotEqual((long)Uid, scope.Account);
            scope.Stage(1);
            scope.Open();
            scope.AssertRecoveredImages();
            Assert.False(File.Exists(scope.Data.UserPath));
            Assert.Single(Directory.GetFiles(scope.Data.Users, "*.ini", SearchOption.TopDirectoryOnly));
        }, separateAccount: true);

    [Fact]
    public Task APreviouslyPublishedRealRuntimeCanCloseOverItsInterruptedWalAndRecoverOnReopen() =>
        WithFixture(scope =>
        {
            var previous = scope.Open();
            scope.Stage(1);
            var pending = File.ReadAllBytes(scope.PendingPath);
            previous.Dispose();
            scope.AssertLeaseAvailable();
            Assert.Equal(pending, File.ReadAllBytes(scope.PendingPath));

            var recoveredRuntime = scope.Open();

            scope.AssertRecoveredImages();
            recoveredRuntime.Dispose();
            scope.AssertLeaseAvailable();
            var recovered = scope.Data.Files();
            scope.Open();
            scope.Data.AssertFiles(recovered);
        });

    [Theory]
    [InlineData(1, "empty")]
    [InlineData(1, "first-byte")]
    [InlineData(1, "middle")]
    [InlineData(1, "last-byte-missing")]
    [InlineData(1, "complete")]
    [InlineData(2, "empty")]
    [InlineData(2, "first-byte")]
    [InlineData(2, "middle")]
    [InlineData(2, "last-byte-missing")]
    [InlineData(2, "complete")]
    public Task EngineWrittenStageAndItsLegitimateWrittenPrefixesRecoverUnderTheSameWalCustody(int ordinal, string prefix) =>
        WithFixture(scope =>
        {
            scope.StopAfterStageFlushed(ordinal);
            var path = scope.StagePath(character: ordinal == 1);
            var flushed = File.ReadAllBytes(path);
            var length = prefix switch
            {
                "empty" => 0,
                "first-byte" => 1,
                "middle" => flushed.Length / 2,
                "last-byte-missing" => flushed.Length - 1,
                "complete" => flushed.Length,
                _ => throw new ArgumentOutOfRangeException(nameof(prefix)),
            };
            // Truncation retains a prefix of the actual engine-written AFTER
            // bytes; neither the journal nor a successful writer is fabricated.
            File.WriteAllBytes(path, flushed.AsSpan(0, length).ToArray());
            Assert.Equal(flushed.AsSpan(0, length).ToArray(), File.ReadAllBytes(path));
            var characterStage = scope.StagePath(character: true);
            var itemsStage = scope.StagePath(character: false);

            var runtime = scope.Open();

            scope.AssertRecoveredImages();
            Assert.False(File.Exists(characterStage));
            Assert.False(File.Exists(itemsStage));
            runtime.Dispose();
            scope.AssertLeaseAvailable();
            var recovered = scope.Data.Files();
            scope.Open();
            scope.Data.AssertFiles(recovered);
        });

    [Fact]
    public Task RecoveredAfterImagesFeedANewVisibleD13FrameAndItsActualNativePlayerGraph() =>
        WithFixtureAsync(async scope =>
        {
            scope.Stage(1);
            var runtime = scope.Open();
            scope.AssertRecoveredImages();
            var registry = File.ReadAllBytes(scope.Data.State);
            scope.Own(new NativeLoginBeastMetadataScope(1000));
            var log = scope.Own(new StringWriter());
            var closed = scope.Own(new CancellationTokenSource());
            var sent = new ConcurrentQueue<byte[]>();
            var native = new LegacyNativeGameClientRuntime();
            var handler = new WorldServerFrameHandler(scope.Data.Loader, null,
                LegacyItemWorldDataFixture.Create([CatalogItem()], []), LegacyRuntimeUpdateLoopOptions.Disabled,
                log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) => { sent.Enqueue(frame.ToArray()); return ValueTask.CompletedTask; },
                _ => { closed.Cancel(); return ValueTask.CompletedTask; }, native);
            scope.CloseWith(async () =>
            {
                Exception? disconnect = null;
                try { await connection.DisconnectAsync(); } catch (Exception error) { disconnect = error; }
                try { await connection.CompleteTransportCloseAsync(); }
                catch (Exception error) when (disconnect is not null) { throw new AggregateException(disconnect, error); }
                if (disconnect is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(disconnect).Throw();
            });
            var bytes = new LegacyMsgLoginGamePacket(Uid).ToFrameBytes();
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));

            await handler.HandleFrameAsync(connection, frame);

            var store = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
                .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
            Assert.True(store.TryGet(connection, out var session));
            var current = Assert.IsType<WorldSession>(session);
            Assert.Same(native, current.NativeGameClient);
            Assert.NotNull(native.OrdinarySpellBinding);
            Assert.Equal(700L, current.Character.Money);
            Assert.Equal(700L, native.Player!.Money);
            var savedItem = Assert.Single(current.PlayerItems.InventoryItems);
            Assert.Equal(ItemUid, savedItem.Uid);
            Assert.Equal((ushort)10, savedItem.Durability);
            Assert.True(native.Inventory!.ClientItems.TryGetValue(ItemUid, out var item));
            Assert.Equal((ushort)10, item!.Durability);
            Assert.Equal((ushort)10, item.MaximDurability);
            Assert.False(LegacyTradeJournal.HasPending(scope.Data.Users));
            Assert.Equal(registry, File.ReadAllBytes(scope.Data.State));
            Assert.NotEmpty(sent);
        });

    private static ItemType CatalogItem()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = ItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedStartupRecoveryItem";
        fields[ItemType.DurabilityFieldIndex] = "10";
        fields[ItemType.StackSizeFieldIndex] = "1";
        return new ItemType(checked((int)ItemId), fields[1], fields);
    }
}
