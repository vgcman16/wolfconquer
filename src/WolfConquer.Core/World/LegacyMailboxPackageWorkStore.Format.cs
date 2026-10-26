using System.Text.Json;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private const int MaximumBytes = 16 * 1024;
    private static readonly string[] Keys = ["Version", "Domain", "Operation", "PayloadSha256", "AccountId", "CharacterUid",
        "First", "Count", "Users", "Items", "Mailbox", "CombatGear", "Before", "After", "Completed"];

    internal Record? Read(Guid operation)
    {
        var path = WorkPath(operation);
        EnsureOrdinary(path);
        if (!File.Exists(path)) return null;
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length is <= 0 or > MaximumBytes) throw new InvalidDataException("Package work size is invalid.");
        var bytes = new byte[(int)input.Length];
        input.ReadExactly(bytes);
        if (input.ReadByte() != -1 || input.Length != bytes.Length) throw new IOException("Package work changed during reading.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 3 });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Package work is not an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
            if (!Keys.Contains(property.Name, StringComparer.Ordinal) || !names.Add(property.Name))
                throw new InvalidDataException("Package work properties are invalid.");
        if (names.Count != Keys.Length) throw new InvalidDataException("Package work properties are missing.");
        var row = JsonSerializer.Deserialize<Record>(bytes) ?? throw new InvalidDataException("Package work is missing.");
        if (row.Version != 1 || row.Domain != domain.ToString("N") || row.Operation != operation.ToString("N") ||
            row.AccountId == 0 || row.CharacterUid == 0 || row.First == 0 || row.Count is < 1 or > 40 ||
            (ulong)row.First + (uint)row.Count - 1 > uint.MaxValue || !IsDigest(row.PayloadSha256) ||
            row.Users != users || row.Items != items || row.Mailbox != mailbox || row.CombatGear != gear ||
            row.Before is not { Length: 4 } || row.After is not { Length: 4 } ||
            row.Before.Any(hash => !IsDigest(hash)) || row.After.Any(hash => !IsDigest(hash)))
            throw new InvalidDataException("Package work bindings are invalid.");
        return row;
    }

    private static bool IsDigest(string? text) => text is { Length: 64 } &&
        text.All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f');
}
