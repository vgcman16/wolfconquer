using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;
using Outcome = WolfConquer.Core.Data.LegacyNativeRoleQuests.Outcome;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Fact]
    public void RoleQuestsRetainsNativeImplicitConstructionWithoutStaticWorkerBirth() =>
        Assert.NotNull(new LegacyNativeRoleQuests());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QuestMissingOwnerRejectsWithoutCreatingOneOrTouchingFiles(bool save) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles(createQuestDirectory: false);
            scope.Player.QuestGUI = null;
            var result = save ? LegacyNativeRoleQuests.TrySave(scope.Client) : LegacyNativeRoleQuests.TryLoad(scope.Client);
            Assert.Equal(Outcome.Rejected, result.Status);
            Assert.Null(scope.Player.QuestGUI);
            Assert.False(File.Exists(scope.QuestPath));
            Assert.False(Directory.Exists(Path.Combine(scope.OwnedDirectory!, "Quests")));
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task SaveWritesExactNativeCountRawStatusAndVariableIntentions() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var row = scope.Row(uint.MaxValue, uint.MaxValue, (Status)99, 0, uint.MaxValue, 7);
        row.Time = 0xAABBCCDD;
        var accepted = scope.Row(600, 600, Status.Accepted);
        scope.Quests.AcceptedQuests[600] = accepted;
        File.WriteAllBytes(scope.QuestPath, new byte[999]);
        LegacyNativeRoleQuests.Save(scope.Client);
        Assert.Equal(QuestFile((uint.MaxValue, 99u, 0xAABBCCDDu, new uint[] { 0, uint.MaxValue, 7 }),
            (600u, 0u, 123u, Array.Empty<uint>())), File.ReadAllBytes(scope.QuestPath));
        Assert.Same(row, scope.Quests.src[uint.MaxValue]);
        Assert.Same(accepted, Assert.Single(scope.Quests.AcceptedQuests).Value);
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

    [Theory]
    [InlineData("nullRow")]
    [InlineData("nullIntentions")]
    [InlineData("keyMismatch")]
    [InlineData("duplicatePhysical")]
    [InlineData("acceptedOnly")]
    [InlineData("foreignAlias")]
    [InlineData("finishedAccepted")]
    [InlineData("aliasedMaps")]
    public Task SaveMalformedCompleteBookRejectsWithoutTruncationOrLeaks(string defect) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var row = scope.Row(600, 600, Status.Accepted, 7);
            scope.Quests.AcceptedQuests[600] = row;
            switch (defect)
            {
                case "nullRow": scope.Quests.src[600] = null!; break;
                case "nullIntentions": row.Intentions = null!; break;
                case "keyMismatch": row.UID = 999; break;
                case "duplicatePhysical": scope.Quests.src[601] = row; break;
                case "acceptedOnly": scope.Quests.AcceptedQuests[700] = new Row { UID = 700 }; break;
                case "foreignAlias": scope.Quests.AcceptedQuests[600] = new Row { UID = 600 }; break;
                case "finishedAccepted": row.Status = Status.Finished; break;
                case "aliasedMaps": scope.Quests.AcceptedQuests = scope.Quests.src; break;
            }
            byte[] before = [1, 3, 5, 7, 9];
            File.WriteAllBytes(scope.QuestPath, before);
            var source = scope.Quests.src;
            var accepted = scope.Quests.AcceptedQuests;
            Assert.Equal(Outcome.Rejected, LegacyNativeRoleQuests.TrySave(scope.Client).Status);
            Assert.Equal(before, File.ReadAllBytes(scope.QuestPath));
            Assert.Same(source, scope.Quests.src);
            Assert.Same(accepted, scope.Quests.AcceptedQuests);
            using var exclusive = new FileStream(scope.QuestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task LoadPublishesCompleteRowsCanonicalAcceptedAndRetainsNonconflictingRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var retained = scope.Row(500, 500, Status.Finished, 99);
            Assert.Equal(Outcome.Committed, LoadOwned(scope, QuestFile(
                (600u, 0u, 100u, new uint[] { 1, 2 }),
                (700u, 2u, 200u, Array.Empty<uint>()),
                (800u, 99u, 300u, new uint[] { uint.MaxValue }))).Status);
            Assert.Equal(4, scope.Quests.src.Count);
            Assert.Same(retained, scope.Quests.src[500]);
            Assert.Same(scope.Quests.src[600], Assert.Single(scope.Quests.AcceptedQuests).Value);
            Assert.Equal(new uint[] { 1, 2 }, scope.Quests.src[600].Intentions);
            Assert.Empty(scope.Quests.src[700].Intentions);
            Assert.Equal((Status)99, scope.Quests.src[800].Status);
            Assert.Empty(scope.Queue);
            using var exclusive = new FileStream(scope.QuestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task IdenticalOverlapRetainsExistingCanonicalRowAndIndexesLoadedAccepted(bool indexed) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var retained = scope.Row(600, 600, Status.Accepted, 1, 2);
            retained.Time = 100;
            if (indexed) scope.Quests.AcceptedQuests[600] = retained;
            Assert.Equal(Outcome.Committed, LoadOwned(scope, QuestFile((600u, 0u, 100u, new uint[] { 1, 2 }))).Status);
            Assert.Same(retained, Assert.Single(scope.Quests.src).Value);
            Assert.Same(retained, Assert.Single(scope.Quests.AcceptedQuests).Value);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ConflictingOverlapRejectsWholeLoadWithoutSplitAliases(int difference) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var retained = scope.Row(600, 600, Status.Accepted, 1, 2);
            retained.Time = 100;
            scope.Quests.AcceptedQuests[600] = retained;
            uint status = difference == 0 ? 1u : 0u;
            uint time = difference == 1 ? 200u : 100u;
            uint[] values = difference == 2 ? [1, 3] : difference == 3 ? [1] : [1, 2];
            var source = scope.Quests.src;
            var accepted = scope.Quests.AcceptedQuests;
            Assert.Equal(Outcome.Rejected, LoadOwned(scope, QuestFile(
                (700u, 1u, 10u, new uint[] { 9 }), (600u, status, time, values))).Status);
            Assert.Same(source, scope.Quests.src);
            Assert.Same(accepted, scope.Quests.AcceptedQuests);
            Assert.Same(retained, Assert.Single(source).Value);
            Assert.Same(retained, Assert.Single(accepted).Value);
            Assert.Equal(new uint[] { 1, 2 }, retained.Intentions);
            Assert.Equal(100u, retained.Time);
        });

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    public Task DuplicateDiskUidRejectsWholeLoadInsteadOfPublishingFirstRow(uint status) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var source = scope.Quests.src;
            var accepted = scope.Quests.AcceptedQuests;
            Assert.Equal(Outcome.Rejected, LoadOwned(scope, QuestFile(
                (600u, status, 100u, new uint[] { 1 }), (600u, status, 200u, new uint[] { 2 }),
                (700u, 1u, 300u, new uint[] { 3 }))).Status);
            Assert.Same(source, scope.Quests.src);
            Assert.Same(accepted, scope.Quests.AcceptedQuests);
            Assert.Empty(source);
            Assert.Empty(accepted);
        });

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public Task InvalidSignedRowCountRejectsUnchanged(int count) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var retained = scope.Row(600, 600, Status.Accepted, 7);
        scope.Quests.AcceptedQuests[600] = retained;
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        Assert.Equal(Outcome.Rejected, LoadOwned(scope, bytes).Status);
        Assert.Same(retained, Assert.Single(scope.Quests.src).Value);
        Assert.Same(retained, Assert.Single(scope.Quests.AcceptedQuests).Value);
    });

    [Fact]
    public Task ValidZeroCountRetainsExistingRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var retained = scope.Row(600, 600, Status.Accepted, 7);
        Assert.Equal(Outcome.Committed, LoadOwned(scope, new byte[4]).Status);
        Assert.Same(retained, Assert.Single(scope.Quests.src).Value);
        Assert.Empty(scope.Quests.AcceptedQuests); // Existing Kingdom-style subset stays valid.
    });

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public Task InvalidIntentionLengthRejectsBeforeAnyPublication(int length) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var bytes = QuestFile((600u, 0u, 100u, Array.Empty<uint>()));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), length);
        Assert.Equal(Outcome.Rejected, LoadOwned(scope, bytes).Status);
        Assert.Empty(scope.Quests.src);
        Assert.Empty(scope.Quests.AcceptedQuests);
    });
}
