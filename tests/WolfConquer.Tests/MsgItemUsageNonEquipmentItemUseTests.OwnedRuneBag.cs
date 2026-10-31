using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private Task WithOwnedLegacyRuneBagAsync(WorldDataCatalog source, Func<OwnedEquipmentBehaviorFixture, Task> body, params uint[] uids)
    {
        var loader = new CharacterSnapshotLoader(_usersRoot, _itemsRoot);
        var inputs = uids.Select(uid => new OwnedEquipmentBehaviorInput(
            Assert.IsType<CharacterSnapshot>(loader.TryLoadByAccountIdForAdmission(uid).Snapshot),
            new PlayerItemSnapshotLoader(_itemsRoot).LoadByCharacterUid(uid),
            new PlayerSpellSnapshotLoader(loader.PlayerSpellsRoot!).LoadByCharacterUid(uid))).ToArray();
        Assert.NotEmpty(inputs);
        // No Equip, origin, generated UID or accepting saver is introduced.
        // These exact saved rows are admitted by the real D13 owner first.
        return OwnedEquipmentBehaviorFixture.RunAsync(inputs[0], OwnedLegacyRuneBagCatalog(source), body, inputs.Skip(1).ToArray());
    }

    private static WorldDataCatalog OwnedLegacyRuneBagCatalog(WorldDataCatalog source, uint? absentBagCap = null)
    {
        // Actual DBServer itemtype2/4/25/46, read independently for these old
        // fixtures. Container moves do not import Equip class requirements.
        var known = new Dictionary<uint, (uint Class, uint Spell)>
        { [4_010_501] = (7000, 1425000), [4_020_501] = (6000, 1416000),
          [4_020_514] = (6000, 1416013), [4_020_515] = (6000, 1416014),
          [4_020_516] = (6000, 1416015), [4_020_517] = (6000, 1416016),
          [4_020_615] = (7000, 1444014), [4_030_501] = (0, 4030501) };
        var rows = source.Items.ToDictionary(row => checked((uint)row.Id));
        foreach (var (id, values) in known)
        {
            var fields = rows.TryGetValue(id, out var old) ? old.Fields.ToArray()
                : OwnedEquipmentBehaviorFixture.Item(id, "OwnedBag" + id).Fields.ToArray();
            fields[ItemType.RequiredClassFieldIndex] = values.Class.ToString(CultureInfo.InvariantCulture);
            fields[ItemType.RequiredLevelFieldIndex] = "0"; fields[ItemType.RuneSpellIdFieldIndex] = values.Spell.ToString(CultureInfo.InvariantCulture);
            if (fields.Length > ItemType.StackSizeFieldIndex) fields[ItemType.StackSizeFieldIndex] = "0";
            if (id == absentBagCap) fields = fields.Take(ItemType.StackSizeFieldIndex).ToArray();
            rows[id] = new(checked((int)id), old?.Name ?? "OwnedBag" + id, fields);
        }
        if (!rows.ContainsKey(1_000_000)) rows.Add(1_000_000, OwnedEquipmentBehaviorFixture.Item(1_000_000, "OwnedBagKept"));
        return NativeCallerWorldDataFixture.Create(rows.Values.ToArray(), [], runeRankings: source.RuneRankings);
    }

    private async Task AssertOwnedLegacyRuneBagMainAsync(bool intoBag)
    {
        const uint actor = 1_293_600; uint target = intoBag ? 5_337_500U : 5_337_510U;
        uint type = intoBag ? 4_010_501U : 4_020_501U;
        WriteSnapshot(accountId: actor, uid: actor, mapId: 1_015, x: 826, y: 565);
        WriteItems(actor, PlayerItemSnapshotLoaderTests.DbItem(target, type, position: intoBag ? (ushort)0 : (ushort)211));
        await WithOwnedLegacyRuneBagAsync(CreateWorldDataCatalog(), async f =>
        {
            var before = f.Current(actor); var selected = Assert.Single(intoBag ? before.PlayerItems.InventoryItems : before.PlayerItems.RuneItems);
            var state = File.ReadAllBytes(f.Data.State); uint high = OwnedBagHighWater(state);
            var moved = intoBag ? selected.WithPosition(211) : selected.WithUid(checked(high + 1)).WithPosition(0);
            var expected = intoBag ? before.PlayerItems with { InventoryItems = [], RuneItems = [moved] }
                : before.PlayerItems with { InventoryItems = [moved], RuneItems = [] };
            var request = CreateRuneBagUse(target, intoBag ? 0UL : 1UL, intoBag ? new uint[] { 8888 } : []);
            Assert.Equal(OwnedBagUsageGolden(target, intoBag ? 0UL : 1UL, intoBag ? new uint[] { 8888 } : []), request);
            var files = OwnedBagExpectedFiles(f, actor, expected); int first = f.ActorFrames.Count, observed = 0;
            f.BeforeSend = (uid, _) =>
            { Assert.Equal(actor, uid); AssertOwnedBagConverged(f, actor, files); observed++; };
            try { await f.SendAsync(actor, request); } finally { f.BeforeSend = null; }
            AssertOwnedBagConverged(f, actor, files); int count = intoBag ? 12 : 3;
            Assert.Equal(count, observed); Assert.Equal(first + count, f.ActorFrames.Count);
            int index = first;
            if (!intoBag) Assert.Equal(OwnedBagRemoveGolden(target), f.ActorFrames[index++]);
            Assert.Equal(OwnedBagItemGolden(moved), f.ActorFrames[index++]); Assert.Equal(request, f.ActorFrames[index++]);
            if (intoBag) LegacySelectedEquipmentQueryAssertions.NeutralQuery(f.ActorFrames, index, actor);
            Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expected), File.ReadAllBytes(f.ItemPath(actor)));
            Assert.Equal(selected.Locked, moved.Locked); Assert.Equal(selected.RuneExperience, moved.RuneExperience);
            AssertOwnedBagUidTransition(f, state, !intoBag);
            Assert.Contains("committed direct rune bag " + (intoBag ? "add" : "remove"), f.Log.ToString(), StringComparison.Ordinal);
        }, actor);
    }

    private async Task AssertOwnedLegacyRuneBagLevelRefusalAsync()
    {
        const uint actor = 1_293_600, target = 5_337_520;
        WriteSnapshot(accountId: actor, uid: actor, mapId: 1_015, x: 826, y: 565, level: 14);
        WriteItems(actor, PlayerItemSnapshotLoaderTests.DbItem(target, 4_030_501, position: 0));
        await WithOwnedLegacyRuneBagAsync(CreateWorldDataCatalog(), async f =>
        {
            var current = f.Current(actor); var files = f.Data.Files(); int first = f.ActorFrames.Count;
            var binding = current.NativeGameClient!.OrdinarySpellBinding!; Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            await f.SendAsync(actor, CreateRuneBagUse(target, 0));
            Assert.Equal(first, f.ActorFrames.Count); Assert.Same(current, f.Current(actor)); f.Data.AssertFiles(files);
            AssertOwnedBagConverged(f, actor, files); Assert.Contains("second reborn", f.Log.ToString(), StringComparison.Ordinal);
        }, actor);
    }
}
