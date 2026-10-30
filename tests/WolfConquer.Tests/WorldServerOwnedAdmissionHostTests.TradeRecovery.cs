using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class WorldServerOwnedAdmissionHostTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task ActualProgramRecoversGenuineTradePairBeforeRawOrEncryptedAdmissionAndDoesNotReplay(
        bool encrypted, bool stageFlushed) =>
        RunTradeHostScenarioAsync(async (fixture, children) =>
        {
            await fixture.InitializeAsync();
            var data = fixture.Data;
            data.WriteConfiguration();
            OwnedHostSeed.Write(data.Root);
            var expected = data.Files();
            if (stageFlushed) await fixture.StopAfterStageFlushedAsync(4);
            else await fixture.StageAsync(3);
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
                // Neither login has run: Program startup itself must settle the
                // genuine four-target WAL, including a partial ownership move.
                fixture.AssertRecoveredImages();
                data.AssertFiles(expected);
                Assert.False(LegacyItemUidRegistry.TryOpen(data.RegistryOptions(), out var competing, out var reason));
                Assert.Null(competing);
                Assert.NotEmpty(reason);

                await AssertTradeParticipantAdmissionAsync(host, fixture, fixture.LeftAccount,
                    LegacyOwnedStartupTradeRecoveryFixture.LeftUid, encrypted);
                await AssertTradeParticipantAdmissionAsync(host, fixture, fixture.RightAccount,
                    LegacyOwnedStartupTradeRecoveryFixture.RightUid, encrypted);

                await host.StopAsync();
                AssertLeaseAvailable(data);
                fixture.AssertRecoveredImages();
                // Complete unrelated saves, sidecars, configuration, catalog,
                // and UID history stay exact across recovery and both logins.
                data.AssertFiles(expected);
            }
        });

    private static async Task AssertTradeParticipantAdmissionAsync(OwnedWorldServerProcess host,
        LegacyOwnedStartupTradeRecoveryFixture fixture, uint account, uint uid, bool encrypted)
    {
        await using var client = await OwnedHostWireClient.ConnectAsync(host.Port, encrypted);
        await client.SendAsync(new LegacyMsgLoginGamePacket(account).ToFrameBytes());
        var frames = await client.ReadThroughHeroAsync();
        Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), frames[0]);
        Assert.True(LegacyLoaderHeroInfoPacket.TryParse(OwnedHostWireClient.Decode(frames[^1]), out var hero));
        Assert.Equal(uid, hero.Uid);
        await host.ReadUntilAsync(line => line.Contains(
            $"legacy MsgLoginGame Success key={account} uid={uid}", StringComparison.Ordinal));
        var expected = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
            fixture.After[fixture.CharacterPath(uid)], out _));
        var saved = fixture.Data.Loader.TryLoadByAccountId(account).Snapshot!;
        Assert.Equal(expected.Money, saved.Money);
        Assert.Equal(expected.ConquerPoints, saved.ConquerPoints);
        Assert.Equal(expected.BankConquerPoints, saved.BankConquerPoints);
        Assert.Equal(expected.BoundConquerPoints, saved.BoundConquerPoints);
        Assert.Equal(1, saved.Online);
    }

    private static async Task RunTradeHostScenarioAsync(
        Func<LegacyOwnedStartupTradeRecoveryFixture, List<OwnedWorldServerProcess>, Task> body)
    {
        var fixture = new LegacyOwnedStartupTradeRecoveryFixture();
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
        else failures.Add(new IOException("Owned trade recovery host did not terminate; fixture retained: " + fixture.Data.Root));
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Owned trade recovery host assertion and/or cleanup failed.", failures);
    }
}
