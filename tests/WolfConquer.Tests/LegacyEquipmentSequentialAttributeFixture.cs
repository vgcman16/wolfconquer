using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // This is sequential 2203 then Equip, not proof of a legal mid-send interleave.
    private static Task OwnedSequentialAttributeEquipmentAsync()
    {
        const uint actor = OwnedEquipmentBehaviorFixture.Actor, triggerUid = 99_109, triggerType = 130_009;
        ItemType Item(uint id, uint mana, uint hp = 0)
        {
            var fields = Enumerable.Repeat("0", 60).ToArray();
            fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedNativeCache" + id;
            fields[18] = hp.ToString(CultureInfo.InvariantCulture); fields[19] = mana.ToString(CultureInfo.InvariantCulture);
            return new ItemType(checked((int)id), fields[1], fields);
        }
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [MagicTypeParser.ParseLine("20002@@20000@@16@@Venom@@1@@0@@1@@8@@1@@0@@30150@@0@@350@@2@@16@@16@@355@@0@@0@@0@@1@@0@@0@@3@@16@@0@@0@@0@@0@@0@@1@@0@@0@@800@@0@@1000@@1@@20@@0@@0@@0@@0@@0@@0@@30100@@0@@0@@800@@1000@@0@@0@@0@@0@@")],
            items: [Item(111_095, 10, 5_000), Item(110_099, 20), Item(410_095, 30),
                Item(150_095, 40), Item(120_095, 50), Item(triggerType, 0)]);
        var equipped = new PlayerItemSnapshotItem[]
        {
            new() { Uid = 99_101, ItemId = 111_095, Position = 1, Durability = 100, MaximumDurability = 100,
                PurificationItemId = 110_099, PurificationDuration = 0 },
            new() { Uid = 99_102, ItemId = 120_095, Position = 2, Durability = 100, MaximumDurability = 100 },
            new() { Uid = 99_106, ItemId = 150_095, Position = 6, Durability = 100, MaximumDurability = 100 },
            new() { Uid = 99_104, ItemId = 410_095, Position = 4, Durability = 100, MaximumDurability = 100 },
        };
        var trigger = new PlayerItemSnapshotItem { Uid = triggerUid, ItemId = triggerType, Position = 0,
            Durability = 100, MaximumDurability = 100, MythSoul = 11, StackSize = 1 };
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14_005, agility: 0,
            strength: 0, mapId: 1000, minHitPoints: 5000) with
            { Vitality = 0, Spirit = 10, AttributePoints = 12, MinMana = 1000 };
        var input = new OwnedEquipmentBehaviorInput(character, new([trigger], [], [], equipped, []));
        return OwnedEquipmentBehaviorFixture.RunAsync(input, world, async f =>
        {
            var initial = f.Current(); var binding = initial.NativeGameClient!.OrdinarySpellBinding!;
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(initial.PlayerSpells.ContainsSpell(20000));
            var before = f.Data.Files(); var expected = before.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            string characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string text = CharacterSnapshotFileEncoding.Decode(expected[characterKey], out var encoding);
            Assert.Contains("\r\nSpirit=10\r\n", text); Assert.Contains("\r\nAtributes=12\r\n", text);
            text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal);
            expected[characterKey] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            f.ActorFrames.Clear();
            await f.SendAsync(actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            f.Data.AssertFiles(expected);
            Assert.Equal((ushort)20, binding.Player.Spirit); Assert.Equal((ushort)2, binding.Player.Atributes);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(new ushort[] { LegacyAttributeSetPacket.Opcode, LegacyPlayerStatusPacket.Opcode,
                LegacyMsgUpdatePacket.Opcode, LegacyMsgUpdatePacket.Opcode, LegacyMsgUpdatePacket.Opcode,
                LegacyMsgUpdatePacket.Opcode, LegacyUserTotalRefineLevPacket.Opcode },
                f.ActorFrames.Select(bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode));

            Dictionary<string, byte[]>? atEquip = null; Dictionary<string, byte[]>? afterGrant = null;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != actor) return;
                if (atEquip is null)
                {
                    var current = f.Current(); Assert.False(current.PlayerSpells.ContainsSpell(20000));
                    Assert.Equal(triggerUid, Assert.Single(current.PlayerItems.EquipmentItems, row => row.Uid == triggerUid).Uid);
                    atEquip = f.Data.Files();
                    string itemKey = Path.GetRelativePath(f.Data.Root, f.ItemPath());
                    foreach (var row in expected)
                        if (!string.Equals(row.Key, itemKey, StringComparison.OrdinalIgnoreCase)) Assert.Equal(row.Value, atEquip[row.Key]);
                    afterGrant = atEquip.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                    string currentText = CharacterSnapshotFileEncoding.Decode(afterGrant[characterKey], out var savedEncoding);
                    Assert.DoesNotContain("ManagedDerivedSpellOrigins=", currentText, StringComparison.OrdinalIgnoreCase);
                    Assert.EndsWith("\r\n", currentText);
                    afterGrant[characterKey] = savedEncoding.GetPreamble().Concat(savedEncoding.GetBytes(currentText + "ManagedDerivedSpellOrigins=20000:1\r\n")).ToArray();
                    string spellKey = Path.GetRelativePath(f.Data.Root, Path.Combine(f.Data.Spells, actor + ".bin"));
                    var priorSpells = afterGrant[spellKey]; int count = BinaryPrimitives.ReadInt32LittleEndian(priorSpells);
                    Assert.True(count >= 0); Assert.Equal(4 + count * 12, priorSpells.Length);
                    var spellImage = new byte[priorSpells.Length + 12]; priorSpells.CopyTo(spellImage, 0);
                    BinaryPrimitives.WriteInt32LittleEndian(spellImage, count + 1);
                    BinaryPrimitives.WriteUInt16LittleEndian(spellImage.AsSpan(priorSpells.Length), 20000);
                    BinaryPrimitives.WriteUInt16LittleEndian(spellImage.AsSpan(priorSpells.Length + 2), 1); afterGrant[spellKey] = spellImage;
                }
                if (!LegacySpellPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var spell) || spell.Id != 20000) return;
                f.Data.AssertFiles(afterGrant!); Assert.Equal((ushort)20, binding.Player.Spirit);
                Assert.Equal(150U, LegacyPhysicalStatusOverlay.ForOwner(f.Current()).MaxMana);
                Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(f.Current(), out var prefixMaximum));
                Assert.Equal(5000U, prefixMaximum);
            };
            try { await f.DispatchAsync(new MsgItemUsagePacket(triggerUid, 3, MsgItemUsageAction.Equip, 0, 0, 0, 0)); }
            finally { f.BeforeSend = null; }
            Assert.NotNull(atEquip); Assert.NotNull(afterGrant); f.Data.AssertFiles(afterGrant!);
            var final = f.Current(); Assert.Equal(20L, final.Character.Spirit); Assert.Equal(2L, final.Character.AttributePoints);
            Assert.Equal(5000L, final.Character.MinHitPoints); Assert.Equal(5000, binding.Player.HitPoints);
            Assert.Equal(750U, LegacyPhysicalStatusOverlay.ForOwner(final).MaxMana);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(final, out var maximum)); Assert.Equal(5060U, maximum);
            Assert.Equal((ushort)1, binding.Storage.ClientSpells[20000].Level); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var frames = f.ActorFrames.Select(OwnedEquipmentBehaviorFixture.ReadFrame).ToArray();
            int grant = Array.FindIndex(frames, frame => LegacySpellPacket.TryParse(frame, out var spell) && spell.Id == 20000);
            int status = Array.FindIndex(frames, frame => frame.Header.Opcode == LegacyPlayerStatusPacket.Opcode);
            Assert.True(grant >= 7);
            status = Array.FindIndex(frames, grant + 1, frame => frame.Header.Opcode == LegacyPlayerStatusPacket.Opcode);
            Assert.True(status > grant);
            Assert.True(LegacyPlayerStatusPacket.TryParse(frames[status], out var published));
            Assert.Equal(5060U, published.MaxHP); Assert.Equal(750U, published.MaxMana);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }
}
