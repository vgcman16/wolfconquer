using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Text;

namespace WolfConquer.Core.Client;

/// <summary>Five ordinal numeric cells only; no current attribute, level, cost or runtime semantics.</summary>
public sealed class Chinese7937XuanbaoAdditionRawRow
{
    internal Chinese7937XuanbaoAdditionRawRow(int line, string raw, string[] fields, uint[] numeric)
    {
        LineNumber = line; RawLine = raw;
        Fields = Array.AsReadOnly(fields.ToArray()); NumericColumns = Array.AsReadOnly(numeric.ToArray());
    }
    public uint RowId => NumericColumns[0];
    public int LineNumber { get; }
    public string RawLine { get; }
    public IReadOnlyList<string> Fields { get; }
    public IReadOnlyList<uint> NumericColumns { get; }
}

/// <summary>Immutable offline source envelope. Never converted into the legacy typed addition table or bound to gameplay.</summary>
public sealed partial class Chinese7937XuanbaoAdditionRawCatalog
{
    private readonly ReadOnlyCollection<Chinese7937XuanbaoAdditionRawRow> rows;
    private readonly FrozenDictionary<uint, Chinese7937XuanbaoAdditionRawRow> byId;
    private Chinese7937XuanbaoAdditionRawCatalog(byte[] packedHash, byte[] decodedHash, Chinese7937XuanbaoAdditionRawRow[] captured)
    {
        rows = Array.AsReadOnly(captured.ToArray()); byId = captured.ToFrozenDictionary(row => row.RowId);
        PackedSha256 = Convert.ToHexString(packedHash).ToLowerInvariant();
        DecodedSha256 = Convert.ToHexString(decodedHash).ToLowerInvariant();
    }

    public const int MaximumBytes = 1_048_576;
    public const int MaximumRows = 4_096;
    public const string RelativeSourcePath = "ini/xuanbao_addition_attr.dat";
    public int DeclaredClientVersion => 7937;
    public string PackedSha256 { get; }
    public string DecodedSha256 { get; }
    public IReadOnlyList<Chinese7937XuanbaoAdditionRawRow> Rows => rows;
    public bool TryGetRow(uint rowId, out Chinese7937XuanbaoAdditionRawRow? row) => byId.TryGetValue(rowId, out row);
    public byte[] ReconstructDecodedBytes() => Encoding.ASCII.GetBytes(string.Concat(rows.Select(row => row.RawLine + "\r\n")));
    public byte[] ReconstructPackedBytes() => ClientItemCatalogCodec.Encode(ReconstructDecodedBytes());
}

public enum Chinese7937XuanbaoAdditionRawFailure
{
    None, UnsupportedVersion, InputBounds, PinRequired, PinMismatch, InverseMismatch,
    UnsupportedSyntax, NumericWidth, DuplicateRowId, RowBounds,
}
