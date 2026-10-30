using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneChanceTests
{
    private sealed partial class ChanceFixture
    {
        internal void AssertConverged()
        {
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(Native.OrdinarySpellBinding);
            Assert.True(Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(Catalog, binding.ItemCatalog);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems), File.ReadAllBytes(ItemsPath));
            Assert.Equal(Current.PlayerSpells.Spells, PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(SpellsPath)).Spells);
            Assert.Equal(Current.PlayerSpells.Spells.Select(row => row.Id).Order(), Native.MySpells!.ClientSpells.Keys.Order());
            foreach (var row in Current.PlayerSpells.Spells)
            {
                var actual = Native.MySpells.ClientSpells[row.Id]; Assert.Equal(row.Level, actual.Level); Assert.Equal(row.Experience, actual.Experience);
                Assert.Equal(row.PreviousLevel, actual.PreviousLevel); Assert.Equal(row.SoulLevel, actual.SoulLevel); Assert.Equal(row.UseSpellSoul, actual.UseSpellSoul);
            }
            foreach (var row in Current.PlayerItems.RuneItems)
            {
                var actual = Assert.Single(binding.Items.Rune.Objects, item => item.Uid == row.Uid);
                Assert.Equal(row.ItemId, actual.ITEM_ID); Assert.Equal(row.RuneExperience, actual.RuneEXP);
                Assert.Equal(row.Position, actual.Position); Assert.Equal(row.StackSize, actual.StackSize);
            }
            var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath), out _));
            Assert.Equal(Current.Character.Money, saved.Money); Assert.Equal(Current.Character.ConquerPoints, saved.ConquerPoints);
            Assert.Equal(Current.Character.Money, Native.Player!.Money); Assert.Equal((uint)Current.Character.ConquerPoints, Native.Player.ConquerPoints);
            Assert.Equal(Current.Character.MinHitPoints, saved.MinHitPoints); Assert.Equal(Current.Character.MinHitPoints, (long)Native.Player.HitPoints);
            Assert.Equal(Origins, saved.Attributes.GetValueOrDefault(Marker)); Assert.False(LegacyTradeJournal.HasPending(Data.Users));
            if (Ledger.Length != 0) Assert.Equal(Ledger, File.ReadAllBytes(Data.State));
            Assert.Equal(OriginalSpells.AsSpan(4, 12).ToArray(), File.ReadAllBytes(SpellsPath).AsSpan(4, 12).ToArray());
        }
    }

    private sealed record ExpectedImages(byte[] Character, byte[] Items, byte[] Spells)
    {
        internal void AssertFiles(ChanceFixture f)
        {
            Assert.Equal(Character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(Items, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(Spells, File.ReadAllBytes(f.SpellsPath)); Assert.Equal(f.Ledger, File.ReadAllBytes(f.Data.State));
        }
    }
    private static ExpectedImages Expect(ChanceFixture f, uint type, uint experience)
    {
        var before = f.Current.PlayerItems;
        var after = before with { RuneItems = before.RuneItems.Select(row => row.Uid == ChanceFixture.Target
            ? row.WithItemId(type).WithRuneExperience(experience) : row).ToArray() };
        return new(File.ReadAllBytes(f.CharacterPath), PlayerItemSnapshotLoader.ToLegacyBytes(after), File.ReadAllBytes(f.SpellsPath));
    }
    private static async Task ChanceAndAssert(ChanceFixture f, uint type, uint experience)
    {
        var expected = Expect(f, type, experience); var before = f.Current;
        var beforeIds = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(File.ReadAllBytes(f.ItemsPath)); int first = f.Sent.Count;
        var nativeSpell = before.PlayerSpells.Spells.Any(row => row.Id == 14160) ? f.Native.MySpells!.ClientSpells[14160] : null;
        f.BeforeSend = _ => { expected.AssertFiles(f); f.AssertConverged(); };
        try { await f.DispatchAsync(ChanceFixture.Request()); } finally { f.BeforeSend = null; }
        expected.AssertFiles(f); f.AssertConverged(); Assert.Equal(first + 1, f.Sent.Count);
        var target = Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == ChanceFixture.Target);
        Assert.Equal(type, target.ItemId); Assert.Equal(experience, target.RuneExperience);
        Assert.Equal(UpdateGolden(target), f.Sent[first]);
        Assert.True(LegacyGameItemPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(f.Sent[first]), out var decoded));
        Assert.Equal(type, decoded.ItemId); Assert.Equal(experience, decoded.RuneExperience); Assert.Equal(ChanceFixture.Target, decoded.Uid);
        Assert.Equal(beforeIds.Order(), LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(expected.Items).Order());
        Assert.Equal(before.Character.Money, f.Current.Character.Money); Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        Assert.Equal(before.Character.MinHitPoints, f.Current.Character.MinHitPoints); Assert.Equal(before.PlayerSpells.Spells, f.Current.PlayerSpells.Spells);
        Assert.Equal(CharacterSnapshotScalarPersistence.BuildPersistedValues(before.Character).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            CharacterSnapshotScalarPersistence.BuildPersistedValues(f.Current.Character).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(before.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            f.Current.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(before.Character.PaidNobility.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase),
            f.Current.Character.PaidNobility.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase));
        if (nativeSpell is not null) Assert.Same(nativeSpell, f.Native.MySpells!.ClientSpells[14160]);
        var score = LegacyRuneStoragePacket.ComputeStorageScore(f.Current.PlayerItems);
        if (score >= LegacyRuneRankingState.MinimumScore) Assert.Equal(score, Assert.Single(f.Rankings.Snapshot(), row => row.CharacterUid == ChanceFixture.Uid).Score);
        else Assert.DoesNotContain(f.Rankings.Snapshot(), row => row.CharacterUid == ChanceFixture.Uid);
    }

    // Independent complete D13 2116 Update. The stored permission control also
    // retains its Bound/Locked bytes; owner/signature and reserved tails are zero.
    private static byte[] UpdateGolden(PlayerItemSnapshotItem item)
    {
        var bytes = new byte[206]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 206);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2116); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), item.Uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), item.ItemId); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 10); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3);
        bytes[18] = checked((byte)item.Position); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(25), 1_416_000U + item.ItemId % 100 - 1);
        bytes[32] = item.Bound; bytes[40] = item.Locked;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), item.StackSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(166), item.RuneExperience); return bytes;
    }
}
