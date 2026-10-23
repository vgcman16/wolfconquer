using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Fact]
    public void RoleQuestsRetainsNativeImplicitConstructionWithoutStaticWorkerBirth() =>
        Assert.NotNull(new LegacyNativeRoleQuests());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QuestOpenFailureReturnsBeforeDereferencingMissingQuestOwner(bool save) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles(createQuestDirectory: false);
            scope.Player.QuestGUI = null;

            if (save) LegacyNativeRoleQuests.Save(scope.Client);
            else LegacyNativeRoleQuests.Load(scope.Client);

            Assert.Null(scope.Player.QuestGUI);
            Assert.False(File.Exists(scope.QuestPath));
            Assert.False(Directory.Exists(Path.Combine(scope.OwnedDirectory!, "Quests")));
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task SaveWritesExactNativeCountAndUnframedVariableIntentionsAndIgnoresAcceptedOnlyRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var row = scope.Row(9, uint.MaxValue, (Status)99, 0, uint.MaxValue, 7);
            row.Time = 0xAABBCCDD;
            scope.Quests.AcceptedQuests[600] = new() { UID = 600, Intentions = [42] };
            File.WriteAllBytes(scope.QuestPath, new byte[999]);

            LegacyNativeRoleQuests.Save(scope.Client);

            Assert.Equal(QuestFile((uint.MaxValue, 99u, 0xAABBCCDDu, new uint[] { 0, uint.MaxValue, 7 })),
                File.ReadAllBytes(scope.QuestPath));
            Assert.Same(row, scope.Quests.src[9]);
            Assert.Equal(600u, Assert.Single(scope.Quests.AcceptedQuests).Value.UID);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task EmptySaveReplacesOldFileWithOnlySignedZeroCount() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        File.WriteAllBytes(scope.QuestPath, Enumerable.Repeat((byte)0xA5, 999).ToArray());
        LegacyNativeRoleQuests.Save(scope.Client);
        Assert.Equal(new byte[4], File.ReadAllBytes(scope.QuestPath));
    });

    [Fact]
    public Task SaveCountsDictionaryEntriesAndRetainsDuplicateValueUidRatherThanDeduplicating() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var row = scope.Row(10, 600, Status.Finished, 1);
            scope.Quests.src[11] = row;
            LegacyNativeRoleQuests.Save(scope.Client);
            var expected = QuestFile((600u, 1u, 123u, new uint[] { 1 }), (600u, 1u, 123u, new uint[] { 1 }));
            Assert.Equal(expected, File.ReadAllBytes(scope.QuestPath));
            Assert.Same(scope.Quests.src[10], scope.Quests.src[11]);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SaveBodyFaultLeavesCountPrefixInsteadOfRollingBackOrClosingInFinally(bool nullRow) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            if (nullRow) scope.Quests.src[600] = null!;
            else scope.Row(600, 600, Status.Accepted).Intentions = null!;
            var file = new LegacyNativeBinaryFile();
            Assert.True(file.Open(scope.QuestPath, FileMode.Create));
            try
            {
                Assert.Throws<NullReferenceException>(() => LegacyNativeRoleQuests.SaveOpenedFile(file, scope.Client));
            }
            finally { file.Close(); }
            Assert.Equal(new byte[] { 1, 0, 0, 0 }, File.ReadAllBytes(scope.QuestPath));
            Assert.Single(scope.Quests.src);
        });

    [Fact]
    public Task LoadPublishesActualNewRowsAndSharedAcceptedAliasWithoutClearingPriorRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var retained = scope.Row(500, 500, Status.Finished, 99);
            LoadOwned(scope, QuestFile(
                (600u, 0u, 100u, new uint[] { 1, 2 }),
                (700u, 2u, 200u, Array.Empty<uint>()),
                (800u, 99u, 300u, new uint[] { uint.MaxValue })));
            Assert.Equal(4, scope.Quests.src.Count);
            Assert.Same(retained, scope.Quests.src[500]);
            Assert.Same(scope.Quests.src[600], Assert.Single(scope.Quests.AcceptedQuests).Value);
            Assert.Equal(new uint[] { 1, 2 }, scope.Quests.src[600].Intentions);
            Assert.Empty(scope.Quests.src[700].Intentions);
            Assert.Equal((Status)99, scope.Quests.src[800].Status);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task LoadExistingSourceKeyKeepsOldRowButAcceptedMapGetsNewLoadedObject() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var retained = scope.Row(600, 999, Status.Finished, 99);
            LoadOwned(scope, QuestFile((600u, 0u, 100u, new uint[] { 1, 2 })));
            Assert.Same(retained, scope.Quests.src[600]);
            var accepted = Assert.Single(scope.Quests.AcceptedQuests).Value;
            Assert.NotSame(retained, accepted);
            Assert.Equal(600u, accepted.UID);
            Assert.Equal(new uint[] { 1, 2 }, accepted.Intentions);
            Assert.Equal(999u, retained.UID);
            Assert.Equal(new uint[] { 99 }, retained.Intentions);
        });

    [Fact]
    public Task DuplicateAcceptedRecordFaultRetainsFirstRealRowAndStopsBeforeLaterRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            Assert.Throws<ArgumentException>(() => LoadOwned(scope, QuestFile(
                (600u, 0u, 100u, new uint[] { 1 }),
                (600u, 0u, 200u, new uint[] { 2 }),
                (700u, 1u, 300u, new uint[] { 3 }))));
            var first = Assert.Single(scope.Quests.src).Value;
            Assert.Equal(100u, first.Time);
            Assert.Equal(new uint[] { 1 }, first.Intentions);
            Assert.Same(first, Assert.Single(scope.Quests.AcceptedQuests).Value);
            Assert.False(scope.Quests.src.ContainsKey(700));
        });

    [Fact]
    public Task FinishedLoadedRowDoesNotRemovePreexistingAcceptedAlias() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var accepted = new Row { UID = 700, Status = Status.Accepted, Intentions = [7] };
        scope.Quests.AcceptedQuests[700] = accepted;
        LoadOwned(scope, QuestFile((700u, 1u, 100u, new uint[] { 2 })));
        Assert.Equal(Status.Finished, scope.Quests.src[700].Status);
        Assert.Same(accepted, scope.Quests.AcceptedQuests[700]);
        Assert.NotSame(accepted, scope.Quests.src[700]);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public Task NonpositiveFileCountRetainsExistingCollectionsWithoutRecoveryOrRejection(int count) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var retained = scope.Row(600, 600, Status.Accepted, 7);
            scope.Quests.AcceptedQuests[600] = retained;
            var bytes = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
            LoadOwned(scope, bytes);
            Assert.Same(retained, Assert.Single(scope.Quests.src).Value);
            Assert.Same(retained, Assert.Single(scope.Quests.AcceptedQuests).Value);
        });

    [Fact]
    public Task EmptyNativeReadRetainsInitialCurrentCountAndLoadsDefaultZeroRecord() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var retained = scope.Row(999, 999, Status.Finished, 7);
            LoadOwned(scope, []);
            Assert.Equal(2, scope.Quests.src.Count);
            Assert.Same(retained, scope.Quests.src[999]);
            var zero = scope.Quests.src[0];
            Assert.Equal(0u, zero.UID);
            Assert.Equal(Status.Accepted, zero.Status);
            Assert.Empty(zero.Intentions);
            Assert.Same(zero, Assert.Single(scope.Quests.AcceptedQuests).Value);
        });

    [Fact]
    public Task NegativeIntentionLengthFaultsBeforePublishingThatRow() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var bytes = QuestFile((600u, 0u, 100u, Array.Empty<uint>()));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), -1);
        Assert.Throws<OverflowException>(() => LoadOwned(scope, bytes));
        Assert.Empty(scope.Quests.src);
        Assert.Empty(scope.Quests.AcceptedQuests);
    });

    [Fact]
    public Task LoadBodyRequiresActualQuestOwnerAndDoesNotAllocateOne() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        scope.Player.QuestGUI = null;
        Assert.Throws<NullReferenceException>(() => LoadOwned(scope, new byte[4]));
        Assert.Null(scope.Player.QuestGUI);
        Assert.Empty(scope.Queue);
    });
}
