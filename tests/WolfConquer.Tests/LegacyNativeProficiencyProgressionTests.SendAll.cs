using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeProficiencyProgressionTests
{
    [Fact]
    public Task SendAllEnumeratesActualValuesInNativeOrderUsesValueIdsAndDoesNotRewriteStoredNeeded() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var first = scope.Record(level: 6, experience: 333, needed: 4242, previous: 2, key: 17, id: 420);
            var second = scope.Record(level: 7, experience: 444, needed: 9999, previous: 3, key: 99, id: 410);
            uint[] order = scope.Storage.ClientProf.Values.Select(value => value.ID).ToArray();

            scope.Storage.SendAll(scope.Packet);

            byte[][] sent = scope.Queue.ToArray();
            Assert.Equal(2, sent.Length);
            Assert.Equal(order, sent.Select(bytes => Read32(bytes, 4)).ToArray());
            Assert.Contains(sent, bytes => bytes.SequenceEqual(Proficiency(420, 6, 333, 4000000)));
            Assert.Contains(sent, bytes => bytes.SequenceEqual(Proficiency(410, 7, 444, 10000000)));
            Assert.Same(first, scope.Storage.ClientProf[17]);
            Assert.Same(second, scope.Storage.ClientProf[99]);
            AssertRecord(first, 420, 6, 333, 4242, 2);
            AssertRecord(second, 410, 7, 444, 9999, 3);
            Assert.Equal(sent[^1], NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position);
        });

    [Fact]
    public Task SendAllNullStoredValueFaultsBeforeSerializationAndDoesNotRepairStorage() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Storage.ClientProf[17] = null!;
            Assert.Throws<NullReferenceException>(() => scope.Storage.SendAll(scope.Packet));
            Assert.Null(scope.Storage.ClientProf[17]);
            Assert.Single(scope.Storage.ClientProf);
            AssertUntouched(scope);
        });
}
