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
            Assert.Same(original, scope.Storage.ClientProf[410]);
            AssertRecord(original, 410, 2, 0, 1200);
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
            AssertRecord(loaded, 410, 2, 100, 1200);
            AssertFrames(scope, Experience(100, 68000, 410));
            LegacyNativeServerDatabasePersistence.SaveClientProfs(reloaded);
            AssertSaved(owned, 410, 2, 100, 1200);
            AssertRecord(original, 410, 2, 0, 1200);
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
            Assert.Same(original, scope.Storage.ClientProf[410]);
            AssertRecord(original, 410, 10, 0, 640000, 10);
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
    public Task ProgressionUsesQueryKeyButBinaryReloadRekeysByFullWidthPhysicalRecordId() =>
        NativePacketTestThread.Run(() =>
        {
            using var owned = new LegacyNativeClientProficiencyPersistenceTests.OwnedProfs();
            using var scope = new Scope();
            const uint physicalId = 0x10000u + 410u;
            scope.Storage.Add(scope.Packet, 410, 1, 1100);
            var original = scope.Storage.ClientProf[410];
            original.ID = physicalId; // Real mutable value; dictionary key deliberately remains 410.
            scope.Queue.Clear();

            scope.Storage.CheckUpdate(410, 2, scope.Packet);
            Assert.Same(original, scope.Storage.ClientProf[410]);
            Assert.False(scope.Storage.ClientProf.ContainsKey(physicalId));
            AssertRecord(original, physicalId, 2, 0, 1200);
            AssertFrames(scope, LevelMessage, Proficiency(physicalId, 2, 0, 68000),
                Experience(0, 68000, physicalId));
            LegacyNativeServerDatabasePersistence.SaveClientProfs(scope.Client);
            AssertSaved(owned, physicalId, 2, 0, 1200);

            scope.Queue.Clear();
            var reloaded = scope.NewClient();
            LegacyNativeServerDatabasePersistence.LoadClientProfs(reloaded);
            Assert.Empty(scope.Queue);
            var storage = reloaded.MyProfs!;
            var pair = Assert.Single(storage.ClientProf);
            Assert.Equal(physicalId, pair.Key);
            Assert.NotSame(original, pair.Value);
            Assert.False(storage.ClientProf.ContainsKey(410));
            Assert.False(storage.CheckProf(410, 2)); // ushort query does not truncate a uint map key.
            AssertRecord(pair.Value, physicalId, 2, 0, 1200);
            AssertSaved(owned, physicalId, 2, 0, 1200);
            storage.SendAll(scope.Packet);
            AssertFrames(scope, Proficiency(physicalId, 2, 0, 68000));
            AssertRecord(pair.Value, physicalId, 2, 0, 1200);
        });
}
