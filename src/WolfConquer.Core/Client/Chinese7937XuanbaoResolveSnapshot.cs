namespace WolfConquer.Core.Client;

/// <summary>Selected literal DATA only; no currency, item classification or reward ownership.</summary>
public sealed class Chinese7937XuanbaoResolveSnapshot
{
    private readonly Chinese7937XuanbaoInfoField[] fields;

    internal Chinese7937XuanbaoResolveSnapshot(string contentSha256, ReadOnlySpan<Chinese7937XuanbaoInfoField> fields)
    {
        ContentSha256 = contentSha256;
        this.fields = fields.ToArray();
        CommonNumberField = this.fields.Single(row => row.Name == "CommonNum").Value;
        GoldNumberField = this.fields.Single(row => row.Name == "GoldNum").Value;
        MaterialIdField = this.fields.Single(row => row.Name == "MaterialID").Value;
    }

    public int ClientVersion => 7937;
    public Chinese7937XuanbaoInfoVersion DeclaredVersion => Chinese7937XuanbaoInfoVersion.Catalog7937;
    public string ContentSha256 { get; }
    public uint CommonNumberField { get; }
    public uint GoldNumberField { get; }
    public uint MaterialIdField { get; }
    public ReadOnlySpan<Chinese7937XuanbaoInfoField> Fields => fields;

    /// <summary>Only the explicit Boolean subset of the observed branch; does not emulate Lua truthiness.</summary>
    public uint SelectNumber(bool bGold) => bGold ? GoldNumberField : CommonNumberField;
}
