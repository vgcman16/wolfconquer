using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;
using Status = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem.QuestStatus;
using Outcome = WolfConquer.Core.Data.LegacyNativeRoleQuests.Outcome;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Fact]
    public Task FirstQuestSaveAndReloadRetainNativeZeroUidAndMoreThanFiveAcceptedRows() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            for (uint uid = 0; uid < 6; uid++) scope.Row(uid, uid, Status.Accepted, uid, uint.MaxValue);
            scope.Quests.AcceptedQuests[0] = scope.Quests.src[0];
            Assert.False(File.Exists(scope.QuestPath));
            Assert.Equal(Outcome.Committed, LegacyNativeRoleQuests.TrySave(scope.Client).Status);
            var expected = Enumerable.Range(0, 6).Select(uid =>
                ((uint)uid, 0u, 123u, new uint[] { (uint)uid, uint.MaxValue })).ToArray();
            Assert.Equal(QuestFile(expected), File.ReadAllBytes(scope.QuestPath));
            scope.Quests.src = new();
            scope.Quests.AcceptedQuests = new();
            Assert.Equal(Outcome.Committed, LegacyNativeRoleQuests.TryLoad(scope.Client).Status);
            Assert.Equal(6, scope.Quests.src.Count);
            Assert.Equal(6, scope.Quests.AcceptedQuests.Count);
            foreach (var pair in scope.Quests.src)
            {
                Assert.Same(pair.Value, scope.Quests.AcceptedQuests[pair.Key]);
                Assert.Equal(new uint[] { pair.Key, uint.MaxValue }, pair.Value.Intentions);
            }
            Assert.Empty(scope.Queue);
            using var exclusive = new FileStream(scope.QuestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });

    [Fact]
    public Task OversizedRealQuestFileRejectsBeforeLoadingRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var retained = scope.Row(600, 600, Status.Finished, 7);
        const long length = (64L * 1024 * 1024) + 1;
        using (var file = new FileStream(scope.QuestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            file.SetLength(length);
        var source = scope.Quests.src;
        var accepted = scope.Quests.AcceptedQuests;
        Assert.Equal(Outcome.Rejected, LegacyNativeRoleQuests.TryLoad(scope.Client).Status);
        Assert.Same(source, scope.Quests.src);
        Assert.Same(accepted, scope.Quests.AcceptedQuests);
        Assert.Same(retained, Assert.Single(source).Value);
        Assert.Equal(length, new FileInfo(scope.QuestPath).Length);
        using var exclusive = new FileStream(scope.QuestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(27)]
    [InlineData(28)]
    [InlineData(47)]
    public Task TruncatedQuestImageRejectsWithoutPublishingDefaultOrPrefixRows(int length) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            var retained = scope.Row(999, 999, Status.Finished, 7);
            var source = scope.Quests.src;
            var accepted = scope.Quests.AcceptedQuests;
            var full = QuestFile((600u, 0u, 100u, new uint[] { 1, 2 }), (700u, 1u, 200u, new uint[] { 3 }));
            Assert.Equal(48, full.Length);
            Assert.Equal(Outcome.Rejected, LoadOwned(scope, full[..length]).Status);
            Assert.Same(source, scope.Quests.src);
            Assert.Same(accepted, scope.Quests.AcceptedQuests);
            Assert.Same(retained, Assert.Single(source).Value);
            Assert.Empty(accepted);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task QuestImageUnconsumedTailRejectsUnchanged() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var bytes = QuestFile((600u, 0u, 100u, new uint[] { 1 }));
        Assert.Equal(Outcome.Rejected, LoadOwned(scope, [.. bytes, 0]).Status);
        Assert.Empty(scope.Quests.src);
        Assert.Empty(scope.Quests.AcceptedQuests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingQuestFileOrDirectoryIsExplicitNonmutatingNotFound(bool directoryExists) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles(directoryExists);
            var source = scope.Quests.src;
            var accepted = scope.Quests.AcceptedQuests;
            Assert.Equal(Outcome.NotFound, LegacyNativeRoleQuests.TryLoad(scope.Client).Status);
            LegacyNativeRoleQuests.Load(scope.Client);
            Assert.Same(source, scope.Quests.src);
            Assert.Same(accepted, scope.Quests.AcceptedQuests);
            Assert.False(File.Exists(scope.QuestPath));
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealQuestSharingDenialRejectsWithoutDestinationOrBookDamage(bool save) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            scope.Row(600, 600, Status.Finished, 7);
            byte[] before = QuestFile((700u, 1u, 100u, new uint[] { 9 }));
            File.WriteAllBytes(scope.QuestPath, before);
            var source = scope.Quests.src;
            var accepted = scope.Quests.AcceptedQuests;
            using (var held = new FileStream(scope.QuestPath, FileMode.Open, FileAccess.Read,
                save ? FileShare.Read : FileShare.None))
            {
                var result = save ? LegacyNativeRoleQuests.TrySave(scope.Client) : LegacyNativeRoleQuests.TryLoad(scope.Client);
                Assert.Equal(Outcome.Rejected, result.Status);
            }
            Assert.Equal(before, File.ReadAllBytes(scope.QuestPath));
            Assert.Same(source, scope.Quests.src);
            Assert.Same(accepted, scope.Quests.AcceptedQuests);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(scope.QuestPath)!));
            using var exclusive = new FileStream(scope.QuestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        });

    [Theory]
    [InlineData("backlink")]
    [InlineData("constructor")]
    [InlineData("zeroUid")]
    [InlineData("configuration")]
    public Task QuestPhysicalOwnerAdmissionRejectsBeforeTouchingExistingFile(string defect) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.OwnFiles();
            string path = scope.QuestPath;
            byte[] before = [5, 4, 3, 2, 1];
            File.WriteAllBytes(path, before);
            switch (defect)
            {
                case "backlink": scope.Quests.Player = null!; break;
                case "constructor":
                    var foreign = new LegacyNativeQuestsRuntime(null!);
                    foreign.Player = scope.Player;
                    scope.Player.QuestGUI = foreign;
                    break;
                case "zeroUid": scope.Player.UID = 0; break;
                case "configuration": LegacyNativeServerConfig.DbLocation = "relative-root"; break;
            }
            Assert.Equal(Outcome.Rejected, LegacyNativeRoleQuests.TrySave(scope.Client).Status);
            Assert.Equal(Outcome.Rejected, LegacyNativeRoleQuests.TryLoad(scope.Client).Status);
            Assert.Equal(before, File.ReadAllBytes(path));
            Assert.Empty(scope.Queue);
        });

    private sealed class QuestHashComparer : IEqualityComparer<uint>
    {
        internal Action? OnHash;
        internal bool ChangingHash;
        internal bool Collapse;
        private int calls;
        public bool Equals(uint left, uint right) => Collapse || left == right;
        public int GetHashCode(uint value)
        {
            OnHash?.Invoke();
            return Collapse ? 0 : unchecked((int)value + (ChangingHash ? calls++ : 0));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task PhysicalDuplicateAcceptedUidRejectsEvenWithChangingHash(bool save) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var row = scope.Row(600, 600, Status.Accepted, 7);
        var comparer = new QuestHashComparer { ChangingHash = true };
        var accepted = new Dictionary<uint, Row>(comparer);
        accepted.Add(600, row);
        accepted.Add(600, row);
        Assert.Equal(2, accepted.Count);
        scope.Quests.AcceptedQuests = accepted;
        byte[] before = QuestFile((700u, 1u, 100u, new uint[] { 9 }));
        File.WriteAllBytes(scope.QuestPath, before);
        var result = save ? LegacyNativeRoleQuests.TrySave(scope.Client) : LegacyNativeRoleQuests.TryLoad(scope.Client);
        Assert.Equal(Outcome.Rejected, result.Status);
        Assert.Same(accepted, scope.Quests.AcceptedQuests);
        Assert.Equal(before, File.ReadAllBytes(scope.QuestPath));
    });

    [Fact]
    public Task CollapsingAcceptedComparerCannotLoseLoadedCanonicalUid() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var source = scope.Quests.src;
        var accepted = new Dictionary<uint, Row>(new QuestHashComparer { Collapse = true });
        scope.Quests.AcceptedQuests = accepted;
        Assert.Equal(Outcome.Rejected, LoadOwned(scope, QuestFile(
            (600u, 0u, 100u, new uint[] { 1 }), (700u, 0u, 200u, new uint[] { 2 }))).Status);
        Assert.Same(source, scope.Quests.src);
        Assert.Same(accepted, scope.Quests.AcceptedQuests);
        Assert.Empty(source);
        Assert.Empty(accepted);
    });

    [Fact]
    public Task LateRealComparerOwnerDriftCannotPublishPreparedQuestMaps() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var comparer = new QuestHashComparer();
        var retained = scope.Row(600, 600, Status.Finished, 7);
        var source = new Dictionary<uint, Row>(comparer) { [600] = retained };
        scope.Quests.src = source;
        var accepted = scope.Quests.AcceptedQuests;
        uint uid = scope.Player.UID;
        comparer.OnHash = () => { comparer.OnHash = null; scope.Player.UID = uid + 1; };
        Assert.Equal(Outcome.Rejected, LoadOwned(scope, QuestFile((700u, 1u, 200u, new uint[] { 9 }))).Status);
        Assert.Same(source, scope.Quests.src);
        Assert.Same(accepted, scope.Quests.AcceptedQuests);
        Assert.Same(retained, Assert.Single(source).Value);
        Assert.Empty(accepted);
    });

    [Fact]
    public Task MergedQuestBookMustRemainRepresentableByTheSameSaveBound() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.OwnFiles();
        var retained = scope.Row(600, 600, Status.Finished);
        retained.Intentions = new uint[((64 * 1024 * 1024) / 4) - 8];
        var source = scope.Quests.src;
        var accepted = scope.Quests.AcceptedQuests;
        Assert.Equal(Outcome.Rejected, LoadOwned(scope, QuestFile((700u, 1u, 200u, Array.Empty<uint>()))).Status);
        Assert.Same(source, scope.Quests.src);
        Assert.Same(accepted, scope.Quests.AcceptedQuests);
        Assert.Same(retained, Assert.Single(source).Value);
        Assert.Empty(accepted);
    });
}
