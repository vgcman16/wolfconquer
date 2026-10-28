using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinaryNativeSpellCoherenceTests
{
    [Fact]
    public async Task BoundPublicNativeLoadMergesRealAnchoredSavedRowsWithoutRevertingUnsavedNativeSelection()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var storage = RequireCoherent(f);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var priorMagic = LegacyNativePoolMagic.Magic;
            try
            {
                LegacyNativePoolMagic.Magic = new LegacyNativeMagicType(); // Genuine empty metadata: opaque IDs are membership only.
                await NativePacketTestThread.Run(() =>
                {
                    using var packet = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
                    storage.Add(packet, 6002, 2, 3, 1, 22);
                    LegacyNativeServerDatabasePersistence.SaveClientSpells(f.Native);
                    byte[] anchored = File.ReadAllBytes(f.SpellPath); var anchor = binding.SavedSpells;
                    Assert.True(anchor.Matches(anchored));
                    packet.SpellCreate(new LegacyNativeMsgSpell { ID = 6001, UseSoul = LegacyNativeMsgSpell.UseSpellSoulTyp.Three });
                    LegacyNativeMsgSpell.HandlerSpell(f.Native, packet);
                    storage.Remove(6002, packet);
                    var unsaved = f.Current;
                    Assert.Equal(3, Assert.Single(unsaved.PlayerSpells.Spells, row => row.Id == 6001).UseSpellSoul);
                    Assert.DoesNotContain(unsaved.PlayerSpells.Spells, row => row.Id == 6002);
                    var existing = Assert.Single(storage.ClientSpells.ToArray(), pair => pair.Key == 6001).Value;
                    int sends = f.Sent.Count;
                    LegacyNativeServerDatabasePersistence.LoadClientSpells(f.Native);
                    Assert.NotSame(unsaved, f.Current); Assert.Same(anchor, binding.SavedSpells);
                    Assert.Equal(anchored, File.ReadAllBytes(f.SpellPath)); Assert.Equal(sends, f.Sent.Count);
                    Assert.Equal(3, Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6001).UseSpellSoul);
                    Assert.Equal(1111, Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6001).Experience);
                    var added = Assert.Single(f.Current.PlayerSpells.Spells, row => row.Id == 6002);
                    Assert.Equal(22, added.Experience); Assert.Equal(3, added.SoulLevel); Assert.Equal(1, added.PreviousLevel);
                    Assert.Same(existing, Assert.Single(RequireCoherent(f).ClientSpells.ToArray(), pair => pair.Key == 6001).Value);
                });
            }
            finally { LegacyNativePoolMagic.Magic = priorMagic; }
        });
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("unrepresentable-soul")]
    public async Task BoundPublicNativeLoadRefusesExternalPinnedImageChangesBeforeAnySavedAnchorAdoption(string fault)
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f); var map = storage.ClientSpells;
            var rows = map.ToArray();
            var anchor = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding).SavedSpells;
            byte[] original = File.ReadAllBytes(f.SpellPath);
            var image = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot([new(6002, 2, 22, 1, 3, 3)]));
            if (fault == "truncated") image = image[..^1]; else image[14] = 5;
            File.WriteAllBytes(f.SpellPath, image); var files = f.Files(); int sends = f.Sent.Count;
            Assert.Throws<InvalidOperationException>(() => LegacyNativeServerDatabasePersistence.LoadClientSpells(f.Native)); // Bound rooted reads reject changed saved evidence before parsing or merging.
            Assert.Same(current, f.Current); Assert.Same(map, storage.ClientSpells); Assert.Equal(rows, map.ToArray());
            f.RequireFiles(files); Assert.Equal(sends, f.Sent.Count);
            Assert.Same(anchor, f.Native.OrdinarySpellBinding!.SavedSpells); Assert.True(anchor.Matches(original));
            Assert.False(anchor.Matches(image)); Assert.False(f.Recovery.IsBlocked);
        });
    }

    [Fact]
    public async Task BoundOpenedLoadAndSaveCannotClaimEvenTheGuessedPinnedFilename()
    {
        await RunAsync(async f =>
        {
            await f.LoginAsync(); var current = f.Current; var storage = RequireCoherent(f); var rows = storage.ClientSpells.ToArray();
            var files = f.Files(); int sent = f.Sent.Count;
            foreach (bool load in new[] { true, false })
            {
                var file = new LegacyNativeItemRecordFile(); Assert.True(file.Open(f.SpellPath, FileMode.Open));
                try
                {
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        if (load) LegacyNativeServerDatabasePersistence.LoadClientSpellsOpenedFile(file, f.Native);
                        else LegacyNativeServerDatabasePersistence.SaveClientSpellsOpenedFile(file, f.Native);
                    });
                }
                finally { if (file.Success) file.Close(); }
            }
            Assert.Same(current, f.Current); Assert.Equal(rows, storage.ClientSpells.ToArray());
            f.RequireFiles(files); Assert.Equal(sent, f.Sent.Count);
            byte[] original = File.ReadAllBytes(f.SpellPath); Assert.Equal(0xA5, original[15]);
            LegacyNativeServerDatabasePersistence.SaveClientSpells(f.Native);
            Assert.Equal(original, File.ReadAllBytes(f.SpellPath)); Assert.Same(current, f.Current); RequireCoherent(f);
        });
    }
}
