using System.Reflection;

namespace WolfConquer.Tests;

public sealed partial class ClientAuditPrefixQualityTests
{
    [Theory]
    [InlineData("/outside.dds", false, "")]
    [InlineData("\\outside.dds", false, "")]
    [InlineData("\\\\host\\share\\outside.dds", false, "")]
    [InlineData("C:/outside.dds", false, "")]
    [InlineData("C:outside.dds", false, "")]
    [InlineData(" \"/outside.dds\" ", false, "")]
    [InlineData("../outside.dds", false, "")]
    [InlineData("data/../outside.dds", false, "")]
    [InlineData("./data/icon.dds", true, "data/icon.dds")]
    [InlineData("data\\icon.dds", true, "data/icon.dds")]
    [InlineData("data/.hidden.dds", true, "data/.hidden.dds")]
    [InlineData("", false, "")]
    [InlineData(".", false, "")]
    public void AssetPresenceParserPreservesRootTraversalAndRelativePathDecisions(
        string input, bool expectedAccepted, string expectedNormalized)
    {
        // Invoke the actual complete private parser without changing its visibility,
        // starting a command process, or replacing the filesystem/path policy owner.
        var program = typeof(ClientAssetPresenceAuditor).Assembly.GetType("Program", throwOnError: true)!;
        var parser = program.GetMethod("TryNormalizeClientAssetPresenceRelativePath",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(parser);
        object?[] arguments = [input, null, null];

        var accepted = Assert.IsType<bool>(parser.Invoke(null, arguments));

        Assert.Equal(expectedAccepted, accepted);
        Assert.Equal(expectedNormalized, Assert.IsType<string>(arguments[1]));
        if (expectedAccepted)
            Assert.Equal(string.Empty, Assert.IsType<string>(arguments[2]));
        else
            Assert.NotEmpty(Assert.IsType<string>(arguments[2]));
    }

    [Theory]
    [InlineData("/outside.lua")]
    [InlineData("\\outside.lua")]
    [InlineData("\\\\host\\share\\outside.lua")]
    [InlineData("C:/outside.lua")]
    [InlineData("../outside.lua")]
    public void StringReferenceAuditorRejectsRootedOrEscapingReferenceAndResourcePaths(string input)
    {
        using var fixture = new ClientFiles();

        var badReference = Assert.Throws<ArgumentException>(() =>
            ClientStringRefAuditor.Audit(fixture.Root, input, ["res.ini"]));
        var badResource = Assert.Throws<ArgumentException>(() =>
            ClientStringRefAuditor.Audit(fixture.Root, "ref.lua", [input]));

        Assert.Contains("Reference file must be relative", badReference.Message);
        Assert.Contains("Resource file must be relative", badResource.Message);
        fixture.AssertUnchanged();
    }

    [Fact]
    public void StringReferenceAuditorRetainsRealNonemptyRelativeFilesAndKeyResults()
    {
        using var fixture = new ClientFiles();

        var report = ClientStringRefAuditor.Audit(fixture.Root, "./ref.lua", ["./res.ini"]);

        Assert.Equal("./ref.lua", report.ReferenceFile);
        Assert.Equal(new[] { "STR_KNOWN", "STR_MISSING" }, report.ReferencedKeys);
        Assert.Equal(new[] { "STR_KNOWN" }, report.ResourceKeys);
        Assert.Equal(new[] { "STR_KNOWN" }, report.AvailableKeys);
        Assert.Equal(new[] { "STR_MISSING" }, report.MissingKeys);
        fixture.AssertUnchanged();
    }
}
