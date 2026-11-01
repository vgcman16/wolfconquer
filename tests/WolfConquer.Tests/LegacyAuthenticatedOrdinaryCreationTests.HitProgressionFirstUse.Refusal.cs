using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData("insert-profs")]
    [InlineData("delete-profs")]
    [InlineData("insert-spells")]
    public Task TheActualAdmissionOriginRefusesLaterEmptyBookInsertionOrDeletionBeforeEffects(string corruption) =>
        RunFirstUseHitAsync(corruption == "insert-spells", corruption != "delete-profs", corruption == "insert-spells", async f =>
        {
            // Declared hostile file drift AFTER genuine admission. Its empty
            // semantic book matches native state, but its saved origin does not.
            var original = f.Owner.Data.Files();
            var path = corruption == "insert-spells" ? f.SpellsPath : f.ProfsPath;
            if (corruption == "delete-profs") File.Delete(path);
            else File.WriteAllBytes(path, new byte[4]);
            var before = CaptureFirstUseRefusal(f);
            await f.AttackAsync();
            AssertFirstUseRefused(f, before);
            Assert.Contains("reason=basicPhysicalFilePreflight:InvalidOperationException", f.Owner.Log.ToString(), StringComparison.Ordinal);
            // Restoring the declared original input does not issue a new owner.
            // The still-current real admission origin must authorize the next hit.
            var key = Path.GetRelativePath(f.Owner.Data.Root, path);
            if (original.TryGetValue(key, out var image)) File.WriteAllBytes(path, image);
            else File.Delete(path);
            f.Clock.Advance(); await f.AttackAsync(); AssertFirstUseNativeBook(f);
            Assert.False(f.Owner.Runtime.Recovery.IsBlocked);
        });

    [WindowsTradeJournalFailureFact]
    public Task FirstUseWritablePreflightFailureLeavesBothMissingFilesAbsentThenTheRealHitCreatesThem() =>
        RunFirstUseHitAsync(true, true, true, async f =>
        {
            var before = CaptureFirstUseRefusal(f);
            using (var denyWrite = new FileStream(f.CharacterPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await f.AttackAsync(); AssertFirstUseRefused(f, before);
                Assert.False(File.Exists(f.ProfsPath)); Assert.False(File.Exists(f.SpellsPath));
                Assert.Contains("physicalHitProgressionWrite:IOException", f.Owner.Log.ToString(), StringComparison.Ordinal);
            }
            f.Clock.Advance(); await f.AttackAsync(); AssertFirstUseNativeBook(f);
            Assert.True(File.Exists(f.ProfsPath)); Assert.Equal(new byte[4], File.ReadAllBytes(f.SpellsPath));
            Assert.False(f.Owner.Runtime.Recovery.IsBlocked);
        });
}
