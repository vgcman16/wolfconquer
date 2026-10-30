using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerOwnedAdmissionHostTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task ActualProgramFinishesConsumedBookAndSpellIntentBeforeRawOrEncryptedAdmissionWithoutReplay(
        bool encrypted, bool stageFlushed) =>
        RunItemSpellHostScenarioAsync(async (fixture, children) =>
        {
            await fixture.InitializeAsync();
            var data = fixture.Data;
            data.WriteConfiguration();
            WriteItemSpellHostSeed(data.Root);
            var expected = data.Files();
            if (stageFlushed) await fixture.StopAfterStageFlushedAsync(3);
            else await fixture.StageAsync(2);
            foreach (var (path, image) in fixture.After)
                expected[Path.GetRelativePath(data.Root, path)] = image.ToArray();
            Assert.True(LegacyTradeJournal.HasPending(data.Users));
            await fixture.CloseAsync();

            for (var restart = 0; restart < 2; restart++)
            {
                var host = new OwnedWorldServerProcess(data, encrypted, configured: true, listen: true);
                children.Add(host);
                await host.StartListeningAsync();
                Assert.Contains("Owned admission: existing leased domain bound; ordinary saved-state admission enabled.", host.Lines);
                // Program startup must finish the genuine three-target intent
                // before admission can observe a consumed book without its spell.
                fixture.AssertRecoveredImages();
                data.AssertFiles(expected);
                Assert.False(LegacyItemUidRegistry.TryOpen(data.RegistryOptions(), out var competing, out var reason));
                Assert.Null(competing);
                Assert.NotEmpty(reason);

                await using (var client = await OwnedHostWireClient.ConnectAsync(host.Port, encrypted))
                {
                    const uint uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
                    await client.SendAsync(new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
                    var frames = await client.ReadThroughHeroAsync();
                    Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), frames[0]);
                    Assert.True(LegacyLoaderHeroInfoPacket.TryParse(OwnedHostWireClient.Decode(frames[^1]), out var hero));
                    Assert.Equal(uid, hero.Uid);
                    await host.ReadUntilAsync(line => line.Contains(
                        $"legacy MsgLoginGame Success key={uid} uid={uid}", StringComparison.Ordinal));
                    fixture.AssertRecoveredImages();
                    data.AssertFiles(expected);
                }

                await host.StopAsync();
                AssertLeaseAvailable(data);
                fixture.AssertRecoveredImages();
                // Every saved row, reserved spell byte, catalog, configuration,
                // and UID ledger is identical on the second restart as well.
                data.AssertFiles(expected);
            }
        });

    private static void WriteItemSpellHostSeed(string ownedRoot)
    {
        OwnedHostSeed.Write(ownedRoot);
        var catalog = LegacyOwnedStartupItemSpellRecoveryFixture.BuildCatalog();
        var root = Path.Combine(ownedRoot, "seed");
        var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        File.WriteAllText(Path.Combine(root, "items.json"), JsonSerializer.Serialize(catalog.Items, json), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "magic.json"), JsonSerializer.Serialize(catalog.Magic, json), new UTF8Encoding(false));
        var manifestPath = Path.Combine(root, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllBytes(manifestPath))!.AsObject();
        var counts = manifest["counts"]!.AsObject();
        counts["items"] = catalog.Items.Count;
        counts["magic"] = catalog.Magic.Count;
        File.WriteAllText(manifestPath, manifest.ToJsonString(), new UTF8Encoding(false));
    }

    private static async Task RunItemSpellHostScenarioAsync(
        Func<LegacyOwnedStartupItemSpellRecoveryFixture, List<OwnedWorldServerProcess>, Task> body)
    {
        var fixture = new LegacyOwnedStartupItemSpellRecoveryFixture();
        var children = new List<OwnedWorldServerProcess>();
        var failures = new List<Exception>();
        try { await body(fixture, children); }
        catch (Exception error) { failures.Add(error); }
        foreach (var child in children.AsEnumerable().Reverse())
        {
            try { await child.DisposeAsync(); }
            catch (Exception error) { failures.Add(error); }
        }
        try { await fixture.CloseAsync(); }
        catch (Exception error) { failures.Add(error); }
        if (children.All(child => child.Stopped))
        {
            if (failures.Count == 0)
                try { fixture.Data.Dispose(); }
                catch (Exception error) { failures.Add(error); }
        }
        else failures.Add(new IOException("Owned item/spell host did not terminate; fixture retained: " + fixture.Data.Root));
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Owned item/spell host assertion and/or cleanup failed.", failures);
    }
}
