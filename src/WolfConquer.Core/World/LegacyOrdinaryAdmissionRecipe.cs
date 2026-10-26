using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyOrdinaryStarterTemplate(uint ItemId, ushort Position, ushort Durability, ushort MaximumDurability);

internal sealed record LegacyOrdinaryAdmissionRecipe(uint Account, LegacyOrdinaryAdmissionRoots Roots,
    Dictionary<LegacyTradeJournalFileKind, byte[]?> BeforeHashes, byte[] CharacterAfter, byte[] SpellsAfter,
    byte[] ProficienciesAfter, LegacyOrdinaryStarterTemplate[] Items)
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal byte[] Encode()
    {
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Utf8, true))
        {
            writer.Write("ordinary-admission-v1"); writer.Write(Account);
            foreach (var kind in Enum.GetValues<LegacyTradeJournalFileKind>())
            {
                writer.Write((int)kind); writer.Write(Roots.Root(kind) ?? "");
                var image = BeforeHashes.GetValueOrDefault(kind);
                writer.Write(image is not null); if (image is not null) writer.Write(image);
            }
            writer.Write(CharacterAfter); writer.Write(SpellsAfter); writer.Write(ProficienciesAfter);
            writer.Write(Items.Length);
            foreach (var row in Items)
            { writer.Write(row.ItemId); writer.Write(row.Position); writer.Write(row.Durability); writer.Write(row.MaximumDurability); }
        }
        if (output.Length > LegacyItemUidRegistry.MaximumProducerPayloadBytes) throw new InvalidDataException("Admission recipe is too large.");
        return output.ToArray();
    }

    internal static LegacyOrdinaryAdmissionRecipe Decode(byte[] image, LegacyOrdinaryAdmissionRoots roots)
    {
        if (image.Length is < 1 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes) throw new InvalidDataException("Invalid recipe length.");
        using var stream = new MemoryStream(image, false);
        using var reader = new BinaryReader(stream, Utf8, true);
        string Text()
        {
            var length = reader.Read7BitEncodedInt();
            if (length is < 0 or > 32768 || length > stream.Length - stream.Position) throw new InvalidDataException("Invalid recipe string.");
            return Utf8.GetString(reader.ReadBytes(length));
        }
        byte[] Hash()
        {
            var bytes = reader.ReadBytes(32);
            if (bytes.Length != 32) throw new InvalidDataException("Truncated recipe digest.");
            return bytes;
        }
        if (Text() != "ordinary-admission-v1") throw new InvalidDataException("Unknown admission recipe.");
        var account = reader.ReadUInt32();
        var before = new Dictionary<LegacyTradeJournalFileKind, byte[]?>();
        foreach (var kind in Enum.GetValues<LegacyTradeJournalFileKind>())
        {
            if (reader.ReadInt32() != (int)kind || Text() != (roots.Root(kind) ?? ""))
                throw new InvalidDataException("Recipe differs from configured admission roots.");
            before[kind] = reader.ReadBoolean() ? Hash() : null;
        }
        var character = Hash(); var spells = Hash(); var profs = Hash();
        var count = reader.ReadInt32();
        if (account == 0 || count is < 1 or > 4) throw new InvalidDataException("Invalid admission recipe identity/count.");
        var items = new LegacyOrdinaryStarterTemplate[count];
        for (var index = 0; index < count; index++)
        {
            items[index] = new(reader.ReadUInt32(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16());
            if (items[index].ItemId == 0 || items[index].Position is < 3 or > 6 || items[index].Durability == 0 ||
                items[index].MaximumDurability != items[index].Durability) throw new InvalidDataException("Invalid starter template.");
        }
        if (items.Select(row => row.Position).Distinct().Count() != count || stream.Position != stream.Length)
            throw new InvalidDataException("Ambiguous recipe tail/membership.");
        var result = new LegacyOrdinaryAdmissionRecipe(account, roots, before, character, spells, profs, items);
        if (!result.Encode().AsSpan().SequenceEqual(image)) throw new InvalidDataException("Noncanonical admission recipe.");
        return result;
    }

    internal PlayerItemSnapshot Materialize(LegacyItemUidReservationReceipt receipt)
    {
        if (receipt.First == 0 || receipt.AccountId != Account || receipt.CharacterUid != Account || receipt.Count != Items.Length ||
            (ulong)receipt.First + (uint)receipt.Count - 1 > uint.MaxValue) throw new InvalidDataException("Invalid retained starter range.");
        var rows = Items.Select((row, index) =>
        {
            var uid = checked(receipt.First + (uint)index);
            return new PlayerItemSnapshotItem { Uid = uid, ItemId = row.ItemId, Position = row.Position,
                Durability = row.Durability, MaximumDurability = row.MaximumDurability,
                Color = 3U + (unchecked(uid + row.ItemId) % 6U) };
        }).ToArray();
        return new([], [], [], rows, []);
    }

    internal bool IsBefore(IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> current) =>
        Roots.Kinds.All(kind => Matches(current.GetValueOrDefault(kind), BeforeHashes.GetValueOrDefault(kind)));

    internal bool IsAfter(IReadOnlyDictionary<LegacyTradeJournalFileKind, byte[]?> current, LegacyItemUidReservationReceipt receipt) =>
        Roots.Kinds.All(kind => Matches(current.GetValueOrDefault(kind), kind switch
        {
            LegacyTradeJournalFileKind.Character => CharacterAfter,
            LegacyTradeJournalFileKind.Items => SHA256.HashData(PlayerItemSnapshotLoader.ToLegacyBytes(Materialize(receipt))),
            LegacyTradeJournalFileKind.Spells => SpellsAfter,
            LegacyTradeJournalFileKind.Proficiencies => ProficienciesAfter,
            _ => BeforeHashes.GetValueOrDefault(kind),
        }));

    private static bool Matches(byte[]? image, byte[]? hash) => image is null ? hash is null :
        hash is not null && SHA256.HashData(image).AsSpan().SequenceEqual(hash);
}
