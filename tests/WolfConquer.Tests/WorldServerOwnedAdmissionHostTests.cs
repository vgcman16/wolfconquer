using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerOwnedAdmissionHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualProgramAdmitsSavedCharacterOverRawOrEncryptedWireAndRestartDoesNotRegrant(bool encrypted) =>
        OwnedHostScenario.RunAsync(async scenario =>
        {
            var fixture = scenario.Data;
            fixture.WriteConfiguration();
            OwnedHostSeed.Write(fixture.Root);
            var before = fixture.Files();
            var original = fixture.Loader.TryLoadByAccountId(LegacyOwnedAdmissionRuntimeFixture.Uid).Snapshot!;
            // Independent source literals for this fixture's four attributes of 10 and no bonuses.
            Assert.Equal((330L, 50L, 50L), (original.MinHitPoints, original.MinMana, original.MaxMana));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var host = scenario.Create(encrypted);
                await host.StartListeningAsync();
                Assert.Contains("Owned admission: existing leased domain bound; ordinary saved-state admission enabled.", host.Lines);
                Assert.False(LegacyItemUidRegistry.TryOpen(fixture.RegistryOptions(), out var competing, out var reason));
                Assert.Null(competing);
                Assert.False(string.IsNullOrWhiteSpace(reason));
                await using (var client = await OwnedHostWireClient.ConnectAsync(host.Port, encrypted))
                {
                    await client.SendAsync(new LegacyMsgLoginGamePacket(LegacyOwnedAdmissionRuntimeFixture.Uid).ToFrameBytes());
                    var frames = await client.ReadThroughHeroAsync();
                    Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), frames[0]);
                    Assert.True(LegacyLoaderHeroInfoPacket.TryParse(OwnedHostWireClient.Decode(frames[^1]), out var hero));
                    Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.Uid, hero.Uid);
                    Assert.Equal("OwnedStartup", hero.Name);
                    Assert.Equal(140, hero.Level);
                    Assert.Equal(7051U, hero.Class);
                    Assert.Equal(2, hero.ServerId);
                    await host.ReadUntilAsync(line => line.Contains(
                        $"legacy MsgLoginGame Success key={LegacyOwnedAdmissionRuntimeFixture.Uid} uid={LegacyOwnedAdmissionRuntimeFixture.Uid}",
                        StringComparison.Ordinal));
                    var saved = fixture.Loader.TryLoadByAccountId(LegacyOwnedAdmissionRuntimeFixture.Uid).Snapshot!;
                    Assert.Equal(original.Money, saved.Money);
                    Assert.Equal(original.ConquerPoints, saved.ConquerPoints);
                    Assert.Equal(original.BankConquerPoints, saved.BankConquerPoints);
                    Assert.Equal(original.Level, saved.Level);
                    Assert.Equal(original.Experience, saved.Experience);
                    Assert.Equal((330L, 50L, 50L), (saved.MinHitPoints, saved.MinMana, saved.MaxMana));
                    Assert.Equal(1, saved.Online);
                }
                await host.StopAsync();
                AssertLeaseAvailable(fixture);
                var expected = before.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                var character = Path.GetRelativePath(fixture.Root, fixture.UserPath);
                var text = Encoding.UTF8.GetString(expected[character]);
                Assert.Contains("Online=0\r\n", text, StringComparison.Ordinal);
                expected[character] = Encoding.UTF8.GetBytes(text.Replace("Online=0\r\n", "Online=1\r\n", StringComparison.Ordinal));
                // Exact inventory, spells, proficiencies, quests, gear, UID history,
                // config and authored catalog membership; only Online changes.
                fixture.AssertFiles(expected);
            }
        });

    [Theory]
    [InlineData("wrong-domain")]
    [InlineData("unconfigured-namespace")]
    [InlineData("pending-journal")]
    public Task ActualProgramRefusesUnownedOrUnreconciledStorageBeforeOpeningListener(string failure) =>
        OwnedHostScenario.RunAsync(async scenario =>
        {
            var fixture = scenario.Data;
            fixture.WriteConfiguration();
            OwnedHostSeed.Write(fixture.Root);
            if (failure == "wrong-domain")
            {
                var json = JsonNode.Parse(File.ReadAllText(fixture.ConfigPath))!.AsObject();
                json["ExpectedDomain"] = Guid.NewGuid().ToString("N");
                File.WriteAllText(fixture.ConfigPath, json.ToJsonString());
            }
            if (failure == "pending-journal") fixture.StagePendingSnapshotJournal();
            var before = fixture.Files();
            var host = scenario.Create(encrypted: false, configured: failure != "unconfigured-namespace");
            await host.StartToExitAsync();
            Assert.Equal(1, host.ExitCode);
            Assert.Contains(host.Lines, line => line.StartsWith("WorldServer owned admission startup refused: ", StringComparison.Ordinal));
            Assert.DoesNotContain(host.Lines, line => line.StartsWith("Listening on ", StringComparison.Ordinal));
            Assert.DoesNotContain(host.Lines, line => line.Contains("ordinary admission committed", StringComparison.Ordinal));
            fixture.AssertFiles(before);
            if (failure != "pending-journal") AssertLeaseAvailable(fixture);
        });

    [Fact]
    public Task ActualSecondWorldProcessCannotClaimFirstWorldProcessStorageLease() =>
        OwnedHostScenario.RunAsync(async scenario =>
        {
            var fixture = scenario.Data;
            fixture.WriteConfiguration();
            OwnedHostSeed.Write(fixture.Root);
            var before = fixture.Files();
            var first = scenario.Create(encrypted: true);
            await first.StartListeningAsync();
            var second = scenario.Create(encrypted: false);
            await second.StartToExitAsync();
            Assert.Equal(1, second.ExitCode);
            Assert.Contains(second.Lines, line => line.StartsWith("WorldServer owned admission startup refused: ", StringComparison.Ordinal));
            Assert.DoesNotContain(second.Lines, line => line.StartsWith("Listening on ", StringComparison.Ordinal));
            Assert.False(first.HasExited);
            Assert.False(LegacyItemUidRegistry.TryOpen(fixture.RegistryOptions(), out var competing, out _));
            Assert.Null(competing);
            fixture.AssertFiles(before);
            await first.StopAsync();
            AssertLeaseAvailable(fixture);
            var restarted = scenario.Create(encrypted: false);
            await restarted.StartListeningAsync();
            await restarted.StopAsync();
            AssertLeaseAvailable(fixture);
            fixture.AssertFiles(before);
        });

    [Fact]
    public Task ActualNonListeningProgramReturnsNormallyAndDisposesItsClaimedOwner() =>
        OwnedHostScenario.RunAsync(async scenario =>
        {
            scenario.Data.WriteConfiguration();
            OwnedHostSeed.Write(scenario.Data.Root);
            var before = scenario.Data.Files();
            var host = scenario.Create(encrypted: false, listen: false);
            await host.StartToExitAsync();
            Assert.Equal(0, host.ExitCode);
            Assert.Contains("Owned admission: existing leased domain bound; ordinary saved-state admission enabled.", host.Lines);
            Assert.Contains(host.Lines, line => line.Contains("Pass --listen", StringComparison.Ordinal));
            AssertLeaseAvailable(scenario.Data);
            scenario.Data.AssertFiles(before);
        });

    private static void AssertLeaseAvailable(LegacyOwnedAdmissionRuntimeFixture fixture)
    {
        Assert.True(LegacyItemUidRegistry.TryOpen(fixture.RegistryOptions(), out var owner, out var reason), reason);
        using (Assert.IsType<LegacyItemUidRegistry>(owner)) { }
    }
}
