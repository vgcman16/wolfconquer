using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeProficiencyPersistenceIntegrationTests
{
    [Fact]
    public Task LevelProgressionPersistsStaleNeededReloadsAndContinuesOnFreshRealOwners() =>
        NativePacketTestThread.Run(() =>
        {
            using var owned = new LegacyNativeClientProficiencyPersistenceTests.OwnedProfs();
            using var scope = new Scope();
            scope.Storage.Add(scope.Packet, 410, 1, 1100);
            var original = scope.Storage.ClientProf[410];
            AssertRecord(original, 410, 1, 1100, 1200);
            AssertFrames(scope, Proficiency(410, 1, 1100, 1200), Experience(1100, 1200, 410));
            scope.Queue.Clear();

            // Native scaling: 2 * 100 / (1 + 1) = 100, exactly the level-one remainder.
            scope.Storage.CheckUpdate(410, 2, scope.Packet);
            Assert.NotSame(original, scope.Storage.ClientProf[410]);
            AssertRecord(original, 410, 1, 1100, 1200);
            AssertRecord(scope.Storage.ClientProf[410], 410, 2, 0, 1200);
            AssertFrames(scope, LevelMessage, Proficiency(410, 2, 0, 68000), Experience(0, 68000, 410));

            LegacyNativeServerDatabasePersistence.SaveClientProfs(scope.Client);
            AssertSaved(owned, 410, 2, 0, 1200);
            scope.Queue.Clear();
            var reloaded = scope.NewClient();
            LegacyNativeServerDatabasePersistence.LoadClientProfs(reloaded);
            Assert.Empty(scope.Queue);
            var storage = reloaded.MyProfs!;
            var loaded = Assert.Single(storage.ClientProf).Value;
            Assert.NotSame(original, loaded);
            AssertRecord(loaded, 410, 2, 0, 1200);
            Assert.True(storage.CheckProf(410, 2));
            Assert.False(storage.CheckProf(410, 3));
            AssertSaved(owned, 410, 2, 0, 1200);

            storage.SendAll(scope.Packet);
            AssertFrames(scope, Proficiency(410, 2, 0, 68000));
            Assert.Equal(1200u, loaded.NeededExperience);
            scope.Queue.Clear();
            storage.CheckUpdate(410, 3, scope.Packet); // 3 * 100 / (2 + 1) = 100.
            Assert.NotSame(loaded, storage.ClientProf[410]);
            AssertRecord(loaded, 410, 2, 0, 1200);
            AssertRecord(storage.ClientProf[410], 410, 2, 100, 1200);
            AssertFrames(scope, Experience(100, 68000, 410));
            LegacyNativeServerDatabasePersistence.SaveClientProfs(reloaded);
            AssertSaved(owned, 410, 2, 100, 1200);
            AssertRecord(original, 410, 1, 1100, 1200);
        });

    [Fact]
    public Task PreviousLevelJumpPersistsAllFiveFieldsAndReloadedSendRecomputesCurrentThreshold() =>
        NativePacketTestThread.Run(() =>
        {
            using var owned = new LegacyNativeClientProficiencyPersistenceTests.OwnedProfs();
            using var scope = new Scope();
            scope.Storage.Add(scope.Packet, 410, 4, 639900, PreviousLevel: 10);
            var original = scope.Storage.ClientProf[410];
            AssertRecord(original, 410, 4, 639900, 640000, 10);
            scope.Queue.Clear();

            // 5 * 100 / (4 + 1) = 100. Level five immediately jumps to previous ten.
            scope.Storage.CheckUpdate(410, 5, scope.Packet);
            Assert.NotSame(original, scope.Storage.ClientProf[410]);
            AssertRecord(original, 410, 4, 639900, 640000, 10);
            AssertRecord(scope.Storage.ClientProf[410], 410, 10, 0, 640000, 10);
            AssertFrames(scope, LevelMessage, Proficiency(410, 10, 0, 90000000),
                Experience(0, 90000000, 410));
            LegacyNativeServerDatabasePersistence.SaveClientProfs(scope.Client);
            AssertSaved(owned, 410, 10, 0, 640000, 10);

            scope.Queue.Clear();
            var reloaded = scope.NewClient();
            LegacyNativeServerDatabasePersistence.LoadClientProfs(reloaded);
            Assert.Empty(scope.Queue);
            var storage = reloaded.MyProfs!;
            var loaded = Assert.Single(storage.ClientProf).Value;
            Assert.NotSame(original, loaded);
            AssertRecord(loaded, 410, 10, 0, 640000, 10);
            Assert.True(storage.CheckProf(410, 10));
            Assert.False(storage.CheckProf(410, 11));
            AssertSaved(owned, 410, 10, 0, 640000, 10);
            storage.SendAll(scope.Packet);
            AssertFrames(scope, Proficiency(410, 10, 0, 90000000));
            AssertRecord(loaded, 410, 10, 0, 640000, 10);
        });

    [Fact]
    public Task MismatchedDictionaryKeyAndPhysicalRecordIdRefusesWithoutSaveOrOutput() =>
        NativePacketTestThread.Run(() =>
        {
            using var owned = new LegacyNativeClientProficiencyPersistenceTests.OwnedProfs();
            using var scope = new Scope();
            const uint physicalId = 0x10000u + 410u;
            scope.Storage.Add(scope.Packet, 410, 1, 1100);
            LegacyNativeServerDatabasePersistence.SaveClientProfs(scope.Client);
            byte[] savedBefore = File.ReadAllBytes(owned.FilePath);
            var original = scope.Storage.ClientProf[410];
            original.ID = physicalId;
            scope.Queue.Clear();
            byte[] packetBefore = NativeSpellTestFixture.Bytes(scope.Packet);
            int position = scope.Packet.Position, size = scope.Packet.Size;
            Assert.Throws<InvalidOperationException>(() => scope.Storage.CheckUpdate(410, 2, scope.Packet));
            Assert.Same(original, scope.Storage.ClientProf[410]);
            AssertRecord(original, physicalId, 1, 1100, 1200);
            Assert.False(scope.Storage.ClientProf.ContainsKey(physicalId));
            Assert.Empty(scope.Queue);
            Assert.Equal(position, scope.Packet.Position);
            Assert.Equal(size, scope.Packet.Size);
            Assert.Equal(packetBefore, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(savedBefore, File.ReadAllBytes(owned.FilePath));
        });
}
