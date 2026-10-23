using WolfConquer.Core.Data;
using static WolfConquer.Core.Data.WindowsJournalRootIdentity;

namespace WolfConquer.Tests;

public sealed partial class WindowsJournalRootIdentityTests
{
    [WindowsTradeJournalFailureFact]
    public void SameOpenedDirectoryHasSamePhysicalIdentity() => Run(f =>
    {
        using var captured = Capture(f.Users, f.Users + Path.DirectorySeparatorChar + ".", f.Quests);
        Assert.Equal(ObservedRootRelation.SameDirectory, captured.Inspect(RootRole.Users, RootRole.Items));
    });

    [WindowsTradeJournalFailureFact]
    public void ActualAncestorIsRecognizedInBothDirections() => Run(f =>
    {
        using var captured = Capture(f.Users, f.Child, f.Quests);
        Assert.Equal(ObservedRootRelation.FirstIsAncestor, captured.Inspect(RootRole.Users, RootRole.Items));
        Assert.Equal(ObservedRootRelation.SecondIsAncestor, captured.Inspect(RootRole.Items, RootRole.Users));
    });

    [WindowsTradeJournalFailureFact]
    public void SiblingsSharingVolumeAndParentAreNotReportedAsOverlapping() => Run(f =>
    {
        using var captured = Capture(f.Users, f.Items, f.Quests);
        Assert.Equal(ObservedRootRelation.DisjointCapturedChains, captured.Inspect(RootRole.Users, RootRole.Items));
        Assert.Equal(ObservedRootRelation.DisjointCapturedChains, captured.Inspect(RootRole.Items, RootRole.Quests));
    });

    [WindowsTradeJournalFailureFact]
    public void DisposedOwnerCannotOfferEvenAnInspectionResult() => Run(f =>
    {
        var captured = Capture(f.Users, f.Items, f.Quests);
        captured.Dispose();
        captured.Dispose();
        Assert.Throws<ObjectDisposedException>(() => captured.Inspect(RootRole.Users, RootRole.Items));
    });

    [WindowsTradeJournalFailureFact]
    public void IncompleteCaptureReleasesTheAlreadyOpenedRoot() => Run(f =>
    {
        Assert.ThrowsAny<IOException>(() => Capture(f.Users, f.Missing, f.Quests));
        var moved = f.Users + "-moved";
        Directory.Move(f.Users, moved);
        Directory.Move(moved, f.Users);
    });

    [WindowsTradeJournalFailureFact]
    public void OrdinaryFileCannotBecomeAConfiguredDirectory() => Run(f =>
    {
        var file = Path.Combine(f.Items, "not-a-directory");
        File.WriteAllBytes(file, [1, 2, 3]);
        Assert.ThrowsAny<IOException>(() => Capture(f.Users, file, f.Quests));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(file));
    });

    [WindowsTradeJournalFailureFact]
    public void RetainedRootDeniesOrdinaryRenameAndDisposalReleasesThatPin() => Run(f =>
    {
        var moved = f.Users + "-moved";
        using (var captured = Capture(f.Users, f.Items, f.Quests))
        {
            Assert.ThrowsAny<IOException>(() => Directory.Move(f.Users, moved));
            Assert.True(Directory.Exists(f.Users));
            Assert.False(Directory.Exists(moved));
        }
        Directory.Move(f.Users, moved);
        Directory.Move(moved, f.Users);
    });

    [WindowsTradeJournalFailureFact]
    public void DirectoryPinsDoNotRequireRetainingReplaceableLeafHandles() => Run(f =>
    {
        var leaf = Path.Combine(f.Users, "301.ini");
        File.WriteAllBytes(leaf, [1, 2, 3]);
        using var captured = Capture(f.Users, f.Items, f.Quests);
        SnapshotFilePersistence.WriteAllBytesAtomically(leaf, [4, 5, 6], ".root-pin-test-");
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(leaf));
        Assert.Single(Directory.GetFiles(f.Users));
        Assert.Equal(ObservedRootRelation.DisjointCapturedChains, captured.Inspect(RootRole.Users, RootRole.Items));
    });

    [WindowsTradeJournalFailureFact]
    public void UnsupportedNamespaceInputsAndUnknownRolesRefuseExplicitly() => Run(f =>
    {
        foreach (var input in new[] { "relative", @"C:relative", @"\\server\share\Users", @"\\?\C:\Users" })
            Assert.ThrowsAny<ArgumentException>(() => Capture(input, f.Items, f.Quests));
        using var captured = Capture(f.Users, f.Items, f.Quests);
        Assert.Throws<ArgumentOutOfRangeException>(() => captured.Inspect((RootRole)99, RootRole.Users));
    });
}
