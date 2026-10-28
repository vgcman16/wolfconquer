using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task GenuineNativeAddStaysUnsavedUntilActualV8SaveAndDoesNotRevertMemory()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var storage = RequireCoherent(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var anchor = binding.SavedSpells;
            byte[] saved = File.ReadAllBytes(f.SpellPath), character = File.ReadAllBytes(f.CharacterPath);
            Assert.True(anchor.Matches(saved));
            var exposedCopy = Assert.IsType<byte[]>(anchor.Copy()); exposedCopy[0] ^= 0x7f;
            Assert.True(anchor.Matches(saved)); // Reading evidence cannot mutate its private bytes.
            await NativePacketTestThread.Run(() =>
            {
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                storage.Add(packet, 6002, 6, 2, 3, 444);
                var unsaved = f.Current;
                Assert.Equal(444, Assert.Single(unsaved.PlayerSpells.Spells, row => row.Id == 6002).Experience);
                Assert.Equal(saved, File.ReadAllBytes(f.SpellPath)); Assert.Same(anchor, binding.SavedSpells);
                RequireCoherent(f); int sends = f.Sent.Count;
                LegacyNativeServerDatabasePersistence.SaveClientSpells(f.Native);
                Assert.Same(unsaved, f.Current); Assert.Same(storage, f.Native.MySpells);
                Assert.NotSame(anchor, binding.SavedSpells); Assert.True(binding.SavedSpells.Matches(File.ReadAllBytes(f.SpellPath)));
                Assert.Equal(sends, f.Sent.Count); Assert.Equal(character, File.ReadAllBytes(f.CharacterPath));
                RequireSavedMembership(f.Current, File.ReadAllBytes(f.SpellPath));
                RequireSavedOpaqueByte(File.ReadAllBytes(f.SpellPath), 6001, 0xA5);
                Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users)); RequireCoherent(f);
            });
        });
    }

    [Fact]
    public async Task GenuineNativeXpCanRemainAheadOfSavedAnchorThenPersistWithoutReversion()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); RequireCoherent(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var anchor = binding.SavedSpells; byte[] saved = File.ReadAllBytes(f.SpellPath);
            byte[] character = File.ReadAllBytes(f.CharacterPath);
            await NativePacketTestThread.Run(() =>
            {
                var equipment = f.Native.Equipment; uint rate = LegacyNativeServerConfig.ExpRateSpell;
                try
                {
                    f.Native.Equipment = new LegacyNativeEquipItemStorage(f.Native);
                    LegacyNativeServerConfig.ExpRateSpell = 1;
                    var metadata = Enumerable.Range(0, 6).ToDictionary(i => (ushort)i,
                        i => new LegacyNativeMagicType.Magic { ID = 6001, Level = (byte)i, Experience = 10000 });
                    var attack = new LegacyNativeInteractQuery { UID = f.Player.UID, OpponentUID = f.Player.UID, SpellID = 6001, AtkType = 24 };
                    using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                    LegacyNativeUpdateSpell.CheckUpdate(packet, f.Native, attack, 600, metadata);
                    var unsaved = f.Current;
                    Assert.Equal(1711, Assert.Single(unsaved.PlayerSpells.Spells, row => row.Id == 6001).Experience);
                    Assert.Equal(saved, File.ReadAllBytes(f.SpellPath)); Assert.Same(anchor, binding.SavedSpells);
                    LegacyNativeServerDatabasePersistence.SaveClientSpells(f.Native);
                    Assert.Same(unsaved, f.Current); RequireCoherent(f);
                    Assert.Equal(1711, Assert.Single(PlayerSpellSnapshotLoader.Parse(File.ReadAllBytes(f.SpellPath)).Spells, row => row.Id == 6001).Experience);
                    Assert.NotSame(anchor, binding.SavedSpells); Assert.True(binding.SavedSpells.Matches(File.ReadAllBytes(f.SpellPath)));
                    Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); RequireSavedOpaqueByte(File.ReadAllBytes(f.SpellPath), 6001, 0xA5);
                    Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
                }
                finally { f.Native.Equipment = equipment; LegacyNativeServerConfig.ExpRateSpell = rate; }
            });
        }, [new(6001, 4, 1111, 0, 3, 2)], lineage: false);
    }

    [Fact]
    public async Task GenuineNativeSaveRefusesExternalDiskDriftWithoutBlessingItAsNewEvidence()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var storage = RequireCoherent(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var anchor = binding.SavedSpells; byte[] saved = File.ReadAllBytes(f.SpellPath);
            await NativePacketTestThread.Run(() =>
            {
                using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                storage.Add(packet, 6002, 2);
                var unsaved = f.Current; var map = storage.ClientSpells; var rows = map.ToArray(); int sends = f.Sent.Count;
                var foreign = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(
                    PlayerSpellSnapshotLoader.Parse(saved).Spells.Select(row => row.Id == 6001 ? row with { Experience = 9000 } : row).ToArray()));
                File.WriteAllBytes(f.SpellPath, foreign); // Explicit external edit in this owned fixture only.
                Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.SaveClientSpells(f.Native));
                Assert.Equal(foreign, File.ReadAllBytes(f.SpellPath)); Assert.Same(anchor, binding.SavedSpells);
                Assert.False(anchor.Matches(foreign)); Assert.True(anchor.Matches(saved));
                Assert.Same(unsaved, f.Current); Assert.Same(map, storage.ClientSpells); Assert.Equal(rows, map.ToArray());
                Assert.Equal(sends, f.Sent.Count); Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Users));
                // Explicitly restore the known saved evidence, then exercise the real saver again.
                File.WriteAllBytes(f.SpellPath, saved);
                LegacyNativeServerDatabasePersistence.SaveClientSpells(f.Native);
                Assert.Same(unsaved, f.Current); Assert.NotSame(anchor, binding.SavedSpells);
                RequireSavedMembership(unsaved, File.ReadAllBytes(f.SpellPath)); RequireCoherent(f);
            });
        });
    }

    private static void RequireSavedMembership(WorldSession session, byte[] bytes) =>
        Assert.Equal(session.PlayerSpells.Spells.OrderBy(row => row.Id), PlayerSpellSnapshotLoader.Parse(bytes).Spells.OrderBy(row => row.Id));

    private static void RequireSavedOpaqueByte(byte[] bytes, ushort id, byte expected)
    {
        var rows = PlayerSpellSnapshotLoader.Parse(bytes).Spells;
        var index = Enumerable.Range(0, rows.Count).Single(index => rows[index].Id == id);
        Assert.Equal(expected, bytes[sizeof(int) + index * PlayerSpellSnapshotLoader.LegacySpellSize + 11]);
    }
}
