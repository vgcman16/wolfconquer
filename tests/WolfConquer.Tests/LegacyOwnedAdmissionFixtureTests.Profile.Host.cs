using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualRawOrEncryptedProgramKeepsD13ClassU32AndGrantsCycloneOnlyOnFirstAdmission(bool encrypted)
    {
        var options = await CreateD13ProfileAsync(useCli: encrypted);
        AssertProfileInitial(options, 1000);
        // This genuine JSON seed intentionally has empty Magic: learning1110
        // does not look it up. This test neither casts nor claims cast readiness.
        OwnedHostSeed.Write(Destination);
        var before = Files(Destination);
        Dictionary<string, byte[]>? committed = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var host = new OwnedWorldServerProcess(options, Destination, ConfigurationPath, encrypted);
            collectionHost = host; // Actual existing cleanup retains a still-active owned child.
            OwnedHostWireClient? client = null;
            var failures = new List<Exception>();
            try
            {
                await host.StartListeningAsync();
                client = await OwnedHostWireClient.ConnectAsync(host.Port, encrypted);
                await client.SendAsync(new LegacyMsgLoginGamePacket(AccountUid).ToFrameBytes());
                var frames = await client.ReadThroughHeroAsync(throughOpcode: attempt == 0 ? (ushort)2101 : (ushort)2221);
                AssertD13ProfileHeroClasses(frames);
                if (attempt == 0)
                    Assert.Equal(ProfileCycloneFrame, Assert.Single(frames, value => IsProfileOpcode(value, 2101)));
                await host.ReadUntilAsync(line => line.Contains(
                    $"legacy MsgLoginGame Success key={AccountUid} uid={AccountUid}", StringComparison.Ordinal));
                AssertProfileCommitted(options, before);

                var confirmation = ProfileConfirmSpells();
                await client.SendAsync(confirmation);
                var tail = await ReadProfileConfirmationAsync(client, confirmation);
                Assert.Equal(ProfileCycloneFrame, Assert.Single(tail, value => IsProfileOpcode(value, 2101)));
                Assert.Equal(confirmation, tail[^1]);
                if (committed is null) committed = Files(Destination);
                else AssertFiles(Destination, committed);
            }
            catch (Exception error) { failures.Add(error); }
            try { if (client is not null) await client.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
            try { await host.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
            RethrowOwnedAppearanceFailures(failures);
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(options.CreateSnapshotLoader(), options, out var reopened, out var reason), reason);
            using (Assert.IsType<LegacyOwnedAdmissionRuntime>(reopened)) AssertProfileCommitted(options, before);
        }
    }

    private static async Task<byte[][]> ReadProfileConfirmationAsync(OwnedHostWireClient client, byte[] expected)
    {
        var frames = new List<byte[]>();
        for (var batch = 0; batch < 8; batch++)
        {
            var next = await client.ReadThroughHeroAsync(throughOpcode: MsgActionPacket.Opcode);
            frames.AddRange(next);
            if (next[^1].AsSpan().SequenceEqual(expected)) return frames.ToArray();
        }
        throw new InvalidDataException("The actual Action78 echo was absent from bounded owned Host output.");
    }
}
