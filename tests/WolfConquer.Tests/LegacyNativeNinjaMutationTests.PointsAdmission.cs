using System.Collections.Concurrent;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    [Theory]
    [InlineData("uid", 0U)] [InlineData("uid", 3U)]
    [InlineData("player", 0U)] [InlineData("player", 3U)]
    [InlineData("retired", 0U)] [InlineData("retired", 3U)]
    public Task PointCreditsRequireThePositiveCurrentPhysicalActorEvenForZero(string invalid, uint amount) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); SeedPointCredit(scope);
            switch (invalid)
            {
                case "uid": scope.Client.Player.UID = 0; break;
                case "player": scope.Client.Player = new LegacyNativeGameClientRuntime().Player; break;
                case "retired": scope.Client.MyNinja = new LegacyNativeNinja(scope.Client); break;
                default: throw new InvalidOperationException("Unknown actual owner refusal.");
            }
            var before = new PointCreditImage(scope);

            var error = Record.Exception(() => scope.Ninja.AddPoints(amount));

            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
            Assert.Empty(scope.Log);
        });

    [Theory]
    [InlineData(0U)] [InlineData(3U)]
    public Task InvalidLongSealRefusesBeforeAnyPointStoreOrRental(uint amount) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); SeedPointCredit(scope);
            // The invalid nine-character ASCII seal is safely truncated to eight
            // bytes by the old writer; corrected preparation refuses its length.
            PointsSealField.SetValue(null, "123456789");
            var before = new PointCreditImage(scope);

            var error = Record.Exception(() => scope.Ninja.AddPoints(amount));

            before.RequireUnchanged();
            Assert.IsType<InvalidDataException>(error);
        });

    [Theory]
    [InlineData(0U)] [InlineData(3U)]
    public Task NullActualRentalRowRefusesWithoutPointOrPacketMutation(uint amount) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); SeedPointCredit(scope);
            var priorPool = PointsBinField.GetValue(null);
            PointsBinField.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([null!]));
            try
            {
                var before = new PointCreditImage(scope);
                var error = Record.Exception(() => scope.Ninja.AddPoints(amount));
                before.RequireUnchanged(requireAvailable: false);
                Assert.IsType<InvalidOperationException>(error);
            }
            finally { PointsBinField.SetValue(null, priorPool); }
        });

    [Theory]
    [InlineData(0U)] [InlineData(3U)]
    public Task HeldActualNinjaPreparationCannotAdmitAnotherPointCredit(uint amount) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); SeedPointCredit(scope);
            var before = new PointCreditImage(scope);
            var gate = PointsGateField.GetValue(scope.Ninja)!;
            Exception? error;
            lock (gate) error = Record.Exception(() => scope.Ninja.AddPoints(amount));
            before.RequireUnchanged();
            Assert.IsType<InvalidOperationException>(error);
        });

    [Fact]
    public Task PointCreditRetainsEveryOtherAggregateFieldAndActualItemRow() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); SeedPointCredit(scope);
            var before = new PointCreditImage(scope);
            var prior = Assert.Single(scope.Queue);

            scope.Ninja.AddPoints(3);

            before.RequireNonPointState(74);
            Assert.Equal(2, scope.Queue.Count);
            Assert.Same(prior, scope.Queue.First());
            scope.Sent(Frame(2, 0, 0, 74), 1);
            Assert.Empty(scope.Log);
        });
}
