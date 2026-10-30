namespace WolfConquer.Core.World;

public enum LegacyGmCommandKind { Help, Inspect, Kit }
public enum LegacyGmInspectionSection { Overview, Position, Vitals, Inventory, Equipment, Spells, Proficiencies, Quests, Mail }
public enum LegacyGmCommandParseStatus { NotReserved, Rejected, Parsed }
public enum LegacyGmCommandError { None, TooLong, InvalidCharacters, InvalidSyntax, UnknownCommand, UnknownInspection, UnknownKit, InvalidOperationId }

public sealed class LegacyGmCommand
{
    internal LegacyGmCommand(LegacyGmCommandKind kind, LegacyGmInspectionSection? inspection = null,
        string? kitName = null, Guid? operationId = null)
    {
        Kind = kind;
        Inspection = inspection;
        KitName = kitName;
        OperationId = operationId;
    }

    public LegacyGmCommandKind Kind { get; }
    public LegacyGmInspectionSection? Inspection { get; }
    public string? KitName { get; }
    public Guid? OperationId { get; }
}

public sealed class LegacyGmCommandParseResult
{
    internal LegacyGmCommandParseResult(LegacyGmCommandParseStatus status, LegacyGmCommand? command = null,
        LegacyGmCommandError error = LegacyGmCommandError.None)
    {
        Status = status;
        Command = command;
        Error = error;
    }

    public LegacyGmCommandParseStatus Status { get; }
    public LegacyGmCommand? Command { get; }
    public LegacyGmCommandError Error { get; }
}

/// <summary>Pure reserved-chat grammar, not permission or runtime actor admission.</summary>
public static class LegacyGmCommandParser
{
    public const int MaximumCharacters = 256;
    private const string Prefix = "@wolf";

    public static LegacyGmCommandParseResult Parse(string? text)
    {
        if (!IsReserved(text)) return new(LegacyGmCommandParseStatus.NotReserved);
        if (text!.Length > MaximumCharacters) return Reject(LegacyGmCommandError.TooLong);
        foreach (var value in text)
            if (value is < ' ' or > '~') return Reject(LegacyGmCommandError.InvalidCharacters);
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return Reject(LegacyGmCommandError.InvalidSyntax);
        if (EqualsToken(tokens[1], "help"))
            return tokens.Length == 2 ? Accept(new(LegacyGmCommandKind.Help)) : Reject(LegacyGmCommandError.InvalidSyntax);
        if (EqualsToken(tokens[1], "inspect")) return Inspect(tokens);
        if (EqualsToken(tokens[1], "kit")) return Kit(tokens);
        return Reject(LegacyGmCommandError.UnknownCommand);
    }

    private static bool IsReserved(string? text) => text is not null &&
        text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) &&
        (text.Length == Prefix.Length || text[Prefix.Length] is ' ' or '\t' or '\r' or '\n' or '\f' or '\v');

    private static LegacyGmCommandParseResult Inspect(string[] tokens)
    {
        if (tokens.Length is < 2 or > 3) return Reject(LegacyGmCommandError.InvalidSyntax);
        var section = tokens.Length == 2 ? "overview" : tokens[2];
        LegacyGmInspectionSection? parsed = section.ToLowerInvariant() switch
        {
            "overview" => LegacyGmInspectionSection.Overview,
            "position" => LegacyGmInspectionSection.Position,
            "vitals" => LegacyGmInspectionSection.Vitals,
            "inventory" => LegacyGmInspectionSection.Inventory,
            "equipment" => LegacyGmInspectionSection.Equipment,
            "spells" => LegacyGmInspectionSection.Spells,
            "proficiencies" => LegacyGmInspectionSection.Proficiencies,
            "quests" => LegacyGmInspectionSection.Quests,
            "mail" => LegacyGmInspectionSection.Mail,
            _ => null
        };
        return parsed is { } value ? Accept(new(LegacyGmCommandKind.Inspect, value)) : Reject(LegacyGmCommandError.UnknownInspection);
    }

    private static LegacyGmCommandParseResult Kit(string[] tokens)
    {
        if (tokens.Length != 4) return Reject(LegacyGmCommandError.InvalidSyntax);
        var name = tokens[2].ToLowerInvariant();
        if (name is not ("basic-trojan" or "socket-materials" or "consumables"))
            return Reject(LegacyGmCommandError.UnknownKit);
        if (tokens[3].Length != 32 || !Guid.TryParseExact(tokens[3], "N", out var operation) || operation == Guid.Empty)
            return Reject(LegacyGmCommandError.InvalidOperationId);
        return Accept(new(LegacyGmCommandKind.Kit, kitName: name, operationId: operation));
    }

    private static bool EqualsToken(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static LegacyGmCommandParseResult Accept(LegacyGmCommand command) => new(LegacyGmCommandParseStatus.Parsed, command);
    private static LegacyGmCommandParseResult Reject(LegacyGmCommandError error) => new(LegacyGmCommandParseStatus.Rejected, error: error);
}
