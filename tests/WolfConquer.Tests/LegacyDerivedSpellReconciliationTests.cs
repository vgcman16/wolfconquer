using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Every outcome is DATA-only. No marker/desired row is a live grant or owner.
public sealed partial class LegacyDerivedSpellReconciliationTests
{
    [Theory]
    [InlineData(21930, true)] [InlineData(21930, false)]
    [InlineData(20000, true)] [InlineData(20000, false)]
    public void UnmarkedSavedRowsRetainAllProgressSoulOrderAndOpaqueBytes(int id, bool active)
    {
        var character = CharacterBytes();
        var row = new PlayerSpellSnapshotEntry(checked((ushort)id), 4, 12345, 3, 2, 2);
        var before = SpellBytes((Unrelated, (byte)0xA5), (row, (byte)0xDB));
        var characterCopy = character.ToArray(); var spellCopy = before.ToArray();
        var result = LegacyDerivedSpellReconciliation.Project(character, before,
            new[] { Desired(checked((ushort)id), active ? (ushort)7 : null) });
        Assert.Equal(characterCopy, result.CopyCharacterAfter()); Assert.Equal(spellCopy, result.CopySpellsAfter());
        Assert.Empty(result.Reconciled.Changes); Assert.Empty(LegacyDerivedSpellOrigins.Read(result.Reconciled.Character));
        Assert.Equal(new[] { Unrelated, row }, result.Reconciled.Spells.Spells);
        Assert.Equal(characterCopy, character); Assert.Equal(spellCopy, before);
    }

    [Fact]
    public void MissingRowsProduceSourceOrderedDataAndCanonicalOriginsWithoutAdoptingAnOwner()
    {
        var character = CharacterBytes(); var before = SpellBytes((Unrelated, (byte)0xA5));
        var result = LegacyDerivedSpellReconciliation.Project(character, before,
            new[] { Desired(21930, 4), Desired(20000, 2) });
        Assert.Equal(CharacterBytes("20000:2,21930:4"), result.CopyCharacterAfter());
        Assert.Equal(SpellBytes((Unrelated, (byte)0xA5), (Grant(21930, 4), (byte)0), (Grant(20000, 2), (byte)0)),
            result.CopySpellsAfter());
        Assert.Equal(new ushort[] { 21930, 20000 }, result.Reconciled.Changes.Select(row => row.Id));
        Assert.Equal(new ushort[] { 1000, 21930, 20000 }, result.Reconciled.Spells.Spells.Select(row => row.Id));
        Assert.Equal(CharacterBytes(), character); Assert.Equal(SpellBytes((Unrelated, (byte)0xA5)), before);
    }

    [Fact]
    public void SnapshotModelAndPhysicalProjectionAgreeWithoutInventingPhysicalEvidence()
    {
        var bytes = CharacterBytes(); var character = CharacterSnapshotParser.ParseText(Encoding.UTF8.GetString(bytes));
        var spells = new PlayerSpellSnapshot(new[] { Unrelated });
        var desired = new[] { Desired(21930, 4), Desired(20000, 2) };
        var model = LegacyDerivedSpellReconciliation.Project(character, spells, desired);
        var physical = LegacyDerivedSpellReconciliation.Project(bytes, SpellBytes((Unrelated, (byte)0xA5)), desired);
        Assert.Equal(model.Character.Attributes, physical.Reconciled.Character.Attributes);
        Assert.Equal(model.Spells.Spells, physical.Reconciled.Spells.Spells);
        Assert.Equal(model.Changes, physical.Reconciled.Changes);
        Assert.Same(character.PlayerQuests, model.Character.PlayerQuests);
        Assert.Equal(character.SourceFile, model.Character.SourceFile);
        Assert.False(character.Attributes.ContainsKey(LegacyDerivedSpellOrigins.Key));
        Assert.Equal(new[] { Unrelated }, spells.Spells);
    }

    [Fact]
    public void ExactOwnedRowsCanChangeOrRetireWithoutTouchingAnotherOwner()
    {
        // This fixture is a pure recorded DATA claim, never live authority.
        var before = SpellBytes((Unrelated, (byte)0xA5), (Grant(21930, 4), (byte)0), (Grant(20000, 2), (byte)0));
        var result = LegacyDerivedSpellReconciliation.Project(CharacterBytes("20000:2,21930:4"), before,
            new[] { Desired(21930, 3), Desired(20000, null) });
        Assert.Equal(CharacterBytes("21930:3"), result.CopyCharacterAfter());
        Assert.Equal(SpellBytes((Unrelated, (byte)0xA5), (Grant(21930, 3), (byte)0)), result.CopySpellsAfter());
        Assert.Equal(new[] { Desired(21930, 3), Desired(20000, null) }, result.Reconciled.Changes);
        var retired = LegacyDerivedSpellReconciliation.Project(result.CopyCharacterAfter(), result.CopySpellsAfter(),
            new[] { Desired(21930, null) });
        Assert.Equal(CharacterBytes(""), retired.CopyCharacterAfter());
        Assert.Equal(SpellBytes((Unrelated, (byte)0xA5)), retired.CopySpellsAfter());
    }

