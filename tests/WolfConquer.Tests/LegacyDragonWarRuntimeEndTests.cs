using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonWarRuntimeEndTests
{
    private const int LegacyDragonWarMapId = 700;
    private const int LegacyDragonSwingFlag = 0xA0;
    private const int TwinCityMapId = 1002;

    [Fact]
    public void EndRewardEconomyPolicyPreservesSourceObservedValuesAndTunedValues()
    {
        var policy = LegacyDragonWarRuntimeStateStore.EndRewardEconomyPolicy;

        Assert.Equal("MrRayzo.Game.MsgTournaments.MsgDragonWar", policy.SourceRewardClassName);
        Assert.Equal("CheckUp", policy.SourceRewardMethodName);
        Assert.Equal("MrRayzo.Game.MsgTournaments.MsgDragonWar.CheckUp", policy.SourceRewardOwnerName);
        Assert.Equal(1_000_000, policy.SourceObservedWinnerConquerPointsReward);
        Assert.Equal(75_000, policy.TunedWinnerConquerPointsReward);
        Assert.Equal((ulong)500, policy.SourceObservedWinnerClassExperienceReward);
        Assert.Equal((ulong)500, policy.TunedWinnerClassExperienceReward);
        Assert.Equal(722_057U, policy.SourceObservedWinnerItemId);
        Assert.Equal(722_057U, policy.TunedWinnerItemId);
        Assert.Equal((ushort)4, policy.SourceObservedWinnerItemStackSize);
        Assert.Equal((ushort)1, policy.TunedWinnerItemStackSize);
        Assert.Equal(2, policy.SourceObservedWinnerItemRequiredFreeSlots);
        Assert.Equal(1, policy.TunedWinnerItemRequiredFreeSlots);
        Assert.Equal(
            "You received 75000 ConquerPoints, 500 ClassExperience and 1 PowerExpBall. ",
            policy.TunedWinnerMessage);
        Assert.True(
            policy.SourceObservedWinnerConquerPointsReward > policy.TunedWinnerConquerPointsReward);
        Assert.True(policy.SourceObservedWinnerItemStackSize > policy.TunedWinnerItemStackSize);
    }

    [Fact]
    public void CreateDueRuntimeUpdatesEndsAliveDragonWarWithTunedRewardsMessagesAndCleanup()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var policy = LegacyDragonWarRuntimeStateStore.EndRewardEconomyPolicy;
        var openedAt = new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero);
        var sessions = new[]
        {
            CreateSession(
                uid: 1,
                name: "Alpha",
                conquerPoints: 100,
                attributes: DragonWarAttributes(score: 90, classExperience: 125, hasDragonSwing: true)),
            CreateSession(
                uid: 2,
                name: "Bravo",
                conquerPoints: 200,
                attributes: DragonWarAttributes(score: 70, classExperience: 25, hasDragonSwing: true)),
            CreateSession(
                uid: 3,
                name: "Charlie",
                conquerPoints: 300,
                attributes: DragonWarAttributes(score: 30, classExperience: 75, hasDragonSwing: false)),
        };
        store.Open(openedAt);

        var joinedSessions = sessions.Select(session =>
        {
            Assert.True(store.TryJoin(session, out var joined));
            var attributes = new Dictionary<string, string>(
                joined.Character.Attributes,
                StringComparer.OrdinalIgnoreCase);
            foreach (var attribute in session.Character.Attributes)
            {
                attributes[attribute.Key] = attribute.Value;
            }

            return joined with { Character = joined.Character with { Attributes = attributes } };
        }).ToList();
        store.SetProcess(LegacyDragonWarProcess.Alive);

        var tick = store.CreateDueRuntimeUpdates(openedAt.AddMinutes(5).AddSeconds(1), joinedSessions);

        Assert.Contains(
            tick.Updates,
            update => update.Broadcast &&
                update.ChatType == LegacyMsgMessagePacket.CenterChatType &&
                update.Message == "DragonWar has ended. All Players of DragonWar has teleported to TwinCity.");
        Assert.Contains(
            tick.Updates,
            update => update.Broadcast &&
                update.ChatType == LegacyMsgMessagePacket.BroadcastChatType &&
                update.Message == "Alpha has Won  DragonWar. ");
        Assert.Contains(
            tick.Updates,
            update => update.RecipientUid == 1 &&
                update.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                update.Message == policy.TunedWinnerMessage);
        Assert.Contains(
            tick.Updates,
            update => update.RecipientUid == 2 &&
                update.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                update.Message ==
                    $"You received {policy.TunedWinnerConquerPointsReward / 2} ConquerPoints. ");
        Assert.Contains(
            tick.Updates,
            update => update.RecipientUid == 3 &&
                update.ChatType == LegacyMsgMessagePacket.TalkChatType &&
                update.Message ==
                    $"You received {policy.TunedWinnerConquerPointsReward / 3} ConquerPoints. ");

        var updatedByUid = tick.SessionUpdates
            .Select(update => update.Session)
            .ToDictionary(session => session.Character.Uid);

        Assert.Equal(LegacyDragonWarProcess.Dead, store.Process);
        Assert.Equal(joinedSessions.Count, updatedByUid.Count);
        Assert.All(
            tick.SessionUpdates,
            update => Assert.Equal(policy.SourceRewardOwnerName, update.RewardSourceMethodName));

        var winner = updatedByUid[1].Character;
        Assert.Equal(100 + policy.TunedWinnerConquerPointsReward, winner.ConquerPoints);
        Assert.Equal(
            (125UL + policy.TunedWinnerClassExperienceReward).ToString(),
            winner.Attributes["ClassExperience"]);
        AssertTwinCityTeleport(winner);
        Assert.False(HasLegacyStatusFlag(winner.Attributes, LegacyDragonSwingFlag));
        var winnerUpdate = tick.SessionUpdates.Single(update => update.RecipientUid == 1);
        AssertDragonWarConquerPointOwnerFrame(
            winnerUpdate,
            100 + policy.TunedWinnerConquerPointsReward);

        var winnerItems = updatedByUid[1].PlayerItems.InventoryItems;
        var rewardItem = Assert.Single(winnerItems);
        Assert.Equal(policy.TunedWinnerItemId, rewardItem.ItemId);
        Assert.Equal(policy.TunedWinnerItemStackSize, rewardItem.StackSize);
        Assert.Contains(
            winnerUpdate.OwnerFrameBytes,
            frame => LegacyGameItemPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.ItemId == policy.TunedWinnerItemId &&
                packet.StackSize == policy.TunedWinnerItemStackSize &&
                packet.Mode == LegacyGameItemPacket.AddItemMode);

        var secondSession = updatedByUid[2];
        var second = secondSession.Character;
        Assert.Equal(200 + policy.TunedWinnerConquerPointsReward / 2, second.ConquerPoints);
        Assert.Equal("25", second.Attributes["ClassExperience"]);
        AssertTwinCityTeleport(second);
        Assert.False(HasLegacyStatusFlag(second.Attributes, LegacyDragonSwingFlag));
        Assert.Empty(secondSession.PlayerItems.InventoryItems);
        AssertDragonWarConquerPointOwnerFrame(
            tick.SessionUpdates.Single(update => update.RecipientUid == 2),
            200 + policy.TunedWinnerConquerPointsReward / 2);

        var thirdSession = updatedByUid[3];
        var third = thirdSession.Character;
        Assert.Equal(300 + policy.TunedWinnerConquerPointsReward / 3, third.ConquerPoints);
        Assert.Equal("75", third.Attributes["ClassExperience"]);
        AssertTwinCityTeleport(third);
        Assert.False(HasLegacyStatusFlag(third.Attributes, LegacyDragonSwingFlag));
        Assert.Empty(thirdSession.PlayerItems.InventoryItems);
        AssertDragonWarConquerPointOwnerFrame(
            tick.SessionUpdates.Single(update => update.RecipientUid == 3),
            300 + policy.TunedWinnerConquerPointsReward / 3);
    }

    [Fact]
    public void CreateDueRuntimeUpdatesUsesReturnedItemModeWhenWinnerInventoryLacksTunedRewardSpace()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var policy = LegacyDragonWarRuntimeStateStore.EndRewardEconomyPolicy;
        var openedAt = new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero);
        var fullInventory = Enumerable.Range(1, 40)
            .Select(index => new PlayerItemSnapshotItem
            {
                Uid = (uint)index,
                ItemId = 100_000U + (uint)index,
                Position = 0,
                StackSize = 1,
            })
            .ToArray();
        var session = CreateSession(
            uid: 10,
            name: "Packed",
            conquerPoints: 0,
            attributes: DragonWarAttributes(score: 100, classExperience: 0, hasDragonSwing: true),
            playerItems: new PlayerItemSnapshot(fullInventory, [], [], [], []));
        store.Open(openedAt);
        Assert.True(store.TryJoin(session, out var joined));
        var joinedAttributes = new Dictionary<string, string>(
            joined.Character.Attributes,
            StringComparer.OrdinalIgnoreCase);
        foreach (var attribute in session.Character.Attributes)
        {
            joinedAttributes[attribute.Key] = attribute.Value;
        }

        joined = joined with
        {
            Character = joined.Character with
            {
                Attributes = joinedAttributes,
            },
        };
        store.SetProcess(LegacyDragonWarProcess.Alive);

        var tick = store.CreateDueRuntimeUpdates(openedAt.AddMinutes(5).AddSeconds(1), [joined]);
        var update = Assert.Single(tick.SessionUpdates);

        Assert.True(update.PlayerItemsDirty);
        Assert.Equal(fullInventory.Length, update.Session.PlayerItems.InventoryItems.Count);
        var returnedItem = Assert.Single(update.Session.PlayerItems.WarehouseItems);
        Assert.Equal(policy.TunedWinnerItemId, returnedItem.ItemId);
        Assert.Equal(policy.TunedWinnerItemStackSize, returnedItem.StackSize);
        Assert.Equal(65_535U, returnedItem.WarehouseId);
        Assert.Equal(0, returnedItem.Position);
        Assert.Contains(
            update.OwnerFrameBytes,
            frame => LegacyGameItemPacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.ItemId == policy.TunedWinnerItemId &&
                packet.StackSize == policy.TunedWinnerItemStackSize &&
                packet.Mode == LegacyGameItemPacket.AddItemReturnedMode);
    }

    private static void AssertTwinCityTeleport(CharacterSnapshot character)
    {
        Assert.Equal(TwinCityMapId, character.MapId);
        Assert.Equal(410, character.X);
        Assert.Equal(355, character.Y);
    }

    private static Dictionary<string, string> DragonWarAttributes(
        int score,
        int classExperience,
        bool hasDragonSwing)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DragonWarScore"] = score.ToString(),
            ["DragonWarHits"] = "5",
            ["ClassExperience"] = classExperience.ToString(),
        };
        if (hasDragonSwing)
        {
            attributes["StatusFlags"] = BuildStatusFlags(LegacyDragonSwingFlag);
        }

        return attributes;
    }

    private static WorldSession CreateSession(
        long uid,
        string name,
        long conquerPoints,
        IReadOnlyDictionary<string, string> attributes,
        PlayerItemSnapshot? playerItems = null)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name,
            Character: CreateSnapshot(uid, name, conquerPoints, attributes),
            PlayerItems: playerItems ?? PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(
        long uid,
        string name,
        long conquerPoints,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 705,
            Y: 577,
            PreviousMapId: 1002,
            PreviousMapX: 705,
            PreviousMapY: 577,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 61_229,
            MinMana: 0,
            Money: 0,
            ConquerPoints: conquerPoints,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "",
            Attributes: new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var values = new ulong[4];
        foreach (var flag in flags)
        {
            var offset = flag / 64;
            if ((uint)offset >= values.Length)
            {
                continue;
            }

            values[offset] |= 1UL << (flag % 64);
        }

        return string.Join(',', values);
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            HasLegacyStatusFlag(raw, flag);
    }

    private static void AssertDragonWarConquerPointOwnerFrame(
        LegacyDragonWarRuntimeSessionUpdate update,
        long expectedConquerPoints)
    {
        var expectedValue = checked((ulong)expectedConquerPoints);
        Assert.Contains(
            update.OwnerFrameBytes,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.Uid == update.RecipientUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ConquerPoints &&
                    entry.Value == expectedValue));
        Assert.DoesNotContain(
            update.OwnerFrameBytes,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var packet) &&
                packet.Uid == update.RecipientUid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.Conquer &&
                    entry.Value == expectedValue));
    }

    private static bool HasLegacyStatusFlag(string statusFlags, int flag)
    {
        if (statusFlags.Contains('/', StringComparison.Ordinal))
        {
            var slashParts = statusFlags.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var wordIndex = flag / 32;
            return wordIndex < slashParts.Length &&
                ulong.TryParse(slashParts[wordIndex], out var word) &&
                (word & (1UL << (flag % 32))) != 0;
        }

        var parts = statusFlags.Split(',', StringSplitOptions.TrimEntries);
        var offset = flag / 64;
        return offset < parts.Length &&
            ulong.TryParse(parts[offset], out var value) &&
            (value & (1UL << (flag % 64))) != 0;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
