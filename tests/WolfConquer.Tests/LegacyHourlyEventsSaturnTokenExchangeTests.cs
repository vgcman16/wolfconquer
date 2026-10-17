using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyHourlyEventsSaturnTokenExchangeTests : IDisposable
{
    private const uint EventsInHourNpcId = 5_595;
    private const uint ArrestTokenItemId = 711_624;
    private const uint ArrestTokenUid = 7_116_240;
    private const long CharacterUid = 1_293_600;
    private const long AccountId = 100;
    private const long ExchangeConquerPoints = 200_000;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-hourly-saturn-exchange-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option107ConsumesOneArrestTokenGrantsConquerPointsAndPersistsBothSnapshots()
    {
        var usersRoot = Path.Combine(_root, "Users");
        var itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        var characterPath = WriteCharacterSnapshot(usersRoot, conquerPoints: 25);
        var initialItems = Items(new PlayerItemSnapshotItem
        {
            Uid = ArrestTokenUid,
            ItemId = ArrestTokenItemId,
            Position = 0,
            StackSize = 1,
        });
        new PlayerItemSnapshotLoader(itemsRoot).SaveByCharacterUid(CharacterUid, initialItems);

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(initialItems, usersRoot, conquerPoints: 25);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerItemsRoot: itemsRoot),
            session,
            EventsInHourNpcId,
            optionId: 107,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(ExchangeConquerPoints + 25, updated.Character.ConquerPoints);
        Assert.DoesNotContain(updated.PlayerItems.InventoryItems, item => item.ItemId == ArrestTokenItemId);

        var persistedCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
        Assert.Equal(ExchangeConquerPoints + 25, persistedCharacter.ConquerPoints);
        var persistedItems = new PlayerItemSnapshotLoader(itemsRoot).LoadByCharacterUid(CharacterUid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == ArrestTokenItemId);

        var removalIndex = sent.FindIndex(IsArrestTokenRemoval);
        var walletIndex = sent.FindIndex(IsConquerPointsUpdate);
        var dialogIndex = sent.FindIndex(bytes => IsNpcDialog(bytes, "Congratulations! You received 200,000 Conquer Points."));
        Assert.True(removalIndex >= 0);
        Assert.True(walletIndex > removalIndex);
        Assert.True(dialogIndex > walletIndex);

        var output = log.ToString();
        Assert.Contains("source=NpcHandler.EventsInHour option=107 itemId=711624 consumed=1", output);
        Assert.Contains("persisted LegacyNpc HourlyEvents Saturn exchange character", output);
        Assert.Contains("persisted LegacyNpc HourlyEvents Saturn exchange items", output);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc HourlyEvents Saturn exchange", output);
    }

    [Fact]
    public async Task Option107WithoutArrestTokenReturnsSourceFailureDialogWithoutMutation()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(PlayerItemSnapshot.Empty, usersRoot: null, conquerPoints: 25);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyHourlyEventsNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            EventsInHourNpcId,
            optionId: 107,
            CancellationToken.None);

        Assert.True(handled);
        Assert.True(sessions.TryGet(connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal(25, updated.Character.ConquerPoints);
        Assert.Empty(updated.PlayerItems.InventoryItems);
        Assert.DoesNotContain(sent, IsArrestTokenRemoval);
        Assert.DoesNotContain(sent, IsConquerPointsUpdate);
        Assert.Contains(sent, bytes => IsNpcDialog(bytes, "You don't have enough Arrest Tokens!"));
        Assert.Contains("source inventory token is missing", log.ToString());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot items,
        string? usersRoot,
        long conquerPoints)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "hourly-saturn-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "SaturnTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = conquerPoints.ToString(System.Globalization.CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventory)
    {
        return new PlayerItemSnapshot(inventory, [], [], [], []);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static bool IsArrestTokenRemoval(byte[] bytes)
    {
        return MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Id == ArrestTokenUid &&
            packet.Action == MsgItemUsageAction.RemoveInventory;
    }

    private static bool IsConquerPointsUpdate(byte[] bytes)
    {
        return LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Updates.Any(update =>
                update.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                update.Value == checked((ulong)(ExchangeConquerPoints + 25)));
    }

    private static bool IsNpcDialog(byte[] bytes, string expectedText)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.InteractType == LegacyNpcReplyInteractType.Dialog &&
            packet.Text == expectedText;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static string WriteCharacterSnapshot(string usersRoot, long conquerPoints)
    {
        var path = Path.Combine(usersRoot, AccountId + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{CharacterUid}}
            Body=1003
            Face=0
            Name=SaturnTester
            Class=7051
            Map=1002
            X=430
            Y=378
            FirstClass=7051
            SecoundeClass=0
            Reborn=0
            Level=140
            Haire=0
            Experience=0
            PMap=1002
            PMapX=430
            PMapY=378
            Agility=0
            Strength=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=1
            MinMana=0
            ConquerPoints={{conquerPoints}}
            BankCps=0
            BoundConquerPoints=0
            Money=0
            PkPoints=0
            Online=0

            [PaidNobility]
            Identifier=0
            LastNobilityDonation=0
            PaidRank=0
            PeriodTime=0
            IsActive=False
            Rank=Serf
            Position=-1
            """);
        return path;
    }
}
