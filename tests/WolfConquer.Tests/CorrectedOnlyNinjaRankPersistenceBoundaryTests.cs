namespace WolfConquer.Tests;

// NEVER run on the old DbRead/DbWrite wrappers: huge allocation, NativeConsole
// diagnostics and malformed-Unicode writer cleanup are outside that baseline.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class CorrectedOnlyNinjaRankPersistenceBoundaryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("Count=bad\n")]
    [InlineData("Count=-1\n")]
    [InlineData("Count=0\0\n")]
    [InlineData("Wrong=1\n")]
    [InlineData("Count=1=2\n")]
    [InlineData("Count=2147483647\n")]
    [InlineData("Count=2147483648\n")]
    public void MalformedOrEnormousHeaderRefusesWithoutAllocationOrPartialRegistry(string header) =>
        LegacyNativeNinjaRankTests.RunCorrectedOnlyRankHeader(header);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedUtf8CannotPublishReplacementCharacters(bool incomplete) =>
        LegacyNativeNinjaRankTests.RunCorrectedOnlyRankEncoding(incomplete);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrSharingDeniedFilePreservesTheWholeLiveRegistry(bool sharing) =>
        LegacyNativeNinjaRankTests.RunCorrectedOnlyRankAccess(sharing);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UnpairedUnicodeTextRefusesBeforeAnySaveFileMutation(bool guild, bool low) =>
        LegacyNativeNinjaRankTests.RunCorrectedOnlyRankSaveUnicode(guild, low);
}
