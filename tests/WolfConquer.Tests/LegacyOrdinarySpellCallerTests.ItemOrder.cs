using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using ItemFixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinarySpellCallerTests
{
    private static byte[] UnsortedItemImage(bool rune)
    {
        if (rune)
            return PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                [PlayerItemSnapshotLoaderTests.DbItem(ItemFixture.ItemUid + 1, 4020515, 212, stackSize: 1),
                 PlayerItemSnapshotLoaderTests.DbItem(ItemFixture.ItemUid, 4020517, 102, stackSize: 1)], []);
        return PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [PlayerItemSnapshotLoaderTests.DbItem(ItemFixture.ItemUid, 725016, 0, stackSize: 1),
             PlayerItemSnapshotLoaderTests.DbItem(ItemFixture.ItemUid + 3, 4020515, 212, stackSize: 1),
             PlayerItemSnapshotLoaderTests.DbItem(ItemFixture.ItemUid + 2, 4020515, 211, stackSize: 1)], []);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AdmittedUnsortedItemsCommitBookOrRuneBeforeOutput(bool rune)
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(rune, async f =>
        {
            var raw = UnsortedItemImage(rune);
            Assert.False(raw.AsSpan().SequenceEqual(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(raw))));
            File.WriteAllBytes(f.ItemsPath, raw);
            await f.Login();
            Assert.Equal(raw, File.ReadAllBytes(f.ItemsPath));
            var beforeCharacter = File.ReadAllBytes(f.CharacterPath);
            var before = f.Current; int first = f.Sent.Count; bool observed = false;
            f.OnSend = _ =>
            {
                if (observed) return;
                observed = true;
                Assert.NotSame(before, f.Current);
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), File.ReadAllBytes(f.ItemsPath));
                Assert.Equal(f.Current.PlayerSpells.Spells, new PlayerSpellSnapshotLoader(f.Spells)
                    .LoadByCharacterUid(ItemFixture.Uid).Spells);
                var persistedCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(f.CharacterPath));
                Assert.Equal(f.Current.Character.Uid, persistedCharacter.Uid);
                Assert.Equal(f.Current.Character.MinHitPoints, persistedCharacter.MinHitPoints);
                if (!rune) Assert.Equal(beforeCharacter, File.ReadAllBytes(f.CharacterPath));
                Assert.False(LegacyTradeJournal.HasPending(f.Users));
            };
            await f.Send(rune ? RuneRequest() : BookRequest());
            f.OnSend = null;
            Assert.True(observed, f.Log.ToString());
            Assert.True(f.Sent.Count > first);
            if (rune)
            {
                Assert.Equal((ushort)212, Assert.Single(f.Current.PlayerItems.RuneItems, r => r.Uid == ItemFixture.ItemUid).Position);
                Assert.DoesNotContain(f.Current.PlayerSpells.Spells, s => s.Id == 14160);
                Assert.True(MsgActionPacket.TryParse(Frame(f.Sent[first]), out var removal));
                Assert.Equal((MsgActionType)109, removal.Type);
                Assert.Equal(RuneRequest(), f.Sent[first + 1]);
            }
            else
            {
                Assert.DoesNotContain(f.Current.PlayerItems.InventoryItems, r => r.Uid == ItemFixture.ItemUid);
                Assert.Contains(f.Current.PlayerSpells.Spells, s => s.Id == 1360);
                Assert.True(MsgItemUsagePacket.TryParse(Frame(f.Sent[first]), out var consume));
                Assert.Equal(MsgItemUsageAction.RemoveInventory, consume.Action);
                Assert.Equal(ItemFixture.ItemUid, consume.Id);
            }
        });
    }

    [Fact]
    public void RawRecordWitnessRetainsDepositsPerfectionAndOpaqueBytes()
    {
        var child = PlayerItemSnapshotLoaderTests.DbItem(1103, 1003, 0);
        var child2 = PlayerItemSnapshotLoaderTests.DbItem(1104, 1004, 0);
        var first = PlayerItemSnapshotLoaderTests.DbItem(1101, 1001, 0, depositedItems: new[] { child, child2 });
        var firstReversed = PlayerItemSnapshotLoaderTests.DbItem(1101, 1001, 0, depositedItems: new[] { child2, child });
        var second = PlayerItemSnapshotLoaderTests.DbItem(1102, 1002, 0);
        var p1 = PlayerItemSnapshotLoaderTests.Perfection(1101, 2, 3, 4, "owner", "sig");
        var p2 = PlayerItemSnapshotLoaderTests.Perfection(1102, 5, 6, 7, "owner2", "sig2");
        var firstOrder = PlayerItemSnapshotLoaderTests.BuildPlayerItems([first, second], [p1, p2]);
        var secondOrder = PlayerItemSnapshotLoaderTests.BuildPlayerItems([second, firstReversed], [p2, p1]);
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(firstOrder);
        var method = typeof(WorldPacketHandlers).GetMethod("ItemSpellRawRecordsMatch", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.True((bool)method!.Invoke(null, [firstOrder, secondOrder])!);
        var changedOpaque = firstOrder.ToArray(); changedOpaque[4 + 14] ^= 0x40;
        Assert.False((bool)method.Invoke(null, [changedOpaque, secondOrder])!);
        var changedPerfection = firstOrder.ToArray(); changedPerfection[^1] ^= 0x20;
        Assert.False((bool)method.Invoke(null, [changedPerfection, secondOrder])!);
        var changedChild = firstOrder.ToArray(); changedChild[4 + PlayerItemSnapshotLoader.LegacyDbItemSize + 14] ^= 0x40;
        Assert.False((bool)method.Invoke(null, [changedChild, secondOrder])!);
        var wrongParent = PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [PlayerItemSnapshotLoaderTests.DbItem(1101, 1001, 0, depositedItems: new[] { child }),
             PlayerItemSnapshotLoaderTests.DbItem(1102, 1002, 0, depositedItems: new[] { child2 })], [p1, p2]);
        Assert.False((bool)method.Invoke(null, [wrongParent, secondOrder])!);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AdmittedUnsortedItemsRejectUnrepresentedOpaqueDrift(bool rune)
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(rune, async f =>
        {
            File.WriteAllBytes(f.ItemsPath, UnsortedItemImage(rune));
            await f.Login(); var current = f.Current;
            var bytes = File.ReadAllBytes(f.ItemsPath);
            bytes[4 + 14] ^= 0x40; // Unmodeled DBItem padding, not a modeled property.
            File.WriteAllBytes(f.ItemsPath, bytes);
            var before = f.Files(); int first = f.Sent.Count;
            await f.Send(rune ? RuneRequest() : BookRequest());
            Assert.Same(current, f.Current);
            Assert.Equal(first, f.Sent.Count); f.CheckFiles(before);
            Assert.Contains("itemSpellItemBaselineStaleOrUnrepresentable", f.Log.ToString());
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });
    }

    [Fact]
    public async Task ExactCapturedBeforeImageRejectsExternalReordering()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(true, async f =>
        {
            var raw = UnsortedItemImage(true); File.WriteAllBytes(f.ItemsPath, raw); await f.Login();
            var character = File.ReadAllBytes(f.CharacterPath); var spells = File.ReadAllBytes(f.SpellsPath);
            LegacyTradeJournalChange[] captured =
            [new(LegacyTradeJournalFileKind.Character, ItemFixture.Uid, character, character),
             new(LegacyTradeJournalFileKind.Items, ItemFixture.Uid, raw, raw),
             new(LegacyTradeJournalFileKind.Spells, ItemFixture.Uid, spells, spells)];
            string[] paths = [f.CharacterPath, f.ItemsPath, f.SpellsPath];
            var method = typeof(WorldPacketHandlers).GetMethod("ItemSpellBeforeImagesMatch", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Assert.True((bool)method!.Invoke(null, [paths, captured])!);
            var changed = raw.ToArray(); const int width = PlayerItemSnapshotLoader.LegacyDbItemSize;
            raw.AsSpan(4 + width, width).CopyTo(changed.AsSpan(4, width));
            raw.AsSpan(4, width).CopyTo(changed.AsSpan(4 + width, width));
            File.WriteAllBytes(f.ItemsPath, changed);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(raw)),
                PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(changed)));
            Assert.False((bool)method.Invoke(null, [paths, captured])!);
            Assert.Equal(changed, File.ReadAllBytes(f.ItemsPath));
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
        });
    }
}
