using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeSpellJoinFixture;
using Item = WolfConquer.Core.Data.PlayerItemSnapshotItem;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    private static Item TrainingWeapon(uint uid, uint id, ushort position) => new()
        { Uid = uid, ItemId = id, Position = position, Durability = 1, MaximumDurability = 1, StackSize = 1 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JoinedTrainingSelectsActualAlternateWeaponsInsteadOfMismatchedOrAbsentMainSlots(bool mainPresent)
    {
        var rows = new List<Item>
        {
            TrainingWeapon(900454, 410009, 54), TrainingWeapon(900455, 420009, 55),
        };
        if (mainPresent)
        {
            rows.Add(TrainingWeapon(900404, 440009, 4));
            rows.Add(TrainingWeapon(900405, 450009, 5));
        }
        var items = PlayerItemSnapshot.Empty with { EquipmentItems = rows.ToArray() };
        await WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(async f =>
        {
            await f.LoginAsync(); RequireCoherent(f);
            f.Store.Set(f.Connection, f.Current with { AlternateEquipmentActive = true }, out var selected);
            Assert.Same(selected, f.Current); Assert.True(f.Current.AlternateEquipmentActive);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var equipment = f.Native.Equipment!; var map = equipment.ClientItems; var nativeRows = map.ToArray();
            var files = f.Files(); int first = f.Sent.Count;
            f.ObserveSend = (bytes, _) =>
            {
                AssertTrainingOwner(f, binding);
                Assert.Equal(1200U, Assert.Single(f.Current.PlayerProficiencies.Proficiencies, row => row.Id == 410).Experience);
                Assert.Equal(2200U, Assert.Single(f.Current.PlayerProficiencies.Proficiencies, row => row.Id == 420).Experience);
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.False(Monitor.IsEntered(f.Native.SpellAdmissionSyncRoot));
                Assert.False(Monitor.IsEntered(f.Player.SpellAdmissionSyncRoot));
                f.Sent.Add(bytes); return ValueTask.CompletedTask;
            };

            await NativePacketTestThread.Run(() => ApplyActualTraining(f)); f.ObserveSend = null;

            AssertTrainingWire(f.Sent.Skip(first).ToArray(), weapons: true);
            AssertTrainingOwner(f, binding); Assert.Same(equipment, f.Native.Equipment); Assert.Same(map, equipment.ClientItems);
            foreach (var row in nativeRows) Assert.Same(row.Value, map[row.Key]);
            Assert.Equal(1711, Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6001).Experience);
            Assert.DoesNotContain(f.Current.PlayerProficiencies.Proficiencies, row => row.Id is 440 or 450);
            Assert.Equal(2, f.Current.PlayerProficiencies.Proficiencies.Count);
            Assert.Same(selected.PlayerItems, f.Current.PlayerItems); f.RequireFiles(files);
        }, [new(6001, 4, 1111, 0, 3, 2)], lineage: false, training: true, playerItems: items);
    }

    [Fact]
    public async Task JoinedTrainingWithNoActualWeaponsPublishesOnlySpellProgressAndNoProficiencyGrant()
    {
        await WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(async f =>
        {
            await f.LoginAsync(); var before = f.Current; var files = f.Files(); int first = f.Sent.Count;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var equipment = f.Native.Equipment!; Assert.Empty(equipment.ClientItems);
            var profBytes = PlayerProficiencySnapshotLoader.ToLegacyBytes(before.PlayerProficiencies);

            await NativePacketTestThread.Run(() => ApplyActualTraining(f));

            AssertTrainingWire(f.Sent.Skip(first).ToArray(), weapons: false); AssertTrainingOwner(f, binding);
            Assert.Equal(1711, Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6001).Experience);
            Assert.Equal(profBytes, PlayerProficiencySnapshotLoader.ToLegacyBytes(f.Current.PlayerProficiencies));
            Assert.Equal(1000U, f.Native.MyProfs!.ClientProf[410].Experience);
            Assert.Equal(2000U, f.Native.MyProfs.ClientProf[420].Experience);
            Assert.Same(equipment, f.Native.Equipment); Assert.Empty(equipment.ClientItems); f.RequireFiles(files);
        }, [new(6001, 4, 1111, 0, 3, 2)], lineage: false, training: true, playerItems: PlayerItemSnapshot.Empty);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AmbiguousActualActiveWeaponSlotRefusesWithoutPartialProgressOrOutput(bool alternate)
    {
        ushort position = alternate ? (ushort)54 : (ushort)4;
        var items = PlayerItemSnapshot.Empty with { EquipmentItems = new[]
        {
            TrainingWeapon(900411, 410009, position), TrainingWeapon(900412, 420009, position),
        } };
        await WorldServerFrameHandlerTests.WithNativeSpellJoinAsync(async f =>
        {
            await f.LoginAsync(); RequireCoherent(f);
            f.Store.Set(f.Connection, f.Current with { AlternateEquipmentActive = alternate }, out var before);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var equipment = f.Native.Equipment!; var map = equipment.ClientItems; var nativeRows = map.ToArray();
            var spells = f.Native.MySpells!.ClientSpells; var spellRows = spells.ToArray();
            var profs = f.Native.MyProfs!.ClientProf; var profRows = profs.ToArray();
            var files = f.Files(); int first = f.Sent.Count;

            var error = await Record.ExceptionAsync(async () => await NativePacketTestThread.Run(() => ApplyActualTraining(f)));

            Assert.IsType<InvalidOperationException>(error);
            Assert.Same(before, f.Current); AssertTrainingOwner(f, binding);
            Assert.Same(equipment, f.Native.Equipment); Assert.Same(map, equipment.ClientItems);
            foreach (var row in nativeRows) Assert.Same(row.Value, map[row.Key]);
            Assert.Same(spells, f.Native.MySpells.ClientSpells); Assert.Equal(spellRows, spells.ToArray());
            Assert.Same(profs, f.Native.MyProfs.ClientProf); Assert.Equal(profRows, profs.ToArray());
            Assert.Equal(1111, Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6001).Experience);
            Assert.Equal(1000U, profs[410].Experience); Assert.Equal(2000U, profs[420].Experience);
            Assert.Equal(first, f.Sent.Count); f.RequireFiles(files);
            Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
        }, [new(6001, 4, 1111, 0, 3, 2)], lineage: false, training: true, playerItems: items);
    }

    private static void ApplyActualTraining(Fixture f)
    {
        uint spellRate = LegacyNativeServerConfig.ExpRateSpell, profRate = LegacyNativeServerConfig.ExpRateProf;
        try
        {
            LegacyNativeServerConfig.ExpRateSpell = 1; LegacyNativeServerConfig.ExpRateProf = 1;
            var metadata = Enumerable.Range(0, 6).ToDictionary(index => (ushort)index,
                index => new LegacyNativeMagicType.Magic { ID = 6001, Level = (byte)index, Experience = 10000 });
            var attack = new LegacyNativeInteractQuery
                { UID = f.Player.UID, OpponentUID = f.Player.UID, SpellID = 6001, AtkType = 24 };
            using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
            LegacyNativeUpdateSpell.CheckUpdate(packet, f.Native, attack, 600, metadata);
        }
        finally { LegacyNativeServerConfig.ExpRateSpell = spellRate; LegacyNativeServerConfig.ExpRateProf = profRate; }
    }

    private static void AssertTrainingOwner(Fixture f, LegacyOrdinarySpellBinding binding)
    {
        RequireCoherent(f); Assert.Same(binding, f.Native.OrdinarySpellBinding);
        Assert.Same(f.Current.PlayerItems, binding.ItemSnapshot);
        Assert.Same(f.Current.PlayerItems, binding.Items.Image.FinalSnapshot);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
    }

    private static void AssertTrainingWire(byte[][] frames, bool weapons)
    {
        Assert.Equal(weapons ? new ushort[] { 2199, 2199, 2101, 2199, 2199 } : new ushort[] { 2101 },
            frames.Select(bytes => ReadFrame(bytes).Header.Opcode).ToArray());
        int spellIndex = weapons ? 2 : 0;
        Assert.True(LegacySpellPacket.TryParse(ReadFrame(frames[spellIndex]), out var spell));
        Assert.Equal((ushort)6001, spell.Id); Assert.Equal((ushort)4, spell.Level); Assert.Equal(1711, spell.Experience);
        if (!weapons) return;
        Experience(frames[0], 410, 1100); Experience(frames[1], 420, 2100);
        Experience(frames[3], 410, 1200); Experience(frames[4], 420, 2200);
        static void Experience(byte[] frame, uint id, uint xp)
        {
            var payload = new byte[20];
            BinaryPrimitives.WriteUInt32LittleEndian(payload, xp);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), 1_600_000);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), id);
            Assert.Equal(payload, frame.AsSpan(4, 20).ToArray());
        }
    }
}
