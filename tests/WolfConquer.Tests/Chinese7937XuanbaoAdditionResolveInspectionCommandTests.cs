namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAdditionResolveInspectionCommandTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualProcessPublishesOnlyThePinnedCountsHashesAndFalseBoundaries(bool uppercasePins) => WithInputs(async fixture =>
    {
        var args = fixture.Arguments();
        if (uppercasePins) { args[3] = args[3].ToUpperInvariant(); args[5] = args[5].ToUpperInvariant(); }
        AssertSuccess(await fixture.RunUnchanged(args), fixture);
    });

    [Theory]
    [InlineData("missing")]
    [InlineData("truncated")]
    [InlineData("extra")]
    [InlineData("version")]
    [InlineData("short-info-pin")]
    [InlineData("nonhex-addition-pin")]
    public Task ActualClosedArgumentsRefuseWithOnlyAStaticJsonError(string defect) => WithInputs(async fixture =>
    {
        var args = fixture.Arguments();
        switch (defect)
        {
            case "missing": args = [Command]; break;
            case "truncated": args = args[..^1]; break;
            case "extra": args = [.. args, "--activate", PrivateMarker]; break;
            case "version": args[1] = "7931"; break;
            case "short-info-pin": args[3] = new string('a', 63); break;
            default: args[5] = new string('g', 64); break;
        }
        AssertRefusal(await fixture.RunUnchanged(args), fixture, "Arguments");
    });

    [Theory]
    [InlineData(3)] [InlineData(5)]
    public Task ARealProcessPinMismatchCannotPublishPartialCountsOrHashes(int pinIndex) => WithInputs(async fixture =>
    {
        var args = fixture.Arguments(); args[pinIndex] = new string('0', 64);
        AssertRefusal(await fixture.RunUnchanged(args), fixture, "PinMismatch");
    });
}
