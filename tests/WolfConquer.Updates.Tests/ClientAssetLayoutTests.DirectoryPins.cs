using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Updates.ClientLayout;

namespace WolfConquer.Updates.Tests;

public sealed partial class ClientAssetLayoutTests
{
    [WindowsTheory]
    [InlineData("link-parent")]
    [InlineData("target")]
    [InlineData("target-ancestor")]
    public void ActualParentPinsRejectOrdinaryRenameUntilTheirLeaseIsDisposed(string selected) =>
        WithParentPinFixture(fixture =>
        {
            var parent = Directory.CreateDirectory(Path.Combine(fixture.Client, "links")).FullName;
            var targetParent = Directory.CreateDirectory(Path.Combine(fixture.Root, "targets")).FullName;
            var target = Directory.CreateDirectory(Path.Combine(targetParent, "data")).FullName;
            var link = Path.Combine(parent, "new-link");
            var keep = Path.Combine(target, "keep.bin");
            File.WriteAllBytes(keep, [1, 9, 3, 7]);
            var directory = selected switch
            {
                "link-parent" => parent,
                "target" => target,
                "target-ancestor" => targetParent,
                _ => throw new ArgumentOutOfRangeException(nameof(selected)),
            };
            var moved = directory + "-moved";
            using (RetainActualParentPins(link, target))
            {
                var failure = Record.Exception(() => Directory.Move(directory, moved));
                if (failure is null)
                {
                    // Baseline RED must not strand moved fixture paths. Restore
                    // only this exact owned ordinary directory before failing.
                    try { Directory.Move(moved, directory); }
                    catch (Exception restore)
                    {
                        throw new AggregateException(new InvalidOperationException(
                            "A retained updater parent unexpectedly permitted an ordinary rename."), restore);
                    }
                }
                Assert.IsAssignableFrom<IOException>(failure);
                Assert.True(Directory.Exists(directory));
                Assert.False(Directory.Exists(moved));
                Assert.False(Directory.Exists(link));
                Assert.Equal(new byte[] { 1, 9, 3, 7 }, File.ReadAllBytes(keep));
            }
            // Genuine permission/release control: the exact same move must now
            // succeed; a pre-existing ACL refusal cannot satisfy the regression.
            Directory.Move(directory, moved);
            Directory.Move(moved, directory);
            Assert.Equal(new byte[] { 1, 9, 3, 7 }, File.ReadAllBytes(keep));
        });

    [WindowsFact]
    public void ActualParentPinsPreserveChildReplacementAndJunctionCreateExactDelete() =>
        WithParentPinFixture(fixture =>
        {
            var parent = Directory.CreateDirectory(Path.Combine(fixture.Client, "links")).FullName;
            var target = Directory.CreateDirectory(Path.Combine(fixture.Root, "target")).FullName;
            var link = Path.Combine(parent, "new-link");
            var keep = Path.Combine(target, "keep.bin");
            var replacement = Path.Combine(target, "replacement.tmp");
            File.WriteAllBytes(keep, [1, 9, 3, 7]);
            using (RetainActualParentPins(link, target))
            {
                File.WriteAllBytes(replacement, [8, 6, 4, 2]);
                File.Replace(replacement, keep, destinationBackupFileName: null, ignoreMetadataErrors: true);
                Assert.False(File.Exists(replacement));
                Assert.Equal(new byte[] { 8, 6, 4, 2 }, File.ReadAllBytes(keep));
                // Actual public-within-assembly junction operations, not a copied
                // FSCTL/PInvoke or a test callback accepting a pin or identity.
                fixture.Junction(link, target);
                Assert.True(ClientDirectoryJunction.IsExact(link, target));
                Assert.Equal(new byte[] { 8, 6, 4, 2 }, File.ReadAllBytes(Path.Combine(link, "keep.bin")));
                ClientDirectoryJunction.DeleteExact(link, target);
                Assert.False(Directory.Exists(link));
                Assert.Equal(new byte[] { 8, 6, 4, 2 }, File.ReadAllBytes(keep));
            }
            Assert.Equal(new byte[] { 8, 6, 4, 2 }, File.ReadAllBytes(keep));
        });

    private static IDisposable RetainActualParentPins(string link, string target)
    {
        // The synchronous API has no scheduling/fault hook. Invoke the genuine
        // existing private ValidatePair and retain its real DirectoryPins result;
        // this is a parent-pin component control, not an end-to-end race proof.
        var method = typeof(ClientDirectoryJunction).GetMethod("ValidatePair", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        object?[] arguments = [link, target, null, null];
        object? value;
        try { value = method.Invoke(null, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
        return Assert.IsAssignableFrom<IDisposable>(value);
    }

    private static void WithParentPinFixture(Action<LayoutFixture> body)
    {
        var fixture = new LayoutFixture();
        Exception? primary = null;
        try { body(fixture); } catch (Exception error) { primary = error; }
        try { fixture.Dispose(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException("Updater parent-pin test and owned cleanup failed.", primary, cleanup);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
