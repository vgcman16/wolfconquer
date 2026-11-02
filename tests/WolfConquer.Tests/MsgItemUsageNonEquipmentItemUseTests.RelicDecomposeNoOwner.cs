using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public async Task ManagedOnlyDecomposeWithValidCatalogRefusesWithoutOwnedNativeOrRegisteredWriter()
    {
        const uint uid = 5_337_200;
        WriteSnapshot(1_293_600, 1_293_600, 1_002, 430, 380);
        WriteItems(1_293_600, PlayerItemSnapshotLoaderTests.DbItem(uid, 4_100_001, position: 0, stackSize: 1));
        var catalog = CreateWorldDataCatalog(CreateItemType(4_100_001, "OwnedRelic", durability: 500),
            CreateItemType(3_306_885, "Crystal", durability: 1));
        using var log = new StringWriter();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "managed-decompose", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot, _itemsRoot), handoff,
            catalog, LegacyRuntimeUpdateLoopOptions.Disabled, log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var sent = new List<byte[]>(); var connection = CreateSendingConnection(sent);
        await handler.HandleFrameAsync(connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        var owner = GetSession(handler, connection);
        var bytes = LegacyOwnedRelicDecomposeTests.DecomposeFixture.Request(uid); var frame = ReadFrame(bytes);
        Assert.True(MsgItemUsagePacket.TryParse(frame, out var packet));
        Assert.True(LegacyRelicDecomposeRequest.TryParse(packet, frame, out var request, out var reason), reason);
        Assert.True(LegacyRelicDecomposePreparation.TryPrepare(catalog, owner.PlayerItems, request!, out _, out reason), reason);
        Assert.Null(owner.NativeGameClient); Assert.False(connection.HasRegisteredRelicWire);
        var items = PlayerItemSnapshotLoader.ToLegacyBytes(owner.PlayerItems);
        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        var priorFrames = sent.Count;
        await handler.HandleFrameAsync(connection, frame);
        Assert.Equal(priorFrames, sent.Count); Assert.Same(owner, GetSession(handler, connection));
        Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(GetSession(handler, connection).PlayerItems));
        Assert.Equal(files.Keys.Order(), Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Order());
        foreach (var (path, content) in files) Assert.Equal(content, File.ReadAllBytes(path));
    }
}
