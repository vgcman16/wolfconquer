using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task NativeTrainingPublishesSpellAndBothGenuineAdmittedProficienciesTogether()
    {
        var right = new PlayerItemSnapshotItem { Uid = 900410, ItemId = 410009,
            Position = 4, Durability = 1, MaximumDurability = 1, StackSize = 1 };
        var left = new PlayerItemSnapshotItem { Uid = 900420, ItemId = 420009,
            Position = 5, Durability = 1, MaximumDurability = 1, StackSize = 1 };
        var items = PlayerItemSnapshot.Empty with { EquipmentItems = new[] { right, left } };
        await WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; RequireCoherent(f);
            var profs = Assert.IsType<LegacyNativeProficiencyStorage>(f.Native.MyProfs);
            var equipment = Assert.IsType<LegacyNativeEquipItemStorage>(f.Native.Equipment);
            var map = equipment.ClientItems; var nativeRight = map[right.Uid]; var nativeLeft = map[left.Uid];
            Assert.Equal(right.ItemId, nativeRight.ITEM_ID); Assert.Equal(right.Position, nativeRight.Position);
            Assert.Equal(left.ItemId, nativeLeft.ITEM_ID); Assert.Equal(left.Position, nativeLeft.Position);
            var savedItems = File.ReadAllBytes(Path.Combine(f.Root, "PlayersItems", f.Current.Character.Uid + ".bin"));
            Assert.Equal(1000U, Assert.Single(before.PlayerProficiencies.Proficiencies, p => p.Id == 410).Experience);
            Assert.Equal(2000U, Assert.Single(before.PlayerProficiencies.Proficiencies, p => p.Id == 420).Experience);
            Assert.Equal(1000U, profs.ClientProf[410].Experience); Assert.Equal(2000U, profs.ClientProf[420].Experience);
            await NativePacketTestThread.Run(() =>
            {
                uint oldSpellRate = LegacyNativeServerConfig.ExpRateSpell;
                uint oldProfRate = LegacyNativeServerConfig.ExpRateProf;
                try
                {
                    LegacyNativeServerConfig.ExpRateSpell = 1; LegacyNativeServerConfig.ExpRateProf = 1;
                    var metadata = Enumerable.Range(0, 6).ToDictionary(i => (ushort)i,
                        i => new LegacyNativeMagicType.Magic { ID = 6001, Level = (byte)i, Experience = 10000 });
                    var attack = new LegacyNativeInteractQuery { UID = f.Player.UID, OpponentUID = f.Player.UID, SpellID = 6001, AtkType = 24 };
                    using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                    int first = f.Sent.Count;
                    LegacyNativeUpdateSpell.CheckUpdate(packet, f.Native, attack, 600, metadata);
                    AssertTrainingWire(f.Sent.Skip(first).ToArray(), weapons: true);
                    var after = f.Current; Assert.NotSame(before, after);
                    Assert.Equal(1711, Assert.Single(after.PlayerSpells.Spells, s => s.Id == 6001).Experience);
                    Assert.Equal(1200U, Assert.Single(after.PlayerProficiencies.Proficiencies, p => p.Id == 410).Experience);
                    Assert.Equal(2200U, Assert.Single(after.PlayerProficiencies.Proficiencies, p => p.Id == 420).Experience);
                    Assert.Equal(1200U, profs.ClientProf[410].Experience); Assert.Equal(2200U, profs.ClientProf[420].Experience);
                    Assert.Same(equipment, f.Native.Equipment); Assert.Same(map, equipment.ClientItems);
                    Assert.Same(nativeRight, map[right.Uid]); Assert.Same(nativeLeft, map[left.Uid]);
                    Assert.Same(before.PlayerItems, after.PlayerItems);
                    Assert.Equal(savedItems, File.ReadAllBytes(Path.Combine(f.Root, "PlayersItems", after.Character.Uid + ".bin")));
                    RequireCoherent(f);
                }
                finally { LegacyNativeServerConfig.ExpRateSpell = oldSpellRate; LegacyNativeServerConfig.ExpRateProf = oldProfRate; }
            });
        }, [new(6001, 4, 1111, 0, 3, 2)], lineage: false, training: true, playerItems: items);
    }
}
