using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData("experience")]
    [InlineData("level")]
    [InlineData("drop-origin")]
    [InlineData("drop-row")]
    public Task GenericStoreCasCannotAlterGenuinelyQueryOwnedSpell(string attempt) => RunPhysicalDeathAsync(async f =>
    {
        await EquipForDerivedQueryAsync(f);
        var current = f.TargetSession; var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        Assert.Equal("20000:5", current.Character.Attributes["ManagedDerivedSpellOrigins"]);
        Assert.Equal(new PlayerSpellSnapshotEntry(20000, 5, 0, 0, 0, 0),
            Assert.Single(current.PlayerSpells.Spells, row => row.Id == 20000));
        var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase);
        if (attempt == "drop-origin") Assert.True(attributes.Remove("ManagedDerivedSpellOrigins"));
        var rows = current.PlayerSpells.Spells.Where(row => attempt != "drop-row" || row.Id != 20000)
            .Select(row => row.Id != 20000 ? row : attempt switch
            {
                "experience" => row with { Experience = 1 },
                "level" => row with { Level = 6 },
                _ => row
            }).ToArray();
        var proposal = current with { Character = current.Character with { Attributes = attributes }, PlayerSpells = new(rows) };
        var files = f.Owner.Data.Files(); var history = HistoryFiles(f.Owner);
        var ledger = File.ReadAllBytes(f.Owner.Data.State); var newborn = File.ReadAllBytes(f.Owner.CharacterPath);
        var map = binding.Storage.ClientSpells; var native = map[20000];
        var nativeValues = (native.Level, native.Experience, native.PreviousLevel, native.SoulLevel, native.UseSpellSoul);
        var resources = (binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina,
            current.Character.Money, current.Character.MinMana, current.Character.MinHitPoints);
        int targetFrames = f.Owner.Frames(DeathTargetAccount).Count, actorFrames = f.Owner.Frames(CreationFixture.RuneActor).Count;
        int newbornFrames = f.Owner.Sent.Count;
        var error = Assert.Throws<InvalidDataException>(() => f.Owner.Store.TryCompareAndSet(f.Target, current, proposal));
        Assert.Equal("derivedSpellOriginOrReconciliationInvalid", error.Message);
        Assert.Same(current, f.TargetSession); Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Same(map, binding.Storage.ClientSpells); Assert.Same(native, map[20000]);
        Assert.Equal(nativeValues, (native.Level, native.Experience, native.PreviousLevel, native.SoulLevel, native.UseSpellSoul));
        Assert.Equal(resources, (binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina,
            f.TargetSession.Character.Money, f.TargetSession.Character.MinMana, f.TargetSession.Character.MinHitPoints));
        f.Owner.Data.AssertFiles(files); AssertHistoricalFiles(history, HistoryFiles(f.Owner));
        Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State)); Assert.Equal(newborn, File.ReadAllBytes(f.Owner.CharacterPath));
        Assert.Equal(targetFrames, f.Owner.Frames(DeathTargetAccount).Count);
        Assert.Equal(actorFrames, f.Owner.Frames(CreationFixture.RuneActor).Count); Assert.Equal(newbornFrames, f.Owner.Sent.Count);
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users)); Assert.False(f.Owner.Runtime.Recovery.IsBlocked);
    });

    [Fact]
    public Task GenericStoreCasStillAllowsUnmarkedProgressionBesideRealQueryGrant() => RunPhysicalDeathAsync(async f =>
    {
        await EquipForDerivedQueryAsync(f);
        var current = f.TargetSession; var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        // 6001 is an existing unmarked row declared before Bootstrap by the shared fixture.
        var rows = current.PlayerSpells.Spells.Select(row => row.Id == 6001 ? row with { Experience = 2222 } : row).ToArray();
        var files = f.Owner.Data.Files(); int frames = f.Owner.Frames(DeathTargetAccount).Count;
        var history = HistoryFiles(f.Owner); var ledger = File.ReadAllBytes(f.Owner.Data.State);
        var expected = files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        string spellKey = Path.GetRelativePath(f.Owner.Data.Root, f.SpellsPath);
        Assert.Equal((byte)0xC7, expected[spellKey][15]);
        Assert.Equal(1234, BinaryPrimitives.ReadInt32LittleEndian(expected[spellKey].AsSpan(8)));
        BinaryPrimitives.WriteInt32LittleEndian(expected[spellKey].AsSpan(8), 2222);
        Assert.True(f.Owner.Store.TryCompareAndSet(f.Target, current, current with { PlayerSpells = new PlayerSpellSnapshot(rows) }));
        var updated = f.TargetSession;
        Assert.Equal(2222, Assert.Single(updated.PlayerSpells.Spells, row => row.Id == 6001).Experience);
        Assert.Equal(2222, binding.Storage.ClientSpells[6001].Experience);
        Assert.Equal(new PlayerSpellSnapshotEntry(20000, 5, 0, 0, 0, 0), Assert.Single(updated.PlayerSpells.Spells, row => row.Id == 20000));
        Assert.Equal("20000:5", updated.Character.Attributes["ManagedDerivedSpellOrigins"]);
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding)); f.Owner.Data.AssertFiles(expected);
        AssertHistoricalFiles(history, HistoryFiles(f.Owner)); Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
        Assert.Equal(frames, f.Owner.Frames(DeathTargetAccount).Count); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        // CAS persists this ordinary progression; no cast is claimed.
    });
}