    [Theory]
    [InlineData("unknown")] [InlineData("duplicate")] [InlineData("order")] [InlineData("leading-zero")]
    [InlineData("zero-level")] [InlineData("wide-level")] [InlineData("missing-row")]
    [InlineData("wrong-level")] [InlineData("progress")] [InlineData("soul")] [InlineData("opaque")]
    [InlineData("rune-overlap")]
    public void InvalidRecordedClaimsNeverProduceAReconciledImage(string corruption)
    {
        string marker = corruption switch
        {
            "unknown" => "1000:4", "duplicate" => "21930:4,21930:4", "order" => "21930:4,20000:2",
            "leading-zero" => "021930:4", "zero-level" => "21930:0", "wide-level" => "21930:256", _ => "21930:4",
        };
        var row = Grant(21930, corruption == "wrong-level" ? (ushort)5 : (ushort)4);
        if (corruption == "progress") row = row with { Experience = 1 };
        if (corruption == "soul") row = row with { SoulLevel = 1 };
        var character = CharacterBytes(marker);
        if (corruption == "rune-overlap") character = Encoding.UTF8.GetBytes(CharacterText(marker)
            .Replace("Opaque=", "ManagedRuneSpellOrigins=21930:4\r\nOpaque=", StringComparison.Ordinal));
        var spells = corruption == "missing-row" ? SpellBytes((Unrelated, (byte)0xA5)) :
            SpellBytes((Unrelated, (byte)0xA5), (row, corruption == "opaque" ? (byte)0xA5 : (byte)0));
        var beforeCharacter = character.ToArray(); var beforeSpells = spells.ToArray();
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellReconciliation.Project(character, spells,
            new[] { Desired(21930, null) }));
        Assert.Equal(beforeCharacter, character); Assert.Equal(beforeSpells, spells);
    }

    [Theory]
    [InlineData("new-claim")] [InlineData("drop-claim")] [InlineData("new-level")]
    [InlineData("progress")] [InlineData("remove-row")]
    public void GenericWritersCannotAdoptOrPermanentizeClaimedMembership(string change)
    {
        var before = CharacterSnapshotParser.ParseText(CharacterText(change == "new-claim" ? null : "21930:4"));
        var beforeSpells = new PlayerSpellSnapshot(new[] { Unrelated, Grant(21930, 4) });
        var after = CharacterSnapshotParser.ParseText(CharacterText(change switch
        { "drop-claim" => null, "new-level" => "21930:5", _ => "21930:4" }));
        var row = Grant(21930, change == "new-level" ? (ushort)5 : (ushort)4);
        if (change == "progress") row = row with { Experience = 1 };
        var afterSpells = new PlayerSpellSnapshot(change == "remove-row" ? new[] { Unrelated } : new[] { Unrelated, row });
        Assert.Throws<InvalidDataException>(() => LegacyDerivedSpellOrigins.RequireUnchanged(before, beforeSpells, after, afterSpells));
    }

    [Fact]
    public void OrdinaryUnmarkedProgressionAndUnrelatedChangesRemainDataCompatible()
    {
        var character = CharacterSnapshotParser.ParseText(CharacterText("21930:4"));
        var before = new PlayerSpellSnapshot(new[] { Unrelated, Grant(21930, 4), Grant(20000, 2) });
        var after = new PlayerSpellSnapshot(new[] { Unrelated with { Experience = 999 }, Grant(21930, 4),
            Grant(20000, 2) with { Experience = 12, SoulLevel = 1, UseSpellSoul = 1 } });
        LegacyDerivedSpellOrigins.RequireUnchanged(character, before, character with { MinHitPoints = 0 }, after);
    }

    private static readonly PlayerSpellSnapshotEntry Unrelated = new(1000, 2, 357, 1, 1, 1);
    private static PlayerSpellSnapshotEntry Grant(ushort id, ushort level) => new(id, level, 0, 0, 0, 0);
    private static LegacyDerivedSpellDesiredRow Desired(ushort id, ushort? level) => new(id, level is { } value ? Grant(id, value) : null);

    private static byte[] SpellBytes(params (PlayerSpellSnapshotEntry Row, byte Opaque)[] rows)
    {
        var bytes = new byte[4 + rows.Length * 12]; BinaryPrimitives.WriteInt32LittleEndian(bytes, rows.Length);
        for (int index = 0; index < rows.Length; index++)
        {
            var (row, opaque) = rows[index]; var record = bytes.AsSpan(4 + index * 12, 12);
            BinaryPrimitives.WriteUInt16LittleEndian(record, row.Id); BinaryPrimitives.WriteUInt16LittleEndian(record[2..], row.Level);
            BinaryPrimitives.WriteInt32LittleEndian(record[4..], row.Experience); record[8] = row.PreviousLevel;
            record[9] = row.SoulLevel; record[10] = row.UseSpellSoul; record[11] = opaque;
        }
        return bytes;
    }

    private static byte[] CharacterBytes(string? marker = null) => Encoding.UTF8.GetBytes(CharacterText(marker));
    private static string CharacterText(string? marker = null) => string.Join("\r\n", new[]
    {
        "; original text", "[Character]", "UID=1001", "Name=DerivedData", "Body=1008", "Face=154", "Haire=0",
        "Class=7051", "FirstClass=0", "SecoundeClass=0", "Reborn=2", "Level=140", "Experience=0", "Map=1002",
        "X=430", "Y=380", "PMap=1002", "PMapX=430", "PMapY=380", "Strength=10", "Agility=10", "Vitaliti=10",
        "Spirit=10", "Atributes=0", "MinHitPoints=1700", "MinMana=50", "Money=777", "ConquerPoints=222",
        "BoundConquerPoints=0", "BankCps=0", "PkPoints=0", "Online=1", "Opaque=a=b;#retained",
        "[Unrelated]", "UID=42", "Sentinel=exact",
    }).Replace("Opaque=a=b;#retained\r\n", marker is null ? "Opaque=a=b;#retained\r\n" :
        "Opaque=a=b;#retained\r\n" + LegacyDerivedSpellOrigins.Key + "=" + marker + "\r\n", StringComparison.Ordinal);
}
