using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    private static void SeedUserProducer(Scope scope)
    {
        scope.Ninja.Flag = 1; scope.Ninja.Exp = 2;
        scope.Ninja.Fire = 3; scope.Ninja.Water = 4; scope.Ninja.Earth = 5;
        scope.Ninja.Wind = 6; scope.Ninja.Lightning = 7;
        scope.Ninja.tmp_Fire = 8; scope.Ninja.tmp_Water = 9; scope.Ninja.tmp_Earth = 10;
        scope.Ninja.tmp_Wind = 11; scope.Ninja.tmp_Lightning = 12;
        scope.Ninja.FireMastery = 14; scope.Ninja.WaterMastery = 15; scope.Ninja.EarthMastery = 16;
        scope.Ninja.WindMastery = 17; scope.Ninja.LightningMastery = 18; scope.Ninja.UnlockMastery = 19;
        var item = scope.Item(900, 100, 1);
        item.Position = 17; item.Exp = 1000; item.UK = 2000;
        scope.Meta(700, 100, 1, 20);
    }

    private static byte[] SeededUserFrame(uint uid) =>
        Frame(0, uid, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 112, 14, 15, 16, 17, 18, 19);

    private static ulong[] ProducerScalarState(LegacyNativeNinja ninja) =>
    [
        ninja.Flag, ninja.Exp, ninja.Fire, ninja.Water, ninja.Earth, ninja.Wind, ninja.Lightning,
        ninja.tmp_Fire, ninja.tmp_Water, ninja.tmp_Earth, ninja.tmp_Wind, ninja.tmp_Lightning,
        ninja.FireMastery, ninja.WaterMastery, ninja.EarthMastery, ninja.WindMastery,
        ninja.LightningMastery, ninja.UnlockMastery,
    ];

    [Fact]
    public Task CompleteProducerUsesSuppliedUidAndEveryScalarWithRealComputedScoreWithoutMutation() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedUserProducer(scope);
            scope.Client.Player.UID = 444;
            scope.Ninja.user = null!; // Source producer uses its supplied recipient, not Ninja.user.
            var unrelated = new LegacyNativeNinja { Exp = 999 };
            scope.Client.MyNinja = unrelated;
            ulong[] before = ProducerScalarState(scope.Ninja);
            var items = scope.Ninja.Items;
            var item = items[900];
            var metadata = scope.Metadata;
            var row = metadata[700];
            byte[] expected = SeededUserFrame(123);
            // Root's preserved Framework/x86 object-typed protobuf-net control.
            Assert.Equal(Convert.FromHexString(
                "0800107B180120022803300438054006480750085809600A680B700C787080010E88010F900110980111A00112A80113"),
                expected[4..]);

            LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 123);

            Assert.Single(scope.Queue);
            scope.Sent(expected);
            Assert.Equal(3, scope.Available);
            Assert.Equal(444u, scope.Client.Player.UID);
            Assert.Same(unrelated, scope.Client.MyNinja);
            Assert.Equal(999u, unrelated.Exp);
            Assert.Null(scope.Ninja.user);
            Assert.Equal(before, ProducerScalarState(scope.Ninja));
            Assert.Same(items, scope.Ninja.Items);
            Assert.Same(item, Assert.Single(items).Value);
            Assert.Equal(new uint[] { 100, 1, 17, 1000, 2000 },
                new[] { item.ItemID, item.Level, item.Position, item.Exp, item.UK });
            Assert.Same(metadata, LegacyNativeNinjaFile.gouyu_type);
            Assert.Same(row, Assert.Single(metadata).Value);
            Assert.Equal(20u, row.Score);
            Assert.Empty(scope.Log);
        });

    [Fact]
    public Task ProducerRecomputesScoreFromAliasedRowsAndLivePhysicalMetadataEachTime() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.Fire = 2;
            scope.Ninja.FireMastery = 1;
            var item = scope.Item(50, 100);
            item.Position = 17; item.Exp = 123; item.UK = 456;
            Assert.True(scope.Ninja.Items.TryAdd(51, item));
            var row = scope.Meta(1000, 100, 1, 10);
            scope.Metadata.Add(1001, row);
            LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 10);
            scope.Sent(Frame(0, 10, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 37, 1));

            row.Score = 20;
            LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 11);
            scope.Sent(Frame(0, 11, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 57, 1), 1);
            item.Level = 2; // Genuine DBItem miss still charges Fire*4 for each aliased value.
            LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 12);
            scope.Sent(Frame(0, 12, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 17, 1), 2);
            scope.Meta(2000, 100, 2, 30);
            LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 13);
            scope.Sent(Frame(0, 13, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 77, 1), 3);

            Assert.Equal(4, scope.Queue.Count);
            Assert.Equal(0, scope.Available);
            Assert.Same(item, scope.Ninja.Items[50]);
            Assert.Same(item, scope.Ninja.Items[51]);
            Assert.Equal(new uint[] { 100, 2, 17, 123, 456 },
                new[] { item.ItemID, item.Level, item.Position, item.Exp, item.UK });
            Assert.Equal(2u, scope.Ninja.Fire);
            Assert.Equal(1u, scope.Ninja.FireMastery);
        });

    [Fact]
    public Task NullNinjaFaultsAfterActualTakeAndDisposesTheUnserializedPacket() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.Flag = 55; scope.Ninja.Exp = 66;
            ulong[] before = ProducerScalarState(scope.Ninja);
            Assert.Throws<NullReferenceException>(() =>
                LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, null!, 123));
            scope.FaultDisposed();
            Assert.Equal(before, ProducerScalarState(scope.Ninja));
        });

    [Fact]
    public Task EligibleMissingMetadataFaultPrecedesEncodingAndFakeRecipientSuppression() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = scope.Item(1, 100);
            scope.Client.Fake = true;
            scope.Ninja.Exp = 11;
            LegacyNativeNinjaFile.gouyu_type = null!;
            ulong[] before = ProducerScalarState(scope.Ninja);
            Assert.Throws<NullReferenceException>(() =>
                LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 123));
            scope.FaultDisposed();
            Assert.Equal(before, ProducerScalarState(scope.Ninja));
            Assert.Same(item, scope.Ninja.Items[1]);
            Assert.Empty(scope.Log);
        });

    [Fact]
    public Task NullRecipientFaultOccursAfterActualSerializationAndWrapperDisposal() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.Flag = 7; scope.Ninja.Exp = 9;
            Assert.Throws<NullReferenceException>(() =>
                LegacyNativeMsgNinja.MsgNinjaUser.Create(null!, scope.Ninja, uint.MaxValue));
            scope.Serialized(Frame(0, uint.MaxValue, 7, 9));
            Assert.Equal(3, scope.Available);
            Assert.Empty(scope.Queue);
            Assert.Equal(7ul, scope.Ninja.Flag);
            Assert.Equal(9u, scope.Ninja.Exp);
            Assert.Empty(scope.Log);
        });

    [Fact]
    public Task FakeRecipientSuppressesOnlyTheSendAfterCompleteSerializationAndDisposal() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            SeedUserProducer(scope);
            ulong[] before = ProducerScalarState(scope.Ninja);
            scope.Client.Fake = true;
            scope.Client.Socket = null!;
            LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 987);
            scope.Serialized(SeededUserFrame(987));
            Assert.Equal(3, scope.Available);
            Assert.Empty(scope.Queue);
            Assert.Equal(before, ProducerScalarState(scope.Ninja));
            Assert.Empty(scope.Log);
        });

    [Fact]
    public Task MissingMetadataWithoutEligibleItemsStillProducesTheTrueMasteryOnlyScore() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.FireMastery = 1; scope.Ninja.WaterMastery = 2; scope.Ninja.EarthMastery = 3;
            scope.Ninja.WindMastery = 4; scope.Ninja.LightningMastery = 5;
            scope.Item(1, 99); scope.Item(2, 606);
            LegacyNativeNinjaFile.gouyu_type = null!;
            LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 321);
            scope.Sent(Frame(0, 321, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 15, 1, 2, 3, 4, 5));
            Assert.Single(scope.Queue);
            Assert.Equal(2, scope.Ninja.Items.Count);
        });

    [Fact]
    public Task NullRecycleBinFaultsBeforeAnyOwnedPacketIsTakenOrNinjaIsRead() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.UseNullBin();
            Assert.Throws<NullReferenceException>(() =>
                LegacyNativeMsgNinja.MsgNinjaUser.Create(null!, null!, 123));
            scope.RestoreOwnedBin();
            scope.NoSerialization();
        });

    [Fact]
    public Task PrimitiveEncoderReturnsItsOriginalStreamWithNativeCursorAndDoesNotTakeARecycledPacket() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var message = new LegacyNativeMsgNinja.MsgNinjaUser();
            Assert.Same(scope.Packet, scope.Packet.CreateNinjaUser(message));
            Assert.Equal(Frame(), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.Equal(4, scope.Available);
            Assert.Empty(scope.Queue);
            Assert.Equal(0u, message.Type);
            Assert.Equal(0ul, message.Flag);
            Assert.Throws<InvalidOperationException>(() => LegacyNativeMsgNinja.CreateNinjaUser(null!, null!));
            Assert.Equal(4, scope.Available);
        });
}
