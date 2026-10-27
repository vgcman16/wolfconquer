using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class DataToolLegacyWorldSmokeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyWorldSmokeRefusesInvalidOrUnboundSavedOwnershipWithoutChangingFiles(bool corruptQuest)
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1_293_600);
        await using var owned = new OwnedSmokeWorldFixture(_usersRoot, 1_293_600);
        if (corruptQuest) File.WriteAllBytes(owned.QuestFile, [0, 0, 0]);
        var files = owned.Files();
        // Both paths use a genuine saved domain. One lacks its explicit Frame
        // binding; the other has the real owner but malformed required data.
        var handler = corruptQuest ? owned.Handler : new WorldServerFrameHandler(owned.Loader, owned.Log);
        await using var server = CreateLegacyLoopbackServer(handler.HandleFrameAsync);
        server.Start();

        var result = await RunDataToolAsync("legacy-world-smoke", "127.0.0.1",
            server.BoundEndPoint.Port.ToString(), "1293600", "--timeout-ms", "1000");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("answerOk=False", result.StandardOutput);
        // DH is outside application capture; rejected admission sends no message2179.
        Assert.Contains("observed-opcodes=(none)" + Environment.NewLine, result.StandardOutput);
        Assert.DoesNotContain("answerOk=True", result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.Contains(corruptQuest ? "questSnapshotParseFailed" : "ordinaryAdmissionReconciliationOwnerMissing", owned.Log.ToString());
        Assert.DoesNotContain("ordinary admission committed", owned.Log.ToString());
        owned.AssertFiles(files);
    }
}
