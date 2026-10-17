using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyHourlyEventsTokenFollowupTests : IDisposable
{
    private const uint EventsInHourNpcId = 5_595;
    private const uint ArrestTokenItemId = 711_624;
    private const uint ArrestTokenUid = 7_116_240;
    private const long CharacterUid = 1_293_600;
    private const long AccountId = CharacterUid;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "wolf-hourly-token-followup-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Option109ConsumesFiveUnitsAndGrantsExactly500ClassExperience()
    {
        var result = await InvokeAsync(
            optionId: 109,
            Items(Token(stackSize: 7)),
            classExperience: 25);

        Assert.True(result.Handled);
        Assert.NotSame(result.InitialSession, result.UpdatedSession);
        Assert.Equal("525", result.UpdatedSession.Character.Attributes["ClassExperience"]);
        var remaining = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == ArrestTokenItemId);
        Assert.Equal((ushort)2, remaining.StackSize);

        var unitUpdates = result.Sent.Take(5).Select(AssertGameItem).ToArray();
        Assert.Equal([(ushort)6, 5, 4, 3, 2], unitUpdates.Select(packet => packet.StackSize).ToArray());
        Assert.All(unitUpdates, packet =>
        {
            Assert.Equal(ArrestTokenItemId, packet.ItemId);
            Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        });

        Assert.Contains("option=109 itemId=711624 consumed=5", result.Log);
        Assert.Contains("updatedClassExperience=525", result.Log);
        Assert.Contains("sourceDialogClassExperience=5000", result.Log);
    }

    [Fact]
    public async Task Option109WithFourTokensSendsSourceFailureDialogWithoutMutation()
    {
        var result = await InvokeAsync(
            optionId: 109,
            Items(Token(stackSize: 4)),
            classExperience: 25);

        Assert.True(result.Handled);
        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        Assert.Equal("25", result.PersistedCharacter.Attributes["ClassExperience"]);
        Assert.Equal((ushort)4, Assert.Single(result.PersistedItems.InventoryItems).StackSize);
        AssertExactDialog(
            result.Sent,
            "You don't have enough Arrest Tokens!",
            "I understand.");
        Assert.Contains("option=109 itemId=711624 required=5", result.Log);
    }

    [Fact]
    public async Task Option109AcceptsAndConsumesBoundArrestTokenUnits()
    {
        var result = await InvokeAsync(
            optionId: 109,
            Items(Token(stackSize: 6, bound: 1)),
            classExperience: 25);

        Assert.Equal("525", result.UpdatedSession.Character.Attributes["ClassExperience"]);
        var remaining = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(ArrestTokenItemId, remaining.ItemId);
        Assert.Equal((byte)1, remaining.Bound);
        Assert.Equal((ushort)1, remaining.StackSize);

        var unitUpdates = result.Sent.Take(5).Select(AssertGameItem).ToArray();
        Assert.Equal([(ushort)5, 4, 3, 2, 1], unitUpdates.Select(packet => packet.StackSize).ToArray());
        Assert.All(unitUpdates, packet => Assert.Equal((byte)1, packet.Bound));
    }

    [Fact]
    public async Task Option109RemovesTheWholeFiveTokenStack()
    {
        var result = await InvokeAsync(
            optionId: 109,
            Items(Token(stackSize: 5)),
            classExperience: 25);

        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == ArrestTokenItemId);
        Assert.Equal(
            [(ushort)4, 3, 2, 1],
            result.Sent.Take(4).Select(AssertGameItem).Select(packet => packet.StackSize).ToArray());
        AssertArrestTokenRemoval(result.Sent[4]);
        Assert.DoesNotContain(
            result.PersistedItems.InventoryItems,
            item => item.ItemId == ArrestTokenItemId);
    }

    [Fact]
    public async Task Option109SendsInventoryThenClassExperienceThenSourceDialogOrder()
    {
        var result = await InvokeAsync(
            optionId: 109,
            Items(Token(stackSize: 5)),
            classExperience: 25);

        Assert.Equal(9, result.Sent.Count);
        Assert.Equal(
            [(ushort)4, 3, 2, 1],
            result.Sent.Take(4).Select(AssertGameItem).Select(packet => packet.StackSize).ToArray());
        AssertArrestTokenRemoval(result.Sent[4]);
        AssertClassExperienceUpdate(result.Sent[5], expectedValue: 525);
        AssertNpcReply(
            result.Sent[6],
            LegacyNpcReplyInteractType.Dialog,
            "Congratulations! You received 5000 Divine Class Experience.");
        AssertNpcReply(
            result.Sent[7],
            LegacyNpcReplyInteractType.Option,
            "Thank you.",
            optionId: byte.MaxValue);
        AssertNpcReply(result.Sent[8], LegacyNpcReplyInteractType.Finish, "");
    }

    [Fact]
    public async Task Option109PersistsCharacterAndItemsBeforeSessionCommit()
    {
        var result = await InvokeAsync(
            optionId: 109,
            Items(Token(stackSize: 6)),
            classExperience: 25);

        Assert.Equal(1, result.CommitCount);
        var characterAtCommit = Assert.IsType<CharacterSnapshot>(result.CharacterAtCommit);
        Assert.Equal("525", characterAtCommit.Attributes["ClassExperience"]);
        var itemsAtCommit = Assert.IsType<PlayerItemSnapshot>(result.ItemsAtCommit);
        Assert.Equal((ushort)1, Assert.Single(itemsAtCommit.InventoryItems).StackSize);

        Assert.Equal("525", result.PersistedCharacter.Attributes["ClassExperience"]);
        Assert.Equal((ushort)1, Assert.Single(result.PersistedItems.InventoryItems).StackSize);
        Assert.Contains("persisted LegacyNpc HourlyEvents Saturn class experience exchange character", result.Log);
        Assert.Contains("persisted LegacyNpc HourlyEvents Saturn class experience exchange items", result.Log);
        Assert.DoesNotContain(
            "PARITY-GAP LegacyNpc HourlyEvents Saturn class experience exchange persistence",
            result.Log);
    }

    [Theory]
    [InlineData(111)]
    [InlineData(112)]
    [InlineData(113)]
    public async Task FragmentOptionsAreSourceNoOpsBecauseInnerCasesCannotMatch(byte optionId)
    {
        var result = await InvokeAsync(
            optionId,
            Items(Token(stackSize: 10)),
            classExperience: 25);

        Assert.True(result.Handled);
        Assert.Same(result.InitialSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        Assert.Empty(result.Sent);
        Assert.Equal("25", result.PersistedCharacter.Attributes["ClassExperience"]);
        Assert.Equal((ushort)10, Assert.Single(result.PersistedItems.InventoryItems).StackSize);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId is 3_306_366 or 3_306_367 or 3_306_368);
        Assert.Contains($"option={optionId}", result.Log);
        Assert.Contains("outerOptions=111,112,113 innerOptions=11,12,13", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc HourlyEvents", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        byte optionId,
        PlayerItemSnapshot initialItems,
        ulong classExperience)
    {
        var usersRoot = Path.Combine(_root, "Users");
        var itemsRoot = Path.Combine(_root, "PlayersItems");
        Directory.CreateDirectory(usersRoot);
        WriteCharacterSnapshot(usersRoot, classExperience);

        var itemLoader = new PlayerItemSnapshotLoader(itemsRoot);
        itemLoader.SaveByCharacterUid(CharacterUid, initialItems);
        var characterLoader = new CharacterSnapshotLoader(usersRoot);

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(initialItems, usersRoot, classExperience);
        var commitCount = 0;
        CharacterSnapshot? characterAtCommit = null;
        PlayerItemSnapshot? itemsAtCommit = null;
        var sessions = new WorldSessionStore((_, previous, _) =>
        {
            if (previous is null)
            {
                return;
            }

            commitCount++;
            characterAtCommit = characterLoader.LoadByAccountId(AccountId);
            itemsAtCommit = itemLoader.LoadByCharacterUid(CharacterUid);
        });
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
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        var persistedCharacter = Assert.IsType<CharacterSnapshot>(
            characterLoader.LoadByAccountId(AccountId));
        var persistedItems = itemLoader.LoadByCharacterUid(CharacterUid);
        return new InvokeResult(
            handled,
            session,
            updatedSession,
            commitCount,
            characterAtCommit,
            itemsAtCommit,
            persistedCharacter,
            persistedItems,
            sent,
            log.ToString());
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot items,
        string usersRoot,
        ulong classExperience)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "hourly-token-followup-test",
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
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ClassExperience"] = classExperience.ToString(CultureInfo.InvariantCulture),
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

    private static PlayerItemSnapshotItem Token(ushort stackSize, byte bound = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = ArrestTokenUid,
            ItemId = ArrestTokenItemId,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
        };
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertArrestTokenRemoval(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(ArrestTokenUid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static void AssertClassExperienceUpdate(byte[] bytes, ulong expectedValue)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ClassExperience, update.Type);
        Assert.Equal(expectedValue, update.Value);
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string dialogText,
        string optionText)
    {
        Assert.Equal(3, sent.Count);
        AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog, dialogText);
        AssertNpcReply(
            sent[1],
            LegacyNpcReplyInteractType.Option,
            optionText,
            optionId: byte.MaxValue);
        AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Finish, "");
    }

    private static void AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType interactType,
        string text,
        byte optionId = 0)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(interactType, packet.InteractType);
        Assert.Equal(text, packet.Text);
        Assert.Equal(optionId, packet.OptionId);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void WriteCharacterSnapshot(string usersRoot, ulong classExperience)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, AccountId + ".ini"),
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
            ClassExperience={{classExperience}}
            ConquerPoints=0
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
    }

    private sealed record InvokeResult(
        bool Handled,
        WorldSession InitialSession,
        WorldSession UpdatedSession,
        int CommitCount,
        CharacterSnapshot? CharacterAtCommit,
        PlayerItemSnapshot? ItemsAtCommit,
        CharacterSnapshot PersistedCharacter,
        PlayerItemSnapshot PersistedItems,
        IReadOnlyList<byte[]> Sent,
        string Log);
}
