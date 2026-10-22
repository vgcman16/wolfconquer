using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using ItemQuery = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaItem;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    [Theory]
    [InlineData(0u, 0)]
    [InlineData(8u, 0)]
    [InlineData(9u, 1)]
    [InlineData(10u, 2)]
    [InlineData(11u, 3)]
    [InlineData(12u, 4)]
    [InlineData(13u, 5)]
    [InlineData(14u, 6)]
    [InlineData(15u, 7)]
    [InlineData(16u, 8)]
    [InlineData(17u, 9)]
    [InlineData(18u, 9)]
    [InlineData(uint.MaxValue, 9)]
    public Task GetLevelUsesEveryNativeBoundaryAndDoesNotResendAnUnchangedBadge(uint levels, int badge) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Item(99, 100, levels, 1);
            scope.SetNinia(255);
            scope.Ninja.GetLevel();
            Assert.Equal((byte)badge, scope.Client.Player.NiniaP0);
            scope.Sent(LevelFrame(17, (byte)badge));
            scope.Serialized(0, LevelFrame(17, (byte)badge));
            Assert.Equal(11, scope.Available);
            scope.Ninja.GetLevel();
            Assert.Single(scope.Queue); Assert.Equal(11, scope.Available); scope.Untaken(1);
        });

    [Fact]
    public Task GetLevelReadsClientMyNinjaAndItsRealAlternateFallbackRatherThanReceiverItems() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var selected = new LegacyNativeNinja(scope.Client);
            selected.Items.TryAdd(1, new LegacyNativeNinja.Item { ItemID = 100, Position = 1, Level = 100 });
            selected.Items.TryAdd(2, new LegacyNativeNinja.Item { ItemID = 200, Position = 9, Level = 5 });
            selected.Items.TryAdd(3, new LegacyNativeNinja.Item { ItemID = 300, Position = 2, Level = 4 });
            scope.Client.MyNinja = selected;
            scope.Client.Equipment!.Alternante = true;
            scope.Ninja.Items = null!;
            scope.Ninja.GetLevel();
            Assert.Equal((byte)1, scope.Client.Player.NiniaP0);
            scope.Sent(LevelFrame(17, 1)); Assert.Same(selected, scope.Client.MyNinja);
            Assert.Equal(3, selected.Items.Count); scope.Serialized(0, LevelFrame(17, 1));
        });

    [Theory]
    [InlineData("ninja")]
    [InlineData("equipment")]
    [InlineData("items")]
    [InlineData("player")]
    public Task GetLevelMissingOwnersFaultBeforeTakingOrPublishingBadge(string missing) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.SetNinia(7);
            var player = scope.Client.Player;
            if (missing == "ninja") scope.Client.MyNinja = null;
            if (missing == "equipment") scope.Client.Equipment = null;
            if (missing == "items") scope.Ninja.Items = null!;
            if (missing == "player") scope.Client.Player = null!;
            Assert.Throws<NullReferenceException>(() => scope.Ninja.GetLevel());
            Assert.Equal((byte)7, player.NiniaP0);
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Fact]
    public Task DirectNiniaSetterTakesBeforeMutationAndAlwaysSendsEvenForEqualValues() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.SetNinia(7);
            scope.UseNullBin();
            try
            {
                Assert.Throws<NullReferenceException>(() => scope.Client.Player.NiniaP0 = 3);
                Assert.Equal((byte)7, scope.Client.Player.NiniaP0);
            }
            finally { scope.RestoreOwnedBin(); }
            Assert.Equal(12, scope.Available); scope.Untaken(0);
            scope.Client.Player.NiniaP0 = 3;
            scope.Client.Player.NiniaP0 = 3;
            scope.Sent(LevelFrame(17, 3), LevelFrame(17, 3));
            scope.Serialized(0, LevelFrame(17, 3)); scope.Serialized(1, LevelFrame(17, 3));
            Assert.Equal((byte)3, scope.Client.Player.NiniaP0); Assert.Equal(10, scope.Available);
        });

    [Fact]
    public Task FakeRecipientStillPublishesAndSerializesDirectNiniaUpdate() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Client.Fake = true; scope.Client.Socket = null!;
            scope.Client.Player.NiniaP0 = byte.MaxValue;
            Assert.Equal(byte.MaxValue, scope.Client.Player.NiniaP0);
            Assert.Empty(scope.Queue); Assert.Equal(11, scope.Available);
            scope.Serialized(0, LevelFrame(17, byte.MaxValue)); Assert.Empty(scope.Log);
        });

    [Fact]
    public Task FullLoadingPublishesRankReconcilesSkillsChangesBadgeThenSendsUserAndAllItemFields() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = scope.Item(999, 100, 9, 1, 4, 5);
            scope.Meta(1, 100, 9, 1000, 16310, 16320);
            scope.Ninja.Fire = 2; scope.Ninja.FireMastery = 3;
            scope.Ninja.Exp = 6; scope.Ninja.Flag = 7;
            scope.Spell(16330);
            scope.Ninja.Loading();
            byte[] remove = RemoveFrame(17, 16330);
            byte[] level = LevelFrame(17, 1);
            byte[] user = UserFrame(0, 17, 7, 6, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1011, 3);
            byte[] items = ItemFrame(ItemQuery.TypeID.Load, 17, (100, 9, 1, 4, 5));
            scope.Sent(SpellFrame(16310, 9), SpellFrame(16320, 9), remove, level, user, items);
            Assert.Equal(1011u, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
            Assert.Equal(1011u, scope.Rank(RankType.Fire).RankingList[17].TotalPoints);
            Assert.Equal((byte)1, scope.Client.Player.NiniaP0);
            Assert.Equal(new ushort[] { 16310, 16320 }, scope.Spells.ClientSpells.Keys.Order());
            Assert.Same(item, Assert.Single(scope.Ninja.Items).Value);
            Assert.Equal(new uint[] { 100, 9, 1, 4, 5 }, new[] { item.ItemID, item.Level, item.Position, item.Exp, item.UK });
            Assert.Equal(7UL, scope.Ninja.Flag); Assert.Equal(6u, scope.Ninja.Exp); Assert.Equal(2u, scope.Ninja.Fire);
            scope.Serialized(0, remove, untouchedTail: false); scope.Serialized(1, level);
            scope.Serialized(2, user); scope.Serialized(3, items);
            Assert.Equal(8, scope.Available); scope.Untaken(4); Assert.Empty(scope.Log);
        });

    [Fact]
    public Task NonNinjaLoadingRemovesStaleRankButNeverTouchesMissingGameplayOwnersOrPackets() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Rank(RankType.Overall).UpdateItem(new LegacyNativeNinjaRank.Entry { UID = 17, TotalPoints = 3000 });
            scope.SetClass(4000);
            scope.Client.Equipment = null; scope.Client.MySpells = null; scope.Client.MyNinja = null;
            scope.Ninja.Items = null!;
            scope.Ninja.Loading();
            Assert.Empty(scope.Rank(RankType.Overall).RankingList);
            Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Fact]
    public Task LoadingRankFaultPreventsEveryLaterPacketPhase() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.FireMastery = 1000;
            LegacyNativeNinjaRank.Ranks = new(); // Genuine SafeDictionary absent lookup returns null.
            Assert.Throws<NullReferenceException>(() => scope.Ninja.Loading());
            Assert.Empty(scope.Queue); Assert.Empty(scope.Spells.ClientSpells);
            Assert.Equal((byte)0, scope.Client.Player.NiniaP0); Assert.Equal(12, scope.Available); scope.Untaken(0);
        });

    [Fact]
    public Task LoadingAlternateFaultRetainsAlreadyPublishedRanksAndDisposesItsTakenPacket() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.FireMastery = 1000;
            scope.Client.Equipment = null; // AddInfo tolerates this; Alternate does not.
            Assert.Throws<NullReferenceException>(() => scope.Ninja.Loading());
            Assert.Equal(1000u, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
            Assert.Equal(1000u, scope.Rank(RankType.Fire).RankingList[17].TotalPoints);
            Assert.Empty(scope.Queue); Assert.Equal((byte)0, scope.Client.Player.NiniaP0);
            scope.UnserializedDisposed(0); Assert.Equal(11, scope.Available); scope.Untaken(1);
        });

    [Fact]
    public Task LoadingGetLevelFaultOccursAfterAlternateAndBeforeUserOrItemProducers() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Ninja.FireMastery = 1000;
            scope.Spell(16310);
            scope.Client.MyNinja = null;
            Assert.Throws<NullReferenceException>(() => scope.Ninja.Loading());
            scope.Sent(RemoveFrame(17, 16310)); Assert.Empty(scope.Spells.ClientSpells);
            Assert.Equal(1000u, scope.Rank(RankType.Overall).RankingList[17].TotalPoints);
            Assert.Equal((byte)0, scope.Client.Player.NiniaP0);
            scope.Serialized(0, RemoveFrame(17, 16310)); Assert.Equal(11, scope.Available); scope.Untaken(1);
        });
}
