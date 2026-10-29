using System.Collections.Frozen;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Client;

public enum Chinese7935EchoCatalogVersion { Catalog7930 = 7930, Catalog7931 = 7931 }

public readonly record struct Chinese7935EchoCatalogRow(
    int Id, int AttributeType, uint AttributeValue, int ExtraAttributeType, uint ExtraAttributeValue);

/// <summary>Owned literal data; declared version/digest grant no runtime or gameplay authority.</summary>
public sealed class Chinese7935EchoCatalogSnapshot
{
    private readonly Chinese7935EchoCatalogRow[] rows;
    private readonly FrozenDictionary<int, Chinese7935EchoCatalogRow> byId;
    internal Chinese7935EchoCatalogSnapshot(Chinese7935EchoCatalogVersion declaredVersion, string contentSha256,
        ReadOnlySpan<Chinese7935EchoCatalogRow> rows)
    {
        DeclaredVersion = declaredVersion; ContentSha256 = contentSha256;
        this.rows = rows.ToArray(); byId = this.rows.ToFrozenDictionary(row => row.Id);
    }
    public const int SchemaVersion = 1;
    public Chinese7935EchoCatalogVersion DeclaredVersion { get; }
    public string ContentSha256 { get; }
    public int Count => rows.Length;
    public ReadOnlySpan<Chinese7935EchoCatalogRow> Rows => rows;
    public bool TryGetRow(int id, out Chinese7935EchoCatalogRow row) => byId.TryGetValue(id, out row);

    /// <summary>Fresh caller-owned projection; live contributions and binding remain external prerequisites.</summary>
    public Chinese7935EchoType6Thresholds.Row[] CopyThresholdRows()
    {
        var result = new Chinese7935EchoType6Thresholds.Row[rows.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            result[index] = new(row.AttributeType, row.AttributeValue, row.ExtraAttributeType, row.ExtraAttributeValue);
        }
        return result;
    }
}

public enum Chinese7935EchoCatalogFailure
{
    None, UnsupportedVersion, InputBounds, InvalidUtf8, UnsupportedSyntax, LiteralBounds,
    DuplicateRow, DuplicateField, RowSchema, NumericWidth, PathRequired, UnsafePath,
    FileRead, SourceChanged, PinRequired, PinMismatch, KeyRequired,
}
