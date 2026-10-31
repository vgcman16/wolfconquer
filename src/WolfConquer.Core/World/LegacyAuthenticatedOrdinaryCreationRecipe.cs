using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// A complete historical projection, never a live account or birth permit.
// Only the actual authenticated producer may issue its durable intent.
internal sealed partial class LegacyAuthenticatedOrdinaryCreationRecipe
{
    internal const string Kind = "authenticated-ordinary-creation-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static IReadOnlyList<LegacyTradeJournalFileKind> TargetKinds { get; } = Array.AsReadOnly(
    new[] { LegacyTradeJournalFileKind.Character, LegacyTradeJournalFileKind.Items,
        LegacyTradeJournalFileKind.Spells, LegacyTradeJournalFileKind.Proficiencies,
        LegacyTradeJournalFileKind.CombatGear, LegacyTradeJournalFileKind.Quests });
    internal uint Account { get; }
    internal string Username { get; }
    internal string TicketDigest { get; }
    internal LegacyOrdinaryAdmissionRoots Roots { get; }
    internal string MailboxRoot { get; }
    internal LegacyAuthenticatedCharacterCreationProjection Projection { get; }

    internal LegacyAuthenticatedOrdinaryCreationRecipe(uint account, string username, string ticketDigest,
        LegacyOrdinaryAdmissionRoots roots, string mailbox,
        LegacyAuthenticatedCharacterCreationProjection projection)
    {
        if (account == 0 || projection.Account != account || string.IsNullOrWhiteSpace(username) ||
            Utf8.GetByteCount(username) > 1024 || ticketDigest.Length != 64 ||
            ticketDigest.Any(value => value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            TargetKinds.Any(kind => roots.Root(kind) is null) || string.IsNullOrWhiteSpace(mailbox))
            throw new InvalidDataException("Authenticated creation identity or configured routes are incomplete.");
        Account = account; Username = username; TicketDigest = ticketDigest;
        Roots = roots; MailboxRoot = mailbox; Projection = projection;
    }

    internal byte[] Encode()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Utf8, true);
        writer.Write(Kind); writer.Write(Account); writer.Write(Username); writer.Write(TicketDigest);
        writer.Write(Projection.Name); writer.Write(Projection.Body); writer.Write(Projection.ClassSelection);
        writer.Write(Projection.PreparedAt.Ticks);
        foreach (var kind in TargetKinds) { writer.Write((int)kind); writer.Write(Roots.Root(kind)!); }
        writer.Write(MailboxRoot);
        var metadata = JsonSerializer.SerializeToUtf8Bytes(Projection.CopyMetadata());
        writer.Write(metadata.Length); writer.Write(metadata); writer.Flush();
        if (stream.Length > LegacyItemUidRegistry.MaximumProducerPayloadBytes)
            throw new InvalidDataException("Authenticated creation recipe exceeds its producer payload bound.");
        return stream.ToArray();
    }

    internal LegacyItemUidReservationRequest CreateRequest()
    {
        var digest = SHA256.HashData(Encode());
        var operation = new Guid(digest.AsSpan(0, 16));
        if (operation == Guid.Empty) throw new InvalidDataException("Authenticated creation operation is empty.");
        return new(operation, LegacyOrdinaryAdmissionUidOwner.Writer, Account, Account,
            Convert.ToHexString(digest).ToLowerInvariant(), Projection.Items.Length);
    }

    internal static bool HasKind(byte[] bytes)
    {
        if (bytes.Length is < 1 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes) return false;
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream, Utf8, true);
        return ReadText(reader, stream) == Kind;
    }

    private static string ReadText(BinaryReader reader, Stream stream)
    {
        var length = reader.Read7BitEncodedInt();
        if (length is < 0 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes || length > stream.Length - stream.Position)
            throw new InvalidDataException("Authenticated creation string is incomplete or oversized.");
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new InvalidDataException("Authenticated creation string is truncated.");
        return Utf8.GetString(bytes);
    }

    internal static LegacyAuthenticatedOrdinaryCreationRecipe Decode(byte[] bytes,
        LegacyOrdinaryAdmissionRoots roots, string mailbox)
    {
        if (bytes.Length is < 1 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes)
            throw new InvalidDataException("Invalid authenticated creation recipe size.");
        using var stream = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(stream, Utf8, true);
        string Text() => ReadText(reader, stream);
        if (Text() != Kind) throw new InvalidDataException("Unknown authenticated creation recipe.");
        var account = reader.ReadUInt32(); var username = Text(); var ticketDigest = Text(); var name = Text();
        var body = reader.ReadUInt16(); var selection = reader.ReadUInt16(); var ticks = reader.ReadInt64();
        if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
            throw new InvalidDataException("Authenticated creation time is invalid.");
        foreach (var kind in TargetKinds)
            if (reader.ReadInt32() != (int)kind || Text() != roots.Root(kind))
                throw new InvalidDataException("Authenticated creation recipe routes differ from configuration.");
        if (Text() != mailbox) throw new InvalidDataException("Authenticated creation mailbox differs from configuration.");
        var count = reader.ReadInt32();
        if (count is < 1 or > LegacyItemUidRegistry.MaximumProducerPayloadBytes || count != stream.Length - stream.Position)
            throw new InvalidDataException("Authenticated creation metadata or tail is invalid.");
        using var document = JsonDocument.Parse(reader.ReadBytes(count), new JsonDocumentOptions { MaxDepth = 5 });
        var metadata = LegacyAuthenticatedCharacterCreationMetadata.Read(document.RootElement);
        var projection = LegacyAuthenticatedCharacterCreationProjection.Rebuild(account, name, body, selection,
            new DateTime(ticks, DateTimeKind.Unspecified), metadata);
        var recipe = new LegacyAuthenticatedOrdinaryCreationRecipe(account, username, ticketDigest, roots, mailbox, projection);
        if (!recipe.Encode().AsSpan().SequenceEqual(bytes))
            throw new InvalidDataException("Authenticated creation recipe is noncanonical.");
        return recipe;
    }
}
