using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(1u, 4_200_018u, 1, 2)]
    [InlineData(2u, 4_200_016u, 1, 2)]
    [InlineData(3u, 4_200_017u, 1, 2)]
    [InlineData(1u, 4_200_018u, 0, 0)]
    [InlineData(2u, 4_200_016u, 0, 0)]
    [InlineData(3u, 4_200_017u, 0, 0)]
    [InlineData(1u, 4_200_018u, 1, 0)]
    [InlineData(2u, 4_200_016u, 1, 0)]
    [InlineData(3u, 4_200_017u, 1, 0)]
    [InlineData(2u, 4_200_016u, 255, 3)]
    public async Task HandlerDragonSkinAdmissionRefusesBoundOrEmptyRowsWithoutDebitSkinOrSave(
        uint skinType, uint itemId, byte bound, ushort stackSize)
    {
        using var log = new StringWriter();
        var fixture = await EnterDragonSkinAdmissionAsync(
        [
            DragonSkinAdmissionItem(9_005, itemId, stackSize, bound),
            DragonSkinAdmissionItem(9_004, 1_088_001, 5, 0),
        ], log);
        var before = GetSession(fixture.Handler, fixture.Connection);
        var itemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
        var attributes = before.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        var files = CaptureDragonSkinAdmissionFiles();

        // A caller-supplied success bit and foreign UID must not turn refusal into success.
        foreach (var action in new[] { LegacyDragonSkinType.Unlock, LegacyDragonSkinType.Retrieve })
        {
            int first = fixture.Sent.Count;
            await fixture.Handler.HandleFrameAsync(fixture.Connection,
                ReadFrame(new LegacyDragonSkinPacket(action, UserID: 123, skinType, OK: true).ToFrameBytes()));

            Assert.Equal(new LegacyDragonSkinPacket(action, 1_293_600, skinType, OK: false).ToFrameBytes(),
                Assert.Single(fixture.Sent.Skip(first)));
            var after = GetSession(fixture.Handler, fixture.Connection);
            Assert.Same(before, after);
            Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
            Assert.Equal(attributes, after.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
            Assert.Equal("0/0/", LegacyDragonSkinState.FromSnapshot(after.Character).ToSnapshotValue());
            AssertDragonSkinAdmissionFilesUnchanged(files);
        }

        Assert.DoesNotContain("WorldServer unlocked LegacyDragonSkin", log.ToString());
        Assert.DoesNotContain("WorldServer retrieved LegacyDragonSkin", log.ToString());
        Assert.DoesNotContain("WorldServer persisted LegacyDragonSkin", log.ToString());
    }

    [Theory]
    [InlineData(1u, 4_200_018u, 1)]
    [InlineData(2u, 4_200_016u, 1)]
    [InlineData(3u, 4_200_017u, 1)]
    [InlineData(1u, 4_200_018u, 3)]
    [InlineData(2u, 4_200_016u, 3)]
    [InlineData(3u, 4_200_017u, 3)]
    public async Task HandlerDragonSkinAdmissionConsumesFirstPositiveUnboundRowAfterIneligibleRows(
        uint skinType, uint itemId, ushort eligibleStackSize)
    {
        // Descending UIDs prove list order is preserved rather than choosing the lowest UID.
        PlayerItemSnapshotItem[] rows =
        [
            DragonSkinAdmissionItem(9_005, itemId, 7, 1),
            DragonSkinAdmissionItem(9_004, itemId, 0, 0),
            DragonSkinAdmissionItem(9_003, itemId, eligibleStackSize, 0),
            DragonSkinAdmissionItem(9_002, itemId, 8, 0),
            DragonSkinAdmissionItem(9_001, 1_088_001, 9, 0),
        ];
        using var log = new StringWriter();
        var fixture = await EnterDragonSkinAdmissionAsync(rows, log);
        var before = GetSession(fixture.Handler, fixture.Connection);
        byte[] initialBytes = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot(rows, [], [], [], [], []));
        Assert.Equal(initialBytes, PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems));
        var previousAttributes = before.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        int first = fixture.Sent.Count;

        await fixture.Handler.HandleFrameAsync(fixture.Connection,
            ReadFrame(new LegacyDragonSkinPacket(LegacyDragonSkinType.Unlock, 1_293_600, skinType, OK: false).ToFrameBytes()));

        var after = GetSession(fixture.Handler, fixture.Connection);
        var remaining = DragonSkinAdmissionItem(9_003, itemId, checked((ushort)(eligibleStackSize - 1)), 0);
        var expectedRows = eligibleStackSize == 1
            ? rows.Where(item => item.Uid != 9_003).ToArray()
            : rows.Select(item => item.Uid == 9_003 ? remaining : item).ToArray();
        byte[] expectedBytes = PlayerItemSnapshotLoader.ToLegacyBytes(new PlayerItemSnapshot(expectedRows, [], [], [], [], []));
        Assert.Equal(expectedBytes, PlayerItemSnapshotLoader.ToLegacyBytes(after.PlayerItems));
        Assert.Equal(expectedBytes, File.ReadAllBytes(Path.Combine(_usersRoot, "PlayersItems", "1293600.bin")));
        Assert.Equal(initialBytes, PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems));
        Assert.Equal(previousAttributes, before.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
        Assert.Equal(before.Character with { Attributes = after.Character.Attributes }, after.Character);
        Assert.Equal(previousAttributes.Where(pair => pair.Key != "AnimaSkin"),
            after.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).Where(pair => pair.Key != "AnimaSkin"));
        Assert.Equal($"0/1/{skinType}/", LegacyDragonSkinState.FromSnapshot(after.Character).ToSnapshotValue());
        var savedCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(_usersRoot, "1293600.ini")));
        Assert.Equal($"0/1/{skinType}/", LegacyDragonSkinState.FromSnapshot(savedCharacter).ToSnapshotValue());

        Assert.Equal(first + 2, fixture.Sent.Count);
        byte[] expectedDebit = eligibleStackSize == 1
            ? new MsgItemUsagePacket(9_003, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()
            : LegacyGameItemPacket.FromSnapshotItem(remaining, mode: LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
        Assert.Equal(expectedDebit, fixture.Sent[first]);
        Assert.Equal(new LegacyDragonSkinPacket(LegacyDragonSkinType.Unlock, 1_293_600, skinType, OK: true).ToFrameBytes(),
            fixture.Sent[first + 1]);
        Assert.Contains("WorldServer persisted LegacyDragonSkin", log.ToString());
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        EnterDragonSkinAdmissionAsync(IReadOnlyList<PlayerItemSnapshotItem> items, TextWriter log)
    {
        Directory.CreateDirectory(_usersRoot);
        string itemsRoot = Path.Combine(_usersRoot, "PlayersItems");
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(1_293_600, uid: 1_293_600, animaSkin: "0/0/");
        new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(1_293_600,
            new PlayerItemSnapshot(items, [], [], [], [], []));
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(1_293_600, "dragon-skin-admission", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot, itemsRoot), handoff, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        await handler.HandleFrameAsync(connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
        Assert.Equal(1_293_600, GetSession(handler, connection).Character.Uid);
        return (handler, connection, sent);
    }

    private static PlayerItemSnapshotItem DragonSkinAdmissionItem(uint uid, uint itemId, ushort stackSize, byte bound) => new()
    {
        Uid = uid, ItemId = itemId, Position = 0, StackSize = stackSize, Bound = bound,
        Durability = 321, MaximumDurability = 432, SocketProgress = 1234, SocketOne = 1, SocketTwo = 2,
        Effect = 7, Plus = 5, Bless = 3, Enchant = 4, PlusProgress = 345,
        Color = 3, ItemPoints = 77, RuneExperience = 88,
        Attribute1 = 11, Attribute2 = 22, Attribute3 = 33, Attribute4 = 44, Attribute5 = 55,
        PerfectionLevel = 2, PerfectionProgress = 123, OwnerUid = 42,
        OwnerName = "Fixture Owner", Signature = "dragon admission",
    };

    private Dictionary<string, (byte[] Bytes, DateTime LastWriteUtc)> CaptureDragonSkinAdmissionFiles() =>
        Directory.EnumerateFiles(_usersRoot, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(_usersRoot, path),
            path => (File.ReadAllBytes(path), File.GetLastWriteTimeUtc(path)), StringComparer.Ordinal);

    private void AssertDragonSkinAdmissionFilesUnchanged(Dictionary<string, (byte[] Bytes, DateTime LastWriteUtc)> before)
    {
        var after = CaptureDragonSkinAdmissionFiles();
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, expected) in before)
        {
            Assert.Equal(expected.Bytes, after[path].Bytes);
            Assert.Equal(expected.LastWriteUtc, after[path].LastWriteUtc);
        }
    }
}
