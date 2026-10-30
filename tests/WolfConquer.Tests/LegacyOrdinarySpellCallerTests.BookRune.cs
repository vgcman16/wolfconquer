using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using ItemFixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinarySpellCallerTests
{
    private static void CheckItemNative(ItemFixture f)
    {
        var native = Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells).ClientSpells.ToArray();
        Assert.Equal(f.Current.PlayerSpells.Spells.Select(s => s.Id).Order(), native.Select(p => p.Key).Order());
        foreach (var pair in native)
        {
            var row = Assert.Single(f.Current.PlayerSpells.Spells, s => s.Id == pair.Key);
            Assert.Equal(row.Level, pair.Value.Level); Assert.Equal(row.Experience, pair.Value.Experience);
            Assert.Equal(row.UseSpellSoul, pair.Value.UseSpellSoul);
        }
    }
    private static byte[] BookRequest() => new MsgItemUsagePacket(ItemFixture.ItemUid, 0, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes();
    private static byte[] RuneRequest() => new MsgItemUsagePacket(ItemFixture.ItemUid, 102, MsgItemUsageAction.UnequipRune, 511043495, 0, 0, 0).ToFrameBytes();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualAdmittedBookAndRuneHandlersCommitThreeImagesBeforeSuccessfulOutput(bool rune)
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(rune, async f =>
        {
            await f.Login();
            if (rune) await f.EstablishOwnedRuneOriginAsync();
            var before = f.Current; CheckItemNative(f);
            Assert.True(f.Recovery.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
            byte[] oldCharacter = File.ReadAllBytes(f.CharacterPath), oldSpells = File.ReadAllBytes(f.SpellsPath);
            int first = f.Sent.Count; bool firstObserved = false;
            f.OnSend = sentFrame =>
            {
                if (firstObserved) return;
                firstObserved = true; var committed = f.Current;
                Assert.NotSame(before, committed); Assert.False(LegacyTradeJournal.HasPending(f.Users));
                var itemDisk = new PlayerItemSnapshotLoader(f.Items).LoadByCharacterUid(ItemFixture.Uid);
                var spellDisk = new PlayerSpellSnapshotLoader(f.Spells).LoadByCharacterUid(ItemFixture.Uid);
                var characterText = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _);
                var characterDisk = CharacterSnapshotParser.ParseText(characterText);
                Assert.Equal(committed.Character.Uid, characterDisk.Uid);
                Assert.Equal(committed.Character.MinHitPoints, characterDisk.MinHitPoints);
                Assert.Equal(committed.PlayerSpells.Spells, spellDisk.Spells);
                if (rune)
                {
                    Assert.Equal((ushort)212, Assert.Single(itemDisk.RuneItems, x => x.Uid == ItemFixture.ItemUid).Position);
                    Assert.DoesNotContain(spellDisk.Spells, s => s.Id == 14160);
                    Assert.Equal("", characterDisk.Attributes["ManagedRuneSpellOrigins"]);
                    var ranking = Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler)
                        .GetField("_runeRankings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(f.Handler));
                    Assert.Equal(10600U, Assert.Single(ranking.Snapshot(), row => row.CharacterUid == ItemFixture.Uid).Score);
                }
                else { Assert.DoesNotContain(itemDisk.InventoryItems, x => x.Uid == ItemFixture.ItemUid); Assert.Contains(spellDisk.Spells, s => s.Id == 1360); }
            };
            await f.Send(rune ? RuneRequest() : BookRequest()); f.OnSend = null; var after = f.Current;
            Assert.True(firstObserved);
            Assert.NotSame(before, after); CheckItemNative(f); Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.Equal(before.Character.Money, after.Character.Money); Assert.Equal(before.Character.ConquerPoints, after.Character.ConquerPoints);
            var persistedItems = new PlayerItemSnapshotLoader(f.Items).LoadByCharacterUid(ItemFixture.Uid);
            var persistedSpells = new PlayerSpellSnapshotLoader(f.Spells).LoadByCharacterUid(ItemFixture.Uid);
            Assert.Equal(after.PlayerSpells.Spells, persistedSpells.Spells);
            if (rune)
            {
                Assert.Equal((ushort)212, Assert.Single(after.PlayerItems.RuneItems, x => x.Uid == ItemFixture.ItemUid).Position);
                Assert.Equal((ushort)212, Assert.Single(persistedItems.RuneItems, x => x.Uid == ItemFixture.ItemUid).Position);
                Assert.DoesNotContain(after.PlayerSpells.Spells, s => s.Id == 14160);
                Assert.Equal(Convert.FromHexString("01000000").Concat(oldSpells.Skip(4).Take(12)).ToArray(), File.ReadAllBytes(f.SpellsPath));
                Assert.Equal("", after.Character.Attributes["ManagedRuneSpellOrigins"]);
                Assert.True(MsgActionPacket.TryParse(Frame(f.Sent[first]), out var removal));
                Assert.Equal((MsgActionType)109, removal.Type); Assert.Equal(14160U, removal.DwParam);
                Assert.Equal(RuneRequest(), f.Sent[first + 1]);
            }
            else
            {
                Assert.DoesNotContain(after.PlayerItems.InventoryItems, x => x.Uid == ItemFixture.ItemUid);
                Assert.DoesNotContain(persistedItems.InventoryItems, x => x.Uid == ItemFixture.ItemUid);
                Assert.Contains(after.PlayerSpells.Spells, s => s.Id == 1360);
                Assert.Equal(Convert.FromHexString("02000000").Concat(oldSpells.Skip(4)).Concat(Convert.FromHexString("500500000000000000000000")).ToArray(), File.ReadAllBytes(f.SpellsPath));
                Assert.True(MsgItemUsagePacket.TryParse(Frame(f.Sent[first]), out var consumed));
                Assert.Equal(MsgItemUsageAction.RemoveInventory, consumed.Action); Assert.Equal(ItemFixture.ItemUid, consumed.Id);
                Assert.Equal(Convert.FromHexString("19003508000000005005000000000000000000000000000000"), f.Sent[first + 1]);
            }
            Assert.Equal(0xA5, File.ReadAllBytes(f.SpellsPath)[15]);
            if (!rune) Assert.Equal(oldCharacter, File.ReadAllBytes(f.CharacterPath));
            Assert.True(f.Sent.Count > first + 1);
        }, ownedRuneOrigin: rune);
    }

    [Theory]
    [InlineData(false, "recovery")]
    [InlineData(false, "item-drift")]
    [InlineData(false, "native-drift")]
    [InlineData(true, "recovery")]
    [InlineData(true, "item-drift")]
    [InlineData(true, "native-drift")]
    public async Task ActualBookAndRunePreflightRefusalCannotConsumeOrPublishSuccess(bool rune, string fault)
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(rune, async f =>
        {
            await f.Login(); var before = f.Current; CheckItemNative(f);
            if (fault == "recovery") f.Recovery.Block("owned v9 refusal control");
            else if (fault == "item-drift") { byte[] bytes = File.ReadAllBytes(f.ItemsPath); bytes[20] ^= 1; File.WriteAllBytes(f.ItemsPath, bytes); }
            else Assert.IsType<LegacyNativeSpellStorage>(f.Native.MySpells).ClientSpells[6001].Experience++;
            var files = f.Files(); int first = f.Sent.Count;
            await f.Send(rune ? RuneRequest() : BookRequest());
            Assert.Same(before, f.Current); Assert.Equal(first, f.Sent.Count); f.CheckFiles(files);
            Assert.Equal(before.Character.Money, f.Current.Character.Money); Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            if (rune) { Assert.Equal((ushort)102, Assert.Single(before.PlayerItems.RuneItems, x => x.Uid == ItemFixture.ItemUid).Position); Assert.Contains(before.PlayerSpells.Spells, s => s.Id == 14160); }
            else { Assert.Contains(before.PlayerItems.InventoryItems, x => x.Uid == ItemFixture.ItemUid); Assert.DoesNotContain(before.PlayerSpells.Spells, s => s.Id == 1360); }
        });
    }
}
