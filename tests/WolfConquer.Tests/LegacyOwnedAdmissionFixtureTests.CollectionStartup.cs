using System.Buffers;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    private OwnedWorldServerProcess? collectionHost;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApiOrActualCliFixturePersistsNativeEmptyCollectionAndPublishes2450BeforeHero(bool useCli)
    {
        var options = await CreateCollectionStartupFixtureAsync(useCli);
        var loader = options.CreateSnapshotLoader();
        AssertEmptyCollectionSaved(loader);
        var initialState = File.ReadAllBytes(Path.Combine(options.ItemsRoot, ".managed-item-uids", "state.json"));
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(loader, options, out var opened, out var reason), reason);
        await using var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, null, LegacyItemWorldDataFixture.Create([], []),
            LegacyRuntimeUpdateLoopOptions.Disabled, log,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, _) => { sent.Enqueue(bytes.ToArray()); return ValueTask.CompletedTask; });
        try
        {
            await handler.HandleFrameAsync(connection, CollectionStartupFrame(new LegacyMsgLoginGamePacket(AccountUid).ToFrameBytes()));

            AssertCollectionImmediatelyBeforeHero(sent.ToArray());
            Assert.Contains("ordinary admission committed", log.ToString(), StringComparison.Ordinal);
            AssertEmptyCollectionSaved(loader);
            Assert.Equal(initialState, File.ReadAllBytes(Path.Combine(options.ItemsRoot, ".managed-item-uids", "state.json")));
            Assert.False(LegacyTradeJournal.HasPending(options.UsersRoot));
        }
        finally { await connection.CompleteTransportCloseAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualProgramPublishesSeededCollection2450BeforeHeroOnRawAndEncryptedStartup(bool encrypted)
    {
        // Use the actual CLI-created fixture in one mode and the genuine public
        // offline creator in the other. Neither path bootstraps during Host start.
        var options = await CreateCollectionStartupFixtureAsync(useCli: encrypted);
        var loader = options.CreateSnapshotLoader();
        AssertEmptyCollectionSaved(loader);
        OwnedHostSeed.Write(Destination);
        var initialState = File.ReadAllBytes(Path.Combine(options.ItemsRoot, ".managed-item-uids", "state.json"));
        await using var host = new OwnedWorldServerProcess(options, Destination, ConfigurationPath, encrypted);
        collectionHost = host;
        await host.StartListeningAsync();
        Assert.Contains("Owned admission: existing leased domain bound; ordinary saved-state admission enabled.", host.Lines);
        await using (var client = await OwnedHostWireClient.ConnectAsync(host.Port, encrypted))
        {
            await client.SendAsync(new LegacyMsgLoginGamePacket(AccountUid).ToFrameBytes());
            AssertCollectionImmediatelyBeforeHero(await client.ReadThroughHeroAsync());
            await host.ReadUntilAsync(line => line.Contains(
                $"legacy MsgLoginGame Success key={AccountUid} uid={AccountUid}", StringComparison.Ordinal));
            AssertEmptyCollectionSaved(loader);
            Assert.Equal(initialState, File.ReadAllBytes(Path.Combine(options.ItemsRoot, ".managed-item-uids", "state.json")));
            Assert.False(LegacyTradeJournal.HasPending(options.UsersRoot));
        }
        await host.StopAsync();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(loader, options, out var reopened, out var reason), reason);
        using (Assert.IsType<LegacyOwnedAdmissionRuntime>(reopened)) AssertEmptyCollectionSaved(loader);
    }

    private async Task<LegacyOwnedAdmissionOptions> CreateCollectionStartupFixtureAsync(bool useCli)
    {
        if (useCli)
        {
            var result = await RunDataToolAsync("seed-owned-admission-fixture", Destination,
                AccountUid.ToString(CultureInfo.InvariantCulture), AccountUid.ToString(CultureInfo.InvariantCulture),
                ItemUid.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Error);
        }
        else _ = Create();
        Assert.True(LegacyOwnedAdmissionOptions.TryReadFile(ConfigurationPath, out var configured, out var reason), reason);
        return Assert.IsType<LegacyOwnedAdmissionOptions>(configured);
    }

    private static void AssertEmptyCollectionSaved(CharacterSnapshotLoader loader)
    {
        var saved = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(AccountUid));
        Assert.Equal("0", saved.Attributes["CollectionID"]);
        Assert.Equal("0/0/", saved.Attributes["CollectionStorage"]);
        Assert.True(LegacyCollectionStoragePacket.TryCreateFromSnapshot(saved, out var packet));
        // Header8/opcode2450 and native protobuf fields1=0,2=0; no item rows.
        Assert.Equal(Convert.FromHexString("0800920908001000"), packet.ToFrameBytes());
        Assert.Empty(packet.Scraps);
        Assert.Empty(packet.Entries);
    }

    private static void AssertCollectionImmediatelyBeforeHero(byte[][] frames)
    {
        Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), frames[0]);
        var decoded = frames.Select(CollectionStartupFrame).ToArray();
        var index = Array.FindIndex(decoded, frame => frame.Header.Opcode == LegacyCollectionStoragePacket.Opcode);
        Assert.InRange(index, 1, frames.Length - 2);
        Assert.Single(decoded, frame => frame.Header.Opcode == LegacyCollectionStoragePacket.Opcode);
        Assert.Equal(Convert.FromHexString("0800920908001000"), frames[index]);
        Assert.Equal(LegacyServerConfigPacket.Opcode, decoded[index - 1].Header.Opcode);
        Assert.Equal(LegacyLoaderHeroInfoPacket.Opcode, decoded[index + 1].Header.Opcode);
    }

    private static PacketFrame CollectionStartupFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
