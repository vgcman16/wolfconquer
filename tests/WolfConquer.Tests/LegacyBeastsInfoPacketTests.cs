using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyBeastsInfoPacketTests : IDisposable
{
    private const uint CharacterUid = 1_293_600;

    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-beasts-info-handler-" + Guid.NewGuid());

    [Fact]
    public void LegacyBeastsInfoOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2341, WorldPacketOpcodes.LegacyBeastsInfo);
        Assert.Equal(WorldPacketOpcodes.LegacyBeastsInfo, LegacyBeastsInfoPacket.Opcode);
    }

    [Fact]
    public void LegacyBeastsInfoStartupSendsInactiveZeroPointSnapshots()
    {
        Assert.True(LegacyBeastsInfoPacket.TryCreateStartup(Snapshot(
            activated: false,
            totalPoints: 0,
            flag: 0), PlayerItemSnapshot.Empty, out var packet));

        Assert.Equal(1_293_600U, packet.Uid);
        Assert.Equal((byte)0, packet.Level);
        Assert.Equal(0U, packet.Points);
        Assert.Equal(1U, packet.FixedLevel);
        Assert.Equal(1U, packet.Unknown2);
        Assert.False(packet.Activated);
        Assert.Equal(0U, packet.Flag);
    }

    [Fact]
    public void LegacyBeastsInfoStartupUsesLiveBeastProgressionAndRuneActivation()
    {
        var snapshot = Snapshot(
            activated: true,
            totalPoints: 2_448_000,
            flag: 31);
        var playerItems = PlayerItemsWithRunePositions(101, 102, 103, 104, 105);

        Assert.True(LegacyBeastsInfoPacket.TryCreateStartup(snapshot, playerItems, out var packet));

        Assert.Equal(1_293_600U, packet.Uid);
        Assert.Equal((byte)100, packet.Level);
        Assert.Equal(19_200U, packet.Points);
        Assert.Equal(1U, packet.FixedLevel);
        Assert.Equal(1U, packet.Unknown2);
        Assert.True(packet.Activated);
        Assert.Equal(31U, packet.Flag);
    }

    [Fact]
    public void LegacyBeastsInfoStartupComputesActivatedFromEquippedRuneItems()
    {
        var snapshot = Snapshot(
            activated: true,
            totalPoints: 0,
            flag: 0);
        var fourEquippedAndOneBaggedRune = PlayerItemsWithRunePositions(101, 102, 103, 104, 211);

        Assert.True(LegacyBeastsInfoPacket.TryCreateStartup(snapshot, fourEquippedAndOneBaggedRune, out var packet));

        Assert.False(packet.Activated);
    }

    [Fact]
    public void LegacyBeastsInfoWritesRequiredUidAndOmitsDefaultOptionalFields()
    {
        var packet = new LegacyBeastsInfoPacket(
            Uid: 1_293_600,
            Level: 0,
            Points: 0,
            FixedLevel: 0,
            Unknown2: 0,
            Activated: false,
            Flag: 0);

        Assert.Equal(
            "0800250908a0fa4e",
            Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());
    }

    [Fact]
    public void LegacyBeastsInfoWritesLiveStartupProtobufShape()
    {
        Assert.True(LegacyBeastsInfoPacket.TryCreateStartup(
            Snapshot(activated: true, totalPoints: 2_448_000, flag: 31),
            PlayerItemsWithRunePositions(101, 102, 103, 104, 105),
            out var packet));

        Assert.Equal(
            "1600250908a0fa4e106418809601200128013001381f",
            Convert.ToHexString(packet.ToFrameBytes()).ToLowerInvariant());
    }

    [Fact]
    public void LegacyBeastsInfoRoundTrips()
    {
        var packet = new LegacyBeastsInfoPacket(
            Uid: 1_293_600,
            Level: 100,
            Points: 19_200,
            FixedLevel: 1,
            Unknown2: 1,
            Activated: true,
            Flag: 31);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyBeastsInfoPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyBeastsInfoParseRequiresUidButAcceptsOmittedDefaults()
    {
        var frame = ReadFrame(new LegacyBeastsInfoPacket(
            Uid: 1_293_600,
            Level: 0,
            Points: 0,
            FixedLevel: 0,
            Unknown2: 0,
            Activated: false,
            Flag: 0).ToFrameBytes());

        Assert.True(LegacyBeastsInfoPacket.TryParse(frame, out var parsed));

        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal((byte)0, parsed.Level);
        Assert.Equal(0U, parsed.Points);
        Assert.Equal(0U, parsed.FixedLevel);
        Assert.Equal(0U, parsed.Unknown2);
        Assert.False(parsed.Activated);
        Assert.Equal(0U, parsed.Flag);
    }

    [Fact]
    public void LegacyBeastsInfoParseRejectsMissingUid()
    {
        var writer = new PacketWriter(LegacyBeastsInfoPacket.Opcode, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyBeastsInfoPacket.TryParse(frame, out _));
    }

    [Fact]
    public async Task HandlerLegacyBeastsInfoCurrentSessionQuerySendsCurrentBeastsInfo()
    {
        var (handler, connection, sent) = await LoginWithBeastProgressAndRuneItemsAsync();
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBeastsInfoPacket(
                Uid: CharacterUid,
                Level: 0,
                Points: 0,
                FixedLevel: 0,
                Unknown2: 0,
                Activated: false,
                Flag: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest + 1, sent.Count);
        Assert.True(LegacyBeastsInfoPacket.TryParse(ReadFrame(sent[countBeforeRequest]), out var response));
        Assert.Equal(CharacterUid, response.Uid);
        Assert.Equal((byte)100, response.Level);
        Assert.Equal(19_200U, response.Points);
        Assert.Equal(1U, response.FixedLevel);
        Assert.Equal(1U, response.Unknown2);
        Assert.True(response.Activated);
        Assert.Equal(31U, response.Flag);
    }

    [Fact]
    public async Task HandlerLegacyBeastsInfoMissingActiveSessionSendsNoFrame()
    {
        var (handler, connection, sent) = await LoginWithBeastProgressAndRuneItemsAsync();
        var countBeforeRequest = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyBeastsInfoPacket(
                Uid: 9_999_001,
                Level: 0,
                Points: 0,
                FixedLevel: 0,
                Unknown2: 0,
                Activated: false,
                Flag: 0).ToFrameBytes()));

        Assert.Equal(countBeforeRequest, sent.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private static CharacterSnapshot Snapshot(bool activated, uint totalPoints, uint flag)
    {
        return CharacterSnapshotParser.ParseText($$"""
            [Character]
            UID=1293600
            TailedBeastActivated={{activated}}
            TailedBeastTotalPoints={{totalPoints}}
            TailedBeastFlag={{flag}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
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
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0
            """);
    }

    private static PlayerItemSnapshot PlayerItemsWithRunePositions(params ushort[] positions)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: [],
            RuneItems: positions
                .Select((position, index) => new PlayerItemSnapshotItem
                {
                    Uid = (uint)(10_000 + index),
                    ItemId = 402005,
                    Position = position,
                })
                .ToArray(),
            WardrobeItems: []);
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginWithBeastProgressAndRuneItemsAsync()
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(itemsRoot);
        WriteLoginSnapshot(usersRoot);
        File.WriteAllBytes(
            Path.Combine(itemsRoot, CharacterUid + ".bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(CreateEquippedRuneDbItems(), []));

        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(usersRoot));
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(CharacterUid).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private static IReadOnlyList<PlayerItemSnapshotLoaderTests.TestDbItem> CreateEquippedRuneDbItems()
    {
        return Enumerable.Range(0, 5)
            .Select(index => PlayerItemSnapshotLoaderTests.DbItem(
                uid: (uint)(5_000_000 + index),
                itemId: (uint)(4_010_501 + index),
                position: (ushort)(101 + index),
                stackSize: 1))
            .ToArray();
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void WriteLoginSnapshot(string usersRoot)
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
            TailedBeastTotalPoints=2448000
            TailedBeastFlag=31
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
