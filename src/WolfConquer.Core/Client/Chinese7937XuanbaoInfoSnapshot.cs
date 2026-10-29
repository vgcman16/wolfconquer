using System.Collections.Frozen;

namespace WolfConquer.Core.Client;

public enum Chinese7937XuanbaoInfoVersion { Catalog7931 = 7931, Catalog7937 = 7937 }

public readonly record struct Chinese7937XuanbaoInfoField(string Name, uint Value);

/// <summary>Owned selected literal data. No live Lua, currency, permission or activation authority.</summary>
public sealed class Chinese7937XuanbaoInfoSnapshot
{
    private readonly Chinese7937XuanbaoInfoField[] fields;
    private readonly FrozenDictionary<string, uint> byName;

    internal Chinese7937XuanbaoInfoSnapshot(Chinese7937XuanbaoInfoVersion declaredVersion, string contentSha256,
        ReadOnlySpan<Chinese7937XuanbaoInfoField> fields)
    {
        DeclaredVersion = declaredVersion;
        ContentSha256 = contentSha256;
        this.fields = fields.ToArray();
        byName = this.fields.ToFrozenDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal);
    }

    public const int SchemaVersion = 1;
    public Chinese7937XuanbaoInfoVersion DeclaredVersion { get; }
    public string ContentSha256 { get; }
    public int Count => fields.Length;
    public ReadOnlySpan<Chinese7937XuanbaoInfoField> Fields => fields;

    public bool TryGetValue(string? name, out uint value)
    {
        value = 0;
        return name is not null && byName.TryGetValue(name, out value);
    }
}

public enum Chinese7937XuanbaoInfoFailure
{
    None, UnsupportedVersion, InputBounds, InvalidGbk, UnsupportedSyntax, LiteralBounds,
    SelectedRelationshipMissing, AmbiguousSelectedForm, DuplicateField, NumericWidth,
    PathRequired, UnsafePath, FileRead, SourceChanged, PinRequired, PinMismatch,
}
