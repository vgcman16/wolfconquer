using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerOwnedAdmissionHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualProgramRecoversItsSingleOwnerJournalBeforeRawOrEncryptedLoginAndDoesNotReplay(bool encrypted) =>
        OwnedHostScenario.RunAsync(async scenario =>
        {
            var fixture = scenario.Data;
            fixture.WriteConfiguration();
            OwnedHostSeed.Write(fixture.Root);
            var expected = fixture.Files();
            var original = fixture.Loader.TryLoadByAccountId(LegacyOwnedAdmissionRuntimeFixture.Uid).Snapshot!;
            var state = File.ReadAllBytes(fixture.State);
            var itemPath = Path.Combine(fixture.Items,
                LegacyOwnedAdmissionRuntimeFixture.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
            var items = File.ReadAllBytes(itemPath);
            var afterCharacter = ExpectedRecoveredOnlineImage(File.ReadAllBytes(fixture.UserPath));
            expected[Path.GetRelativePath(fixture.Root, fixture.UserPath)] = afterCharacter;
            fixture.StagePendingSnapshotJournal();
            Assert.True(LegacyTradeJournal.HasPending(fixture.Users));

            for (var attempt = 0; attempt < 2; attempt++)
            {
                var host = scenario.Create(encrypted);
                await host.StartListeningAsync();
                Assert.Contains("Owned admission: existing leased domain bound; ordinary saved-state admission enabled.", host.Lines);
                // Recovery must finish in actual Program startup, before this
                // client connects or any successful login could save the image.
                Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
                Assert.Equal(afterCharacter, File.ReadAllBytes(fixture.UserPath));
                Assert.Equal(state, File.ReadAllBytes(fixture.State));
                Assert.Equal(items, File.ReadAllBytes(itemPath));
                fixture.AssertFiles(expected);
                Assert.False(LegacyItemUidRegistry.TryOpen(fixture.RegistryOptions(), out var competing, out var reason));
                Assert.Null(competing);
                Assert.NotEmpty(reason);

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
                    Assert.Equal(1, saved.Online);
                }

                await host.StopAsync();
                AssertLeaseAvailable(fixture);
                Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
                fixture.AssertFiles(expected);
            }
        });

    private static byte[] ExpectedRecoveredOnlineImage(byte[] before)
    {
        var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        Assert.Contains("Online=0\r\n", text, StringComparison.Ordinal);
        return encoding.GetPreamble().Concat(encoding.GetBytes(
            text.Replace("Online=0\r\n", "Online=1\r\n", StringComparison.Ordinal))).ToArray();
    }
}
