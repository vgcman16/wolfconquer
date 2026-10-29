using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [WindowsGameAcceptanceFact]
    public async Task ActualMailboxRunnerCanPrepareConsumablesWithoutSendingOrChangingPlayerFiles()
    {
        using var f = new WalkFixture();
        var before = Files(f.Root);
        var original = f.Arguments;
        var args = original.Take(Array.IndexOf(original, "-ExpectedMap"))
            .Select(arg => arg == "login-walk-save" ? "mailbox-gear" : arg)
            .Concat(["-Kit", "consumables"]).ToArray();
        var result = await RunAsync(args);
        Assert.Equal(2, result.ExitCode); Assert.Empty(result.Error); AssertFiles(f.Root, before);
        var summary = JsonNode.Parse(result.Output)!;
        Assert.Equal("RequestPreparedNotSent", summary["state"]!["status"]!.GetValue<string>());
        Assert.Equal("consumables", summary["state"]!["kit"]!.GetValue<string>());
        var request = f.Read("request.json");
        Assert.Equal(new uint[] { 1000000, 1001000, 723726 },
            request["items"]!.AsArray().Select(row => row!["itemId"]!.GetValue<uint>()));
        Assert.All(request["items"]!.AsArray(), row => Assert.Equal(1, row!["stackSize"]!.GetValue<int>()));
        var receipt = f.Read("receipt.json");
        Assert.Equal(new[] { "inspect-owned-player", "create-operator-test-kit" },
            receipt["steps"]!.AsArray().Select(row => row!["name"]!.GetValue<string>()));
        Assert.Equal("NotRequested", receipt["sourceChecks"]!["status"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(f.Evidence, "delivery-result.json")));
        f.Complete = true;
    }
}
