using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyRuneStorageHandlerTests
{
    private Task AssertOwnedStorageMoveAsync(byte type, uint uid, uint itemId, ushort beforePosition,
        ushort afterPosition, uint requiredClass, uint encoded)
    {
        WriteSnapshot(reborn: 2);
        var character = CharacterSnapshotParser.ParseText(File.ReadAllText(Path.Combine(_usersRoot, "1293600.ini")));
        var raw = PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [PlayerItemSnapshotLoaderTests.DbItem(uid, itemId, position: beforePosition)], []);
        var items = PlayerItemSnapshotLoader.Parse(raw);
        var metadata = OwnedEquipmentBehaviorFixture.Item(itemId, "OwnedLegacyStorage", new Dictionary<int, string>
        {
            [ItemType.RequiredClassFieldIndex] = requiredClass.ToString(CultureInfo.InvariantCulture),
            [ItemType.RuneSpellIdFieldIndex] = encoded.ToString(CultureInfo.InvariantCulture),
        });
        var catalog = NativeCallerWorldDataFixture.Create(new ItemType[] { metadata }, [], magicEffects:
            MagicEffectParser.Parse($"[{encoded / 100 * 100}]\r\nIsRuneSkill=1\r\n", "owned-storage-MagicEffect.ini"));
        return OwnedEquipmentBehaviorFixture.RunAsync(new(character, items), catalog, async f =>
        {
            var before = f.Current(); var ledger = File.ReadAllBytes(f.Data.State);
            var expected = before.PlayerItems with { RuneItems = before.PlayerItems.RuneItems.Select(row => row.WithPosition(afterPosition)).ToArray() };
            var image = PlayerItemSnapshotLoader.ToLegacyBytes(expected); int first = f.ActorFrames.Count, observed = 0;
            f.BeforeSend = (_, _) =>
            {
                Assert.Equal(image, File.ReadAllBytes(f.ItemPath())); Assert.Equal(image, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current().PlayerItems));
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                Assert.Equal(ledger, File.ReadAllBytes(f.Data.State)); observed++;
            };
            try { await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyRuneStoragePacket(type, uid, 0).ToFrameBytes()); }
            finally { f.BeforeSend = null; }
            Assert.Equal(11, observed); Assert.Equal(first + 11, f.ActorFrames.Count);
            AssertRuneStorage(f.ActorFrames[first], type, uid, hpAdd: 0);
            LegacySelectedEquipmentQueryAssertions.NeutralQuery(f.ActorFrames, first + 1, OwnedEquipmentBehaviorFixture.Actor);
            Assert.Equal(before.PlayerSpells.Spells, f.Current().PlayerSpells.Spells);
            foreach (var pair in before.Character.Attributes.Where(pair => !pair.Key.Equals("MinHitPoints", StringComparison.OrdinalIgnoreCase) &&
                         !pair.Key.Equals("Online", StringComparison.OrdinalIgnoreCase)))
                Assert.Equal(pair.Value, f.Current().Character.Attributes[pair.Key]);
            var after = f.Current().Character;
            Assert.Equal(before.Character.PaidNobility.OrderBy(pair => pair.Key), after.PaidNobility.OrderBy(pair => pair.Key));
            Assert.Equal(before.Character.PlayerQuests.Quests, after.PlayerQuests.Quests);
            Assert.Equal(before.Character with { MinHitPoints = after.MinHitPoints, Online = after.Online,
                Attributes = after.Attributes, PaidNobility = after.PaidNobility, PlayerQuests = after.PlayerQuests }, after);
            Assert.Equal(image, File.ReadAllBytes(f.ItemPath())); Assert.Equal(ledger, File.ReadAllBytes(f.Data.State));
            Assert.Contains($"type={type}", f.Log.ToString(), StringComparison.Ordinal);
            Assert.Contains("committed LegacyRuneStorage", f.Log.ToString(), StringComparison.Ordinal);
        });
    }
}
