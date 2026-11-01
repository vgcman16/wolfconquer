using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedAttributeCacheTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;

    private static (OwnedEquipmentBehaviorInput Input, WorldDataCatalog Catalog) Inputs()
    {
        var head = OwnedEquipmentBehaviorFixture.Item(111_095, fields: new Dictionary<int, string>
            { [18] = "4000", [19] = "100" });
        var necklace = OwnedEquipmentBehaviorFixture.Item(120_095, fields: new Dictionary<int, string>
            { [18] = "1000", [19] = "50" });
        var items = new PlayerItemSnapshotItem[]
        {
            new() { Uid = 99101, ItemId = 111_095, Position = 1, Durability = 100, MaximumDurability = 100, StackSize = 1 },
            new() { Uid = 99102, ItemId = 120_095, Position = 2, Durability = 100, MaximumDurability = 100, StackSize = 1 },
        };
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        return (new(character, new PlayerItemSnapshot([], [], [], items, [])),
            OwnedEquipmentBehaviorFixture.Catalog(new ItemType[] { head, necklace }));
    }

    [Fact]
    public Task CompletedQueryRetainsBothEquippedHpAndManaWhenAllocating()
    {
        var (input, catalog) = Inputs();
        return OwnedEquipmentBehaviorFixture.RunAsync(input, catalog, async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(before, out var oldMaximum));
            Assert.Equal(5030U, oldMaximum);
            Assert.Equal(450U, LegacyPhysicalStatusOverlay.ForOwner(before).MaxMana);
            await AssertFullRebuildAsync(f);
        });
    }

    private static async Task AssertFullRebuildAsync(OwnedEquipmentBehaviorFixture f)
    {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var files = f.Data.Files();
            var expected = files.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var text = CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
            Assert.Contains("\r\nSpirit=10\r\n", text); Assert.Contains("\r\nAtributes=12\r\n", text);
            text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal);
            expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            var first = f.ActorFrames.Count;
            await f.SendAsync(Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            var current = f.Current();
            Assert.Equal(20L, current.Character.Spirit); Assert.Equal(2L, current.Character.AttributePoints);
            Assert.Equal((ushort)20, binding.Player.Spirit); Assert.Equal((ushort)2, binding.Player.Atributes);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(current, out var maximum));
            Assert.Equal(5060U, maximum); Assert.Equal(750U, LegacyPhysicalStatusOverlay.ForOwner(current).MaxMana);
            Assert.Equal(200L, current.Character.MinHitPoints); Assert.Equal(200, binding.Player.HitPoints);
            f.Data.AssertFiles(expected);
            var frames = f.ActorFrames.Skip(first).Select(OwnedEquipmentBehaviorFixture.ReadFrame).ToArray();
            // 2203 echoes first, then runs the complete CallItems:false query:
            // blood-tide zero setter, status, final clamp, pair and perfection.
            Assert.Equal(new ushort[] { 2203, 2176, 2176, 2301, 2176, 2176, 2176, 2176, 2128 },
                frames.Select(frame => frame.Header.Opcode));
            Assert.True(LegacyAttributeSetPacket.TryParse(frames[0], out var echo));
            Assert.Equal(new LegacyAttributeSetPacket(0, 0, 0, 10), echo);
            Assert.True(LegacyMsgUpdatePacket.TryParse(frames[1], out var update));
            Assert.Contains(update.Updates, row => row.Type == LegacyMsgUpdateDataType.MaxHitpoints && row.Value == 5060);
            Assert.True(LegacyPlayerStatusPacket.TryParse(frames[3], out var rebuilt));
            Assert.Equal(5060U, rebuilt.MaxHP); Assert.Equal(750U, rebuilt.MaxMana);
            Assert.Equal(21U, rebuilt.MagicAttack); // Spirit20 plus the real late-profile +1.
            first = f.ActorFrames.Count;
            await f.SendAsync(Actor, new LegacyPlayerStatusQueryPacket(Actor).ToFrameBytes());
            var statusFrame = Assert.Single(f.ActorFrames.Skip(first));
            Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(statusFrame), out var status));
            Assert.Equal(5060U, status.MaxHP); Assert.Equal(750U, status.MaxMana); Assert.Equal(21U, status.MagicAttack);
            f.Data.AssertFiles(expected);
            var committed = f.Current();
            first = f.ActorFrames.Count;
            await f.SendAsync(Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            Assert.Same(committed, f.Current());
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(expected);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
    }
}
