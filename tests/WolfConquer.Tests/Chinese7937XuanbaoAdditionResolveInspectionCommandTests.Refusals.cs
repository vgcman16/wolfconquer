using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAdditionResolveInspectionCommandTests
{
    [Theory]
    [InlineData(2, "relative", "PathRequired")] [InlineData(4, "relative", "PathRequired")]
    [InlineData(2, "missing", "UnsafePath")] [InlineData(4, "missing", "UnsafePath")]
    [InlineData(2, "directory", "UnsafePath")] [InlineData(4, "directory", "UnsafePath")]
    public Task BothInputsRequireExplicitExistingOrdinaryFiles(int pathIndex, string kind, string failure) => WithInputs(async fixture =>
    {
        var args = fixture.Arguments();
        if (kind == "relative") args[pathIndex] = Path.GetFileName(args[pathIndex]);
        else if (kind == "missing") args[pathIndex] = Path.Combine(fixture.Root, "missing-" + PrivateMarker);
        else
        {
            args[pathIndex] = Path.Combine(fixture.Root, "owned-directory");
            Directory.CreateDirectory(args[pathIndex]);
        }
        AssertRefusal(await fixture.RunUnchanged(args), fixture, failure);
    });

    [Theory]
    [InlineData(2)] [InlineData(4)]
    public Task AnExclusivelyLeasedInputReturnsAStaticFileFailureAndReleasesTheOtherLease(int pathIndex) => WithInputs(async fixture =>
    {
        var args = fixture.Arguments(); var before = fixture.Capture(); Result result;
        using (var locked = new FileStream(args[pathIndex], FileMode.Open, FileAccess.Read, FileShare.None))
            result = await RunProcess(fixture.Root, args);
        AssertRefusal(result, fixture, "FileRead"); fixture.AssertUnchanged(before);
        using var first = new FileStream(fixture.InfoPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var second = new FileStream(fixture.AdditionPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    [Fact]
    public Task ACompleteResolvePrefixWithAnUnclosedTailPublishesNoPartialData() => WithInputs(async fixture =>
    {
        fixture.SetInfo(Chinese7937XuanbaoResolveFixture.Gbk(Chinese7937XuanbaoResolveFixture.Module() + "\nlocal malformedTail = (\n"));
        AssertRefusal(await fixture.RunUnchanged(fixture.Arguments()), fixture, "ResolveParser:UnsupportedSyntax");
    });

    [Fact]
    public Task ResolveNumericOverflowUsesOnlyItsStaticParserFailure() => WithInputs(async fixture =>
    {
        fixture.SetInfo(Chinese7937XuanbaoResolveFixture.Gbk(Chinese7937XuanbaoResolveFixture.Module("4294967296")));
        AssertRefusal(await fixture.RunUnchanged(fixture.Arguments()), fixture, "ResolveParser:NumericWidth");
    });

    [Theory]
    [InlineData("99@@1@@2@@3@@\r\n", "UnsupportedSyntax")]
    [InlineData("2700000001@@1@@2@@3@@4@@\r\n", "DuplicateRowId")]
    [InlineData("99@@1@@2@@3@@4294967296@@\r\n", "NumericWidth")]
    public Task AValidAdditionPrefixCannotHideAnInvalidCompleteFinalRow(string tail, string failure) => WithInputs(async fixture =>
    {
        fixture.SetAddition(Addition + tail);
        AssertRefusal(await fixture.RunUnchanged(fixture.Arguments()), fixture, "AdditionParser:" + failure);
    });

    [Fact]
    public Task AnOversizedPlainCaptureRefusesBeforeAnyParserSummary() => WithInputs(async fixture =>
    {
        fixture.SetInfo(new byte[Chinese7937DecodedXuanbaoInfo.MaximumBytes + 1]);
        AssertRefusal(await fixture.RunUnchanged(fixture.Arguments()), fixture, "InputBounds");
    });
}
