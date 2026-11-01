using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private sealed record DerivedQueryBefore(Dictionary<string, byte[]> Files, Dictionary<string, byte[]> After,
        Dictionary<string, byte[]> History, byte[] Ledger, byte[] Newborn, LegacyOrdinarySpellBinding Binding);

    private static async Task<DerivedQueryBefore> EquipForDerivedQueryAsync(PhysicalDeathFixture f,
        LegacyJournalInterruption? interruption = null)
    {
        DerivedQueryBefore? before = null;
        f.Owner.BeforeSend = (account, bytes) =>
        {
            if (account != DeathTargetAccount) return;
            if (before is null)
            {
                Assert.False(f.TargetSession.PlayerSpells.ContainsSpell(20000));
                Assert.Equal(DeathArmorUid, Assert.Single(f.TargetSession.PlayerItems.EquipmentItems).Uid);
                Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
                var files = f.Owner.Data.Files();
                var after = files.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                string characterKey = Path.GetRelativePath(f.Owner.Data.Root, f.CharacterPath);
                string spellKey = Path.GetRelativePath(f.Owner.Data.Root, f.SpellsPath);
                string text = CharacterSnapshotFileEncoding.Decode(files[characterKey], out var encoding);
                Assert.EndsWith("\r\n", text);
                Assert.DoesNotContain("ManagedDerivedSpellOrigins=", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("\r\n[", text, StringComparison.Ordinal); // Exactly one Character section.
                after[characterKey] = encoding.GetPreamble().Concat(encoding.GetBytes(text + "ManagedDerivedSpellOrigins=20000:5\r\n")).ToArray();
                var spells = files[spellKey]; Assert.Equal(16, spells.Length); Assert.Equal((byte)0xC7, spells[15]);
                var changed = new byte[28]; spells.CopyTo(changed, 0);
                BinaryPrimitives.WriteInt32LittleEndian(changed, 2);
                BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(16), 20000);
                BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(18), 5);
                after[spellKey] = changed;
                before = new(files, after, HistoryFiles(f.Owner), File.ReadAllBytes(f.Owner.Data.State),
                    File.ReadAllBytes(f.Owner.CharacterPath), f.TargetSession.NativeGameClient!.OrdinarySpellBinding!);
                return;
            }
            if (bytes.AsSpan().SequenceEqual(DeathGrantedGolden())) f.Owner.Data.AssertFiles(before.After);
        };
        f.Owner.Handler.DerivedSpellQueryJournalInterruption = interruption;
        try
        {
            await f.Owner.SendOnAsync(f.Target,
                new MsgItemUsagePacket(DeathArmorUid, 3, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes());
        }
        finally { f.Owner.BeforeSend = null; f.Owner.Handler.DerivedSpellQueryJournalInterruption = null; }
        Assert.NotNull(before);
        return before;
    }

    private static void AssertDerivedQueryHistory(PhysicalDeathFixture f, DerivedQueryBefore before)
    {
        AssertHistoricalFiles(before.History, HistoryFiles(f.Owner));
        Assert.Equal(before.Ledger, File.ReadAllBytes(f.Owner.Data.State));
        Assert.Equal(before.Newborn, File.ReadAllBytes(f.Owner.CharacterPath));
    }
}
