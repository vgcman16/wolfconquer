using System.Buffers;
using System.Net;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBeastsOptFeedTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;
    private const uint FeedUid = 7_000_001;

    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-beasts-opt-feed-" + Guid.NewGuid());

    [Fact]
    public async Task LegacyBeastsOptFeedExistingInventoryItemAddsMappedPointsAndEchoesTypeOne()
    {
        var (handler, connection, sent) = await LoginWithRuneAndInventoryItemsAsync(
            initialBeastPoints: 0,
            PlayerItemSnapshotLoaderTests.DbItem(
                uid: FeedUid,
                itemId: 3_009_102,
                position: 0,
                stackSize: 1));
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBeastsOptPacket(
                Type: 1,
                Items: [new LegacyBeastsOptPacket.Item(FeedUid, Stack: 1)]).ToFrameBytes()));

        AssertRemoveInventoryFrame(sent[countBeforeRequest], FeedUid);
        AssertShowEquipmentRefresh(sent[countBeforeRequest + 1], alternante: 0);
        var (beastsInfo, echo) = AssertAppendedBeastsInfoAndEcho(sent, countBeforeRequest);
        Assert.True(beastsInfo.Activated);
        Assert.Equal((byte)1, beastsInfo.Level);
        Assert.Equal(1_000U, beastsInfo.Points);
        Assert.Equal(1U, beastsInfo.FixedLevel);
        Assert.Equal(1U, beastsInfo.Unknown2);
        Assert.Equal(1U, echo.Type);
        var item = Assert.Single(echo.Items);
        Assert.Equal(FeedUid, item.UID);
        Assert.Equal((ushort)1, item.Stack);
    }

    [Fact]
    public async Task LegacyBeastsOptFeedMissingInventoryItemEchoesCurrentInfoWithoutPointMutation()
    {
        var (handler, connection, sent) = await LoginWithRuneAndInventoryItemsAsync(initialBeastPoints: 700);
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBeastsOptPacket(
                Type: 1,
                Items: [new LegacyBeastsOptPacket.Item(UID: 9_999_001, Stack: 1)]).ToFrameBytes()));

        Assert.DoesNotContain(sent.Skip(countBeforeRequest), IsRemoveInventoryFrame);
        AssertShowEquipmentRefresh(sent[countBeforeRequest], alternante: 0);
        var (beastsInfo, echo) = AssertAppendedBeastsInfoAndEcho(sent, countBeforeRequest);
        Assert.True(beastsInfo.Activated);
        Assert.Equal((byte)1, beastsInfo.Level);
        Assert.Equal(700U, beastsInfo.Points);
        Assert.Equal(1U, echo.Type);
        var item = Assert.Single(echo.Items);
        Assert.Equal(9_999_001U, item.UID);
        Assert.Equal((ushort)1, item.Stack);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginWithRuneAndInventoryItemsAsync(
            uint initialBeastPoints,
            params PlayerItemSnapshotLoaderTests.TestDbItem[] inventoryItems)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteSnapshot(usersRoot, initialBeastPoints);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, CharacterUid + ".bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                inventoryItems.Concat(CreateEquippedRunes()).ToArray(),
                []));

        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(CharacterUid, "user02", ConquerRuntimeDefaults.WorldServer);
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot), handoff);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private static IReadOnlyList<PlayerItemSnapshotLoaderTests.TestDbItem> CreateEquippedRunes()
    {
        return Enumerable.Range(0, 5)
            .Select(index => PlayerItemSnapshotLoaderTests.DbItem(
                uid: (uint)(5_000_000 + index),
                itemId: (uint)(4_010_501 + index),
                position: (ushort)(101 + index),
                stackSize: 1))
            .ToArray();
    }

    private static (LegacyBeastsInfoPacket BeastsInfo, LegacyBeastsOptPacket Echo) AssertAppendedBeastsInfoAndEcho(
        IReadOnlyList<byte[]> sent,
        int countBeforeRequest)
    {
        Assert.True(
            sent.Count >= countBeforeRequest + 2,
            $"Expected at least two appended frames, but only saw {sent.Count - countBeforeRequest}.");
        Assert.True(LegacyBeastsInfoPacket.TryParse(ReadFrame(sent[^2]), out var beastsInfo));
        Assert.Equal(CharacterUid, beastsInfo.Uid);
        Assert.True(LegacyBeastsOptPacket.TryParse(ReadFrame(sent[^1]), out var echo));
        Assert.Equal(1U, echo.Type);
        return (beastsInfo, echo);
    }

    private static void AssertRemoveInventoryFrame(byte[] bytes, uint itemUid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var usage));
        Assert.Equal(itemUid, usage.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, usage.Action);
    }

    private static void AssertShowEquipmentRefresh(byte[] bytes, uint alternante)
    {
        Assert.True(LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, packet.Action);
        Assert.Equal(alternante, packet.Alternante);
    }

    private static bool IsRemoveInventoryFrame(byte[] bytes)
    {
        return MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var usage) &&
            usage.Action == MsgItemUsageAction.RemoveInventory;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void WriteSnapshot(string usersRoot, uint initialBeastPoints)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, CharacterUid + ".ini"),
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn=2
            Level=140
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            GuildDontion=900
            PkPoints=0
            Online=7
            Day={{DateTime.Now.DayOfYear}}
            TailedBeastActivated=True
            TailedBeastTotalPoints={{initialBeastPoints}}
            Achivement=0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/
            Activeness=0/80/2/1/10/1/22/0/0/
            Dragon=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Pheonix=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Tiger=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Turtle=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            ChiPoints=153850

            [PaidNobility]
            Rank=King
            Identifier={{CharacterUid}}
            """);
    }
}
