using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedManagedManaConsumableTradeTests
{
    private const uint PotionUid = 2001, OtherUid = 2002, PotionType = 1001000, OtherType = 1088001;

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RealOfferedAndUnofferedPotionUsesPreserveTheActualTradeThroughBothConfirmations(bool offerPotion)
    {
        var character = Fixture.Character(minHitPoints: 10, reborn: 0) with
            { Money = 1000, ConquerPoints = 100, MinMana = 10, MaxMana = 5000 };
        var actorItems = new PlayerItemSnapshot(
            [new() { Uid = PotionUid, ItemId = PotionType, StackSize = 3, Durability = 1, MaximumDurability = 1 },
             new() { Uid = OtherUid, ItemId = OtherType, StackSize = 1, Durability = 1, MaximumDurability = 1 }], [], [], [], []);
        var peer = new OwnedEquipmentBehaviorInput(Fixture.Character(uid: Fixture.Peer, name: "ManagedManaTradePeer",
            x: 831, minHitPoints: 1000, reborn: 0) with { Money = 2000, ConquerPoints = 200, MaxMana = 5000 }, PlayerItemSnapshot.Empty);
        var catalog = Fixture.Catalog(Fixture.Item(PotionType, "OwnedManagedManaTradePotion", new Dictionary<int, string>
            { [ItemType.ItemMPFieldIndex] = "70", [ItemType.StackSizeFieldIndex] = "10" }), Fixture.Item(OtherType, "OwnedManagedManaTradeMeteor"));
        return Fixture.RunAsync(new(character, actorItems), catalog, async f =>
        {
            Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("runtime exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
            var actorBinding = Binding(f, Fixture.Actor); var peerBinding = Binding(f, Fixture.Peer);
            AssertHp(f, Fixture.Actor, 10); AssertHp(f, Fixture.Peer, 1000);
            AssertMana(f, Fixture.Actor, 10); AssertMana(f, Fixture.Peer, 5000);
            var actorStart = f.Frames(Fixture.Actor).Count; var peerStart = f.Frames(Fixture.Peer).Count;
            await Trade(f, Fixture.Actor, new(0) { UID = Fixture.Peer });
            await Trade(f, Fixture.Peer, new(1) { UID = Fixture.Actor });
            Assert.True(f.Current().LegacyTradeActive); Assert.True(f.Current(Fixture.Peer).LegacyTradeActive);
            uint offeredUid = offerPotion ? PotionUid : OtherUid;
            await Trade(f, Fixture.Actor, new(6) { ItemUID = offeredUid });
            var trades = Assert.IsType<LegacyTradeStateStore>(typeof(WorldServerFrameHandler)
                .GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
            LegacyTradeParty party; LegacyTradeOffer offer;
            lock (trades.SyncRoot)
            {
                Assert.True(trades.TryGet(f.Connection(Fixture.Actor), out party!));
                offer = Assert.Single(party.Items, pair => pair.Key == offeredUid).Value;
            }
            var originalOffered = Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == offeredUid);
            Assert.Same(originalOffered, offer.Item); Assert.True(offer.Closure.MatchesSource(originalOffered));
            Assert.Contains(f.Frames(Fixture.Peer).Skip(peerStart), bytes =>
                LegacyGameItemPacket.TryParse(Fixture.ReadFrame(bytes), out var preview) && preview.Uid == offeredUid && preview.Mode == 2);
            var before = f.Current(); var beforeFiles = f.Data.Files();
            int first = f.Frames(Fixture.Actor).Count, peerBeforeUse = f.Frames(Fixture.Peer).Count;
            bool firstObserved = false;
            if (!offerPotion)
                f.BeforeSend = (uid, bytes) =>
                {
                    if (uid != Fixture.Actor || firstObserved) return;
                    firstObserved = true; AssertHp(f, Fixture.Actor, 10); AssertMana(f, Fixture.Actor, 80);
                    var saved = new PlayerItemSnapshotLoader(f.Data.Items).LoadByCharacterUid(Fixture.Actor);
                    Assert.Equal((ushort)2, Assert.Single(saved.InventoryItems, row => row.Uid == PotionUid).StackSize);
                    Assert.Single(saved.InventoryItems, row => row.Uid == OtherUid);
                    Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                };
            await f.DispatchAsync(new MsgItemUsagePacket(PotionUid, 0, MsgItemUsageAction.Equip, 1234, 0, 0, 0));
            f.BeforeSend = null;
            if (offerPotion)
            {
                Assert.Same(before, f.Current()); Assert.Null(f.Current().LastLegacyPotionUseUtc);
                Assert.Equal(first, f.Frames(Fixture.Actor).Count); Assert.Equal(peerBeforeUse, f.Frames(Fixture.Peer).Count);
                f.Data.AssertFiles(beforeFiles); AssertHp(f, Fixture.Actor, 10); AssertMana(f, Fixture.Actor, 10);
            }
            else
            {
                Assert.True(firstObserved, f.Log.ToString()); Assert.Equal(2, f.Frames(Fixture.Actor).Count - first);
                Assert.True(LegacyGameItemPacket.TryParse(Fixture.ReadFrame(f.Frames(Fixture.Actor)[first]), out var potion));
                Assert.Equal(PotionUid, potion.Uid); Assert.Equal((ushort)2, potion.StackSize);
                Assert.Equal(LegacyGameItemPacket.UpdateItemMode, potion.Mode);
                Assert.True(LegacyMsgUpdatePacket.TryParse(Fixture.ReadFrame(f.Frames(Fixture.Actor)[first + 1]), out var current));
                var mana = Assert.Single(current.Updates); Assert.Equal(LegacyMsgUpdateDataType.Mana, mana.Type);
                Assert.Equal(80UL, mana.Value); Assert.Equal(peerBeforeUse, f.Frames(Fixture.Peer).Count);
            }
            var afterPotion = Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == offeredUid);
            bool originalOfferRetained = ReferenceEquals(originalOffered, afterPotion) && offer.Closure.MatchesSource(afterPotion);
            // Reach both real final confirmations before asserting identity, so
            // a stale closure exposes the actual offeredItemChanged/refund path.
            await Trade(f, Fixture.Actor, new(9)); await Trade(f, Fixture.Peer, new(9));
            Assert.True(HasCompletion(f, Fixture.Actor, actorStart), f.Log.ToString());
            Assert.True(HasCompletion(f, Fixture.Peer, peerStart), f.Log.ToString());
            Assert.True(originalOfferRetained, "An unchanged real offered closure must retain its original row identity through unrelated managed Mana use.");
            Assert.False(f.Current().LegacyTradeActive); Assert.False(f.Current(Fixture.Peer).LegacyTradeActive);
            Assert.DoesNotContain(f.Current().PlayerItems.InventoryItems, row => row.Uid == offeredUid);
            var transferred = Assert.Single(f.Current(Fixture.Peer).PlayerItems.InventoryItems, row => row.Uid == offeredUid);
            Assert.Equal(offerPotion ? (ushort)3 : (ushort)1, transferred.StackSize);
            if (!offerPotion) Assert.Equal((ushort)2, Assert.Single(f.Current().PlayerItems.InventoryItems, row => row.Uid == PotionUid).StackSize);
            AssertHp(f, Fixture.Actor, 10); AssertHp(f, Fixture.Peer, 1000);
            AssertMana(f, Fixture.Actor, offerPotion ? 10 : 80); AssertMana(f, Fixture.Peer, 5000);
            Assert.Same(actorBinding, Binding(f, Fixture.Actor)); Assert.Same(peerBinding, Binding(f, Fixture.Peer));
            foreach (var uid in new[] { Fixture.Actor, Fixture.Peer })
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current(uid).PlayerItems), File.ReadAllBytes(f.ItemPath(uid)));
            var all = f.Current().PlayerItems.InventoryItems.Concat(f.Current(Fixture.Peer).PlayerItems.InventoryItems).ToArray();
            Assert.Equal(all.Length, all.Select(row => row.Uid).Distinct().Count());
            Assert.Equal(1000L, f.Current().Character.Money); Assert.Equal(2000L, f.Current(Fixture.Peer).Character.Money);
            Assert.Equal(100L, f.Current().Character.ConquerPoints); Assert.Equal(200L, f.Current(Fixture.Peer).Character.ConquerPoints);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
        }, peer);
    }

    private static Task Trade(Fixture fixture, uint uid, LegacyTradePacket packet) => fixture.SendAsync(uid, packet.ToNativeFrameBytes());
    private static bool HasCompletion(Fixture fixture, uint uid, int start) => fixture.Frames(uid).Skip(start)
        .Any(bytes => LegacyTradePacket.TryParse(Fixture.ReadFrame(bytes), out var packet) && packet.Action == 4);
    private static LegacyOrdinarySpellBinding Binding(Fixture fixture, uint uid)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(fixture.Current(uid).NativeGameClient!.OrdinarySpellBinding);
        Assert.False(binding.IsRetired); Assert.True(fixture.Store.IsBoundSpellOwnerCurrent(binding)); return binding;
    }
    private static void AssertHp(Fixture fixture, uint uid, int hp)
    {
        var current = fixture.Current(uid); Assert.Equal((long)hp, current.Character.MinHitPoints);
        Assert.Equal(hp, Assert.IsType<LegacyNativePlayerRuntime>(current.NativePlayer).HitPoints);
        Assert.Equal((long)hp, CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
            File.ReadAllBytes(fixture.CharacterPath(uid)), out _)).MinHitPoints);
    }
    private static void AssertMana(Fixture fixture, uint uid, long mana)
    {
        var current = fixture.Current(uid); Assert.Equal(mana, current.Character.MinMana);
        Assert.Equal(mana.ToString(System.Globalization.CultureInfo.InvariantCulture), current.Character.Attributes["MinMana"]);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(fixture.CharacterPath(uid)), out _));
        Assert.Equal(mana, saved.MinMana); Assert.Equal(5000L, saved.MaxMana); Assert.Equal(5000L, current.Character.MaxMana);
        // Native Mana ownership/hydration is open. No stale/zero native value is
        // required by a successful managed transaction or its trade continuation.
    }
}
