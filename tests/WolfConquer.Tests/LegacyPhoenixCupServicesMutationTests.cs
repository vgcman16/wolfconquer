using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyPhoenixCupServicesMutationTests : IDisposable
{
    private const uint NpcId = 19_436;
    private const uint SpiritItemId = 3_300_056;
    private const uint SilverFragmentItemId = 3_300_053;
    private const uint GoldenFragmentItemId = 3_300_054;
    private const uint HolyFragmentItemId = 3_300_055;
    private const long AccountId = 100;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPhoenixCupServicesMutationTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<uint, ushort, uint, string> PaidRewardCases => new()
    {
        { 1, 1, SilverFragmentItemId, "You received SilverPhoenixCupFragment*1!" },
        { 1, 2, SilverFragmentItemId, "You received SilverPhoenixCupFragment*2!" },
        { 2, 1, GoldenFragmentItemId, "You received GoldenPhoenixCupFragment*1!" },
        { 2, 2, GoldenFragmentItemId, "You received GoldenPhoenixCupFragment*2!" },
    };

    public static TheoryData<byte, string[]> PhoenixCupInfoCases => new()
    {
        {
            31,
            [
                "Holy Phoenix Cup's basic attributes: 1% Blessed, HP +1200, MP +1200, Defense +1000, \n",
                "                  P-Strike +1%, M-Strike +1%, Break +1%,      Anti-break +1% \n",
                "Bonus attributes if tempered: Lv.3 Toxin Eraser, Lv.5 Draining Touch, Lv.2 Blood Spawn, \n",
                "                   Lv.5 Free Soul, Lv.5 Killing Flash, Lv.3 Straight Life, HP +500",
            ]
        },
        {
            32,
            [
                "Golden Phoenix Cup's basic attributes: 1% Blessed, HP +1000, MP +1000, Defense +500 \n",
                "Bonus attributes if tempered: Lv.3 Toxin Eraser, Lv.4 Draining Touch, Lv.2 Blood Spawn, \n",
                "                  Lv.4 Free Soul, Lv.4 Killing Flash, Lv.3 Straight Life, HP +300 \n",
            ]
        },
        {
            33,
            [
                "Silver Phoenix Cup's basic attributes: 1% Blessed, HP +900, MP +900, Defense +300 \n",
                "Bonus attributes if tempered: Lv.3 Toxin Eraser, Lv.3 Draining Touch, Lv.1 Blood Spawn, \n",
                "                  Lv.3 Free Soul, Lv.3 Killing Flash, Lv.2 Straight Life \n",
            ]
        },
        {
            34,
            [
                "Bronze Phoenix Cup's basic attributes: 1% Blessed, HP +800, MP +800, Defense +150 \n",
                "Bonus attributes if tempered: Lv.3 Toxin Eraser, Lv.2 Draining Touch, Lv.2 Killing Flash,  Lv.2 Free Soul \n",
            ]
        },
    };

    public static TheoryData<byte, string, byte, string> ConfirmationDialogCases => new()
    {
        {
            5,
            "Are you sure you want to try with 5x Spirit of Legend?",
            6,
            "Yes.~I`ll~pay~the~5xSpiritofLegend."
        },
        {
            2,
            "Are you sure you want to try with 15,000 CPs instead of a Spirit of Legend?",
            3,
            "Yes.~I`ll~pay~the~money."
        },
    };

    [Theory]
    [MemberData(nameof(ConfirmationDialogCases))]
    public async Task ConfirmationDialogsPreserveExactSourceFrames(
        byte optionId,
        string expectedText,
        byte yesOptionId,
        string yesText)
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog());

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        var replies = result.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(expectedText, replies[0].Text);
        Assert.Contains(
            replies,
            packet => packet.InteractType == LegacyNpcReplyInteractType.Option &&
                packet.OptionId == yesOptionId &&
                packet.Text == yesText);
        Assert.Contains(
            replies,
            packet => packet.InteractType == LegacyNpcReplyInteractType.Option &&
                packet.OptionId == byte.MaxValue &&
                packet.Text == "I`ll~think~about~it.");
        AssertAvatarAndFinish(replies, avatarId: 7);
    }

    [Fact]
    public async Task PhoenixCupRootPreservesExactSourceFramesAndTooltipMarkup()
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId: 0,
            originalItems,
            Catalog());

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        var replies = result.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(
            [
                "I can turn a Spirit of Legend into a random temporary Phoenix Cup or <tip color=0xfff05050 desc=A#certain#number#of#cup#fragments#can#be#combined#into#the#corresponding#Phoenix#Cup.>cup fragment</tip>.",
                "~Equipping the Phoenix Cup will bring you attribute bonus. If you have any cup that you don`t need anymore, you can exchange it for <tip color=0xfff05050 desc=30#fragments#can#be#combined#into#a#90-day#Holy#Phoenix#Cup.>Holy Phoenix Cup Fragment</tip> or",
                "~Chi Points.",
            ],
            replies
                .Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Dialog)
                .Select(packet => packet.Text));
        Assert.Equal(
            [
                ("Use~the~Spirit~of~Legend.", (byte)1),
                ("Swap~for~cup~fragment~or~Chi.", byte.MaxValue),
                ("Learn~more~about~Phoenix~Cups.", (byte)30),
                ("I`ll~talk~to~you~later.", byte.MaxValue),
            ],
            replies
                .Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Option)
                .Select(packet => (packet.Text, packet.OptionId)));
        AssertAvatarAndFinish(replies, avatarId: 7);
    }

    [Fact]
    public async Task AstralPhoenixPreservesExactSourceFrames()
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId: 0,
            originalItems,
            Catalog(),
            npcId: 19_452);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        var replies = result.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(
            [
                "1.Join the Cross-Server Elite PK Tournament and you can get Elite Points. \n",
                "2.Elite Points can be used to exchange for reward. \n",
                "3.Top 8 players can get valuable reward such as exclusive halo and garments.",
            ],
            replies
                .Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Dialog)
                .Select(packet => packet.Text));
        var close = Assert.Single(
            replies,
            packet => packet.InteractType == LegacyNpcReplyInteractType.Option);
        Assert.Equal("I~see.", close.Text);
        AssertAvatarAndFinish(replies, avatarId: 4);
    }

    [Fact]
    public async Task PhoenixCupInfoMenuPreservesExactSourceFrames()
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId: 30,
            originalItems,
            Catalog());

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        var replies = result.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(
            [
                "The Cross-server Elite PK Tournament will award 4 kinds of phoenix cups which grant different attributes.",
                "~Which cup would you like to know about?",
            ],
            replies
                .Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Dialog)
                .Select(packet => packet.Text));
        Assert.Equal(
            [
                ("Holy~Phoenix~Cup.", (byte)31),
                ("Golden~Phoenix~Cup.", (byte)32),
                ("Silver~Phoenix~Cup.", (byte)33),
                ("Bronze~Phoenix~Cup.", (byte)34),
            ],
            replies
                .Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Option)
                .Select(packet => (packet.Text, packet.OptionId)));
        AssertAvatarAndFinish(replies, avatarId: 7);
    }

    [Theory]
    [MemberData(nameof(PhoenixCupInfoCases))]
    public async Task PhoenixCupDetailsPreserveExactTemperedBonusFrames(
        byte optionId,
        string[] expectedText)
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog());

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        var replies = result.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(
            expectedText,
            replies
                .Where(packet => packet.InteractType == LegacyNpcReplyInteractType.Dialog)
                .Select(packet => packet.Text));
        var close = Assert.Single(
            replies,
            packet => packet.InteractType == LegacyNpcReplyInteractType.Option);
        Assert.Equal("I~see.", close.Text);
        Assert.Equal(byte.MaxValue, close.OptionId);
        AssertAvatarAndFinish(replies, avatarId: 7);
    }

    [Theory]
    [MemberData(nameof(PaidRewardCases))]
    public async Task PaidRollsPersistOnlyReachableFragmentsInExactSourcePacketOrder(
        uint rewardRoll,
        ushort rewardAmount,
        uint rewardItemId,
        string expectedMessage)
    {
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(FragmentType(rewardItemId)),
            rewardRoll: rewardRoll,
            rewardAmount: rewardAmount);

        Assert.True(result.Handled);
        Assert.Equal(5_000, result.UpdatedSession.Character.ConquerPoints);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(11U, reward.Uid);
        Assert.Equal(rewardAmount, reward.StackSize);
        Assert.Equal(0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == HolyFragmentItemId);

        Assert.Equal(3, result.Sent.Count);
        var update = AssertScalarUpdate(result.Sent[0]);
        var cp = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, cp.Type);
        Assert.Equal(5_000UL, cp.Value);
        AssertSystemTalk(result.Sent[1], expectedMessage);
        var itemPacket = AssertGameItem(result.Sent[2]);
        Assert.Equal(rewardItemId, itemPacket.ItemId);
        Assert.Equal(rewardAmount, itemPacket.StackSize);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, itemPacket.Mode);

        Assert.Equal(5_000, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
        AssertPlayerItemsEqual(
            result.UpdatedSession.PlayerItems,
            Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains($"rewardRoll={rewardRoll}", result.Log);
        Assert.Contains("sourceOrder=conquer-points+talk-message+add-item", result.Log);
    }

    [Fact]
    public async Task UnforcedRuntimeRandomStaysInsideExclusiveSourceBounds()
    {
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(
                FragmentType(SilverFragmentItemId),
                FragmentType(GoldenFragmentItemId)),
            includeForcedRewards: false);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId is SilverFragmentItemId or GoldenFragmentItemId);
        Assert.InRange(reward.StackSize, (ushort)1, (ushort)2);
        Assert.DoesNotContain(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == HolyFragmentItemId);
    }

    [Fact]
    public async Task PaidRewardUsesCompatibleUnboundFragmentStack()
    {
        var originalItems = Items(Item(10, SilverFragmentItemId, stackSize: 14, locked: 1));
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId, stackLimit: 15)),
            rewardRoll: 1,
            rewardAmount: 1);

        var reward = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(10U, reward.Uid);
        Assert.Equal(15, reward.StackSize);
        Assert.Equal(0, reward.Locked);
        var itemPacket = AssertGameItem(result.Sent[2]);
        Assert.Equal(10U, itemPacket.Uid);
        Assert.Equal(15, itemPacket.StackSize);
        Assert.Equal(0, itemPacket.Locked);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, itemPacket.Mode);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, Assert.Single(AssertScalarUpdate(result.Sent[0]).Updates).Type);
        AssertSystemTalk(result.Sent[1], "You received SilverPhoenixCupFragment*1!");
    }

    [Theory]
    [InlineData((ushort)14, (byte)0, (ushort)2)]
    [InlineData((ushort)1, (byte)1, (ushort)1)]
    public async Task IncompatibleFragmentStackCreatesSeparateUnboundReward(
        ushort existingStackSize,
        byte existingBound,
        ushort rewardAmount)
    {
        var originalItems = Items(Item(
            10,
            SilverFragmentItemId,
            stackSize: existingStackSize,
            bound: existingBound));
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId, stackLimit: 15)),
            rewardRoll: 1,
            rewardAmount: rewardAmount);

        var fragments = result.UpdatedSession.PlayerItems.InventoryItems
            .Where(item => item.ItemId == SilverFragmentItemId)
            .OrderBy(item => item.Uid)
            .ToArray();
        Assert.Equal(2, fragments.Length);
        Assert.Equal(existingStackSize, fragments[0].StackSize);
        Assert.Equal(existingBound, fragments[0].Bound);
        Assert.Equal(11U, fragments[1].Uid);
        Assert.Equal(rewardAmount, fragments[1].StackSize);
        Assert.Equal(0, fragments[1].Bound);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, AssertGameItem(result.Sent[2]).Mode);
    }

    [Fact]
    public async Task SpiritCostDebitsOnlyEligibleUnboundRowsAndPreservesForeignStack()
    {
        var originalItems = Items(
            Item(10, SpiritItemId, stackSize: 2, bound: 1),
            Item(20, SpiritItemId, stackSize: 5));
        var result = await InvokeAsync(
            optionId: 6,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)),
            rewardRoll: 1,
            rewardAmount: 2);

        var remainingSpirit = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == SpiritItemId);
        Assert.Equal(10U, remainingSpirit.Uid);
        AssertPlayerItemsEqual(Items(originalItems.InventoryItems[0]), Items(remainingSpirit));
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == SilverFragmentItemId);
        Assert.Equal(11U, reward.Uid);
        Assert.Equal(2, reward.StackSize);

        Assert.Equal(7, result.Sent.Count);
        AssertItemUpdate(result.Sent[0], uid: 20, stackSize: 4);
        AssertItemUpdate(result.Sent[1], uid: 20, stackSize: 3);
        AssertItemUpdate(result.Sent[2], uid: 20, stackSize: 2);
        AssertItemUpdate(result.Sent[3], uid: 20, stackSize: 1);
        AssertRemoval(result.Sent[4], uid: 20);
        AssertSystemTalk(result.Sent[5], "You received SilverPhoenixCupFragment*2!");
        Assert.Equal(LegacyGameItemPacket.AddItemMode, AssertGameItem(result.Sent[6]).Mode);
        AssertPlayerItemsEqual(
            result.UpdatedSession.PlayerItems,
            Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains("sourceOrder=remove-items+talk-message+add-item", result.Log);
        Assert.Empty(Directory.EnumerateFiles(_root, "*.phoenix-*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ForeignSpiritAfterEligibleStackIsNotChargedByLaterPasses()
    {
        var originalItems = Items(
            Item(10, SpiritItemId, stackSize: 5),
            Item(20, SpiritItemId, stackSize: 2, bound: 1));
        var result = await InvokeAsync(
            optionId: 6,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)),
            rewardRoll: 1,
            rewardAmount: 1);

        AssertItemUpdate(result.Sent[0], uid: 10, stackSize: 4);
        AssertItemUpdate(result.Sent[1], uid: 10, stackSize: 3);
        AssertItemUpdate(result.Sent[2], uid: 10, stackSize: 2);
        AssertItemUpdate(result.Sent[3], uid: 10, stackSize: 1);
        AssertRemoval(result.Sent[4], uid: 10);
        AssertPlayerItemsEqual(
            Items(originalItems.InventoryItems[1]),
            Items(Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20)));
        AssertPlayerItemsEqual(
            result.UpdatedSession.PlayerItems,
            Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
    }

    [Fact]
    public async Task SpiritRewardMergeSendsFiveConsumesThenTalkThenStackUpdate()
    {
        var originalItems = Items(
            Item(10, SpiritItemId, stackSize: 5),
            Item(20, SilverFragmentItemId, stackSize: 5, locked: 1));
        var result = await InvokeAsync(
            optionId: 6,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)),
            rewardRoll: 1,
            rewardAmount: 2);

        Assert.Equal(7, result.Sent.Count);
        AssertSystemTalk(result.Sent[5], "You received SilverPhoenixCupFragment*2!");
        var rewardPacket = AssertGameItem(result.Sent[6]);
        Assert.Equal(20U, rewardPacket.Uid);
        Assert.Equal(7, rewardPacket.StackSize);
        Assert.Equal(0, rewardPacket.Locked);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, rewardPacket.Mode);
    }

    [Fact]
    public async Task BoundOnlySpiritsUseExactSourceDialogWithoutMutation()
    {
        var originalItems = Items(Item(10, SpiritItemId, stackSize: 5, bound: 1));
        var result = await InvokeAsync(
            optionId: 6,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)));

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        AssertExactDialogText(
            result.Sent,
            "You should give me a Spirit of Legend or 15,000 CPs before trying to make a phoenix cup.");
        Assert.Contains(
            result.Sent.Select(AssertNpcReply),
            packet => packet.InteractType == LegacyNpcReplyInteractType.Option && packet.Text == "Okay.");
    }

    [Fact]
    public async Task BoundAndZeroSizedSpiritsDoNotCompleteTheUnboundGate()
    {
        var originalItems = Items(
            Item(5, SpiritItemId, stackSize: 0),
            Item(10, SpiritItemId, stackSize: 4),
            Item(20, SpiritItemId, stackSize: 5, bound: 1));
        var result = await InvokeAsync(
            optionId: 6,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)));

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        AssertExactDialogText(
            result.Sent,
            "You should give me a Spirit of Legend or 15,000 CPs before trying to make a phoenix cup.");
    }

    [Fact]
    public async Task OptionOneDoesNotOfferSpiritPathForBoundStack()
    {
        var originalItems = Items(Item(10, SpiritItemId, stackSize: 5, bound: 1));
        var result = await InvokeAsync(
            optionId: 1,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)));

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        AssertExactDialogText(
            result.Sent,
            "It seems you don`t have 5x Spirit of Legend. If you really want the Phoenix Cup, you can also pay 15,000 CPs to make a try.");
        Assert.Contains(
            result.Sent.Select(AssertNpcReply),
            packet => packet.InteractType == LegacyNpcReplyInteractType.Option &&
                packet.OptionId == 2 &&
                packet.Text == "Deal!~Here~are~15,000~CPs.");
    }

    [Fact]
    public async Task OptionOneOffersExactSpiritPathForFiveUnboundItems()
    {
        var originalItems = Items(Item(10, SpiritItemId, stackSize: 5));
        var result = await InvokeAsync(
            optionId: 1,
            originalItems,
            Catalog());

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        var replies = result.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal(
            "f you really want the Phoenix Cup, you can also pay 5 Spirit of Legend to make a try.",
            replies[0].Text);
        Assert.Contains(
            replies,
            packet => packet.InteractType == LegacyNpcReplyInteractType.Option &&
                packet.OptionId == 5 &&
                packet.Text == "Deal!~Here~are~5xSpiritofLegend.");
        AssertAvatarAndFinish(replies, avatarId: 7);
    }

    [Fact]
    public async Task FullInventoryBlocksPaidAttemptBeforeWalletMutation()
    {
        var inventory = new List<PlayerItemSnapshotItem>
        {
            Item(50, SilverFragmentItemId, stackSize: 1),
        };
        inventory.AddRange(Enumerable.Range(1, 39).Select(index =>
            Item(checked((uint)index), checked((uint)(800_000 + index)))));
        var originalItems = Items(inventory.ToArray());
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)));

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Equal(
            "Please make 1 more space in your inventory.",
            AssertMessageBox(Assert.Single(result.Sent)).Text);
    }

    [Fact]
    public async Task FullInventoryBlocksSpiritAttemptBeforeItemsCanFreeSlot()
    {
        var inventory = new List<PlayerItemSnapshotItem> { Item(50, SpiritItemId, stackSize: 5) };
        inventory.AddRange(Enumerable.Range(1, 39).Select(index =>
            Item(checked((uint)index), checked((uint)(810_000 + index)))));
        var originalItems = Items(inventory.ToArray());
        var result = await InvokeAsync(
            optionId: 6,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)));

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Equal(
            "Please make 1 more space in your inventory.",
            AssertMessageBox(Assert.Single(result.Sent)).Text);
    }

    [Fact]
    public async Task InsufficientCpUsesExactSourceDialogWithoutMutation()
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)),
            conquerPoints: 14_999);

        AssertMutationFree(result, originalItems, conquerPoints: 14_999);
        AssertExactDialogText(
            result.Sent,
            "You should give me a Spirit of Legend or 15,000 CPs before trying to make a phoenix cup.");
        Assert.Contains(
            result.Sent.Select(AssertNpcReply),
            packet => packet.InteractType == LegacyNpcReplyInteractType.Option && packet.Text == "Okay.");
    }

    [Fact]
    public async Task InTradePaidAttemptIsSilentAndMutationFree()
    {
        var originalItems = Items();
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)),
            legacyTradeActive: true);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=in-trade; source branch is silent", result.Log);
    }

    [Fact]
    public async Task PaidTransactionFailureRollsBackWalletAndReward()
    {
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)),
            persistedCharacterUid: CharacterUid + 1);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Equal(20_000, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
        AssertPlayerItemsEqual(originalItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains("uidMismatch", result.Log);
    }

    [Fact]
    public async Task SpiritItemPersistenceFailureDoesNotConsumeOrSend()
    {
        Directory.CreateDirectory(_root);
        var blockedRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedRoot, "not a directory");
        var originalItems = Items(Item(10, SpiritItemId, stackSize: 5));
        var result = await InvokeAsync(
            optionId: 6,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)),
            playerItemsRootOverride: blockedRoot,
            seedPlayerItems: false);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("item-persistence-", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedRoot));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.phoenix-*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)6)]
    public async Task MissingFragmentMetadataDoesNotChargeOrConsume(byte optionId)
    {
        var originalItems = optionId == 6
            ? Items(Item(10, SpiritItemId, stackSize: 5))
            : Items(Item(10, 720_001));
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog());

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing", result.Log);
        AssertPlayerItemsEqual(originalItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Equal(20_000, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)6)]
    public async Task InvalidFragmentStackMetadataDoesNotChargeOrConsume(byte optionId)
    {
        var originalItems = optionId == 6
            ? Items(Item(10, SpiritItemId, stackSize: 5))
            : Items(Item(10, 720_001));
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId, stackLimit: 0)));

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-invalid", result.Log);
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)6)]
    public async Task RewardUidExhaustionDoesNotChargeOrConsume(byte optionId)
    {
        var originalItems = optionId == 6
            ? Items(
                Item(10, SpiritItemId, stackSize: 5),
                Item(uint.MaxValue, 720_001))
            : Items(Item(uint.MaxValue, 720_001));
        var result = await InvokeAsync(
            optionId,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)));

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Contains("item-uid-exhausted", result.Log);
    }

    [Fact]
    public async Task StaleSessionRegistrationDoesNotOverwriteWalletOrItems()
    {
        var originalItems = Items(Item(10, 720_001));
        var result = await InvokeAsync(
            optionId: 3,
            originalItems,
            Catalog(FragmentType(SilverFragmentItemId)),
            staleSessionRegistration: true);

        AssertMutationFree(result, originalItems, conquerPoints: 20_000);
        Assert.Empty(result.Sent);
        Assert.Equal(20_000, Assert.IsType<CharacterSnapshot>(result.PersistedCharacter).ConquerPoints);
        AssertPlayerItemsEqual(originalItems, Assert.IsType<PlayerItemSnapshot>(result.PersistedItems));
        Assert.Contains("session-registration-missing-or-stale", result.Log);
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
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        long conquerPoints = 20_000,
        uint rewardRoll = 1,
        uint rewardAmount = 1,
        bool legacyTradeActive = false,
        long? persistedCharacterUid = null,
        string? playerItemsRootOverride = null,
        bool seedPlayerItems = true,
        bool staleSessionRegistration = false,
        uint npcId = NpcId,
        bool includeForcedRewards = true)
    {
        var invocationRoot = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var usersRoot = Path.Combine(invocationRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        var characterPath = WriteCharacterSnapshot(
            usersRoot,
            persistedCharacterUid ?? CharacterUid,
            conquerPoints);
        var playerItemsRoot = playerItemsRootOverride ?? Path.Combine(invocationRoot, "PlayersItems");
        if (seedPlayerItems)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (includeForcedRewards)
        {
            attributes["LegacyPhoenixCupRewardRoll"] = rewardRoll.ToString(CultureInfo.InvariantCulture);
            attributes["LegacyPhoenixCupRewardAmount"] = rewardAmount.ToString(CultureInfo.InvariantCulture);
        }
        var session = CreateSession(
            originalItems,
            conquerPoints,
            attributes,
            usersRoot,
            legacyTradeActive);
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        sessions.Set(
            connection,
            staleSessionRegistration
                ? session with { PlayerItems = Items(Item(99, 720_099)) }
                : session);

        using var log = new StringWriter();
        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        var updatedSession = session;
        if (!staleSessionRegistration && sessions.TryGet(connection, out var stored) && stored is not null)
        {
            updatedSession = stored;
        }

        PlayerItemSnapshot? persistedItems = null;
        var itemPath = Path.Combine(playerItemsRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        if (Directory.Exists(playerItemsRoot) && File.Exists(itemPath))
        {
            persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        }

        CharacterSnapshot? persistedCharacter = null;
        if (File.Exists(characterPath))
        {
            persistedCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(characterPath));
        }

        return new InvokeResult(
            handled,
            sent,
            log.ToString(),
            session,
            updatedSession,
            persistedItems,
            persistedCharacter);
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot items,
        long conquerPoints,
        IReadOnlyDictionary<string, string> attributes,
        string usersRoot,
        bool legacyTradeActive)
    {
        return new WorldSession(
            AccountId: AccountId,
            Username: "phoenix-cup-services-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PhoenixTester",
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
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: items,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot,
            LegacyTradeActive: legacyTradeActive);
    }

    private static string WriteCharacterSnapshot(string usersRoot, long uid, long conquerPoints)
    {
        var path = Path.Combine(usersRoot, AccountId + ".ini");
        File.WriteAllText(
            path,
            $$"""
            [Character]
            UID={{uid}}
            Body=1003
            Face=0
            Name=PhoenixTester
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
            """);
        return path;
    }

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem Item(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0,
        byte locked = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
            Locked = locked,
        };
    }

    private static ItemType FragmentType(uint itemId, ushort stackLimit = 30)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "PhoenixCupFragment";
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] = "1";
        fields[46] = stackLimit.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemId), "PhoenixCupFragment", fields);
    }

    private static void AssertMutationFree(
        InvokeResult result,
        PlayerItemSnapshot originalItems,
        long conquerPoints)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(conquerPoints, result.UpdatedSession.Character.ConquerPoints);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
    }

    private static void AssertExactDialogText(IReadOnlyList<byte[]> sent, string text)
    {
        var dialog = AssertNpcReply(sent[0]);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(text, dialog.Text);
    }

    private static void AssertAvatarAndFinish(
        IReadOnlyList<LegacyNpcReplyPacket> replies,
        ushort avatarId)
    {
        var avatar = Assert.Single(
            replies,
            packet => packet.InteractType == LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(avatarId, avatar.InputMaxLength);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[^1].InteractType);
    }

    private static void AssertItemUpdate(byte[] bytes, uint uid, ushort stackSize)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(stackSize, packet.StackSize);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
    }

    private static void AssertRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static void AssertSystemTalk(byte[] bytes, string message)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        Assert.Contains(message, packet.Content);
    }

    private static LegacyNpcReplyPacket AssertMessageBox(byte[] bytes)
    {
        var packet = AssertNpcReply(bytes);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, packet.InteractType);
        return packet;
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyMsgUpdatePacket AssertScalarUpdate(byte[] bytes)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog Catalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://phoenix-cup-services",
                new Dictionary<string, int>()),
            clientConfigs: [], items: items, itemAddBonuses: [], levelExperience: [], runeLevelExperience: [],
            runeStorageAttributes: [], itemRefineEffects: [], itemRefineEffectEx: [], itemRefineUpgrades: [],
            transformInfo: [], furniture: [], hairColorTypes: [], hairFaceStorageTypes: [], coatStorageTypes: [],
            coatStorageAttributes: [], coatColorRules: [], xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [], xuanbaoRandomAttributes: [], christmasNpcs: [], tutorTypes: [],
            tutorBattleLimitTypes: [], beastAttributes: [], fateExperience: [], spiritRates: [], hundredWeapons: [],
            gouyuTypes: [], gouyuImmortals: [], specialEffectAttributes: [], daoqiDictTypes: [],
            syndicateFormationTypes: [], syndicateSkillTypes: [], disdainRules: [], jiangHuAttributes: [],
            jiangHuCultivateStatuses: [], refineryBoxes: [], refinerySouls: [], refineryMaterials: [],
            stateCountFiles: [], binarySeedFiles: [], emptySeedFiles: [], backupSeedFiles: [], pokerTables: [],
            rouletteTables: [], npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, string.Empty, 0, []), [], "fixture://activity"),
            meltingTypeRules: [], lotteryPrizes: [], arenaRecords: [], elitePkRecords: [], teamElitePkRecords: [],
            skillTeamPkRecords: [], classPkWarRecords: [], combatGearRanks: [], prestigeRanks: [], innerPowerRanks: [],
            runeRankings: [], staticStatues: [], newPlayerRewards: [], votes: [], couplesPkRecords: [],
            socialAssociations: [], guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"), classLevelStats: [], professionLevelUps: [],
            professionLevelBenefits: [], professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [], questMissions: [], quizQuestions: [], magic: [], magicEffects: [],
            magicTypeOperations: [], titleTypes: [], combatGear: [], traps: [], npcs: [], sobNpcs: [], portals: [],
            maps: [], mapNames: [], monsters: [], monsterSpawns: [], lotteryDictionary: [], collectionRewardTiers: [],
            taskRewardTypes: [], taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, string.Empty, new Dictionary<string, string>()),
            neiGongSecrets: [], neiGongTypes: [], subProfessions: [], syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0), stageGoalStages: [], stageGoalTasks: [], shops: [], shopItems: [],
            exchangeShopGoods: [], exchangeShopGoodsEx: [], exchangeShopLevels: [], honorShopItems: [],
            honorShopRecommendations: [], racePointShopItems: [], racePointShopRecommendations: [],
            goldenLeagueShopItems: [], goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(), booths: [], boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        PlayerItemSnapshot? PersistedItems,
        CharacterSnapshot? PersistedCharacter);
}
