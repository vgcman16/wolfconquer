using System.Buffers.Binary;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    internal static void AssertPhysicalDeathRecord(PhysicalDeathFixture f, PhysicalDeathBefore before, JsonNode record)
    {
        Assert.Equal(22, record["Version"]!.GetValue<int>());
        var operation = record["PhysicalDeath"]!.AsObject();
        Assert.Equal(new[] { "Alternate", "DerivedSpells", "GrantedRows", "ItemsBefore", "QueryAdmitted", "RankingRows", "TargetUid" },
            operation.Select(pair => pair.Key).Order(StringComparer.Ordinal));
        Assert.Equal(DeathTargetUid, operation["TargetUid"]!.GetValue<uint>());
        Assert.True(operation["QueryAdmitted"]!.GetValue<bool>()); Assert.False(operation["Alternate"]!.GetValue<bool>());
        Assert.Empty(operation["RankingRows"]!.AsArray());
        Assert.Equal(before.Files[Path.GetRelativePath(f.Owner.Data.Root, f.ItemsPath)],
            Convert.FromBase64String(operation["ItemsBefore"]!.GetValue<string>()));
        var derived = operation["DerivedSpells"]!.AsArray(); var granted = operation["GrantedRows"]!.AsArray();
        Assert.Equal(f.Changed ? 1 : 0, derived.Count); Assert.Equal(derived.Count, granted.Count);
        if (f.Changed)
        {
            Assert.Equal((ushort)20000, derived[0]!["Id"]!.GetValue<ushort>());
            var row = derived[0]!["After"]!;
            Assert.Equal((ushort)20000, row["Id"]!.GetValue<ushort>()); Assert.Equal((ushort)5, row["Level"]!.GetValue<ushort>());
            Assert.Equal(0, row["Experience"]!.GetValue<int>());
            foreach (string key in new[] { "PreviousLevel", "SoulLevel", "UseSpellSoul" }) Assert.Equal((byte)0, row[key]!.GetValue<byte>());
            var actual = Assert.Single(f.Owner.Catalog.MagicByTypeId[20000]);
            Assert.Equal(actual.Id, granted[0]!["Id"]!.GetValue<int>()); Assert.Equal(20000, granted[0]!["TypeId"]!.GetValue<int>());
            Assert.Equal(actual.Name, granted[0]!["Name"]!.GetValue<string>());
            Assert.Equal(actual.Fields, granted[0]!["Fields"]!.AsArray().Select(field => field!.GetValue<string>()).ToArray());
        }
        var targets = new[] { f.CharacterPath, f.SpellsPath }; var changes = record["Changes"]!.AsArray(); Assert.Equal(2, changes.Count);
        for (int index = 0; index < changes.Count; index++)
        {
            var change = changes[index]!; var kind = index == 0 ? LegacyTradeJournalFileKind.Character : LegacyTradeJournalFileKind.Spells;
            Assert.Equal((int)kind, change["Kind"]!.GetValue<int>());
            Assert.Equal((long)(index == 0 ? DeathTargetAccount : DeathTargetUid), change["FileId"]!.GetValue<long>());
            string key = Path.GetRelativePath(f.Owner.Data.Root, targets[index]);
            Assert.Equal(before.Files[key], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(before.After[key], Convert.FromBase64String(change["After"]!.GetValue<string>()));
        }
    }

    internal static void AssertDeathHistoryUnchanged(PhysicalDeathFixture f, PhysicalDeathBefore before)
    {
        Assert.Equal(before.Ledger, File.ReadAllBytes(f.Owner.Data.State));
        AssertHistoricalFiles(before.History, HistoryFiles(f.Owner));
        Assert.Equal(before.Newborn, File.ReadAllBytes(f.Owner.CharacterPath));
    }

    private static void AssertPhysicalDeathPublished(PhysicalDeathFixture f)
    {
        var before = Assert.IsType<PhysicalDeathBefore>(f.Before);
        f.Owner.Data.AssertFiles(before.After); AssertDeathHistoryUnchanged(f, before);
        var actor = f.ActorSession; var target = f.TargetSession;
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(before.ActorBinding));
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(before.TargetBinding));
        Assert.Equal(0L, target.Character.MinHitPoints); Assert.Equal(0, before.TargetBinding.Player.HitPoints);
        Assert.Equal(5000L, actor.Character.MinHitPoints); Assert.Equal(5000, before.ActorBinding.Player.HitPoints);
        Assert.Equal(before.Actor.Character.MinMana, actor.Character.MinMana); Assert.Equal(50U, before.ActorBinding.Player.Mana);
        Assert.Equal(before.Target.Character.MinMana, target.Character.MinMana); Assert.Equal(50U, before.TargetBinding.Player.Mana);
        Assert.Equal((ushort)100, before.ActorBinding.Player.Stamina); Assert.Equal((ushort)100, before.TargetBinding.Player.Stamina);
        Assert.Equal(0U, (uint)before.ActorBinding.Player.Action); Assert.NotEqual(before.Stamp, before.ActorBinding.Player.AttackStamp);
        Assert.False(before.ActorBinding.Client.OnAutoAttack); // killed targets never arm another hit
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(target.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        if (f.Changed) Assert.Equal("20000:5", target.Character.Attributes["ManagedDerivedSpellOrigins"]);
        else Assert.False(target.Character.Attributes.ContainsKey("ManagedDerivedSpellOrigins"));
        var expected = PlayerSpellSnapshotLoader.Parse(before.After[Path.GetRelativePath(f.Owner.Data.Root, f.SpellsPath)]);
        Assert.Equal(expected.Spells, target.PlayerSpells.Spells);
        var native = before.TargetBinding.Storage.ClientSpells;
        Assert.Equal(expected.Spells.Select(row => row.Id).Order(), native.Keys.Order());
        foreach (var row in expected.Spells)
        {
            var value = native[row.Id]; Assert.Equal(row.Level, value.Level); Assert.Equal(row.Experience, value.Experience);
            Assert.Equal(row.PreviousLevel, value.PreviousLevel); Assert.Equal(row.SoulLevel, value.SoulLevel); Assert.Equal(row.UseSpellSoul, value.UseSpellSoul);
        }
        foreach (var binding in new[] { before.ActorBinding, before.TargetBinding })
        {
            string path = f.Owner.Data.Options().Roots.PathFor(LegacyTradeJournalFileKind.Items, binding.Uid);
            var normalized = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(path)));
            var current = f.Owner.CurrentFor(binding.Connection);
            Assert.Equal(normalized, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(normalized, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
            Assert.Same(binding.Client.MyProfs!.ClientProf, binding.Proficiencies.ClientProf);
        }
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    }

    private static byte[] DeathInteractionGolden()
    {
        var body = new List<byte>();
        foreach (var field in new (int, ulong)[] { (4, CreationFixture.RuneActor), (5, DeathTargetUid), (7, 831), (8, 584),
            (13, 2), (15, 244), (17, 0), (21, 17), (25, 0), (26, 19) })
        { DeathVarint(body, (ulong)field.Item1 * 8); DeathVarint(body, field.Item2); }
        var bytes = new byte[body.Count + 4]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2135); body.CopyTo(bytes, 4); return bytes;
    }

    private static byte[] DeathGrantedGolden()
    {
        var bytes = new byte[25]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 25);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2101);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 20000); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 5);
        return bytes;
    }

    private static void DeathVarint(List<byte> bytes, ulong value)
    { while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; } bytes.Add((byte)value); }
}
