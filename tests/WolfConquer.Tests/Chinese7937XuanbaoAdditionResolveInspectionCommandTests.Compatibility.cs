using System.Text;
using System.Text.Json;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937XuanbaoAdditionResolveInspectionCommandTests
{
    [Theory]
    [InlineData("help")] [InlineData("--help")]
    public Task ExistingHelpStillListsBothOldAndNewInspectionRoutes(string help) => WithInputs(async fixture =>
    {
        var result = await fixture.RunUnchanged(help);
        Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error);
        Assert.Contains("WolfConquer.DataTool " + Command + " 7937", result.Output, StringComparison.Ordinal);
        Assert.Contains("WolfConquer.DataTool client-xuanbao7937-inspect 7937", result.Output, StringComparison.Ordinal);
        Assert.Contains("WolfConquer.DataTool client-item-catalog-codec <decode|encode>", result.Output, StringComparison.Ordinal);
    });

    [Fact]
    public Task TheEarlierXuanbaoInspectionCommandRetainsItsExistingStdoutRefusalContract() => WithInputs(async fixture =>
    {
        var result = await fixture.RunUnchanged("client-xuanbao7937-inspect");
        Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal("refused", document.RootElement.GetProperty("Status").GetString());
        Assert.Equal("arguments", document.RootElement.GetProperty("Stage").GetString());
        Assert.Equal("ExpectedBuildAndThreePinnedFiles", document.RootElement.GetProperty("Reason").GetString());
        Assert.False(document.RootElement.GetProperty("OutputFilesWritten").GetBoolean());
    });

    [Fact]
    public Task AnExistingCodecCommandStillReadsItsActualSynthetic65FieldInput() => WithInputs(async fixture =>
    {
        var fields = Enumerable.Repeat("0", 65).ToArray(); fields[0] = "901"; fields[1] = "SyntheticOldCodec";
        var path = Path.Combine(fixture.Root, "old-codec.dat");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(string.Join("@@", fields) + "@@\r\n"));
        var result = await fixture.RunUnchanged("client-item-catalog-codec", "encode", path);
        Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error);
        Assert.Contains("Mode=encode Rows=1 FieldsPerRow=65", result.Output, StringComparison.Ordinal);
        Assert.Contains("ExactRoundtrip=True", result.Output, StringComparison.Ordinal);
    });
}
