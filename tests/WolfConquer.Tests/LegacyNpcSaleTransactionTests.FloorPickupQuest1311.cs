using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    private const uint PickupQuestItemId = 711_352;
    private const string PickupQuestItemName = "Quest1311Loot";
    private string PickupQuestRoot => Path.Combine(root, "Quests");
    private string PickupQuestSpellsRoot => Path.Combine(root, "PlayersSpells");
    private string PickupQuestProficienciesRoot => Path.Combine(root, "PlayersProfs");
    private string PickupQuestCombatGearRoot => Path.Combine(root, "PlayersCombatGear");
    private string PickupQuestPath(uint uid) => Path.Combine(PickupQuestRoot, uid + ".bin");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedQuest1311CreditsOnePerWholeStackPickupPersistsBeforeObjectiveAndCannotReplay(bool emptyVector)
    {
        const uint uid = 1_299_901;
        uint[] intentions = emptyVector ? [] : [uint.MaxValue, 9U, 77U];
        var quests = SeedPickupQuest(uid, PlayerQuestStatus.Accepted, intentions);
        var expected = quests.WithQuest(quests.Quests[0] with
        { Intentions = emptyVector ? [1U] : [0U, 9U, 77U] });
        var owner = NewQuestPickupOwner();
        Player? player = null;
        var armed = false;
        var observed = 0;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || ReadFrame(bytes).Header.Opcode != MsgQuestDataPacket.Opcode) return;
            observed++;
            callbackFailure ??= Record.Exception(() =>
            {
                Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected), File.ReadAllBytes(PickupQuestPath(uid)));
                Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected),
                    PlayerQuestSnapshotLoader.Serialize(Current(owner, player!).Character.PlayerQuests));
            });
        });
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(quests),
            PlayerQuestSnapshotLoader.Serialize(Current(owner, player).Character.PlayerQuests));
        var item = SeedQuest1311Floor(owner, player);
        var first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        Assert.Null(callbackFailure);
        Assert.Equal(1, observed);
        AssertQuest1311PickupFrames(player.Sent.Skip(first).ToArray(), expected.Quests[0].Intentions);
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected), File.ReadAllBytes(PickupQuestPath(uid)));
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected), PlayerQuestSnapshotLoader.Serialize(
            new PlayerQuestSnapshotLoader(PickupQuestRoot).LoadByCharacterUid(uid)));
        Assert.Equal(PlayerQuestStatus.Accepted, Current(owner, player).Character.PlayerQuests.Quests[0].Status);
        Assert.Equal(123U, Current(owner, player).Character.PlayerQuests.Quests[0].Time);
        Assert.Equal((ushort)3, Assert.Single(Current(owner, player).PlayerItems.InventoryItems, entry => entry.Uid == item.Uid).StackSize);
        var completed = Current(owner, player);
        first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        Assert.Same(completed, Current(owner, player));
        Assert.Empty(player.Sent.Skip(first));
        Assert.Equal(1, observed);
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected), File.ReadAllBytes(PickupQuestPath(uid)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinishedOrMissingQuest1311DoesNotAutoAcceptCreditOrSendObjectiveAfterPickup(bool missing)
    {
        const uint uid = 1_299_911;
        var quests = SeedPickupQuest(uid, missing ? null : PlayerQuestStatus.Finished, [5U, 9U]);
        var owner = NewQuestPickupOwner();
        var player = await Login(owner, uid);
        var item = SeedQuest1311Floor(owner, player);
        var questBytes = File.ReadAllBytes(PickupQuestPath(uid));
        var first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        AssertQuest1311PickupFrames(player.Sent.Skip(first).ToArray(), expectedIntentions: null);
        Assert.Equal(questBytes, File.ReadAllBytes(PickupQuestPath(uid)));
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(quests),
            PlayerQuestSnapshotLoader.Serialize(Current(owner, player).Character.PlayerQuests));
        Assert.Contains(Current(owner, player).PlayerItems.InventoryItems, entry => entry.Uid == item.Uid && entry.StackSize == 3);
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Quest1311FollowUpUsesLatestWalletItemsAndQuestPublishedByPickupMessageCallback()
    {
        const uint uid = 1_299_921;
        SeedPickupQuest(uid, PlayerQuestStatus.Accepted, [2U, 9U]);
        var owner = NewQuestPickupOwner();
        Player? player = null;
        WorldSession? latest = null;
        var armed = false;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || !IsQuest1311PickupMessage(bytes)) return;
            callbackFailure = Record.Exception(() =>
            {
                var current = Current(owner, player!);
                var quests = current.Character.PlayerQuests.WithQuest(new(1311, PlayerQuestStatus.Accepted, 543, [10U, 99U]))
                    .WithQuest(new(9002, PlayerQuestStatus.Accepted, 999, [41U]));
                latest = current with
                {
                    Character = current.Character with { Money = 7_777, PlayerQuests = quests },
                    PlayerItems = current.PlayerItems with
                    {
                        InventoryItems = current.PlayerItems.InventoryItems.Append(new PlayerItemSnapshotItem
                        { Uid = KeptUid + 20, ItemId = ItemId, StackSize = 1, Color = 5 }).ToArray(),
                    },
                    ActiveNpcId = 777,
                };
                owner.Sessions.Set(player!.Connection, latest);
                Assert.Equal(PlayerQuestSnapshotLoader.Serialize(quests), File.ReadAllBytes(PickupQuestPath(uid)));
            });
        });
        SeedQuest1311Floor(owner, player);
        var first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        Assert.Null(callbackFailure);
        Assert.NotNull(latest);
        var expected = latest.Character.PlayerQuests.WithQuest(new(1311, PlayerQuestStatus.Accepted, 543, [11U, 99U]));
        var after = Current(owner, player);
        Assert.Equal(7_777, after.Character.Money);
        Assert.Equal(777U, after.ActiveNpcId);
        Assert.Same(latest.PlayerItems, after.PlayerItems);
        Assert.Equal(latest with { Character = latest.Character with { PlayerQuests = after.Character.PlayerQuests } }, after);
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected), PlayerQuestSnapshotLoader.Serialize(after.Character.PlayerQuests));
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(expected), File.ReadAllBytes(PickupQuestPath(uid)));
        Assert.Equal(7_777, CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid))).Money);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(latest.PlayerItems), File.ReadAllBytes(ItemPath(uid)));
        AssertQuest1311PickupFrames(player.Sent.Skip(first).ToArray(), [11U, 99U]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Quest1311FollowUpCannotCreditRemovedSessionOrReplacementSameUidPlayer(bool replacePlayer)
    {
        const uint uid = 1_299_931;
        SeedPickupQuest(uid, PlayerQuestStatus.Accepted, [5U, 9U]);
        var owner = NewQuestPickupOwner();
        Player? player = null;
        WorldSession? pickedUp = null;
        var armed = false;
        var invalidated = false;
        Exception? callbackFailure = null;
        player = await Login(owner, uid, bytes =>
        {
            if (!armed || !IsQuest1311PickupMessage(bytes)) return;
            callbackFailure = Record.Exception(() =>
            {
                pickedUp = Current(owner, player!);
                if (replacePlayer)
                {
                    var client = player!.Connection.NativeGameClient!;
                    client.Player = new LegacyNativePlayerRuntime(client) { UID = uid, ServerID = 42, IsCheckedPass = true };
                    invalidated = true;
                }
                else invalidated = owner.Sessions.TryRemove(player!.Connection, out _);
            });
        });
        var before = Current(owner, player);
        var item = SeedQuest1311Floor(owner, player);
        var questBytes = File.ReadAllBytes(PickupQuestPath(uid));
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;
        armed = true;

        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        Assert.Null(callbackFailure);
        Assert.True(invalidated);
        Assert.NotNull(pickedUp);
        AssertQuest1311PickupFrames(player.Sent.Skip(first).ToArray(), expectedIntentions: null);
        Assert.Equal(questBytes, File.ReadAllBytes(PickupQuestPath(uid)));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(before.Character.PlayerQuests),
            PlayerQuestSnapshotLoader.Serialize(pickedUp.Character.PlayerQuests));
        AssertPickupQuestAssetCommitted(uid, before, item);
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        if (replacePlayer) Assert.Same(pickedUp, Current(owner, player));
        else Assert.False(owner.Sessions.TryGet(player.Connection, out _));
    }

    [WindowsD13QuestPickupFact]
    public async Task LockedQuestReplacementDoesNotCreditButLeavesSuccessfulPickupCommitted()
    {
        const uint uid = 1_299_941;
        SeedPickupQuest(uid, PlayerQuestStatus.Accepted, [5U, 9U]);
        var owner = NewQuestPickupOwner();
        var player = await Login(owner, uid);
        var before = Current(owner, player);
        var item = SeedQuest1311Floor(owner, player);
        var questBytes = File.ReadAllBytes(PickupQuestPath(uid));
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var first = player.Sent.Count;

        using (File.Open(PickupQuestPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
            await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        AssertQuest1311PickupFrames(player.Sent.Skip(first).ToArray(), expectedIntentions: null);
        Assert.Equal(questBytes, File.ReadAllBytes(PickupQuestPath(uid)));
        Assert.Equal(PlayerQuestSnapshotLoader.Serialize(before.Character.PlayerQuests),
            PlayerQuestSnapshotLoader.Serialize(Current(owner, player).Character.PlayerQuests));
        Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
        AssertPickupQuestAssetCommitted(uid, before, item);
        Assert.Empty(DropFloor(owner).SnapshotVisible(DateTimeOffset.UtcNow));
        first = player.Sent.Count;

        await PickupAdmissionFloor(owner, player, AdmissionItemFloorUid);

        Assert.Empty(player.Sent.Skip(first));
        Assert.Equal(questBytes, File.ReadAllBytes(PickupQuestPath(uid)));
    }

    private Owner NewQuestPickupOwner()
    {
        Directory.CreateDirectory(PickupQuestRoot);
        var log = new StringWriter();
        var data = LegacyItemWorldDataFixture.Create(QuestPickupItemType(ItemId, "OrdinaryFixture"),
            QuestPickupItemType(PickupQuestItemId, PickupQuestItemName));
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(UsersRoot, ItemsRoot,
                playerSpellsRoot: PickupQuestSpellsRoot, playerProficienciesRoot: PickupQuestProficienciesRoot,
                playersCombatGearRoot: PickupQuestCombatGearRoot, playerQuestsRoot: PickupQuestRoot),
            handoffService: null, worldData: data,
            runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled with { LocalServerIdentity = new(42) },
            log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return new(handler, Assert.IsType<WorldSessionStore>(field.GetValue(handler)), log);
    }

    private PlayerQuestSnapshot SeedPickupQuest(uint uid, PlayerQuestStatus? status, IReadOnlyList<uint> intentions)
    {
        Seed(uid);
        foreach (var participant in new[] { PickupQuestRoot, PickupQuestSpellsRoot,
                     PickupQuestProficienciesRoot, PickupQuestCombatGearRoot }) Directory.CreateDirectory(participant);
        new PlayerSpellSnapshotLoader(PickupQuestSpellsRoot).SaveByCharacterUid(uid, PlayerSpellSnapshot.Empty);
        new PlayerProficiencySnapshotLoader(PickupQuestProficienciesRoot).SaveByCharacterUid(uid, PlayerProficiencySnapshot.Empty);
        new CombatGearSnapshotLoader(PickupQuestCombatGearRoot).SaveByCharacterUid(uid, CombatGearSnapshot.Empty);
        var sentinel = new PlayerQuestSnapshotEntry(9001, PlayerQuestStatus.Finished, 456, [7U, 8U]);
        var quests = status is { } value
            ? new PlayerQuestSnapshot([new(1311, value, 123, intentions), sentinel])
            : new PlayerQuestSnapshot([sentinel]);
        new PlayerQuestSnapshotLoader(PickupQuestRoot).SaveByCharacterUid(uid, quests);
        return quests;
    }

    private PlayerItemSnapshotItem SeedQuest1311Floor(Owner owner, Player player)
    {
        var item = AdmissionFloorItem().WithItemId(PickupQuestItemId);
        var character = Current(owner, player).Character;
        var packet = LegacyFloorItemPacket.FromDroppedItem(item, character, AdmissionItemFloorUid);
        DropFloor(owner).AddVisible(FloorItemRuntimeEntry.ForItem(character.MapId, packet, item));
        return item;
    }

    private static ItemType QuestPickupItemType(uint id, string name)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = "10";
        return new((int)id, name, fields);
    }

    private void AssertPickupQuestAssetCommitted(uint uid, WorldSession before, PlayerItemSnapshotItem item)
    {
        // Native Send clears only this ordinary quest item's Plus. Preserve the
        // complete independent inventory image, including unrelated metadata.
        var expected = ExpectedPickupInventoryBytes(before.PlayerItems, item.WithPosition(0),
            merge: false, stack: item.StackSize);
        Assert.Equal(expected, File.ReadAllBytes(ItemPath(uid)));
    }

    private static bool IsQuest1311PickupMessage(byte[] bytes) =>
        LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Content.Count > 3 &&
        packet.Content[3] == "You have picked up a " + PickupQuestItemName + ".";

    private static void AssertQuest1311PickupFrames(IReadOnlyList<byte[]> frames, IReadOnlyList<uint>? expectedIntentions)
    {
        Assert.Equal(expectedIntentions is null ? 3 : 4, frames.Count);
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(frames[0]), out var item));
        Assert.Equal(PickupQuestItemId, item.ItemId);
        Assert.Equal(AdmissionPickedItemUid, item.Uid);
        Assert.Equal((ushort)3, item.StackSize);
        Assert.True(IsAdmissionFloorRemoval(frames[1], AdmissionItemFloorUid));
        Assert.True(IsQuest1311PickupMessage(frames[2]));
        if (expectedIntentions is null) return;
        Assert.True(MsgQuestDataPacket.TryParseObjectiveFrame(ReadFrame(frames[3]), out var objective));
        Assert.Equal(1311U, objective.Uid);
        Assert.Equal((ushort)0, objective.Unknown);
        Assert.Equal(checked((ushort)expectedIntentions.Count), objective.Count);
        Assert.Equal(expectedIntentions, objective.Intentions);
        // Native MsgBuilder.MsgQuestDataCreate always appends one uint zero.
        Assert.Equal(0U, Assert.Single(objective.ExtraValues));
    }

    private sealed class WindowsD13QuestPickupFactAttribute : FactAttribute
    {
        public WindowsD13QuestPickupFactAttribute()
        { if (!OperatingSystem.IsWindows()) Skip = "Requires Windows quest-file replacement sharing refusal."; }
    }
}
