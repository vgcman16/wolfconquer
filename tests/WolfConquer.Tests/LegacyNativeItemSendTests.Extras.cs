using WolfConquer.Core.World;
using NativeExtra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemSendTests
{
    [Fact]
    public Task SendItemExtraZeroUidGatePrecedesClientStreamAndInvalidExpiryAccess() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = Item();
            item.Purification = Purification(0);
            item.Purification.PurificationDuration = 1;
            item.Purification.AddedOn = DateTime.MaxValue;
            item.Refinary = Refinery(0);
            item.Refinary.EffectDuration = 1;
            item.Refinary.AddedOn = DateTime.MaxValue;

            item.SendItemExtra(null!, null!);

            Assert.False(PurificationAvailable(item.Purification));
            Assert.False(item.Refinary.Available);
            Assert.Equal(NativeExtra.Typing.ExpireTime, item.Purification.Typ);
            Assert.Equal(NativeExtra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
            AssertTail(scope, 0);
        });

    [Fact]
    public Task SendItemExtraMutatesActualStructsAndQueuesOneFullNativeOrderedFrame() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = Item();
            item.Purification = Purification();
            item.Refinary = Refinery();

            item.SendItemExtra(scope.Client, scope.Packet);

            byte[] expected = Convert.FromHexString(
                "4400560802000000470000000800000001350C000300000017000000000000000000000000000000" +
                "52000000030000002D010000050000000B0000001600000000000000");
            Assert.Equal(expected, Assert.Single(scope.Queue));
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
            Assert.Equal(NativeExtra.Typing.Stabilization, item.Purification.Typ);
            Assert.Equal(NativeExtra.Typing.PermanentRefinery, item.Refinary.Typ);
            Assert.True(PurificationAvailable(item.Purification));
            Assert.True(item.Refinary.Available);
            Assert.Equal(71u, item.Purification.ItemUID);
            Assert.Equal(82u, item.Refinary.ItemUID);
            Assert.Equal(724440u, item.Refinary.EffectID);
            AssertTail(scope, 68);
        });

    [Fact]
    public Task SendItemExtraTimedEffectsUseAddingTypesAndActualClockSeconds() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            DateTime start = DateTime.Now.AddSeconds(-7);
            var item = Item();
            item.Purification = Purification();
            item.Purification.AddedOn = start;
            item.Purification.PurificationDuration = 300;
            item.Refinary = Refinery();
            item.Refinary.AddedOn = start;
            item.Refinary.EffectDuration = 600;
            DateTime earliest = DateTime.Now;

            item.SendItemExtra(scope.Client, scope.Packet);

            DateTime latest = DateTime.Now;
            byte[] actual = Assert.Single(scope.Queue);
            uint purificationSeconds = Read32(actual, 32);
            uint refinerySeconds = Read32(actual, 64);
            Assert.InRange(purificationSeconds,
                (uint)((start.AddSeconds(300) - latest).TotalSeconds - 1),
                (uint)((start.AddSeconds(300) - earliest).TotalSeconds + 1));
            Assert.InRange(refinerySeconds,
                (uint)((start.AddSeconds(600) - latest).TotalSeconds - 1),
                (uint)((start.AddSeconds(600) - earliest).TotalSeconds + 1));
            Assert.Equal(ExtraFrame(2,
                71, 6, 800001, 3, 23, 300, purificationSeconds, 0,
                82, 2, 301, 5, 11, 22, refinerySeconds), actual);
            Assert.Equal(NativeExtra.Typing.PurificationAdding, item.Purification.Typ);
            Assert.Equal(NativeExtra.Typing.RefinaryAdding, item.Refinary.Typ);
            Assert.True(PurificationAvailable(item.Purification));
            Assert.True(item.Refinary.Available);
        });

    [Fact]
    public Task SendItemExtraRetainedExpiredUidsStillQueueCountZeroAndClearActualAvailability() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = Item();
            item.Purification = Purification();
            Assert.True(item.Purification.InLife);
            item.Purification.PurificationDuration = 1;
            item.Purification.AddedOn = DateTime.Now.AddMinutes(-1);
            item.Refinary = Refinery();
            item.Refinary.Available = true;
            item.Refinary.EffectDuration = 1;
            item.Refinary.AddedOn = DateTime.Now.AddMinutes(-1);

            item.SendItemExtra(scope.Client, scope.Packet);

            Assert.Equal(Convert.FromHexString("0800560800000000"), Assert.Single(scope.Queue));
            Assert.False(PurificationAvailable(item.Purification));
            Assert.False(item.Refinary.Available);
            Assert.Equal(NativeExtra.Typing.ExpireTime, item.Purification.Typ);
            Assert.Equal(NativeExtra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
            Assert.Equal(71u, item.Purification.ItemUID);
            Assert.Equal(82u, item.Refinary.ItemUID);
            Assert.Equal(8, scope.Packet.Size);
            AssertTail(scope, 8);
        });

    [Fact]
    public Task LateRefineryExpiryFaultRetainsEarlierPurificationMutationWithoutWritingOrSending() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = Item();
            item.Purification = Purification();
            item.Refinary = Refinery();
            item.Refinary.EffectDuration = 1;
            item.Refinary.AddedOn = DateTime.MaxValue;

            Assert.Throws<ArgumentOutOfRangeException>(() => item.SendItemExtra(scope.Client, scope.Packet));

            Assert.True(PurificationAvailable(item.Purification));
            Assert.Equal(NativeExtra.Typing.Stabilization, item.Purification.Typ);
            Assert.False(item.Refinary.Available);
            Assert.Equal(NativeExtra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
            AssertTail(scope, 0);
        });

    [Fact]
    public Task NativeExtraConstructorOwnsDistinctListsAndEmptyCreateArrayReturnsOriginalStream() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var first = new NativeExtra();
            var second = new NativeExtra();
            Assert.NotSame(first.Purifications, second.Purifications);
            Assert.NotSame(first.Refinerys, second.Refinerys);
            Assert.Equal((byte)0, NativeExtra.Artefacts.Refinary);
            Assert.Equal((byte)1, NativeExtra.Artefacts.Purification);
            Assert.Same(scope.Packet, first.CreateArray(scope.Packet));
            Assert.Equal(Convert.FromHexString("0800560800000000"), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Empty(first.Purifications);
            Assert.Empty(first.Refinerys);
            Assert.Equal(4, scope.Packet.Position);
            AssertTail(scope, 8);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NativeCreateArrayPreservesTypesZeroUidAndCopiesWhileMappingEveryEffectRangeEdge(bool interserver) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var message = new NativeExtra();
            var purification = Purification(0);
            purification.Typ = (NativeExtra.Typing)uint.MaxValue;
            message.Purifications.Add(purification);
            uint[] effectIds =
            [
                724439, 724440, 724444, 724445,
                3004139, 3004140, 3004141,
                3006164, 3006165, 3006170, 3006171, uint.MaxValue,
            ];
            uint[] clientEffects =
            [
                724439, 301, 301, 724445,
                3004139, 301, 3004141,
                3006164, 301, 301, 3006171, uint.MaxValue,
            ];
            for (int index = 0; index < effectIds.Length; index++)
            {
                var refinery = Refinery((uint)index + 82);
                refinery.EffectID = effectIds[index];
                message.Refinerys.Add(refinery);
            }

            message.CreateArray(scope.Packet, interserver);

            var words = new List<uint> { 13, 0, uint.MaxValue, 800001, 3, 23, 0, 0, 0 };
            for (int index = 0; index < effectIds.Length; index++)
                words.AddRange([(uint)index + 82, 4,
                    interserver ? effectIds[index] : clientEffects[index], 5, 11, 22, 0]);
            Assert.Equal(ExtraFrame(words.ToArray()), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.False(PurificationAvailable(message.Purifications[0]));
            Assert.All(message.Refinerys, value => Assert.False(value.Available));
            Assert.Equal((NativeExtra.Typing)uint.MaxValue, message.Purifications[0].Typ);
            Assert.Equal(effectIds, message.Refinerys.Select(value => value.EffectID).ToArray());
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task NativeCreateArrayNullListsFaultAfterInitWriterBeforeWritingHeaderOrCount() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            foreach (bool purification in new[] { false, true })
            {
                scope.ResetBuffer();
                var message = new NativeExtra();
                if (purification) message.Purifications = null!;
                else message.Refinerys = null!;
                Assert.Throws<NullReferenceException>(() => message.CreateArray(scope.Packet));
                Assert.Equal(4, scope.Packet.Position);
                Assert.Equal(321, scope.Packet.Size);
                AssertTail(scope, 0);
            }
        });

    [Fact]
    public Task NativeCreateArrayExpiryFaultLeavesWrittenPrefixButNoFinalHeaderOrListMutation() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var message = new NativeExtra();
            var purification = Purification();
            purification.PurificationDuration = 1;
            purification.AddedOn = DateTime.MaxValue;
            message.Purifications.Add(purification);

            Assert.Throws<ArgumentOutOfRangeException>(() => message.CreateArray(scope.Packet));

            byte[] expected = Enumerable.Repeat((byte)0xA5, 48).ToArray();
            ExtraFrame(1, 71, 7, 800001, 3, 23, 1).AsSpan(4).CopyTo(expected.AsSpan(4));
            Assert.Equal(expected, scope.Memory(48));
            Assert.Equal(32, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
            Assert.False(PurificationAvailable(message.Purifications[0]));
            Assert.Equal(NativeExtra.Typing.ExpireTime, message.Purifications[0].Typ);
            Assert.Empty(scope.Queue);
        });
}
