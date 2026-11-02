using System.Collections.Frozen;
using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.Client;

/// <summary>Owned current-client compose/random metadata in exact source order; no gameplay or profile authority.</summary>
public sealed partial class Chinese7937XuanbaoRuleCatalogs
{
    public const int MaximumPackedBytes = 1024 * 1024;
    public const int MaximumRows = 4096;
    public const string ComposeSourceFile = "chinese7937/ini/xuanbao_compose_attr_limit.dat";
    public const string RandomSourceFile = "chinese7937/ini/xuanbao_rand_attr.dat";
    private readonly ReadOnlyCollection<XuanbaoComposeAttributeLimit> composeLimits;
    private readonly ReadOnlyCollection<XuanbaoRandomAttribute> randomAttributes;
    private readonly FrozenDictionary<int, XuanbaoComposeAttributeLimit> composeById;
    private readonly FrozenDictionary<(int Item, int Type), XuanbaoRandomAttribute> randomByKey;

    private Chinese7937XuanbaoRuleCatalogs(XuanbaoComposeAttributeLimit[] compose, XuanbaoRandomAttribute[] random,
        string composePackedHash, string composeDecodedHash, string randomPackedHash, string randomDecodedHash)
    {
        composeLimits = Array.AsReadOnly(compose);
        randomAttributes = Array.AsReadOnly(random);
        composeById = compose.ToFrozenDictionary(row => row.Id);
        randomByKey = random.ToFrozenDictionary(row => (row.ItemTypeId, row.AttributeType));
        ComposePackedSha256 = composePackedHash; ComposeDecodedSha256 = composeDecodedHash;
        RandomPackedSha256 = randomPackedHash; RandomDecodedSha256 = randomDecodedHash;
    }

    public int ClientVersion => 7937;
    public IReadOnlyList<XuanbaoComposeAttributeLimit> ComposeLimits => composeLimits;
    public IReadOnlyList<XuanbaoRandomAttribute> RandomAttributes => randomAttributes;
    public string ComposePackedSha256 { get; }
    public string ComposeDecodedSha256 { get; }
    public string RandomPackedSha256 { get; }
    public string RandomDecodedSha256 { get; }
    public bool TryGetComposeLimit(int id, out XuanbaoComposeAttributeLimit? row) => composeById.TryGetValue(id, out row);
    public bool TryGetRandomAttribute(int item, int type, out XuanbaoRandomAttribute? row) => randomByKey.TryGetValue((item, type), out row);
}

public enum Chinese7937XuanbaoRulesFailure
{
    None, UnsupportedVersion, PinRequired, PinMismatch, InputBounds, InvalidTable, NumericWidth,
    DuplicateComposeId, DuplicateRandomKey, RowBounds, Reconstruction, PathRequired, UnsafePath,
    FileRead, SourceChanged,
}
